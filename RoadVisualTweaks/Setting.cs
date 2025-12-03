using System;
using System.Collections.Generic;
using Colossal;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;
using Game.UI;
using Game.UI.Widgets;
using RoadVisualTweaks.Systems;
using Unity.Entities;

namespace RoadVisualTweaks
{
    [FileLocation(nameof(RoadVisualTweaks))]
    [SettingsUIGroupOrder(kPresetsGroup, kConfigureCarRoadWearGroup, kConfigureGravelRoadWearGroup, kConfigureBusLaneGroup, kConfigureBicycleLaneGroup, kMiscellaneousGroup)]
    [SettingsUIShowGroupName(kPresetsGroup, kConfigureCarRoadWearGroup, kConfigureGravelRoadWearGroup, kConfigureBusLaneGroup, kConfigureBicycleLaneGroup, kMiscellaneousGroup)]
    public class Setting : ModSetting
    {
        public const string kSection = "Main";

        public const string kPresetsGroup = "Presets";
        public const string kConfigureCarRoadWearGroup = "Configure Road Wear";
        public const string kConfigureGravelRoadWearGroup = "Configure Gravel Road Wear";
        public const string kConfigureBusLaneGroup = "Configure Bus Lane";
        public const string kConfigureBicycleLaneGroup = "Configure Narrow Bicycle Lane";
        public const string kMiscellaneousGroup = "Miscellaneous";

        public Setting(IMod mod) : base(mod)
        {
            SetDefaults();
        }

        [SettingsUIButton]
        [SettingsUIConfirmation]
        [SettingsUISection(kSection, kPresetsGroup)]
        public bool PresetDefault
        {
            set
            {
                SetDefaults();
            }
        }

        [SettingsUISection(kSection, kConfigureCarRoadWearGroup)]

        [SettingsUISection(kSection, kConfigureCarRoadWearGroup)]
        public bool CarRoadWearOverrideEnable { get; set; }

        [SettingsUISection(kSection, kConfigureCarRoadWearGroup)]

        public TextureVariantEnum CarRoadWearTextureVariant { get; set; }

        [SettingsUISlider(min = 0, max = 2.0f, step = 0.01f, scalarMultiplier = 1.0f, unit = Unit.kFloatTwoFractions)]
        [SettingsUISection(kSection, kConfigureCarRoadWearGroup)]
        public float CarRoadWearTextureBrightness { get; set; }

        [SettingsUISlider(min = 0, max = 1.0f, step = 0.01f, scalarMultiplier = 1.0f, unit = Unit.kFloatTwoFractions)]
        [SettingsUISection(kSection, kConfigureCarRoadWearGroup)]
        public float CarRoadWearTextureOpacity { get; set; }

        [SettingsUISlider(min = 0, max = 1.0f, step = 0.01f, scalarMultiplier = 1.0f, unit = Unit.kFloatTwoFractions)]
        [SettingsUISection(kSection, kConfigureCarRoadWearGroup)]
        public float CarRoadWearTextureSmoothness { get; set; }



        [SettingsUISection(kSection, kConfigureGravelRoadWearGroup)]

        [SettingsUISection(kSection, kConfigureGravelRoadWearGroup)]
        public bool GravelRoadWearOverrideEnable { get; set; }

        [SettingsUISection(kSection, kConfigureGravelRoadWearGroup)]
        public TextureVariantEnum GravelRoadWearTextureVariant { get; set; }

        [SettingsUISlider(min = 0, max = 2.0f, step = 0.01f, scalarMultiplier = 1.0f, unit = Unit.kFloatTwoFractions)]
        [SettingsUISection(kSection, kConfigureGravelRoadWearGroup)]
        public float GravelRoadWearTextureBrightness { get; set; }

        [SettingsUISlider(min = 0, max = 1.0f, step = 0.01f, scalarMultiplier = 1.0f, unit = Unit.kFloatTwoFractions)]
        [SettingsUISection(kSection, kConfigureGravelRoadWearGroup)]
        public float GravelRoadWearTextureOpacity { get; set; }

        [SettingsUISlider(min = 0, max = 1.0f, step = 0.01f, scalarMultiplier = 1.0f, unit = Unit.kFloatTwoFractions)]
        [SettingsUISection(kSection, kConfigureGravelRoadWearGroup)]
        public float GravelRoadWearTextureSmoothness { get; set; }



        [SettingsUISection(kSection, kConfigureBusLaneGroup)]

        [SettingsUISection(kSection, kConfigureBusLaneGroup)]
        public bool BusLaneOverrideEnable { get; set; }

        [SettingsUISection(kSection, kConfigureBusLaneGroup)]
        public TextureVariantEnum BusLaneTextureVariant { get; set; }

