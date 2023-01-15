#region

using ScoreSaber.Core.Daemons;
using ScoreSaber.Core.ReplaySystem.Data;
using ScoreSaber.Core.ReplaySystem.Recorders;
using ScoreSaber.Core.Services;
using System;
using Zenject;

#endregion

namespace ScoreSaber.Core.ReplaySystem {
    internal class Recorder : IInitializable, IDisposable {
        private readonly EnergyEventRecorder _energyEventRecorder;
        private readonly HeightEventRecorder _heightEventRecorder;
        private readonly string _id;
        private readonly MetadataRecorder _metadataRecorder;
        private readonly NoteEventRecorder _noteEventRecorder;
        private readonly PoseRecorder _poseRecorder;
        private readonly ReplayService _replayService;
        private readonly ScoreEventRecorder _scoreEventRecorder;

        public Recorder(PoseRecorder poseRecorder, MetadataRecorder metadataRecorder,
            NoteEventRecorder noteEventRecorder, ScoreEventRecorder scoreEventRecorder,
            HeightEventRecorder heightEventRecorder, EnergyEventRecorder energyEventRecorder,
            ReplayService replayService) {
            _poseRecorder = poseRecorder;
            _replayService = replayService;
            _metadataRecorder = metadataRecorder;
            _noteEventRecorder = noteEventRecorder;
            _scoreEventRecorder = scoreEventRecorder;
            _heightEventRecorder = heightEventRecorder;
            _energyEventRecorder = energyEventRecorder;

            _id = Guid.NewGuid().ToString();
            Plugin.Log.Debug("Main replay recorder installed");
        }

        public void Dispose() {
        }

        public void Initialize() {
            _replayService.NewPlayStarted(_id, this);
        }

        public ReplayFile Export() {
            return new ReplayFile {
                Metadata = _metadataRecorder.Export(),
                PoseKeyframes = _poseRecorder.Export(),
                HeightKeyframes = _heightEventRecorder.Export(),
                NoteKeyframes = _noteEventRecorder.Export(),
                ScoreKeyframes = _scoreEventRecorder.ExportScoreKeyframes(),
                ComboKeyframes = _scoreEventRecorder.ExportComboKeyframes(),
                MultiplierKeyframes = _scoreEventRecorder.ExportMultiplierKeyframes(),
                EnergyKeyframes = _energyEventRecorder.Export()
            };
        }
    }
}