﻿#region

using IPA.Utilities;
using ScoreSaber.Core.Utils;
using SiraUtil.Tools.FPFC;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zenject;
using Object = UnityEngine.Object;

#endregion

namespace ScoreSaber.Core.ReplaySystem.Legacy {
    internal class LegacyReplayPlayer : IInitializable, ITickable, IDisposable {
        private readonly AudioTimeSyncController _audioTimeSyncController;
        private readonly IFPFCSettings _fpfcSettings;
        private readonly MainCamera _mainCamera;
        private readonly MainSettingsModelSO _mainSettingsModelSO;
        private readonly RelativeScoreAndImmediateRankCounter _relativeScoreAndImmediateRankCounter;
        private readonly SaberManager _saberManager;
        private readonly ScoreUIController _scoreUIController;
        private ComboController _comboController;
        private Camera _desktopCamera;
        private readonly bool _initialFPFCState;
        private readonly List<Z.Keyframe> _keyframes;
        private int _lastKeyframeIndex;
        private int _multiplier;
        private int _multiplierIncreaseMaxProgress;
        private int _multiplierIncreaseProgress;
        private int _playbackPreviousCombo;
        private int _playbackPreviousScore;
        private PlayerTransforms _playerTransforms;

        private ScoreController _scoreController;

        private Camera _spectatorCamera;

        public int cutOrMissedNotes;

        internal LegacyReplayPlayer(List<Z.Keyframe> keyframes, ScoreController scoreController,
            RelativeScoreAndImmediateRankCounter relativeScoreAndImmediateRankCounter,
            AudioTimeSyncController audioTimeSyncController,
            MainCamera mainCamera, SaberManager saberManager, PlayerTransforms playerTransforms,
            IFPFCSettings fpfcSettings, ComboController comboController) {
            _fpfcSettings = fpfcSettings;
            _comboController = comboController;
            _initialFPFCState = fpfcSettings.Enabled;
            _fpfcSettings.Enabled = false;

            _keyframes = keyframes;
            _scoreController = scoreController;

            _relativeScoreAndImmediateRankCounter = relativeScoreAndImmediateRankCounter;
            _audioTimeSyncController = audioTimeSyncController;
            _mainCamera = mainCamera;
            _saberManager = saberManager;
            _playerTransforms = playerTransforms;
            _mainSettingsModelSO = Resources.FindObjectsOfTypeAll<MainSettingsModelSO>()[0];
            _scoreUIController = Resources.FindObjectsOfTypeAll<ScoreUIController>().FirstOrDefault();
        }

        public void Dispose() {
            _fpfcSettings.Changed -= fpfcSettings_Changed;
            _fpfcSettings.Enabled = _initialFPFCState;
        }

        public void Initialize() {
            SetupCameras();
            _fpfcSettings.Changed += fpfcSettings_Changed;
            ScoreUIController.InitData data =
                new ScoreUIController.InitData(ScoreUIController.ScoreDisplayType.MultipliedScore);
            _scoreUIController.SetField("_initData", data);
        }

