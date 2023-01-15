#region

using BeatSaberMarkupLanguage;
using HarmonyLib;
using IPA;
using IPA.Loader;
using IPA.Utilities.Async;
using ScoreSaber.Core;
using ScoreSaber.Core.Daemons;
using ScoreSaber.Core.Data;
using ScoreSaber.Core.ReplaySystem;
using ScoreSaber.Core.ReplaySystem.Installers;
using ScoreSaber.UI.Elements.Profile;
using SiraUtil.Zenject;
using System;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using IPALogger = IPA.Logging.Logger;

#endregion

namespace ScoreSaber {
    [Plugin(RuntimeOptions.DynamicInit)]
    public class Plugin {
        internal static Material Furry;
        internal static Material NonFurry;
        internal static Material NoGlowMatRound;

        private static bool _scoreSubmission = true;
        internal Harmony harmony;

        internal Version LibVersion;

        [Init]
        public Plugin(IPALogger logger, PluginMetadata metadata, Zenjector zenjector) {
            Log = logger;
            Instance = this;

            zenjector.UseLogger(logger);
            zenjector.Expose<ComboUIController>("Environment");
            zenjector.Expose<GameEnergyUIPanel>("Environment");
            zenjector.Install<AppInstaller>(Location.App);
            zenjector.Install<MainInstaller>(Location.Menu);
            zenjector.Install<ImberInstaller>(Location.StandardPlayer);
            zenjector.Install<PlaybackInstaller>(Location.StandardPlayer);
            zenjector.Install<RecordInstaller, StandardGameplayInstaller>();
            zenjector.Install<RecordInstaller, MultiplayerLocalActivePlayerInstaller>();
            zenjector.UseAutoBinder();

            LibVersion = Assembly.GetExecutingAssembly().GetName().Version;
            BSMLParser.instance.RegisterTypeHandler(new ProfileDetailViewTypeHandler());
            BSMLParser.instance.RegisterTag(new ProfileDetailViewTag(metadata.Assembly));

            HttpInstance = new Http(new HttpOptions
                { baseURL = "https://scoresaber.com/api", applicationName = "ScoreSaber-PC", version = LibVersion });
        }

        internal static ReplayState ReplayState { get; set; }
        internal static Recorder ReplayRecorder { get; set; }
        internal static IPALogger Log { get; private set; }
        internal static Plugin Instance { get; private set; }

        internal static Settings Settings { get; private set; }

        internal static Http HttpInstance { get; private set; }

        public static bool ScoreSubmission {
            get => _scoreSubmission;
            set {
                bool canSet = false;
                foreach (StackFrame frame in new StackTrace().GetFrames()) {
                    string namespaceName = frame.GetMethod().ReflectedType.Namespace;
                    if (!string.IsNullOrEmpty(namespaceName)) {
                        if (namespaceName.Contains("BS_Utils") || namespaceName.Contains("SiraUtil")) {
                            canSet = true;
                            break;
                        }
                    }
                }

                if (canSet) {
                    if (!ReplayState.IsPlaybackEnabled) {
                        StandardLevelScenesTransitionSetupDataSO transitionSetup = Resources
                            .FindObjectsOfTypeAll<StandardLevelScenesTransitionSetupDataSO>().FirstOrDefault();
                        MultiplayerLevelScenesTransitionSetupDataSO multiTransitionSetup =
                            Resources.FindObjectsOfTypeAll<MultiplayerLevelScenesTransitionSetupDataSO>()
                                .FirstOrDefault();
                        if (value) {
                            transitionSetup.didFinishEvent -= UploadDaemonHelper.ThreeInstance;
                            transitionSetup.didFinishEvent += UploadDaemonHelper.ThreeInstance;
                            multiTransitionSetup.didFinishEvent -= UploadDaemonHelper.FourInstance;
                            multiTransitionSetup.didFinishEvent += UploadDaemonHelper.FourInstance;
                        } else {
                            transitionSetup.didFinishEvent -= UploadDaemonHelper.ThreeInstance;
                            multiTransitionSetup.didFinishEvent -= UploadDaemonHelper.FourInstance;
                        }

                        _scoreSubmission = value;
                    }
                }
            }
        }

        [OnEnable]
        public void OnEnable() {
            SceneManager.sceneLoaded += SceneLoaded;

            Settings = Settings.LoadSettings();
            ReplayState = new ReplayState();
            if (!Settings.disableScoreSaber) {
                harmony = new Harmony("com.umbranox.BeatSaber.ScoreSaber");
                harmony.PatchAll(Assembly.GetExecutingAssembly());
                PlayerPrefs.SetInt("lbPatched", 1);
            }
        }

        private void SceneLoaded(Scene scene, LoadSceneMode mode) {
            if (scene.name == "MainMenu") {
                SharedCoroutineStarter.instance.StartCoroutine(WaitForLeaderboard());
            }
        }

        private IEnumerator WaitForLeaderboard() {
            yield return new WaitUntil(() => Resources.FindObjectsOfTypeAll<PlatformLeaderboardViewController>().Any());
            NoGlowMatRound = Resources.FindObjectsOfTypeAll<Material>().Where(m => m.name == "UINoGlowRoundEdge")
                .First();
        }


        [OnDisable]
        public void OnDisable() {
            SceneManager.sceneLoaded -= SceneLoaded;
        }

        internal static async Task<Material> GetFurryMaterial() {
            if (Furry == null) {
                AssetBundle bundle = null;

                IEnumerator SeriouslyUnityMakeSomethingBetter() {
                    AssetBundleCreateRequest bundleContainer = AssetBundle.LoadFromMemoryAsync(
                        Utilities.GetResource(Assembly.GetExecutingAssembly(), "ScoreSaber.Resources.cyanisa.furry"));
                    yield return bundleContainer;
                    bundle = bundleContainer.assetBundle;
                }

                await Coroutines.AsTask(SeriouslyUnityMakeSomethingBetter());
                Furry = new Material(bundle.LoadAsset<Material>("FurMat"));
                bundle.Unload(false);
                NonFurry = BeatSaberUI.MainTextFont.material;
                Furry.mainTexture = BeatSaberUI.MainTextFont.material.mainTexture;
            }

            return Furry;
        }

        internal static void LogNull(int number, object hmm) {
            if (hmm == null) {
                Log.Info($"{number}:{hmm.GetType().Name} is null");
            } else {
                Log.Info($"{number}:{hmm.GetType().Name} is not null");
            }
        }

        internal static void LogNull(object hmm) {
            if (hmm == null) {
                Log.Info($"{hmm.GetType().Name} is null");
            } else {
                Log.Info($"{hmm.GetType().Name} is not null");
            }
        }
    }
}