using Colossal.AssetPipeline;
using Colossal.AssetPipeline.Importers;
using Colossal.IO.AssetDatabase;
using Colossal.IO.AssetDatabase.Internal;
using Colossal.Serialization.Entities;
using Colossal.UI.Binding;
using Game;
using Game.Citizens;
using Game.Objects;
using Game.Prefabs;
using Game.Rendering;
using Game.Tools;
using Game.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static Colossal.AssetPipeline.Importers.DefaultTextureImporter;
using static Colossal.AssetPipeline.Importers.TextureImporter;
using static Colossal.IO.AssetDatabase.MaterialLibrary;
using static Game.Rendering.ManagedBatchSystem;
using static Game.Tools.ValidationSystem;
using static Game.UI.NameSystem;
using static RoadVisualTweaks.Setting;
using static RoadVisualTweaks.Systems.ReplaceRoadTextureSystem;

namespace RoadVisualTweaks.Systems
{
    [BurstCompile]
    public struct ImageBrightnessHueJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Color32> input;
        [WriteOnly] public NativeArray<Color32> output;

        public float brightness;
        public float hue;

        public void Execute(int i)
        {
            Color32 c = input[i];
            UnityEngine.Color.RGBToHSV(c, out float h, out float s, out float v);

            Color32 co = UnityEngine.Color.HSVToRGB((h + hue) % 1.0f, s, v);

            output[i] = new Color32(
                (byte)(math.min(co.r * brightness, 255)),
                (byte)(math.min(co.g * brightness, 255)),
                (byte)(math.min(co.b * brightness, 255)),
                c.a
            );
        }
    }

    [BurstCompile]
    public struct ImageBrightnessOpacityJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Color32> input;
        [WriteOnly] public NativeArray<Color32> output;

        public float brightness;
        public float opacity;

        public void Execute(int i)
        {
            Color32 c = input[i]; 

            output[i] = new Color32(
                (byte)(math.min(c.r * brightness, 255)),
                (byte)(math.min(c.g * brightness, 255)),
                (byte)(math.min(c.b * brightness, 255)),
                (byte)(c.a * opacity)
            );
        }
    }

    static class ConfigurableMaterialNames
    {
        public const string CarLane = "CarLane";
        public const string GravelLane = "GravelLane";
        public const string BusLane = "BusLane";
        public const string BikeLane150cm = "BikeLane150cm";
        public const string BikePathPavementSide0cm = "BikePathPavementSide0cm"; // BikePathPavement300cm
        public const string BikePathPavementMiddle300cm = "BikePathPavementMiddle300cm";
        public const string BikePathPavement800cm = "BikePathPavement800cm";
    }

    public partial class ReplaceRoadTextureSystem : GameSystemBase
    {

        public class ConfigurableMaterial
        {
            public UnityEngine.Material material { get; set; } // reference to the material
            public UnityEngine.Texture2D vanillaColorTexture { get; set; } // reference to the vanilla texture 
            public UnityEngine.Texture2D vanillaNormalTexture { get; set; } // reference to the vanilla texture 
            public float vanillaSmoothness { get; set; }

            // The color texture is cached temporarily since the user might apply color adjustments to it

            public Stopwatch cachedTextureStopWatch; // stopwatch for the cached textures
            public UnityEngine.Texture2D cachedSourceColorTexture { get; set; } // catched texture which only remain alive for a short duration (when user is tweaking the sliders)

            // If the adjusted color texture is used, it is finally stored here
            public UnityEngine.Texture2D generatedColorTexture { get; set; }

            // If the custom normal texture is used, it is finally stored here
            public UnityEngine.Texture2D generatedNormalTexture { get; set; }

            public bool usesWorldSpace = false; // true for BikePathPavementMiddle300cm, i'm not sure why this material doesn't use the BaseColor

            public bool? prevOverrideEnable = null;
            public TextureVariantEnum? prevTextureVariant = null;
            public float? prevTextureBrightness = null;
            public float? prevTextureHue = null;
            public float? prevTextureOpacity = null;
            public float? prevTextureSmoothness = null;

            public bool obtainedMaterial = false; // when this is true, it means we have obtained the target material
            public bool obtainedMaterialReplaced = false; // when this is true, it means we have obtained the target material and generated the replacement textures for them
        
            public ConfigurableMaterial()
            {
                cachedTextureStopWatch = new Stopwatch();
            }
        }

        private Dictionary<string, ConfigurableMaterial> configurableMaterials = new Dictionary<string, ConfigurableMaterial>
        {
            { ConfigurableMaterialNames.CarLane, new ConfigurableMaterial() },
            { ConfigurableMaterialNames.GravelLane, new ConfigurableMaterial() },
            { ConfigurableMaterialNames.BusLane, new ConfigurableMaterial() },
            { ConfigurableMaterialNames.BikeLane150cm, new ConfigurableMaterial() },
            { ConfigurableMaterialNames.BikePathPavementSide0cm, new ConfigurableMaterial() },
            { ConfigurableMaterialNames.BikePathPavementMiddle300cm, new ConfigurableMaterial() },
            { ConfigurableMaterialNames.BikePathPavement800cm, new ConfigurableMaterial() },
        };
      
        private PrefabSystem prefabSystem;
        private ManagedBatchSystem managedBatchSystem;

        private int batchedMaterialCount = 0;

        bool inGameOrEditor = false;
        
        protected override void OnCreate()
        {
            base.OnCreate();

            prefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            managedBatchSystem = World.GetOrCreateSystemManaged<ManagedBatchSystem>();
        }
        protected override void OnGameLoadingComplete(Colossal.Serialization.Entities.Purpose purpose, GameMode mode) {
            if((mode & GameMode.GameOrEditor) != 0)
            {
                Mod.log.Info("user is in Game or Editor mode");
                inGameOrEditor = true;
            } else
            {
                Mod.log.Info("user is not in Game or Editor mode");
                inGameOrEditor = false;
            }
        }
        // Store a reference to the relevant materials, as well as the original vanilla textures in a dictionary,
        // for easy retrieval later,
        // returns true if something changed
        private bool ObtainMaterialsAndTexturesReferences()
        {
            // Obtains the materials and textures used,
            // but note that can only obtain materials that are actually used,
            // so for example, if no Bicycle Paths are used,
            // the BikeLane material will not appear, i.e. it will only appear later when the user places it.
            //
            //
            // the game essentially creates "batch" materials from the materials from prefabs
            // - note that this behaviour is not from the Unity Engine but something the developers of this game have implemented,
            // and it turns out that the road lanes uses batched materials.
            //
            // There are lesser batched materials than all materials, so it should be quicker to lookup,
            // but otherwise the other method works too
            //
            //
            // I also tried obtaining the materials from the ECS prefabs and updating them,
            // but it doesn't seem to automatically update the batched materials
            //
            // (The other method was to iterate over UnityEngine.Material[] materials = Resources.FindObjectsOfTypeAll<UnityEngine.Material>();)

            if (managedBatchSystem.materialCount == batchedMaterialCount) return false;

            // This means managedBatchSystem.materialCount changed,
            // so probably new materials were added.
            //
            // While this is not a foolproof method of detecting changes in materials,
            // it should work good enough, and it helps to prevent my code from running too frequently

            batchedMaterialCount = managedBatchSystem.materialCount;

            Mod.log.Info($"Batched materials count changed to {batchedMaterialCount}, trying to obtain the materials");

            bool changedFlag = false;

            // This is not optimized
            foreach (KeyValuePair<string, ConfigurableMaterial> pair1 in configurableMaterials)
            {
                string name = pair1.Key;
                ConfigurableMaterial configurableMaterial = pair1.Value;

                // We start to search if we have not obtained the target material
                if(!configurableMaterial.obtainedMaterial) {
                    foreach (KeyValuePair<ManagedBatchSystem.MaterialKey, UnityEngine.Material> pair2 in managedBatchSystem.materials)
                    {
                        UnityEngine.Material material = pair2.Value;
                        //Mod.log.Info($"batched material name: {material.name} ");

                        {
                            if (material.name.StartsWith("Batch (" + name))
                            {
                                configurableMaterial.material = material;
                               
                                Mod.log.Info("The following was successfully found");
                                Mod.log.Info($"->->material name: {material.name}");

                                changedFlag = true;
                                configurableMaterial.obtainedMaterial = true;

                                configurableMaterial.usesWorldSpace = true; // lets see if the below overrides this

                                if (material.HasProperty("_BaseColorMap"))
                                {
                                    UnityEngine.Texture colorTexture = material.GetTexture("_BaseColorMap"); // Get the texture assigned to _BaseColorMap
                                    if (colorTexture != null)
                                    {
                                        Mod.log.Info($"->->->color texture name: {colorTexture.name}");
                                        configurableMaterial.vanillaColorTexture = material.GetTexture("_BaseColorMap") as Texture2D; // Get the texture assigned to _BaseColorMap

                                        configurableMaterial.usesWorldSpace = false;
                                        //configurableMaterial.vanillaColorTexture = ReadUnreadableTexture(material.GetTexture("_BaseColorMap") as Texture2D); // Get the texture assigned to _BaseColorMap
                                    } 
                                }

                                if (material.HasProperty("_NormalMap"))
                                {
                                    UnityEngine.Texture normalTexture = material.GetTexture("_NormalMap"); // Get the texture assigned to _NormalMap
                                    if (normalTexture != null)
                                    {
                                        Mod.log.Info($"->->->normal texture name: {normalTexture.name}");
                                        configurableMaterial.vanillaNormalTexture = material.GetTexture("_NormalMap") as Texture2D; // Get the texture assigned to _NormalMap

                                        configurableMaterial.usesWorldSpace = false;
                                        //configurableMaterial.vanillaNormalTexture = ReadUnreadableTexture(material.GetTexture("_NormalMap") as Texture2D, true); // Get the texture assigned to _NormalMap
                                    }
                                }

                                if (material.HasProperty("_Smoothness"))
                                {
                                    float v = material.GetFloat("_Smoothness");
                                    Mod.log.Info($"->->->smoothness value: {v}");
                                    configurableMaterial.vanillaSmoothness = v;
                                }


                                // If material does not have any textures in _BaseColorMap, or _NormalMap,
                                // we try _WorldspaceAlbedo and _WorldspaceNormalMap
                                if (configurableMaterial.usesWorldSpace)
                                {
                                    if (material.HasProperty("_WorldspaceAlbedo"))
                                    {
                                        UnityEngine.Texture colorTexture = material.GetTexture("_WorldspaceAlbedo"); // Get the texture assigned to _BaseColorMap
                                        if (colorTexture != null)
                                        {
                                            Mod.log.Info($"->->->_WorldspaceAlbedo texture name: {colorTexture.name}");
                                            configurableMaterial.vanillaColorTexture = material.GetTexture("_WorldspaceAlbedo") as Texture2D; // Get the texture assigned to _BaseColorMap

                                            //configurableMaterial.vanillaColorTexture = ReadUnreadableTexture(material.GetTexture("_BaseColorMap") as Texture2D); // Get the texture assigned to _BaseColorMap
                                        } 
                                    }

                                    if (material.HasProperty("_WorldspaceNormalMap"))
                                    {
                                        UnityEngine.Texture normalTexture = material.GetTexture("_WorldspaceNormalMap"); // Get the texture assigned to _NormalMap
                                        if (normalTexture != null)
                                        {
                                            Mod.log.Info($"->->->_WorldspaceNormalMap  texture name: {normalTexture.name}");
                                            configurableMaterial.vanillaNormalTexture = material.GetTexture("_WorldspaceNormalMap") as Texture2D; // Get the texture assigned to _NormalMap
                                            
                                            //configurableMaterial.vanillaNormalTexture = ReadUnreadableTexture(material.GetTexture("_NormalMap") as Texture2D, true); // Get the texture assigned to _NormalMap
                                        }
                                    }
                                }

                                PrintAllProperties(material);
                            }
                        }
                    }
                }
            }

            return changedFlag;

            /*EntityQueryDesc[] queryDesc = new[]
            {
                new EntityQueryDesc
                {
                    All = new[]
                    {
                        ComponentType.ReadOnly<NetLaneData>(),
                        ComponentType.ReadOnly<SubMesh>(),
                        ComponentType.ReadOnly<CarLaneData>(), // filters for only CarLane (includes car, bus, bicycle lanes, and other stuff like trams)
                    }
                },
            };

            var query = GetEntityQuery(queryDesc);
            var entities = query.ToEntityArray(Allocator.Temp);

            foreach (Entity entity in entities)
            {
                if (prefabSystem.TryGetPrefab<PrefabBase>(entity, out PrefabBase prefab))
                {
                    if (prefab is NetLaneGeometryPrefab)
                    {
                        NetLaneGeometryPrefab netLaneGeometryPrefab = prefab as NetLaneGeometryPrefab;

                        //Mod.log.Info("netlane prefab: " + prefab.name);

                        //foreach (ComponentBase c in prefab.components)
                        //{
                        //    Mod.log.Info("c: " + c.name);
                        //}

                        foreach (NetLaneMeshInfo mi in netLaneGeometryPrefab.m_Meshes)
                        {
                            //Mod.log.Info("  mesh name: " + mi.m_Mesh.name);

                            UnityEngine.Material[] materialse = mi.m_Mesh.ObtainMaterials();
                            foreach (UnityEngine.Material material in materialse)
                            {
                                //Mod.log.Info("    material name: " + material.name);

                                foreach (string name in materialNames)
                                {
                                    if (material.name.StartsWith(name))
                                    {
                                        if (!ConfigurableMaterials.ContainsKey(name))
                                        {
                                            ConfigurableMaterial ConfigurableMaterial = new ConfigurableMaterial();

                                            ConfigurableMaterial.material = material;
                                            ConfigurableMaterial.vanillaColorTexture = material.GetTexture("_BaseColorMap") as Texture2D; // Get the texture assigned to _BaseColorMap
                                            ConfigurableMaterial.vanillaNormalTexture = material.GetTexture("_NormalMap") as Texture2D; // Get the texture assigned to _NormalMap
                                            
                                            Mod.log.Info("The following was successfully found");
                                            Mod.log.Info($"netlane prefab: {prefab.name}");
                                            Mod.log.Info($"->mesh name: {mi.m_Mesh.name}");
                                            Mod.log.Info($"->->material name: {material.name}");
                                            if (material.HasProperty("_BaseColorMap"))
                                            {
                                                UnityEngine.Texture colorTexture = material.GetTexture("_BaseColorMap"); // Get the texture assigned to _BaseColorMap
                                                if (colorTexture != null)
                                                {
                                                    Mod.log.Info($"->->->color texture name: {colorTexture.name}");
                                                }
                                            }
                                            if (material.HasProperty("_NormalMap"))
                                            {
                                                UnityEngine.Texture normalTexture = material.GetTexture("_NormalMap"); // Get the texture assigned to _NormalMap
                                                if (normalTexture != null)
                                                {
                                                    Mod.log.Info($"->->->normal texture name: {normalTexture.name}");
                                                }
                                            }
                                            if (material.HasProperty("_Smoothness"))
                                            {
                                                float v = material.GetFloat("_Smoothness"); 
                                                 Mod.log.Info($"->->->smoothness value: {v}");
                                            }

                                            ConfigurableMaterials.Add(name, ConfigurableMaterial);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }*/
        }


        public void UpdateMaterialsAndTextures(bool updateAlreadyReplacedMaterials)
        {
            // Note that UpdateMaterialsAndTextures() may also be called by settings

            // This is not optimized, so if user changes the settings, all textures will be regenerated
            foreach (KeyValuePair<string, ConfigurableMaterial> pair in configurableMaterials)
            {
                string name = pair.Key;
                ConfigurableMaterial configurableMaterial = pair.Value;
                
                // We only update the material when we have obtained it,
                // and we update once, or update it when the setting is changed
                if(configurableMaterial.obtainedMaterial &&
                    (updateAlreadyReplacedMaterials || !configurableMaterial.obtainedMaterialReplaced)) {
                    
                    configurableMaterial.obtainedMaterialReplaced = true;

                    TextureVariantEnum textureVariant = TextureVariantEnum.Vanilla;
                    float textureBrightness = 0.0f;
                    float textureOpacity = 0.0f;
                    float textureHue = 0.0f;
                    float textureSmoothness = 0.0f;
                    bool overrideEnable = false;

                    if (name == ConfigurableMaterialNames.CarLane) {
                        overrideEnable = Mod.MySetting.CarRoadWearOverrideEnable;
                        textureVariant = Mod.MySetting.CarRoadWearTextureVariant;
                        textureBrightness = Mod.MySetting.CarRoadWearTextureBrightness;
                        textureOpacity = Mod.MySetting.CarRoadWearTextureOpacity;
                        textureSmoothness = Mod.MySetting.CarRoadWearTextureSmoothness;
                    } else if (name == ConfigurableMaterialNames.GravelLane)
                    {
                        overrideEnable = Mod.MySetting.GravelRoadWearOverrideEnable;
                        textureVariant = Mod.MySetting.GravelRoadWearTextureVariant;
                        textureBrightness = Mod.MySetting.GravelRoadWearTextureBrightness;
                        textureOpacity = Mod.MySetting.GravelRoadWearTextureOpacity;
                        textureSmoothness = Mod.MySetting.GravelRoadWearTextureSmoothness;
                    } else if (name == ConfigurableMaterialNames.BusLane) {
                        overrideEnable = Mod.MySetting.BusLaneOverrideEnable;
                        textureVariant = Mod.MySetting.BusLaneTextureVariant;
                        textureBrightness = Mod.MySetting.BusLaneTextureBrightness;
                        textureHue = Mod.MySetting.BusLaneTextureHue;
                        textureSmoothness = Mod.MySetting.BusLaneTextureSmoothness;
                    } else if (name == ConfigurableMaterialNames.BikeLane150cm ||
                              name == ConfigurableMaterialNames.BikePathPavementSide0cm || 
                              name == ConfigurableMaterialNames.BikePathPavementMiddle300cm ||
                              name == ConfigurableMaterialNames.BikePathPavement800cm) {
                        overrideEnable = Mod.MySetting.BicycleLaneOverrideEnable;
                        textureVariant = Mod.MySetting.BicycleLaneTextureVariant;
                        textureBrightness = Mod.MySetting.BicycleLaneTextureBrightness;
                        textureHue = Mod.MySetting.BicycleLaneTextureHue;
                        textureSmoothness = Mod.MySetting.BicycleLaneTextureSmoothness;
                    }

                    if (
                        (configurableMaterial.prevOverrideEnable != overrideEnable) ||
                        (configurableMaterial.prevTextureVariant != textureVariant) ||
                        (configurableMaterial.prevTextureBrightness != textureBrightness) ||
                        (configurableMaterial.prevTextureHue != textureHue) ||
                        (configurableMaterial.prevTextureOpacity != textureOpacity) ||
                        (configurableMaterial.prevTextureSmoothness != textureSmoothness)
                    )
                    {
                        Mod.log.Info($"updating material: {name}");


                        string normalKey = configurableMaterial.usesWorldSpace ? "_WorldspaceNormalMap" : "_NormalMap";
                        string colorKey = configurableMaterial.usesWorldSpace ? "_WorldspaceAlbedo" : "_BaseColorMap";

                        if (!overrideEnable)
                        {
                            // If not enabled, just use the vanilla textures
                            configurableMaterial.material.SetTexture(normalKey, configurableMaterial.vanillaNormalTexture); // note that some materials do not have a default normal map
                            configurableMaterial.material.SetTexture(colorKey, configurableMaterial.vanillaColorTexture);
                            configurableMaterial.material.SetFloat("_Smoothness", configurableMaterial.vanillaSmoothness);
                        }
                        else
                        {
                            DefaultTextureImporter defaultTextureImporter = ImporterCache.GetImporter(".png") as DefaultTextureImporter;

                            if (!configurableMaterial.cachedTextureStopWatch.IsRunning || configurableMaterial.prevTextureVariant != textureVariant)
                            {
                                // Cache is invalid, reload the textures

                                if (configurableMaterial.cachedSourceColorTexture != null)
                                {
                                    UnityEngine.Object.Destroy(configurableMaterial.cachedSourceColorTexture);
                                }

                                if (textureVariant == TextureVariantEnum.Vanilla)
                                {
                                    configurableMaterial.cachedSourceColorTexture = ReadUnreadableTexture(configurableMaterial.vanillaColorTexture);
                                }
                                else
                                { // Currently only supports 1 alternative texture
                                    // ----- Load colour texture -----

                                    string colourFilePath = Path.Combine(Mod.myModFolder, "textures", "reflavoured_" + name + "_Color.png");

                                    // I am using the game's assetpipeline importer to load the colour texture from the png file
                                    // However, to be able to modify the texture, I have to copy it (uncompressed) to a regular Unity Texture2D
                                    ImportSettings importSettings = ImportSettings.GetDefault();
                                    importSettings.compressBC = false;
                                    importSettings.computeMips = false;

                                    TextureImporter.Texture tempColorTexture = defaultTextureImporter.Import(importSettings, colourFilePath);
                                    // copy the texture as a UnityEngine.Texture2D
                                    configurableMaterial.cachedSourceColorTexture = (Texture2D)(tempColorTexture.ToUnityTexture());
                                    tempColorTexture.Dispose();
                                }
                            }


                            if(configurableMaterial.prevTextureVariant != textureVariant)
                            {

                                if (configurableMaterial.generatedNormalTexture != null)
                                {
                                    UnityEngine.Object.Destroy(configurableMaterial.generatedNormalTexture);
                                }

                                if (textureVariant != TextureVariantEnum.Vanilla)
                                {
                                    // ----- Load normal texture -----
                                    string normalFilePath = Path.Combine(Mod.myModFolder, "textures", "reflavoured_" + name + "_Normal.png");

                                    ImportSettings importSettings2 = ImportSettings.GetDefault();
                                    importSettings2.normalMap = true;
                                    importSettings2.alphaIsTransparency = false;

                                    if (File.Exists(normalFilePath))
                                    {
                                        TextureImporter.Texture temp2 = defaultTextureImporter.Import(importSettings2, normalFilePath);

                                        configurableMaterial.generatedNormalTexture = (Texture2D)(temp2.ToUnityTexture());
                                        configurableMaterial.generatedNormalTexture.name = name + "_Normal_custom";

                                        temp2.Dispose();
                                    }
                                    else
                                    {
                                        Mod.log.Info($"{normalFilePath} is not found (this is not a warning)");
                                    }
                                }
                            }

                            NativeArray<Color32> rawColorPixels = configurableMaterial.cachedSourceColorTexture.GetPixelData<Color32>(0);
                           
                            // Always destroy the generatedColorTexture, since we will recreate a new one
                            if (configurableMaterial.generatedColorTexture != null)
                            {
                                UnityEngine.Object.Destroy(configurableMaterial.generatedColorTexture);
                            }

                            configurableMaterial.generatedColorTexture = new Texture2D(configurableMaterial.cachedSourceColorTexture.width, configurableMaterial.cachedSourceColorTexture.height);
                            configurableMaterial.generatedColorTexture.name = name + "_BaseColor_custom";

      
                            int imageLen = rawColorPixels.Length;

                            NativeArray<Color32> output = new NativeArray<Color32>(imageLen, Allocator.TempJob);

                            if (name == ConfigurableMaterialNames.BusLane ||
                                name == ConfigurableMaterialNames.BikeLane150cm ||
                                name == ConfigurableMaterialNames.BikePathPavementSide0cm ||
                                name == ConfigurableMaterialNames.BikePathPavementMiddle300cm ||
                                name == ConfigurableMaterialNames.BikePathPavement800cm)
                            {
                                var job = new ImageBrightnessHueJob
                                {
                                    input = rawColorPixels,
                                    output = output,
                                    brightness = textureBrightness,
                                    hue = textureHue
                                };

                                JobHandle handle = job.Schedule(imageLen, 64);
                                handle.Complete();
                            }
                            else
                            {
                                var job = new ImageBrightnessOpacityJob
                                {
                                    input = rawColorPixels,
                                    output = output,
                                    brightness = textureBrightness,
                                    opacity = textureOpacity
                                };

                                JobHandle handle = job.Schedule(imageLen, 64);
                                handle.Complete();
                            }

                            Color32[] modifiedPixels = output.ToArray();

                            configurableMaterial.generatedColorTexture.SetPixels32(modifiedPixels);
                            configurableMaterial.generatedColorTexture.Apply(true);

                            output.Dispose();

                            if (textureVariant == TextureVariantEnum.Vanilla)
                            {
                                configurableMaterial.material.SetTexture(normalKey, configurableMaterial.vanillaNormalTexture); // note that some materials do not have a default normal map
                            }
                            else
                            {
                                configurableMaterial.material.SetTexture(normalKey, configurableMaterial.generatedNormalTexture); // note that some materials do not have a default normal map
                            }
  
                            configurableMaterial.material.SetTexture(colorKey, configurableMaterial.generatedColorTexture);
                            configurableMaterial.material.SetFloat("_Smoothness", textureSmoothness);
                            
                            // start stopwatch for the cache
                            configurableMaterial.cachedTextureStopWatch.Restart();
                        }


                    configurableMaterial.prevOverrideEnable = overrideEnable;
                    configurableMaterial.prevTextureVariant = textureVariant;
                    configurableMaterial.prevTextureBrightness = textureBrightness;
                    configurableMaterial.prevTextureHue = textureHue;
                    configurableMaterial.prevTextureOpacity = textureOpacity;
                    configurableMaterial.prevTextureSmoothness = textureSmoothness;
                }
                }
            }

            Mod.log.Info("update done!");
        }

        protected override void OnUpdate()
        {
            // only update when the game has fully loaded (it helps to prevent unnecessary calls)
            if (inGameOrEditor)
            { 
                if (ObtainMaterialsAndTexturesReferences())
                {
                    // Only update if changed
                    UpdateMaterialsAndTextures(false);
                }
            }

            // Free up cached texture resources after a short time
            foreach (KeyValuePair<string, ConfigurableMaterial> pair in configurableMaterials)
            {
                if(pair.Value.cachedTextureStopWatch.ElapsedMilliseconds > 10000)
                {
                    pair.Value.cachedTextureStopWatch.Stop();
                }
            }
        }

        /*void createRedTexture(Texture2D newTexture)
        {
            //Set all pixels to red
            UnityEngine.Color c;
            c.r = 0.8f;
            c.g = 0.15f;
            c.b = 0.15f;
            c.a = 0.15f;

            UnityEngine.Color[] pixels = new UnityEngine.Color[1024 * 1024];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = c;
            }
            newTexture.SetPixels(pixels);
            newTexture.Apply();
        }*/

        public void DebugCommand()
        {
            Mod.log.Info("Executing debug code");

            string localAppDataDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string localLowDirectory = Path.Combine(localAppDataDirectory, "..", "LocalLow");
            string modDataDirectory = Path.Combine(localLowDirectory, "Colossal Order", "Cities Skylines II", "ModsData", "RoadVisualTweaks");

            foreach (KeyValuePair<string, ConfigurableMaterial> pair1 in configurableMaterials)
            {
                string name = pair1.Key;
                ConfigurableMaterial configurableMaterial = pair1.Value;

                Texture2D colorTexture = configurableMaterial.vanillaColorTexture;
                Texture2D normalTexture = configurableMaterial.vanillaNormalTexture;

                Mod.log.Info($"processing {name}");

                if (colorTexture != null)
                {

                    string fileName = "reflavoured_" + name + "_Color.png";
                    Mod.log.Info("dumping the above texture as " + fileName);

                    DumpTexture(colorTexture, modDataDirectory, fileName);
                } else
                {
                    Mod.log.Info("vanillaColorTexture is null");
                }

                if (normalTexture != null)
                {

                    string fileName = "reflavoured_" + name + "_Normal.png";
                    Mod.log.Info("dumping the above texture as " + fileName);

                    DumpTexture(normalTexture, modDataDirectory, fileName, true);
                } else
                {
                    Mod.log.Info("vanillaNormalTexture is null");
                }
            }

            Mod.log.Info("Entity query for net lanes method");
            EntityQueryDesc[] queryDesc = new[]
            {
                new EntityQueryDesc
                {
                    All = new[]
                    {
                        ComponentType.ReadOnly<NetLaneData>(),
                        ComponentType.ReadOnly<SubMesh>(),
                        ComponentType.ReadOnly<CarLaneData>(), // filters for only CarLane (includes car, bus, bicycle lanes, and other stuff like trams)
                    }
                },
            };

            var query = GetEntityQuery(queryDesc);
            var entities = query.ToEntityArray(Allocator.Temp);

            foreach ( Entity entity in entities ) {
                if (prefabSystem.TryGetPrefab<PrefabBase>(entity, out PrefabBase prefab))
                {
                    if (prefab is NetLaneGeometryPrefab)
                    {
                        NetLaneGeometryPrefab netLaneGeometryPrefab = prefab as NetLaneGeometryPrefab;

                        Mod.log.Info($"type: {prefab.GetType()}, name: {prefab.name}");

                        foreach (ComponentBase c in prefab.components)
                        {
                            Mod.log.Info("->c: " + c.name);
                        }

                        foreach (NetLaneMeshInfo mi in netLaneGeometryPrefab.m_Meshes)
                        {
                            Mod.log.Info("->mesh name: " + mi.m_Mesh.name);

                            //UnityEngine.Material[] materialse = mi.m_Mesh.ObtainMaterials();
                            UnityEngine.Material[] materialse = mi.m_Mesh.ObtainMaterials();
                            foreach (UnityEngine.Material material in materialse)
                            {  
                                Mod.log.Info($"->->material name: {material.name}");

                                PrintAllProperties(material);
                                /*if (material.HasProperty("_BaseColorMap"))
                                {
                                    UnityEngine.Texture colorTexture = material.GetTexture("_BaseColorMap"); // Get the texture assigned to _BaseColorMap
                                    if (colorTexture != null)
                                    {
                                        Mod.log.Info($"->->->color texture name: {colorTexture.name}");
                                    }
                                }
                                if (material.HasProperty("_NormalMap"))
                                {
                                    UnityEngine.Texture normalTexture = material.GetTexture("_NormalMap"); // Get the texture assigned to _NormalMap
                                    if (normalTexture != null)
                                    {
                                        Mod.log.Info($"->->->normal texture name: {normalTexture.name}");
                                    }
                                }

                                if (material.HasProperty("_BaseColorMap"))
                                {
                                    UnityEngine.Texture colorTexture = material.GetTexture("_BaseColorMap"); // Get the texture assigned to _BaseColorMap
                                    if (colorTexture != null)
                                    {
                                        Mod.log.Info($"->->->color texture name: {colorTexture.name}");
                                    }
                                }
                                if (material.HasProperty("_NormalMap"))
                                {
                                    UnityEngine.Texture normalTexture = material.GetTexture("_NormalMap"); // Get the texture assigned to _NormalMap
                                    if (normalTexture != null)
                                    {
                                        Mod.log.Info($"->->->normal texture name: {normalTexture.name}");
                                    }
                                }

                                if (material.HasProperty("_Smoothness"))
                                {
                                    float v = material.GetFloat("_Smoothness");
                                    Mod.log.Info($"->->->smoothness value: {v}");
                                }*/
                            }

                                /*for (int i = 0; i < mi.m_Mesh.materialCount; i++)
                                    IReadOnlyDictionary<string, TextureAsset> textures = mi.m_Mesh.GetSurfaceAsset(i).textures;

                                foreach (KeyValuePair<string, TextureAsset> pair in textures)
                                {
                                    Mod.log.Info("Texture key: " + pair.Key + ", name: " + pair.Value.name);
                                }

                                IReadOnlyDictionary<string, UnityEngine.Color> colors = mi.m_Mesh.GetSurfaceAsset(i).colors;


                                foreach (KeyValuePair<string, UnityEngine.Color> pair in colors)
                                {
                                    Mod.log.Info("Color key: " + pair.Key + ", name: " + pair.Value.ToString());
                                }

                                IReadOnlyDictionary<string, float> floats = mi.m_Mesh.GetSurfaceAsset(i).floats;


                                foreach (KeyValuePair<string, float> pair in floats)
                                {
                                    Mod.log.Info("Float key: " + pair.Key + ", name: " + pair.Value);
                                }*/


                                //if( mi.m_Mesh.GetSurfaceAsset(i).TryGetTexture("_BaseColorMap", out TextureAsset tex) ){
                                //Mod.log.Info($"tex {tex.name} {tex.isValid}");
                                //newtex.
                                //newtex.SetData(ConfigurableMaterials["CarLane"].generatedColorTexture);

                                //  TextureAsset newTex = AssetDatabase.GetTransient(0).AddAsset(ConfigurableMaterials["CarLane"].generatedColorTexture)

                                //   mi.m_Mesh.GetSurfaceAsset(i).UpdateTexture("_BaseColorMap", newTex);
                                // }

                                /*   
                                //mi.m_Mesh.GetSurfaceAsset(i).UpdateTexture("_BaseColorMap", newTex);

                                Type type = mi.m_Mesh.GetSurfaceAsset(i).GetType();


                                FieldInfo field = type.GetField("s_DefaultMaterial", BindingFlags.NonPublic | BindingFlags.Static);
                                if (field != null)
                                {

                                    UnityEngine.Material material = (UnityEngine.Material)field.GetValue(null);


                                    Mod.log.Info("material name: " + material.name);

                                    UnityEngine.Texture colorTexture = material.GetTexture("_BaseColorMap"); // Get the texture assigned to _BaseColorMap
                                    if (colorTexture != null)
                                    {
                                        // It is still possible for texture slot to be null, if no texture is assigned to it
                                        Mod.log.Info("texture name in _BaseColorMap: " + colorTexture.name);
                                        // Extra: LaneLine_BaseColor is a texture atlas for the lane lines like crosswalks, center lines, etc.
                                        if (colorTexture.name.StartsWith("CarLane_BaseColor") || colorTexture.name.StartsWith("BusLane_BaseColor") || colorTexture.name.StartsWith("GravelLane_BaseColor") || colorTexture.name.StartsWith("BikeLane150cm_BaseColor"))
                                        {
                                            // Dump original texture to PNG for debugging
                                            string fileName = "reflavoured_" + colorTexture.name.Substring(0, colorTexture.name.IndexOf("_BaseColor")) + "_Color.png";
                                            Mod.log.Info("dumping the above texture as " + fileName);

                                            DumpTexture(colorTexture, modDataDirectory, fileName);
                                        }
                                    }

                                    UnityEngine.Texture normalTexture = material.GetTexture("_NormalMap"); // Get the texture assigned to _BaseColorMap
                                    if (normalTexture != null)
                                    {
                                        // It is still possible for texture slot to be null, if no texture is assigned to it
                                        Mod.log.Info("texture name in _NormalMap: " + normalTexture.name);
                                        // Extra: LaneLine_BaseColor is a texture atlas for the lane lines like crosswalks, center lines, etc.
                                        if (normalTexture.name.StartsWith("CarLane_Normal") || normalTexture.name.StartsWith("BusLane_Normal") || normalTexture.name.StartsWith("GravelLane_Normal") || normalTexture.name.StartsWith("BikeLane150cm_Normal"))
                                        {
                                            // Dump original texture to PNG for debugging
                                            string fileName = "reflavoured_" + normalTexture.name.Substring(0, normalTexture.name.IndexOf("_Normal")) + "_Normal.png";
                                            Mod.log.Info("dumping the above texture as " + fileName);

                                            DumpTexture(normalTexture, modDataDirectory, fileName);
                                        }
                                    }

                                    PrintAllProperties(material); 
                                }*/
                            
                        }
                    }
                }
            }

            /*Mod.log.Info("Entity query for net piece method");
            EntityQueryDesc[] queryDesc2 = new[]
            {
                new EntityQueryDesc
                {
                    All = new[]
                    {
                        ComponentType.ReadOnly<NetPieceData>(), // <- the most important criteria
                        ComponentType.ReadOnly<PlaceableNetPiece>(),
                        ComponentType.ReadOnly<MeshMaterial>()
                        //ComponentType.ReadOnly<SubMesh>()
                    }
                },
            };

            var query2 = GetEntityQuery(queryDesc2);
            var entities2 = query2.ToEntityArray(Allocator.Temp);

            foreach (Entity entity in entities2)
            {
                if (prefabSystem.TryGetPrefab<PrefabBase>(entity, out PrefabBase prefab))
                {
                   

                    Mod.log.Info($"type: {prefab.GetType()}, name: {prefab.name}");
                     
                    foreach (ComponentBase c in prefab.components)
                    {
                        Mod.log.Info("->c: " + c.name);
                    }

                    if (prefab is NetPiecePrefab)
                    {
                        NetPiecePrefab netPiecePrefab = prefab as NetPiecePrefab;
                         
                        UnityEngine.Material[] materialse = netPiecePrefab.ObtainMaterials();
                        foreach (UnityEngine.Material material in materialse)
                        {
                            Mod.log.Info($"->->material name: {material.name}");

                            PrintAllProperties(material);
                        } 
                    }


                    /*foreach (NetLaneMeshInfo mi in prefab.m_Meshes)
                    {
                        Mod.log.Info("->mesh name: " + mi.m_Mesh.name);

                        //UnityEngine.Material[] materialse = mi.m_Mesh.ObtainMaterials();
                        UnityEngine.Material[] materialse = mi.m_Mesh.ObtainMaterials();
                        foreach (UnityEngine.Material material in materialse)
                        {
                            Mod.log.Info($"->->material name: {material.name}");

                            PrintAllProperties(material);
                        }
                    }*/
                /*}
            }*/
            Mod.log.Info("ManagedBatchSystem method");
            Mod.log.Info($"There are {managedBatchSystem.materialCount} batched materials");

            // Obtains all batched materials used,
            // the game essentially creates "batch" materials from the materials from prefabs
            // - note that this behaviour is not from the Unity Engine but something the developers of this game have implemented,
            // and it turns out that the road lanes uses batched materials.
            //
            // There are lesser batched materials than all materials, so I think its quicker to lookup,
            // but other wise the legacy method works too
            //
            // I also tried obtaining the materials from the ECS prefabs and updating them,
            // but it doesn't seem to automatically update the batched materials
       

            foreach (KeyValuePair<ManagedBatchSystem.MaterialKey, UnityEngine.Material> pair in managedBatchSystem.materials)
            {
                UnityEngine.Material material = pair.Value;
                Mod.log.Info($"batched materials: {material.name} ");

                if (material.name.StartsWith("Batch (BikePathPavementMiddle300cm")) //if (material.name.StartsWith("Batch (Road"))
                {
                    if (material.HasProperty("_BaseColorMap"))
                    {
                        UnityEngine.Texture colorTexture = material.GetTexture("_BaseColorMap"); // Get the texture assigned to _BaseColorMap
                        if (colorTexture != null)
                        {
                            Mod.log.Info($"->->->color texture name: {colorTexture.name}");
                            string fileName = material.name.Substring(0, 32) + "_Color_debug.png";
                            DumpTexture(material.GetTexture("_BaseColorMap") as Texture2D, modDataDirectory, fileName); // Get the texture assigned to _NormalMap
                        }
                    }

                    if (material.HasProperty("_NormalMap"))
                    {
                        UnityEngine.Texture normalTexture = material.GetTexture("_NormalMap"); // Get the texture assigned to _NormalMap
                        if (normalTexture != null)
                        {
                            Mod.log.Info($"->->->normal texture name: {normalTexture.name}");
                            string fileName = material.name.Substring(0, 32) + "_Normal_debug.png";
                            DumpTexture(material.GetTexture("_NormalMap") as Texture2D, modDataDirectory, fileName, true); // Get the texture assigned to _NormalMap
                        }
                    }
                    if (material.HasProperty("_WorldspaceAlbedo"))
                    {
                        UnityEngine.Texture colorTexture = material.GetTexture("_WorldspaceAlbedo"); // Get the texture assigned to _BaseColorMap
                        if (colorTexture != null)
                        {
                            Mod.log.Info($"->->->_WorldspaceAlbedo texture name: {colorTexture.name}");
                            string fileName = material.name.Substring(0, 32) + "_WorldspaceAlbedo_debug.png";
                            DumpTexture(material.GetTexture("_WorldspaceAlbedo") as Texture2D, modDataDirectory, fileName); // Get the texture assigned to _NormalMap
                        }
                    }

                    if (material.HasProperty("_WorldspaceNormalMap"))
                    {
                        UnityEngine.Texture normalTexture = material.GetTexture("_WorldspaceNormalMap"); // Get the texture assigned to _NormalMap
                        if (normalTexture != null)
                        {
                            Mod.log.Info($"->->->_WorldspaceNormalMap  texture name: {normalTexture.name}");
                            string fileName = material.name.Substring(0, 32) + "_WorldspaceNormal_debug.png";
                            DumpTexture(material.GetTexture("_WorldspaceNormalMap") as Texture2D, modDataDirectory, fileName, true); // Get the texture assigned to _NormalMap
                        }
                    } 
                }
            }



            Mod.log.Info("Legacy method");
            // Legacy method of obtaining all materials used (it is a superset of the batched materials)
            UnityEngine.Material[] materials = Resources.FindObjectsOfTypeAll<UnityEngine.Material>();

            Mod.log.Info($"There are {materials.Length} materials");

            foreach (UnityEngine.Material material in materials)
            {
                Mod.log.Info($"material name: {material.name}");
                /* /if (material.HasProperty("_BaseColorMap")) // Check if the material has the property 
                if (material.HasTexture("_BaseColorMap"))
                {
                    //Mod.log.Info("material: " + material.name + ", has a texture slot in _BaseColorMap ");
                    UnityEngine.Texture texture = material.GetTexture("_BaseColorMap"); // Get the texture assigned to _BaseColorMap
                    if (texture != null)
                    {
                        // It is still possible for texture slot to be null, if no texture is assigned to it
                        // Mod.log.Info("name of texture in _BaseColorMap: " + texture.name);

                        if (texture.name.StartsWith("CarLane_BaseColor"))
                        {
                             
                            Mod.log.Info("Found car lane material name: " + material.name);
                            Mod.log.Info("Previous ECS car lane material name: " + configurableMaterials["CarLane"].material.name);
                            Mod.log.Info("Are they the same: " + (material == configurableMaterials["CarLane"].material));

                        }
                    }
                }*/

                //PrintAllProperties(material);
            } 

                /*
                                //if (material.HasProperty("_BaseColorMap")) // Check if the material has the property 
                                if (material.HasTexture("_BaseColorMap"))
                                {
                                    //Mod.log.Info("material: " + material.name + ", has a texture slot in _BaseColorMap ");
                                    UnityEngine.Texture texture = material.GetTexture("_BaseColorMap"); // Get the texture assigned to _BaseColorMap
                                    if (texture != null)
                                    {
                                        // It is still possible for texture slot to be null, if no texture is assigned to it
                                        Mod.log.Info("name of texture in _BaseColorMap: " + texture.name);

                                        if (texture.name.StartsWith("CarLane_BaseColor"))
                                        {
                                            Mod.log.Info("Found car lane material name: " + material.name);

                                            /*Mod.log.Info("Color format: " + (texture as Texture2D).format);
                                            Mod.log.Info("Color graphicsFormat: " + texture.graphicsFormat);
                                            Mod.log.Info("Color isDataSRGB: " + texture.isDataSRGB);

                                            Mod.log.Info("Normal format: " + (material.GetTexture("_NormalMap") as Texture2D).format);
                                            Mod.log.Info("Normal graphicsformat: " + material.GetTexture("_NormalMap").graphicsFormat);
                                            Mod.log.Info("Normal isDataSRGB: " + material.GetTexture("_NormalMap").isDataSRGB);

                                            Mod.log.Info("my provided Normal format " + roadWearNormalTexture.format);
                                            Mod.log.Info("my provided Normal graphicsFormat " + roadWearNormalTexture.graphicsFormat);
                                            Mod.log.Info("my provided Normal isDataSRGB " + roadWearNormalTexture.isDataSRGB);*/
                            /*}
                            else if (texture.name.StartsWith("GravelLane_BaseColor"))
                            {

                                DumpTexture(texture, modDataDirectory, "debugOG_GravelLane_BaseColor.png");


                                Mod.log.Info("Found gravel lane material name: " + material.name);
                            }
                        }
                    }
                }*/
                        }

        UnityEngine.Texture2D ReadUnreadableTexture(UnityEngine.Texture texture, bool linear=false)
        {
            if (texture == null) return null;

            // Create a temporary RenderTexture of the same size as the texture
            RenderTexture tmp = RenderTexture.GetTemporary(
                                texture.width,
                                texture.height,
                                0,
                                RenderTextureFormat.Default,
                                linear ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB);


            // Blit the pixels on texture to the RenderTexture
            Graphics.Blit(texture, tmp);


            // Backup the currently set RenderTexture
            RenderTexture previous = RenderTexture.active;


            // Set the current RenderTexture to the temporary one we created
            RenderTexture.active = tmp;


            // Create a new readable Texture2D to copy the pixels to it
            Texture2D myTexture2D = new Texture2D(texture.width, texture.height);//, TextureFormat.RGBA32, true, false);


            // Copy the pixels from the RenderTexture to the new Texture
            myTexture2D.ReadPixels(new Rect(0, 0, tmp.width, tmp.height), 0, 0);
            myTexture2D.Apply();

            // Reset the active RenderTexture
            RenderTexture.active = previous;


            // Release the temporary RenderTexture
            RenderTexture.ReleaseTemporary(tmp);

            return myTexture2D;
        }

        void DumpTexture(UnityEngine.Texture texture, string directoryPath, string fileName, bool linear=false)
        {
            Texture2D myTexture2D = ReadUnreadableTexture(texture, linear);

            byte[] bytes = myTexture2D.EncodeToPNG();

            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            File.WriteAllBytes(Path.Combine(directoryPath, fileName), bytes);

            UnityEngine.Object.Destroy(myTexture2D);
        }

        void PrintAllProperties(UnityEngine.Material material)
        {
            Mod.log.Info($"Extra info, all Textures for Material: {material.name}");

            // Get all property names from the material's shader
            Shader shader = material.shader;
            int propertyCount = shader.GetPropertyCount();

            for (int i = 0; i < propertyCount; i++)
            {
                string propertyName = shader.GetPropertyName(i);

                // Check if the property is a texture
                if (shader.GetPropertyType(i) == UnityEngine.Rendering.ShaderPropertyType.Texture)
                {
                    UnityEngine.Texture texture = material.GetTexture(propertyName);

                    if (texture != null)
                    {
                        Mod.log.Info($"  - {shader.GetPropertyType(i).ToString()} Property: {propertyName}, Texture: {texture.name}");
                    }
                    else
                    {
                        Mod.log.Info($"  - {shader.GetPropertyType(i).ToString()} Property: {propertyName}, No texture assigned.");
                    }
                } if (shader.GetPropertyType(i) == UnityEngine.Rendering.ShaderPropertyType.Range || shader.GetPropertyType(i) == UnityEngine.Rendering.ShaderPropertyType.Float)
                {
                    if (material.HasFloat(propertyName))
                    {
                        Mod.log.Info($"  - {shader.GetPropertyType(i).ToString()} Property: {propertyName}, Float: {material.GetFloat(propertyName)}");
                    }
                    else if (material.HasInteger(propertyName))
                    {
                        Mod.log.Info($"  - {shader.GetPropertyType(i).ToString()} Property: {propertyName}, Float: {material.GetInteger(propertyName)}");
                    }
                    else
                    {
                        Mod.log.Info($"  - {shader.GetPropertyType(i).ToString()} Property: {propertyName}");
                    }
                }
                else if (shader.GetPropertyType(i) == UnityEngine.Rendering.ShaderPropertyType.Color)
                {
                    Mod.log.Info($"  - {shader.GetPropertyType(i).ToString()} Property: {propertyName}, Color: {material.GetColor(propertyName).ToString()}");
                }
                else
                {
                    Mod.log.Info($"  - {shader.GetPropertyType(i).ToString()} Property: {propertyName}");
                }
            }
        }

        protected override void OnDestroy()
        {
            Mod.log.Info("cleaning up");

            foreach (KeyValuePair<string, ConfigurableMaterial> pair in configurableMaterials)
            {
                if (pair.Value.generatedColorTexture != null)
                {
                    UnityEngine.Object.Destroy(pair.Value.generatedColorTexture);
                }

                if (pair.Value.generatedNormalTexture != null)
                {
                    UnityEngine.Object.Destroy(pair.Value.generatedNormalTexture);
                }
            }
        }
    }
}