        public void Tick() {
            float time = _audioTimeSyncController.songTime;
            int keyframeIndex = 0;

            while (keyframeIndex < _keyframes.Count - 2 && _keyframes[keyframeIndex + 1]._time < time) {
                keyframeIndex++;
            }

            Z.Keyframe keyframe1 = _keyframes[keyframeIndex];
            Z.Keyframe keyframe2 = _keyframes[keyframeIndex + 1];

            if (keyframe1 == null) { return; }

            if (keyframe2 == null) { return; }

            float t = (time - keyframe1._time) / Mathf.Max(0.000001f, keyframe2._time - keyframe1._time);

            _saberManager.rightSaber.OverridePositionAndRotation(
                Vector3.Lerp(keyframe1._pos1, keyframe2._pos1, t),
                Quaternion.Lerp(keyframe1._rot1, keyframe2._rot1, t)
            );

            _saberManager.leftSaber.OverridePositionAndRotation(
                Vector3.Lerp(keyframe1._pos2, keyframe2._pos2, t),
                Quaternion.Lerp(keyframe1._rot2, keyframe2._rot2, t)
            );

            Vector3 pos = Vector3.Lerp(keyframe1._pos3, keyframe2._pos3, t);
            Quaternion rot = Quaternion.Lerp(keyframe1._rot3, keyframe2._rot3, t);
            Accessors.HeadTransform(ref _playerTransforms).SetPositionAndRotation(pos, rot);
            Vector3 eulerAngles = rot.eulerAngles;
            Vector3 headRotationOffset = new Vector3(Plugin.Settings.replayCameraXRotation,
                Plugin.Settings.replayCameraYRotation, Plugin.Settings.replayCameraZRotation);
            eulerAngles += headRotationOffset;
            rot.eulerAngles = eulerAngles;

            float t2 = Time.deltaTime * 6f;

            pos.x += Plugin.Settings.replayCameraXOffset;
            pos.y += Plugin.Settings.replayCameraYOffset;
            pos.z += Plugin.Settings.replayCameraZOffset;

            if (!_fpfcSettings.Enabled) {
                _desktopCamera.transform.SetPositionAndRotation(
                    Vector3.Lerp(_desktopCamera.transform.position, pos, t2),
                    Quaternion.Lerp(_desktopCamera.transform.rotation, rot, t2));
            }

            if (_scoreController != null) {
                if (cutOrMissedNotes >= 1) {
                    UpdatePlaybackScore(keyframe1);
                    _lastKeyframeIndex = keyframeIndex;
                }
            }
        }

        private void fpfcSettings_Changed(IFPFCSettings fpfcSettings) {
            if (fpfcSettings.Enabled) {
                _desktopCamera.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            }
        }

        private void SetupCameras() {
            _mainCamera.enabled = false;
            _mainCamera.gameObject.SetActive(false);

            _desktopCamera = Resources.FindObjectsOfTypeAll<Camera>().First(x => x.name == "RecorderCamera");

            //Desktop Camera
            _desktopCamera.fieldOfView = Plugin.Settings.replayCameraFOV;
            _desktopCamera.transform.position = new Vector3(_desktopCamera.transform.position.x,
                _desktopCamera.transform.position.y, _desktopCamera.transform.position.z);
            _desktopCamera.gameObject.SetActive(true);
            _desktopCamera.tag = "MainCamera";
            _desktopCamera.depth = 1;

            _mainCamera.SetField("_camera", _desktopCamera);


            //InGame Camera
            GameObject spectatorObject = new GameObject("SpectatorParent");
            _spectatorCamera = Object.Instantiate(_desktopCamera);
            spectatorObject.transform.position = new Vector3(_mainSettingsModelSO.roomCenter.value.x,
                _mainSettingsModelSO.roomCenter.value.y, _mainSettingsModelSO.roomCenter.value.z);
            Quaternion rotation = new Quaternion {
                eulerAngles = new Vector3(0.0f, _mainSettingsModelSO.roomRotation.value, 0.0f)
            };
            _spectatorCamera.transform.rotation = rotation;
            _spectatorCamera.stereoTargetEye = StereoTargetEyeMask.Both;
            _spectatorCamera.gameObject.SetActive(true);
            _spectatorCamera.depth = 0;
            _spectatorCamera.transform.SetParent(spectatorObject.transform);

            if (Plugin.Settings.enableReplayFrameRenderer) {
                ScreenshotRecorder ss = Resources.FindObjectsOfTypeAll<ScreenshotRecorder>().Last();
                ss.SetField("_folder", Plugin.Settings.replayFramePath);
                ss.enabled = true;
                _desktopCamera.depth = 1;
                DisableGCWhileEnabled gc = Resources.FindObjectsOfTypeAll<DisableGCWhileEnabled>().Last();
                gc.enabled = false;
            }
        }

