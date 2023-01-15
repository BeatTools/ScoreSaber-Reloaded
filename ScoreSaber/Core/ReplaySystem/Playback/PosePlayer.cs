#region

using IPA.Utilities;
using ScoreSaber.Core.ReplaySystem.Data;
using ScoreSaber.Extensions;
using SiraUtil.Tools.FPFC;
using System;
using System.Linq;
using UnityEngine;
using Zenject;
using Object = UnityEngine.Object;

#endregion

namespace ScoreSaber.Core.ReplaySystem.Playback {
    internal class PosePlayer : TimeSynchronizer, IInitializable, ITickable, IScroller, IDisposable {
        private readonly IFPFCSettings _fpfcSettings;
        private readonly MainCamera _mainCamera;
        private readonly MainSettingsModelSO _mainSettingsModelSo;
        private readonly IReturnToMenuController _returnToMenuController;
        private readonly SaberManager _saberManager;
        private readonly VRPoseGroup[] _sortedPoses;
        private Camera _desktopCamera;
        private int _lastIndex;
        private PlayerTransforms _playerTransforms;
        private bool _saberEnabled = true;
        private Camera _spectatorCamera;
        private Vector3 _spectatorOffset;

        private readonly bool _initialFpfcState;

        public PosePlayer(ReplayFile file, MainCamera mainCamera, SaberManager saberManager,
            IReturnToMenuController returnToMenuController, IFPFCSettings fpfcSettings,
            PlayerTransforms playerTransforms) {
            _fpfcSettings = fpfcSettings;
            _initialFpfcState = fpfcSettings.Enabled;
            _fpfcSettings.Enabled = false;

            _mainCamera = mainCamera;
            _saberManager = saberManager;
            _sortedPoses = file.PoseKeyframes.ToArray();
            _returnToMenuController = returnToMenuController;
            _spectatorOffset = new Vector3(0f, 0f, -2f);
            _mainSettingsModelSo = Resources.FindObjectsOfTypeAll<MainSettingsModelSO>()[0];
            _playerTransforms = playerTransforms;
        }

        public void Dispose() {
            _fpfcSettings.Changed -= fpfcSettings_Changed;
            _fpfcSettings.Enabled = _initialFpfcState;
        }

        public void Initialize() {
            SetupCameras();
            //_saberManager.leftSaber.disableCutting = true;
            //_saberManager.rightSaber.disableCutting = true;
            _saberManager.leftSaber.transform.GetComponentInParent<VRController>().enabled = false;
            _saberManager.rightSaber.transform.GetComponentInParent<VRController>().enabled = false;
            _fpfcSettings.Changed += fpfcSettings_Changed;
        }

        public void TimeUpdate(float newTime) {
            for (int c = 0; c < _sortedPoses.Length; c++) {
                if (_sortedPoses[c].Time >= newTime) {
                    _lastIndex = c;
                    Tick();
                    return;
                }
            }
        }

        public void Tick() {
            if (ReachedEnd()) {
                _returnToMenuController.ReturnToMenu();
                return;
            }

            bool foundPoseThisFrame = false;
            while (audioTimeSyncController.songTime >= _sortedPoses[_lastIndex].Time) {
                foundPoseThisFrame = true;
                VRPoseGroup activePose = _sortedPoses[_lastIndex++];

                if (ReachedEnd()) {
                    return;
                }

                VRPoseGroup nextPose = _sortedPoses[_lastIndex + 1];
                UpdatePoses(activePose, nextPose);
            }

            if (foundPoseThisFrame) {
                return;
            }

            if (_lastIndex > 0 && !ReachedEnd()) {
                VRPoseGroup previousGroup = _sortedPoses[_lastIndex - 1];
                UpdatePoses(previousGroup, _sortedPoses[_lastIndex]);
            }
        }

        public event Action<VRPoseGroup> DidUpdatePose;

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
            spectatorObject.transform.position = new Vector3(
                _mainSettingsModelSo.roomCenter.value.x + _spectatorOffset.x,
                _mainSettingsModelSo.roomCenter.value.y + _spectatorOffset.y,
                _mainSettingsModelSo.roomCenter.value.z + _spectatorOffset.z);
            Quaternion rotation = new Quaternion {
                eulerAngles = new Vector3(0.0f, _mainSettingsModelSo.roomRotation.value, 0.0f)
            };
            _spectatorCamera.transform.rotation = rotation;
            _spectatorCamera.stereoTargetEye = StereoTargetEyeMask.Both;
            _spectatorCamera.gameObject.SetActive(true);
            _spectatorCamera.depth = 0;
            _spectatorCamera.transform.SetParent(spectatorObject.transform);

            if (Plugin.Settings.enableReplayFrameRenderer) {
                ScreenshotRecorder ss = Resources.FindObjectsOfTypeAll<ScreenshotRecorder>().Last();
                ss.SetField("_directory", Plugin.Settings.replayFramePath);
                ss.enabled = true;
                _desktopCamera.depth = 1;
                DisableGCWhileEnabled gc = Resources.FindObjectsOfTypeAll<DisableGCWhileEnabled>().Last();
                gc.enabled = false;
            }
        }

        private void UpdatePoses(VRPoseGroup activePose, VRPoseGroup nextPose) {
            if (Input.GetKeyDown(KeyCode.H)) {
                _saberEnabled = !_saberEnabled;
            }

            float lerpTime = (audioTimeSyncController.songTime - activePose.Time) /
                             Mathf.Max(0.000001f, nextPose.Time - activePose.Time);

            //_mainCamera.transform.SetPositionAndRotation(activePose.Head.Position.Convert(), activePose.Head.Rotation.Convert());
            Accessors.HeadTransform(ref _playerTransforms).SetPositionAndRotation(activePose.Head.Position.Convert(),
                activePose.Head.Rotation.Convert());

            if (_saberEnabled) {
                _saberManager.leftSaber.OverridePositionAndRotation(
                    Vector3.Lerp(activePose.Left.Position.Convert(), nextPose.Left.Position.Convert(), lerpTime),
                    Quaternion.Lerp(activePose.Left.Rotation.Convert(), nextPose.Left.Rotation.Convert(), lerpTime)
                );

                _saberManager.rightSaber.OverridePositionAndRotation(
                    Vector3.Lerp(activePose.Right.Position.Convert(), nextPose.Right.Position.Convert(), lerpTime),
                    Quaternion.Lerp(activePose.Right.Rotation.Convert(), nextPose.Right.Rotation.Convert(), lerpTime)
                );
            } else {
                _saberManager.leftSaber.OverridePositionAndRotation(Vector3.zero, new Quaternion(0, 0, 0, 0));
                _saberManager.rightSaber.OverridePositionAndRotation(Vector3.zero, new Quaternion(0, 0, 0, 0));
            }


            Vector3 pos = Vector3.Lerp(activePose.Head.Position.Convert(), nextPose.Head.Position.Convert(), lerpTime);
            Quaternion rot = Quaternion.Lerp(activePose.Head.Rotation.Convert(), nextPose.Head.Rotation.Convert(),
                lerpTime);

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

            DidUpdatePose?.Invoke(activePose);
        }

        private bool ReachedEnd() {
            return _lastIndex >= _sortedPoses.Length - 1;
        }

        public void SetSpectatorOffset(Vector3 value) {
            _spectatorCamera.transform.parent.position = new Vector3(_mainSettingsModelSo.roomCenter.value.x + value.x,
                _mainSettingsModelSo.roomCenter.value.y + value.y, _mainSettingsModelSo.roomCenter.value.z + value.z);

            _spectatorOffset = value;
        }
    }
}