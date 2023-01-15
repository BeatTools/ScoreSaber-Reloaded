#region

using ScoreSaber.Core.ReplaySystem.Data;
using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

#endregion

namespace ScoreSaber.Core.ReplaySystem.Recorders {
    internal class MetadataRecorder : TimeSynchronizer, IInitializable, IDisposable {
        private readonly IGameEnergyCounter _gameEnergyCounter;
        private readonly GameplayCoreSceneSetupData _gameplayCoreSceneSetupData;
        private readonly MainSettingsModelSO _mainSettingsModelSo;
        private readonly BeatmapObjectSpawnController.InitData _beatmapObjectSpawnControllerInitData;
        private float _failTime;

        public MetadataRecorder(GameplayCoreSceneSetupData gameplayCoreSceneSetupData,
            BeatmapObjectSpawnController.InitData beatmapObjectSpawnControllerInitData,
            IGameEnergyCounter gameEnergyCounter) {
            _beatmapObjectSpawnControllerInitData = beatmapObjectSpawnControllerInitData;
            _mainSettingsModelSo = Resources.FindObjectsOfTypeAll<MainSettingsModelSO>()[0];
            _gameEnergyCounter = gameEnergyCounter;
            _gameplayCoreSceneSetupData = gameplayCoreSceneSetupData;
        }

        public void Dispose() {
            _gameEnergyCounter.gameEnergyDidReach0Event -= GameEnergyCounter_gameEnergyDidReach0Event;
        }


        public void Initialize() {
            _gameEnergyCounter.gameEnergyDidReach0Event += GameEnergyCounter_gameEnergyDidReach0Event;
        }


        private void GameEnergyCounter_gameEnergyDidReach0Event() {
            _failTime = audioTimeSyncController.songTime;
        }

        public Metadata Export() {
            VRPosition roomCenter = new VRPosition {
                X = _mainSettingsModelSo.roomCenter.value.x,
                Y = _mainSettingsModelSo.roomCenter.value.y,
                Z = _mainSettingsModelSo.roomCenter.value.z
            };

            return new Metadata {
                Version = "2.0.0",
                LevelID = _gameplayCoreSceneSetupData.difficultyBeatmap.level.levelID,
                Difficulty = _gameplayCoreSceneSetupData.difficultyBeatmap.difficulty.DefaultRating(),
                Characteristic = _gameplayCoreSceneSetupData.difficultyBeatmap.parentDifficultyBeatmapSet
                    .beatmapCharacteristic.serializedName,
                Environment = _gameplayCoreSceneSetupData.environmentInfo.serializedName,
                Modifiers = GetModifierList(_gameplayCoreSceneSetupData.gameplayModifiers),
                NoteSpawnOffset = _beatmapObjectSpawnControllerInitData.noteJumpValue,
                LeftHanded = _gameplayCoreSceneSetupData.playerSpecificSettings.leftHanded,
                InitialHeight = _gameplayCoreSceneSetupData.playerSpecificSettings.playerHeight,
                RoomRotation = _mainSettingsModelSo.roomRotation,
                RoomCenter = roomCenter,
                FailTime = _failTime
            };
        }

        private static string[] GetModifierList(GameplayModifiers modifiers) {
            List<string> result = new List<string>();
            if (modifiers.energyType == GameplayModifiers.EnergyType.Battery) {
                result.Add("BE");
            }

            if (modifiers.noFailOn0Energy) {
                result.Add("NF");
            }

            if (modifiers.instaFail) {
                result.Add("IF");
            }

            if (modifiers.failOnSaberClash) {
                result.Add("SC");
            }

            if (modifiers.enabledObstacleType == GameplayModifiers.EnabledObstacleType.NoObstacles) {
                result.Add("NO");
            }

            if (modifiers.noBombs) {
                result.Add("NB");
            }

            if (modifiers.strictAngles) {
                result.Add("SA");
            }

            if (modifiers.disappearingArrows) {
                result.Add("DA");
            }

            if (modifiers.ghostNotes) {
                result.Add("GN");
            }

            if (modifiers.songSpeed == GameplayModifiers.SongSpeed.Slower) {
                result.Add("SS");
            }

            if (modifiers.songSpeed == GameplayModifiers.SongSpeed.Faster) {
                result.Add("FS");
            }

            if (modifiers.songSpeed == GameplayModifiers.SongSpeed.SuperFast) {
                result.Add("SF");
            }

            if (modifiers.smallCubes) {
                result.Add("SC");
            }

            if (modifiers.strictAngles) {
                result.Add("SA");
            }

            if (modifiers.proMode) {
                result.Add("PM");
            }

            if (modifiers.noArrows) {
                result.Add("NA");
            }

            return result.ToArray();
        }
    }
}