        private void UpdatePlaybackScore(Z.Keyframe keyframe) {
            bool comboChanged = false;
            bool multiplierChanged = false;

            if (_playbackPreviousCombo != keyframe.combo) {
                comboChanged = true;
                Accessors.Combo(ref _comboController) = keyframe.combo;
            }

            if (_playbackPreviousScore != keyframe.score) {
                int maxPossibleRawScore = LeaderboardUtils.OldMaxRawScoreForNumberOfNotes(cutOrMissedNotes);

                _relativeScoreAndImmediateRankCounter?.InvokeMethod<object, RelativeScoreAndImmediateRankCounter>(
                    "UpdateRelativeScoreAndImmediateRank", keyframe.score, keyframe.score, maxPossibleRawScore,
                    maxPossibleRawScore);

                _scoreUIController?.InvokeMethod<object, ScoreUIController>("UpdateScore", keyframe.score,
                    keyframe.score);
            }

            PlaybackMultiplierCheck(keyframe, comboChanged, ref multiplierChanged);

            _playbackPreviousCombo = keyframe.combo;
            _playbackPreviousScore = keyframe.score;

            if (comboChanged) {
                FieldAccessor<ScoreController, Action<int, int>>.Get(_scoreController, "scoreDidChangeEvent").Invoke(
                    keyframe.score,
                    ScoreModel.GetModifiedScoreForGameplayModifiersScoreMultiplier(keyframe.score,
                        Accessors.GameplayMultiplier(ref _scoreController)));
            }

            if (multiplierChanged) {
                FieldAccessor<ScoreController, Action<int, float>>.Get(_scoreController, "multiplierDidChangeEvent")
                    .Invoke(_multiplier, _multiplierIncreaseProgress);
            }
        }

        private void PlaybackMultiplierCheck(Z.Keyframe keyframe, bool comboChanged, ref bool multiplierChanged) {
            if (keyframe.combo > _playbackPreviousCombo) {
                if (_multiplier < 8) {
                    ScoreMultiplierCounter counter = Accessors.MultiplierCounter(ref _scoreController);

                    if (_multiplierIncreaseProgress < _multiplierIncreaseMaxProgress) {
                        _multiplierIncreaseProgress++;

                        Accessors.Progress(ref counter) = _multiplierIncreaseProgress;
                        multiplierChanged = true;
                    }

                    if (_multiplierIncreaseProgress >= _multiplierIncreaseMaxProgress) {
                        _multiplier *= 2;
                        _multiplierIncreaseProgress = 0;
                        _multiplierIncreaseMaxProgress = _multiplier * 2;

                        Accessors.Multiplier(ref counter) = _multiplier;
                        Accessors.Progress(ref counter) = _multiplierIncreaseProgress;
                        Accessors.MaxProgress(ref counter) = _multiplierIncreaseMaxProgress;

                        multiplierChanged = true;
                    }
                }
            } else if (keyframe.combo < _playbackPreviousCombo) {
                if (_multiplierIncreaseProgress > 0) {
                    _multiplierIncreaseProgress = 0;
                    multiplierChanged = true;
                }

                if (_multiplier > 1) {
                    _multiplier /= 2;
                    _multiplierIncreaseMaxProgress = _multiplier * 2;
                    multiplierChanged = true;
                }

                ScoreMultiplierCounter counter = Accessors.MultiplierCounter(ref _scoreController);
                counter.ProcessMultiplierEvent(ScoreMultiplierCounter.MultiplierEventType.Negative);
                FieldAccessor<ScoreController, Action<int, float>>.Get(_scoreController, "multiplierDidChangeEvent")
                    .Invoke(_multiplier, _multiplierIncreaseProgress);
            }
        }

        public bool IsRealMiss() {
            int lastCombo = _keyframes[Math.Max(_lastKeyframeIndex - 10, 0)].combo;

            for (int i = Math.Max(_lastKeyframeIndex - 10, 0);
                 i < Math.Min(_lastKeyframeIndex + 10, _keyframes.Count);
                 i++) {
                if (_keyframes[i].combo < lastCombo) {
                    return true;
                }

                lastCombo = _keyframes[i].combo;
            }

            return false;
        }
    }
}