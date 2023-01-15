#region

using HarmonyLib;

#endregion

namespace ScoreSaber.Core.ReplaySystem.HarmonyPatches {
    [HarmonyPatch(typeof(PauseController), nameof(PauseController.HandleHMDUnmounted))]
    internal class PatchHandleHmdUnmounted {
        internal static bool Prefix() {
            if (Plugin.ReplayState.IsPlaybackEnabled) {
                return false;
            }

            return true;
        }
    }
}