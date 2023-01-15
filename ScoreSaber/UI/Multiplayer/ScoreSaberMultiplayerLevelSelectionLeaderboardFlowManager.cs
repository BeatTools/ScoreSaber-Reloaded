#region

using HMUI;
using IPA.Utilities;
using IPA.Utilities.Async;
using System;
using System.Threading.Tasks;
using Zenject;

#endregion

namespace ScoreSaber.UI.Multiplayer {
    internal class ScoreSaberMultiplayerLevelSelectionLeaderboardFlowManager : IInitializable, IDisposable {
        private readonly LevelSelectionNavigationController _levelSelectionNavigationController;

        private readonly MainFlowCoordinator _mainFlowCoordinator;
        private readonly PlatformLeaderboardViewController _platformLeaderboardViewController;
        private readonly ServerPlayerListViewController _serverPlayerListViewController;

        private bool _currentlyInMulti;
        private bool _firstStartup = true;

        public ScoreSaberMultiplayerLevelSelectionLeaderboardFlowManager(MainFlowCoordinator mainFlowCoordinator,
            ServerPlayerListViewController serverPlayerListViewController,
            PlatformLeaderboardViewController platformLeaderboardViewController,
            LevelSelectionNavigationController levelSelectionNavigationController) {
            _mainFlowCoordinator = mainFlowCoordinator;
            _serverPlayerListViewController = serverPlayerListViewController;
            _platformLeaderboardViewController = platformLeaderboardViewController;
            _levelSelectionNavigationController = levelSelectionNavigationController;
        }

        public void Dispose() {
            _levelSelectionNavigationController.didActivateEvent -= LevelSelectionNavigationController_didActivateEvent;
        }

        public void Initialize() {
            _levelSelectionNavigationController.didActivateEvent += LevelSelectionNavigationController_didActivateEvent;
            _levelSelectionNavigationController.didDeactivateEvent +=
                LevelSelectionNavigationController_didDeactivateEvent;
        }

        private void LevelSelectionNavigationController_didChangeLevelDetailContentEvent(
            LevelSelectionNavigationController controller, StandardLevelDetailViewController.ContentType contentType) {
            if (controller.selectedDifficultyBeatmap == null) {
                HideLeaderboard();
                return;
            }

            ShowLeaderboard();
        }

        private void LevelSelectionNavigationController_didChangeDifficultyBeatmapEvent(
            LevelSelectionNavigationController _, IDifficultyBeatmap beatmap) {
            ShowLeaderboard();
        }

        private void HideLeaderboard() {
            if (!_platformLeaderboardViewController.isInViewControllerHierarchy) {
                return;
            }

            FlowCoordinator currentFlowCoordinator = _mainFlowCoordinator.YoungestChildFlowCoordinatorOrSelf();
            if (!(currentFlowCoordinator is MultiplayerLevelSelectionFlowCoordinator)) {
                return;
            }

            currentFlowCoordinator.InvokeMethod<object, FlowCoordinator>("SetRightScreenViewController", null,
                ViewController.AnimationType.Out);
        }
        
        private void ShowLeaderboard() {
            if (!InMulti()) {
                return;
            }

            IDifficultyBeatmap selected = _levelSelectionNavigationController.selectedDifficultyBeatmap;
            if (selected == null) {
                HideLeaderboard();
                return;
            }

            _platformLeaderboardViewController.SetData(selected);
            FlowCoordinator currentFlowCoordinator = _mainFlowCoordinator.YoungestChildFlowCoordinatorOrSelf();
            currentFlowCoordinator.InvokeMethod<object, FlowCoordinator>("SetRightScreenViewController",
                _platformLeaderboardViewController, ViewController.AnimationType.In);

            if (_firstStartup) {
                _firstStartup = false;
                _serverPlayerListViewController.gameObject.SetActive(false);
            } else {
                _serverPlayerListViewController.gameObject.SetActive(true);
            }
        }


        private void LevelSelectionNavigationController_didActivateEvent(bool firstActivation, bool addedToHierarchy,
            bool screenSystemEnabling) {
            if (!InMulti()) {
                return;
            }

            if (firstActivation) {
                _firstStartup = true;
            }

            _currentlyInMulti = true;
            _levelSelectionNavigationController.didChangeDifficultyBeatmapEvent +=
                LevelSelectionNavigationController_didChangeDifficultyBeatmapEvent;
            _levelSelectionNavigationController.didChangeLevelDetailContentEvent +=
                LevelSelectionNavigationController_didChangeLevelDetailContentEvent;
            ShowLeaderboard();
        }

        private void LevelSelectionNavigationController_didDeactivateEvent(bool removedFromHierarchy,
            bool screenSystemDisabling) {
            if (!InMulti()) {
                return;
            }

            _currentlyInMulti = false;
            _levelSelectionNavigationController.didChangeDifficultyBeatmapEvent -=
                LevelSelectionNavigationController_didChangeDifficultyBeatmapEvent;
            _levelSelectionNavigationController.didChangeLevelDetailContentEvent -=
                LevelSelectionNavigationController_didChangeLevelDetailContentEvent;
        }

        private bool InMulti() {
            if (_currentlyInMulti) {
                return true;
            }

            FlowCoordinator currentFlowCoordinator = _mainFlowCoordinator.YoungestChildFlowCoordinatorOrSelf();
            return currentFlowCoordinator is MultiplayerLevelSelectionFlowCoordinator;
        }
    }
}