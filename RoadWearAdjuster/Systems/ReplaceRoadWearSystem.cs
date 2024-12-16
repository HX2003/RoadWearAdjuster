using Game;
using Game.Objects;
using System.IO;
using UnityEngine;
using static RoadWearAdjuster.Setting;

namespace RoadWearAdjuster.Systems
{
    public partial class ReplaceRoadWearSystem : GameSystemBase
    {
        Texture2D roadWearColourTexture;
        Texture2D roadWearNormalTexture;

        Material carLaneMaterial;
        Material gravelLaneMaterial;

        bool hasGeneratedTextures = false;

        bool hasReplacedCarLaneRoadWearTexture = false;
        bool hasReplacedGravelLaneRoadWearTexture = false;

        //Texture2D newTexture = new Texture2D(2, 2);

        protected override void OnCreate()
        {
            base.OnCreate();
            roadWearColourTexture = new Texture2D(1024, 1024);
            roadWearNormalTexture = new Texture2D(1024, 1024, TextureFormat.ARGB32, true, true); // ARGB32, (RGBA32 does not work!) linear color space
        }

        public void UpdateStoredTextures()
        {
            Mod.log.Info("generating and updating stored textures");
            string fileName = "";

            if (Mod.MySetting.TextureVariant == TextureVariantEnum.Reflavoured)
            {
                fileName = "reflavoured";
            }
            else
            {
                fileName = "vanilla";
            }

            string colourFilePath = Path.Combine(Mod.myModFolder, "textures", fileName + "_colour.png");
            byte[] colourData = File.ReadAllBytes(colourFilePath);
            roadWearColourTexture.LoadImage(colourData);

            UnityEngine.Color[] data = roadWearColourTexture.GetPixels(0);
            for (int i = 0; i < data.Length; ++i)
            {
                float textureBrightness = Mod.MySetting.TextureBrightness;
                data[i].r = data[i].r * textureBrightness;
                data[i].g = data[i].g * textureBrightness;
                data[i].b = data[i].b * textureBrightness;
                data[i].a = data[i].a * Mod.MySetting.TextureOpacity;
            }
            roadWearColourTexture.SetPixels(data);
            roadWearColourTexture.Apply(true);

            string normalFilePath = Path.Combine(Mod.myModFolder, "textures", fileName + "_normal.png");
            byte[] normalData = File.ReadAllBytes(normalFilePath);

            Texture2D tempNormalTexture = new Texture2D(2, 2);
            tempNormalTexture.LoadImage(normalData); // load image only can output srgb colour space, we need to convert to linear

            Graphics.CopyTexture(tempNormalTexture, roadWearNormalTexture);
            roadWearNormalTexture.Apply();

            UnityEngine.Object.Destroy(tempNormalTexture);

            hasGeneratedTextures = true;

            carLaneMaterial?.SetFloat("_Smoothness", Mod.MySetting.TextureSmoothness);
            gravelLaneMaterial?.SetFloat("_Smoothness", Mod.MySetting.TextureSmoothness);
        }

