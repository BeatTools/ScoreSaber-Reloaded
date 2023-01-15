﻿#region

using IPA.Utilities;
using ScoreSaber.Core.ReplaySystem.Data;
using ScoreSaber.Extensions;
using SiraUtil.Affinity;
using SiraUtil.Logging;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zenject;

#endregion

namespace ScoreSaber.Core.ReplaySystem.Playback {
    internal class NotePlayer : TimeSynchronizer, ITickable, IScroller, IAffinity {
        private readonly MemoryPoolContainer<BombNoteController> _bombNotePool;
        private readonly MemoryPoolContainer<GameNoteController> _gameNotePool;

        private readonly Dictionary<NoteCutInfo, NoteEvent> _recognizedNoteCutInfos =
            new Dictionary<NoteCutInfo, NoteEvent>();

        private readonly SaberManager _saberManager;
        private readonly SiraLog _siraLog;
        private readonly NoteEvent[] _sortedNoteEvents;
        private int _lastIndex;

        public NotePlayer(SiraLog siraLog, ReplayFile file, SaberManager saberManager,
            BasicBeatmapObjectManager basicBeatmapObjectManager) {
            _siraLog = siraLog;
            _saberManager = saberManager;
            _gameNotePool = Accessors.GameNotePool(ref basicBeatmapObjectManager);
            _bombNotePool = Accessors.BombNotePool(ref basicBeatmapObjectManager);
            _sortedNoteEvents = file.NoteKeyframes.OrderBy(nk => nk.Time).ToArray();
        }

        public void TimeUpdate(float newTime) {
            for (int c = 0; c < _sortedNoteEvents.Length; c++) {
                if (!(_sortedNoteEvents[c].Time >= newTime)) {
                    continue;
                }

                _lastIndex = c;
                Tick();
                return;
            }

            _lastIndex = _sortedNoteEvents.Count() != 0 ? _sortedNoteEvents.Length - 1 : 0;
        }

        public void Tick() {
            if (_lastIndex >= _sortedNoteEvents.Length) {
                return;
            }

            while (audioTimeSyncController.songTime >= _sortedNoteEvents[_lastIndex].Time) {
                NoteEvent activeEvent = _sortedNoteEvents[_lastIndex++];
                ProcessEvent(activeEvent);

                if (_lastIndex >= _sortedNoteEvents.Length) {
                    break;
                }
            }
        }

        private bool ProcessEvent(NoteEvent activeEvent) {
            bool foundNote = false;
            switch (activeEvent.EventType)
            {
                case NoteEventType.GoodCut:
                case NoteEventType.BadCut: {
                    if (_gameNotePool.activeItems.Any(noteController => HandleEvent(activeEvent, noteController)))
                    {
                        foundNote = true;
                    }

                    break;
                }
                case NoteEventType.Bomb: {
                    if (_bombNotePool.activeItems.Any(bombController => HandleEvent(activeEvent, bombController)))
                    {
                        foundNote = true;
                    }

                    break;
                }
            }

            return foundNote;
        }

        private bool HandleEvent(NoteEvent activeEvent, NoteController noteController) {
            if (!DoesNoteMatchID(activeEvent.NoteID, noteController.noteData)) {
                return false;
            }

            Saber correctSaber = noteController.noteData.colorType == ColorType.ColorA
                ? _saberManager.leftSaber
                : _saberManager.rightSaber;
            Transform noteTransform = noteController.noteTransform;

            NoteCutInfo noteCutInfo = new NoteCutInfo(noteController.noteData,
                activeEvent.SaberSpeed > 2f,
                activeEvent.DirectionOk,
                activeEvent.SaberType == (int)correctSaber.saberType,
                false,
                activeEvent.SaberSpeed,
                activeEvent.SaberDirection.Convert(),
                noteController.noteData.colorType == ColorType.ColorA ? SaberType.SaberA : SaberType.SaberB,
                noteController.noteData.time - activeEvent.Time,
                activeEvent.CutDirectionDeviation,
                activeEvent.CutPoint.Convert(),
                activeEvent.CutNormal.Convert(),
                activeEvent.CutDistanceToCenter,
                activeEvent.CutAngle,
                noteController.worldRotation,
                noteController.inverseWorldRotation,
                noteTransform.rotation,
                noteTransform.position,
                correctSaber.movementData
            );

            _recognizedNoteCutInfos.Add(noteCutInfo, activeEvent);
            noteController.InvokeMethod<object, NoteController>("SendNoteWasCutEvent", noteCutInfo);
            return true;

        }

        [AffinityPostfix]
        [AffinityPatch(typeof(GoodCutScoringElement), nameof(GoodCutScoringElement.Init))]
        protected void ForceCompleteGoodScoringElements(GoodCutScoringElement instance, NoteCutInfo noteCutInfo,
            CutScoreBuffer cutScoreBuffer) {
            // Just in case someone else is creating their own scoring elements, we want to ensure that we're only force completing ones we know we've created
            if (!_recognizedNoteCutInfos.TryGetValue(noteCutInfo, out NoteEvent activeEvent)) {
                return;
            }

            _recognizedNoteCutInfos.Remove(noteCutInfo);

            if (instance.isFinished) {
                return;
            }

            SaberSwingRatingCounter ratingCounter = Accessors.RatingCounter(ref cutScoreBuffer);

            // Supply the rating counter with the proper cut ratings
            Accessors.AfterCutRating(ref ratingCounter) = activeEvent.AfterCutRating;
            Accessors.BeforeCutRating(ref ratingCounter) = activeEvent.BeforeCutRating;

            // Then immediately finish it
            cutScoreBuffer.HandleSaberSwingRatingCounterDidFinish(ratingCounter);

            ScoringElement element = instance;
            Accessors.ScoringElementFinisher(ref element, true);
        }

        private static bool DoesNoteMatchID(NoteID id, NoteData note) {
            return new NoteID {
                Time = note.time, LineIndex = note.lineIndex, LineLayer = (int)note.noteLineLayer,
                ColorType = (int)note.colorType, CutDirection = (int)note.cutDirection
            } == id;
        }
    }
}