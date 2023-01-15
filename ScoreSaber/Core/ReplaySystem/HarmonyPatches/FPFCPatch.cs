#region

using SiraUtil.Affinity;
using SiraUtil.Attributes;

#endregion

namespace ScoreSaber.Core.ReplaySystem.HarmonyPatches {
    [Bind]
    internal class FpfcPatch : IAffinity {
        private readonly bool _isOculus;
        private readonly IVRPlatformHelper _vrPlatformHelper;

        public FpfcPatch(IVRPlatformHelper vrPlatformHelper) {
            _vrPlatformHelper = vrPlatformHelper;
            _isOculus = _vrPlatformHelper is OculusVRHelper;
        }

        [AffinityPatch(typeof(OculusVRHelper), nameof(OculusVRHelper.hasInputFocus), AffinityMethodType.Getter)]
        protected void ForceInputFocus(ref bool result) {
            if (_isOculus && Plugin.ReplayState.IsPlaybackEnabled) {
                result = true;
            }
        }
    }
}