        private void ReplaceTextures()
        {
            //Mod.log.Info("replacing textures"); 
            // Note the shader which draws the road wear is called: BH/Decals/CurvedDecalDeteriorationShader
            // The materials which uses this shader are
            // Batch(GravelLane_a79b1a5d13f7fd94ab43236eb7ed9683)
            // Batch(CarLane_e92cf085d2bacee448d09b477491aaaf)
            // CurvedDecalDeterioration

            // Note: Batch (BusLane_fb7e671d417aaf84fb710a9c427e0729)
            // is not the road wear for bus lanes, but the bus lane color itself (ie. red)

            Material[] materials = Resources.FindObjectsOfTypeAll<Material>();

            foreach (Material material in materials)
            {
                //if (material.HasProperty("_BaseColorMap")) // Check if the material has the property 
                if (material.HasTexture("_BaseColorMap"))
                {
                    //Mod.log.Info("material: " + material.name + ", has a texture slot in _BaseColorMap ");
                    Texture texture = material.GetTexture("_BaseColorMap"); // Get the texture assigned to _BaseColorMap
                    if (texture != null)
                    {
                        // It is still possible for texture slot to be null, if no texture is assigned to it
                        //Mod.log.Info("name of texture in _BaseColorMap: " + texture.name);

                        if (texture.name.StartsWith("CarLane_BaseColor"))
                        {
                            Mod.log.Info("Found car lane material name: " + material.name);
                            material.SetTexture("_BaseColorMap", roadWearColourTexture);
                            material.SetTexture("_NormalMap", roadWearNormalTexture);
                            material.SetFloat("_Smoothness", Mod.MySetting.TextureSmoothness);
                            carLaneMaterial = material;
                            hasReplacedCarLaneRoadWearTexture = true;

                            /*Mod.log.Info("Color format: " + (texture as Texture2D).format);
                            Mod.log.Info("Color graphicsFormat: " + texture.graphicsFormat);
                            Mod.log.Info("Color isDataSRGB: " + texture.isDataSRGB);

                            Mod.log.Info("Normal format: " + (material.GetTexture("_NormalMap") as Texture2D).format);
                            Mod.log.Info("Normal graphicsformat: " + material.GetTexture("_NormalMap").graphicsFormat);
                            Mod.log.Info("Normal isDataSRGB: " + material.GetTexture("_NormalMap").isDataSRGB);

                            Mod.log.Info("my provided Normal format " + roadWearNormalTexture.format);
                            Mod.log.Info("my provided Normal graphicsFormat " + roadWearNormalTexture.graphicsFormat);
                            Mod.log.Info("my provided Normal isDataSRGB " + roadWearNormalTexture.isDataSRGB);*/
                        }
                        else if (texture.name.StartsWith("GravelLane_BaseColor"))
                        {
                            Mod.log.Info("Found gravel lane material name: " + material.name);
                            material.SetTexture("_BaseColorMap", roadWearColourTexture);
                            material.SetTexture("_NormalMap", roadWearNormalTexture);
                            material.SetFloat("_Smoothness", Mod.MySetting.TextureSmoothness);
                            gravelLaneMaterial = material;
                            hasReplacedGravelLaneRoadWearTexture = true;
                        }
                    }
                } 
                
                /*else if (texture != null && texture.name.StartsWith("BusLane_BaseColor"))
                {
                    Mod.log.Info("Found bus material name: " + material.name);
                    material.SetTexture("_BaseColorMap", newTexture);
                    hasReplacedBusLaneTexture = true;
                }*/
            }
        }

        protected override void OnUpdate()
        {
            if (!hasGeneratedTextures)
            {
                UpdateStoredTextures();
            }
            if (!hasReplacedCarLaneRoadWearTexture || !hasReplacedGravelLaneRoadWearTexture) 
            {
                ReplaceTextures();
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
        }
        void dumpTexture(Texture texture, string path)
        {
            // Create a temporary RenderTexture of the same size as the texture
            RenderTexture tmp = RenderTexture.GetTemporary(
                                texture.width,
                                texture.height,
                                0,
                                RenderTextureFormat.Default,
                                RenderTextureReadWrite.Linear);


            // Blit the pixels on texture to the RenderTexture
            Graphics.Blit(texture, tmp);


            // Backup the currently set RenderTexture
            RenderTexture previous = RenderTexture.active;


            // Set the current RenderTexture to the temporary one we created
            RenderTexture.active = tmp;


            // Create a new readable Texture2D to copy the pixels to it
            Texture2D myTexture2D = new Texture2D(texture.width, texture.height);


            // Copy the pixels from the RenderTexture to the new Texture
            myTexture2D.ReadPixels(new Rect(0, 0, tmp.width, tmp.height), 0, 0);
            myTexture2D.Apply();

            byte[] bytes = myTexture2D.EncodeToPNG();
            
            File.WriteAllBytes(path, bytes);
            UnityEngine.Object.Destroy(myTexture2D);

            // Reset the active RenderTexture
            RenderTexture.active = previous;


            // Release the temporary RenderTexture
            RenderTexture.ReleaseTemporary(tmp);
        }*/

        protected override void OnDestroy()
        {
            Mod.log.Info("cleaning up");
            UnityEngine.Object.Destroy(roadWearColourTexture);
            UnityEngine.Object.Destroy(roadWearNormalTexture);
        }
    }
}