#region

using HarmonyLib;
using SiraUtil.Affinity;
using SiraUtil.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

#endregion

namespace ScoreSaber.Core.ReplaySystem.HarmonyPatches {
    internal class CancelScoreControllerBufferFinisher : IAffinity {
        private static readonly FieldInfo MultScore =
            typeof(ScoreController).GetField("_multipliedScore", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo ImmediateScore =
            typeof(ScoreController).GetField("_immediateMaxPossibleMultipliedScore",
                BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly SiraLog _siraLog;

        public CancelScoreControllerBufferFinisher(SiraLog siraLog) {
            _siraLog = siraLog;
        }

        [AffinityTranspiler]
        [AffinityPatch(typeof(ScoreController), nameof(ScoreController.LateUpdate))]
        protected IEnumerable<CodeInstruction> RemoveScoreUpdate(IEnumerable<CodeInstruction> instructions) {
            List<CodeInstruction> codes = instructions.ToList();

            int? startIndex = null;
            int? endIndex = null;
            int count = 0;

#pragma warning disable CS0252 // Possible unintended reference comparison; left hand side needs cast
            for (int i = 0; i < codes.Count; i++) {
                if (!startIndex.HasValue) {
                    if (codes[i].opcode == OpCodes.Ldfld && codes[i].operand == MultScore) {
                        startIndex = i - 1;
                        count = 2;
                    }
                } else if (!endIndex.HasValue) {
                    count++;
                    if (codes[i].opcode == OpCodes.Stfld && codes[i].operand == ImmediateScore) {
                        endIndex = i;
                    }
                } else {
                    break;
                }
            }

            if (startIndex.HasValue && endIndex.HasValue) {
                codes.RemoveRange(startIndex.Value, count);
            } else {
                _siraLog.Error("Unable to cancel score controller buffer setters! Could not find IL group.");
            }

            return codes;
        }
    }
}