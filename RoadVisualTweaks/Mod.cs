using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Game;
using Game.Modding;
using Game.Prefabs;
using Game.SceneFlow;
using System.IO;
using RoadVisualTweaks.Systems;
using UnityEngine;

namespace RoadVisualTweaks
{
    public class Mod : IMod
    {
        public static ILog log = LogManager.GetLogger($"{nameof(RoadVisualTweaks)}.{nameof(Mod)}").SetShowsErrorsInUI(false);
        public static string myModFolder = ""; // the main folder of this mod

        /// <summary>
        /// Gets the static reference to the settings.
        /// </summary>
        public static Setting MySetting
        {
            get;
            private set;
        }

        public void OnLoad(UpdateSystem updateSystem)
        {
            log.Info(nameof(OnLoad));

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset)) {
                log.Info($"Current mod asset at {asset.path}");
                myModFolder = Directory.GetParent(asset.path).FullName;
            }

            MySetting = new Setting(this);
            MySetting.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(MySetting));


            AssetDatabase.global.LoadSettings(nameof(RoadVisualTweaks), MySetting, new Setting(this));

            updateSystem.UpdateAfter<ReplaceRoadTextureSystem>(SystemUpdatePhase.PrefabUpdate);
        }

        public void OnDispose()
        {
            log.Info(nameof(OnDispose));
            if (MySetting != null)
            {
                MySetting.UnregisterInOptionsUI();
                MySetting = null;
            }
        }
    }
}