        [SettingsUISlider(min = 0, max = 2.0f, step = 0.01f, scalarMultiplier = 1.0f, unit = Unit.kFloatTwoFractions)]
        [SettingsUISection(kSection, kConfigureBusLaneGroup)]
        public float BusLaneTextureBrightness { get; set; }

        [SettingsUISlider(min = 0, max = 1.0f, step = 0.01f, scalarMultiplier = 1.0f, unit = Unit.kFloatTwoFractions)]
        [SettingsUISection(kSection, kConfigureBusLaneGroup)]
        public float BusLaneTextureHue { get; set; }

        [SettingsUISlider(min = 0, max = 1.0f, step = 0.01f, scalarMultiplier = 1.0f, unit = Unit.kFloatTwoFractions)]
        [SettingsUISection(kSection, kConfigureBusLaneGroup)]
        public float BusLaneTextureSmoothness { get; set; }



        [SettingsUISection(kSection, kConfigureBicycleLaneGroup)]

        [SettingsUISection(kSection, kConfigureBicycleLaneGroup)]
        public bool BicycleLaneOverrideEnable { get; set; }

        [SettingsUISection(kSection, kConfigureBicycleLaneGroup)]
        public TextureVariantEnum BicycleLaneTextureVariant { get; set; }

        [SettingsUISlider(min = 0, max = 2.0f, step = 0.01f, scalarMultiplier = 1.0f, unit = Unit.kFloatTwoFractions)]
        [SettingsUISection(kSection, kConfigureBicycleLaneGroup)]
        public float BicycleLaneTextureBrightness { get; set; }

        [SettingsUISlider(min = 0, max = 1.0f, step = 0.01f, scalarMultiplier = 1.0f, unit = Unit.kFloatTwoFractions)]
        [SettingsUISection(kSection, kConfigureBicycleLaneGroup)]
        public float BicycleLaneTextureHue { get; set; }

        [SettingsUISlider(min = 0, max = 1.0f, step = 0.01f, scalarMultiplier = 1.0f, unit = Unit.kFloatTwoFractions)]
        [SettingsUISection(kSection, kConfigureBicycleLaneGroup)]
        public float BicycleLaneTextureSmoothness { get; set; }



        [SettingsUIButton]
        [SettingsUISection(kSection, kMiscellaneousGroup)]
        public bool DebugButton
        {
            set
            {
                ReplaceRoadTextureSystem system = World.DefaultGameObjectInjectionWorld?.GetExistingSystemManaged<ReplaceRoadTextureSystem>();
                system?.DebugCommand();
            }
        }

        public override void SetDefaults()
        {
            CarRoadWearOverrideEnable = true;
            CarRoadWearTextureVariant = TextureVariantEnum.Reflavoured;
            CarRoadWearTextureBrightness = 1.0f;
            CarRoadWearTextureOpacity = 0.4f;
            CarRoadWearTextureSmoothness = 0.4f;

            GravelRoadWearOverrideEnable = false;
            GravelRoadWearTextureVariant = TextureVariantEnum.Vanilla;
            GravelRoadWearTextureBrightness = 1.0f;
            GravelRoadWearTextureOpacity = 0.4f;
            GravelRoadWearTextureSmoothness = 0.655f;

            BusLaneOverrideEnable = false;
            BusLaneTextureVariant = TextureVariantEnum.Vanilla;
            BusLaneTextureBrightness = 1.0f;
            BusLaneTextureHue = 0.15f;
            BusLaneTextureSmoothness = 1.0f;

            BicycleLaneOverrideEnable = false;
            BicycleLaneTextureVariant = TextureVariantEnum.Vanilla;
            BicycleLaneTextureBrightness = 1.0f;
            BicycleLaneTextureHue = 0.15f;
            BicycleLaneTextureSmoothness = 1.0f;

            UpdateMaterialsAndTextures();
        }

        public enum TextureVariantEnum
        {
            Reflavoured,
            Vanilla,
        }

        public override void Apply()
        {
            base.Apply();
            UpdateMaterialsAndTextures();
        }

        private void UpdateMaterialsAndTextures()
        {
            Mod.log.Info("settings updated");
            // Manually replace outdated textures when settings are updated
            ReplaceRoadTextureSystem system = World.DefaultGameObjectInjectionWorld?.GetExistingSystemManaged<ReplaceRoadTextureSystem>();
            system?.UpdateMaterialsAndTextures(true);
        }
    }

