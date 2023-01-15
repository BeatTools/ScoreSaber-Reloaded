#region

using HarmonyLib;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#endregion

namespace ScoreSaber.Core.ReplaySystem.HarmonyPatches {
    [HarmonyPatch(typeof(NoteCutSoundEffectManager), nameof(NoteCutSoundEffectManager.HandleNoteWasSpawned))]
    internal class CutSoundEffectOverride {
        private static IEnumerator _buffer;
        private static NoteCutSoundEffectManager _spawnEffectManager;
        private static readonly Queue<NoteController> Effects = new Queue<NoteController>();
        internal static bool Buffer { get; set; }

        internal static bool Prefix(NoteCutSoundEffectManager instance, NoteController noteController) {
            if (Plugin.ReplayState.IsPlaybackEnabled && !Plugin.ReplayState.IsLegacyReplay) {
                if (_spawnEffectManager == null || _spawnEffectManager != instance) {
                    _spawnEffectManager = instance;
                    Effects.Clear();
                    _buffer = null;
                    Buffer = false;
                    return true;
                }

                if (!Buffer) {
                    return true;
                }

                if (!Effects.Contains(noteController)) {
                    Effects.Enqueue(noteController);
                    if (_buffer == null) {
                        _buffer = BufferNoteSpawn(instance);
                        instance.StartCoroutine(_buffer);
                    }

                    return false;
                }

                return true;
            }

            return true;
        }

        private static IEnumerator BufferNoteSpawn(NoteCutSoundEffectManager manager) {
            while (Effects.Count > 0) {
                NoteController effect = Effects.Peek();
                manager.HandleNoteWasSpawned(effect);
                Effects.Dequeue();
                yield return new WaitForEndOfFrame();
            }

            Buffer = false;
            _buffer = null;
        }
    }
}