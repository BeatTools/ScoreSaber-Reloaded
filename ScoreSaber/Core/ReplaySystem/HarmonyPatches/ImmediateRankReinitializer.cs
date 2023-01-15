#region

using HarmonyLib;
using System;

#endregion

namespace ScoreSaber.Core.ReplaySystem.HarmonyPatches {
    [HarmonyPatch(typeof(RelativeScoreAndImmediateRankCounter),
        nameof(RelativeScoreAndImmediateRankCounter.UpdateRelativeScoreAndImmediateRank))]
    internal class ImmediateRankReinitializer {
        internal static bool Prefix(RelativeScoreAndImmediateRankCounter instance, int score, int maxPossibleScore,
            ref Action relativeScoreOrImmediateRankDidChangeEvent) {
            if (Plugin.ReplayState.IsPlaybackEnabled && !Plugin.ReplayState.IsLegacyReplay) {
                if (score == 0 && maxPossibleScore == 0) {
                    Accessors.RelativeScore(ref instance, 1f);
                    Accessors.ImmediateRank(ref instance, RankModel.Rank.SS);
                    relativeScoreOrImmediateRankDidChangeEvent.Invoke();
                    return false;
                }

                return true;
            }

            return true;
        }
    }
}