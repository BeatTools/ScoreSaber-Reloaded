#region

using IPA.Utilities;
using ScoreSaber.Core.ReplaySystem.HarmonyPatches;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

#endregion

namespace ScoreSaber.Core.ReplaySystem.Playback {
    internal class ReplayTimeSyncController : TimeSynchronizer, ITickable {
        private readonly AudioManagerSO _audioManagerSo;
        private readonly BeatmapObjectSpawnController _beatmapObjectSpawnController;
        private readonly List<IScroller> _scrollers;
        private readonly AudioTimeSyncController.InitData _audioInitData;
        private BasicBeatmapObjectManager _basicBeatmapObjectManager;
        private BeatmapCallbacksController _beatmapObjectCallbackController;
        private BeatmapCallbacksController.InitData _callbackInitData;
        private NoteCutSoundEffectManager _noteCutSoundEffectManager;
        private bool _paused;

        public ReplayTimeSyncController(List<IScroller> scrollers, BasicBeatmapObjectManager basicBeatmapObjectManager,
            NoteCutSoundEffectManager noteCutSoundEffectManager,
            BeatmapObjectSpawnController beatmapObjectSpawnController, AudioTimeSyncController.InitData audioInitData,
            BeatmapCallbacksController.InitData initData, BeatmapCallbacksController beatmapObjectCallbackController) {
            _scrollers = scrollers;
            _callbackInitData = initData;
            _audioInitData = audioInitData;
            _basicBeatmapObjectManager = basicBeatmapObjectManager;
            _noteCutSoundEffectManager = noteCutSoundEffectManager;
            _beatmapObjectSpawnController = beatmapObjectSpawnController;
            _beatmapObjectCallbackController = beatmapObjectCallbackController;
            _audioManagerSo = Accessors.AudioManager(ref noteCutSoundEffectManager);
        }

        public void Tick() {
            int index = -1;
            if (Input.GetKeyDown(KeyCode.Alpha1)) {
                index = 0;
            } else if (Input.GetKeyDown(KeyCode.Alpha2)) {
                index = 1;
            } else if (Input.GetKeyDown(KeyCode.Alpha3)) {
                index = 2;
            } else if (Input.GetKeyDown(KeyCode.Alpha4)) {
                index = 3;
            } else if (Input.GetKeyDown(KeyCode.Alpha5)) {
                index = 4;
            } else if (Input.GetKeyDown(KeyCode.Alpha6)) {
                index = 5;
            } else if (Input.GetKeyDown(KeyCode.Alpha7)) {
                index = 6;
            } else if (Input.GetKeyDown(KeyCode.Alpha8)) {
                index = 7;
            } else if (Input.GetKeyDown(KeyCode.Alpha9)) {
                index = 8;
            } else if (Input.GetKeyDown(KeyCode.Alpha0)) {
                index = 9;
            }

            if (index != -1) {
                OverrideTime(audioTimeSyncController.songLength * (index * 0.1f));
            }

            if (Input.GetKeyDown(KeyCode.Minus)) {
                if (audioTimeSyncController.timeScale > 0.1f) {
                    OverrideTimeScale(audioTimeSyncController.timeScale - 0.1f);
                }
            }

            if (Input.GetKeyDown(KeyCode.Equals)) {
                if (audioTimeSyncController.timeScale < 2.0f) {
                    OverrideTimeScale(audioTimeSyncController.timeScale + 0.1f);
                }
            }

            if (Input.GetKeyDown(KeyCode.R)) {
                OverrideTime(0f);
            }

            if (Input.GetKeyDown(KeyCode.Space)) {
                if (_paused) {
                    audioTimeSyncController.Resume();
                } else {
                    CancelAllHitSounds();
                    audioTimeSyncController.Pause();
                }

                _paused = !_paused;
            }
        }

        private void UpdateTimes() {
            foreach (IScroller scroller in _scrollers) {
                scroller.TimeUpdate(audioTimeSyncController.songTime);
            }
        }

        public void OverrideTime(float time) {
            if (Mathf.Abs(time - base.audioTimeSyncController.songTime) <= 0.25f) {
                return;
            }

            AudioTimeSyncController audioTimeSyncController = base.audioTimeSyncController; // UMBRAMEGALUL
            CutSoundEffectOverride.Buffer = true;
            CancelAllHitSounds();

            // Forcibly enabling all the note/obstacle components to ensure their dissolve coroutine executes (it no likey when game pausey).
            foreach (GameNoteController item in Accessors.GameNotePool(ref _basicBeatmapObjectManager).activeItems) {
                item.Hide(false);
                item.Pause(false);
                item.enabled = true;
                item.gameObject.SetActive(true);
                item.Dissolve(0f);
            }

            foreach (BombNoteController item in Accessors.BombNotePool(ref _basicBeatmapObjectManager).activeItems) {
                item.Hide(false);
                item.Pause(false);
                item.enabled = true;
                item.gameObject.SetActive(true);
                item.Dissolve(0f);
            }

            foreach (ObstacleController item in _basicBeatmapObjectManager.activeObstacleControllers) {
                item.Hide(false);
                item.Pause(false);
                item.enabled = true;
                item.gameObject.SetActive(true);
                item.Dissolve(0f);
            }

            AudioTimeSyncController.State previousState = base.audioTimeSyncController.state;

            base.audioTimeSyncController.Pause();
            base.audioTimeSyncController.SeekTo(time / base.audioTimeSyncController.timeScale);

            if (previousState == AudioTimeSyncController.State.Playing) {
                base.audioTimeSyncController.Resume();
            }

            Accessors.InitialStartFilterTime(ref _callbackInitData) = time;
            Accessors.CallbackStartFilterTime(ref _beatmapObjectCallbackController) = time;

            foreach (KeyValuePair<float, CallbacksInTime> callback in Accessors.CallbacksInTime(
                         ref _beatmapObjectCallbackController)) {
                if (callback.Value.lastProcessedNode != null && callback.Value.lastProcessedNode.Value.time > time) {
                    callback.Value.lastProcessedNode = null;
                }
            }

            Accessors.AudioSongTime(ref audioTimeSyncController) = time;

            base.audioTimeSyncController.Update();
            UpdateTimes();
        }

        public void OverrideTimeScale(float newScale) {
            CancelAllHitSounds();
            AudioTimeSyncController audioTimeSyncController = base.audioTimeSyncController; // UMBRAMEGALUL
            Accessors.AudioSource(ref audioTimeSyncController).pitch = newScale;

            Accessors.AudioTimeScale(ref audioTimeSyncController) = newScale;
            Accessors.AudioStartOffset(ref audioTimeSyncController)
                = (Time.timeSinceLevelLoad * audioTimeSyncController.timeScale) -
                  (audioTimeSyncController.songTime + _audioInitData.songTimeOffset);

            _audioManagerSo.musicPitch = 1f / newScale;
            audioTimeSyncController.Update();
        }

        public void CancelAllHitSounds() {
            List<NoteCutSoundEffect> activeItems = Accessors.NoteCutPool(ref _noteCutSoundEffectManager).activeItems;
            for (int i = 0; i < activeItems.Count; i++) {
                NoteCutSoundEffect effect = activeItems[i];
                if (effect.isActiveAndEnabled) {
                    effect.StopPlayingAndFinish();
                }
            }

            _noteCutSoundEffectManager.SetField("_prevNoteATime", -1f);
            _noteCutSoundEffectManager.SetField("_prevNoteBTime", -1f);
        }
    }
}