using System;
using System.Collections.Generic;
using Colossal;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;
using Game.UI;
using Game.UI.Widgets;
using RoadWearAdjuster.Systems;
using Unity.Entities;

namespace RoadWearAdjuster
{
    [FileLocation(nameof(RoadWearAdjuster))]
    [SettingsUIGroupOrder(kPresetsGroup, kConfigureGroup)]
    [SettingsUIShowGroupName(kPresetsGroup, kConfigureGroup)]
    public class Setting : ModSetting
    {
        public const string kSection = "Main";

        public const string kPresetsGroup = "Presets";
        public const string kConfigureGroup = "Configure";

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

        [SettingsUISection(kSection, kConfigureGroup)]
        public TextureVariantEnum TextureVariant { get; set; }

        [SettingsUISlider(min = 0, max = 1.0f, step = 0.01f, scalarMultiplier = 1.0f, unit = Unit.kFloatTwoFractions)]
        [SettingsUISection(kSection, kConfigureGroup)]
        public float TextureBrightness { get; set; }

        [SettingsUISlider(min = 0, max = 1.0f, step = 0.01f, scalarMultiplier = 1.0f, unit = Unit.kFloatTwoFractions)]
        [SettingsUISection(kSection, kConfigureGroup)]
        public float TextureOpacity { get; set; }

        [SettingsUISlider(min = 0, max = 1.0f, step = 0.01f, scalarMultiplier = 1.0f, unit = Unit.kFloatTwoFractions)]
        [SettingsUISection(kSection, kConfigureGroup)]
        public float TextureSmoothness { get; set; }

        public override void SetDefaults()
        {
            TextureVariant = TextureVariantEnum.Reflavoured;
            TextureBrightness = 1.0f;
            TextureOpacity = 0.4f;
            TextureSmoothness = 0.4f;

            UpdateTextures();
        }

        public enum TextureVariantEnum
        {
            Reflavoured,
            Vanilla,
        }

        public override void Apply()
        {
            base.Apply();
            UpdateTextures();
        }

        private void UpdateTextures()
        {
            Mod.log.Info("settings updated");
            // Manually replace outdated textures when settings are updated
            ReplaceRoadWearSystem system = World.DefaultGameObjectInjectionWorld?.GetExistingSystemManaged<ReplaceRoadWearSystem>();
            system?.UpdateStoredTextures();
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
            return new Dictionary<string, string>
            {
                { m_Setting.GetSettingsLocaleID(), "Road Wear Adjuster" },
                { m_Setting.GetOptionTabLocaleID(Setting.kSection), "Main" },

                { m_Setting.GetOptionGroupLocaleID(Setting.kPresetsGroup), "Presets" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kConfigureGroup), "Configure" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.PresetDefault)), "Reset to default" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.PresetDefault)),  "Applies this preset to your configuration" },
                { m_Setting.GetOptionWarningLocaleID(nameof(Setting.PresetDefault)), "Are you sure you want to apply this preset? Your existing configuration will be overriden." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.TextureVariant)), "Texture Variant" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.TextureVariant)), "Select which road wear texture you prefer" },

                { m_Setting.GetEnumValueLocaleID(Setting.TextureVariantEnum.Vanilla), "Vanilla" },
                { m_Setting.GetEnumValueLocaleID(Setting.TextureVariantEnum.Reflavoured), "Reflavoured" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.TextureBrightness)), "Texture Brightness" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.TextureBrightness)), "Modifies the brightness of the base colour of the given texture" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.TextureOpacity)), "Texture Opacity" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.TextureOpacity)), "Modifies the opacity of the given texture" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.TextureSmoothness)), "Texture Smoothness" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.TextureSmoothness)), "Modifies the overall smoothness of the given texture" }
            };
        }

        public void Unload()
        {

        }
    }
}