    public class LocaleEN : IDictionarySource
    {
        private readonly Setting m_Setting;
        public LocaleEN(Setting setting)
        {
            m_Setting = setting;
        }
        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {

            string textureBrightnessLabelString = "Texture Brightness";
            string textureBrightnessDescString = "Modifies the brightness of the given texture";
            string textureOpacityLabelString = "Texture Opacity";
            string textureOpacityDescString = "Modifies the opacity of the given texture";
            string textureSmoothnessLabelString = "Texture Smoothness";
            string textureSmoothnessDescString = "Modifies the smoothness of the given texture";
            string textureSmoothnessNotWorkingDescString = "Modifies the smoothness of the given texture (it doesn't seem to have any effect though)";
            string textureHueLabelString = "Texture Hue";
            string textureHueDescString = "Modifies the hue of the given texture";

            return new Dictionary<string, string>
            {
                { m_Setting.GetSettingsLocaleID(), "Road Visual Tweaks" },
                { m_Setting.GetOptionTabLocaleID(Setting.kSection), "Main" },

                { m_Setting.GetOptionGroupLocaleID(Setting.kPresetsGroup), "Presets" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kConfigureCarRoadWearGroup), "Configure Road Wear" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kConfigureGravelRoadWearGroup), "Configure Gravel Road Wear" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kConfigureBusLaneGroup), "Configure Bus Lane" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kConfigureBicycleLaneGroup), "Configure Bicycle Lane" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kMiscellaneousGroup), "Miscellaneous" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.PresetDefault)), "Reset to default" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.PresetDefault)),  "Applies this mod's default preset to your configuration" },
                { m_Setting.GetOptionWarningLocaleID(nameof(Setting.PresetDefault)), "Are you sure you want to apply this preset? Your existing configuration will be overriden." },

                
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.CarRoadWearOverrideEnable)), "Enable" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.CarRoadWearOverrideEnable)), "Enable modifications to the road wear" },


                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.CarRoadWearTextureVariant)), "Texture Variant" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.CarRoadWearTextureVariant)), "Select which road wear texture you prefer" },

                { m_Setting.GetEnumValueLocaleID(Setting.TextureVariantEnum.Vanilla), "Vanilla" },
                { m_Setting.GetEnumValueLocaleID(Setting.TextureVariantEnum.Reflavoured), "Reflavoured" },

       
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.CarRoadWearTextureBrightness)), textureBrightnessLabelString },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.CarRoadWearTextureBrightness)), textureBrightnessDescString },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.CarRoadWearTextureOpacity)), textureOpacityLabelString },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.CarRoadWearTextureOpacity)), textureOpacityDescString},

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.CarRoadWearTextureSmoothness)), textureSmoothnessLabelString },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.CarRoadWearTextureSmoothness)), textureSmoothnessDescString },


                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.GravelRoadWearOverrideEnable)), "Enable" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.GravelRoadWearOverrideEnable)), "Enable modifications to the road wear" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.GravelRoadWearTextureVariant)), "Texture Variant" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.GravelRoadWearTextureVariant)), "Select which road wear texture you prefer" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.GravelRoadWearTextureBrightness)), textureBrightnessLabelString },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.GravelRoadWearTextureBrightness)), textureBrightnessDescString },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.GravelRoadWearTextureOpacity)), textureOpacityLabelString },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.GravelRoadWearTextureOpacity)), textureOpacityDescString},

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.GravelRoadWearTextureSmoothness)), textureSmoothnessLabelString },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.GravelRoadWearTextureSmoothness)), textureSmoothnessDescString },


                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.BusLaneOverrideEnable)), "Enable" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.BusLaneOverrideEnable)), "Enable modifications to the bus lane" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.BusLaneTextureVariant)), "Texture Variant" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.BusLaneTextureVariant)), "Select which texture you prefer" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.BusLaneTextureBrightness)), textureBrightnessLabelString },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.BusLaneTextureBrightness)), textureBrightnessDescString },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.BusLaneTextureHue)), textureHueLabelString },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.BusLaneTextureHue)), textureHueDescString},

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.BusLaneTextureSmoothness)), textureSmoothnessLabelString },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.BusLaneTextureSmoothness)), textureSmoothnessNotWorkingDescString },


                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.BicycleLaneOverrideEnable)), "Enable" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.BicycleLaneOverrideEnable)), "Enable modifications to the bicycle lane" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.BicycleLaneTextureVariant)), "Texture Variant" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.BicycleLaneTextureVariant)), "Select which texture you prefer" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.BicycleLaneTextureBrightness)), textureBrightnessLabelString },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.BicycleLaneTextureBrightness)), textureBrightnessDescString},

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.BicycleLaneTextureHue)), textureHueLabelString },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.BicycleLaneTextureHue)), textureHueDescString},

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.BicycleLaneTextureSmoothness)), textureSmoothnessLabelString },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.BicycleLaneTextureSmoothness)), textureSmoothnessNotWorkingDescString },


                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.DebugButton)), "Debug" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.DebugButton)),  "For developer use only" }
            };
        }

        public void Unload()
        {

        }
    }
}
