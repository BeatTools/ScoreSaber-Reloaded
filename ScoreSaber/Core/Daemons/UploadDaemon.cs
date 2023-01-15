#if RELEASE

#region

using Newtonsoft.Json;
using ScoreSaber.Core.Data;
using ScoreSaber.Core.Data.Internal;
using ScoreSaber.Core.Data.Models;
using ScoreSaber.Core.Services;
using ScoreSaber.Core.Utils;
using ScoreSaber.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static ScoreSaber.UI.Leaderboard.ScoreSaberLeaderboardViewController;

#endregion

namespace ScoreSaber.Core.Daemons {
    // TODO: Actually make pretty now that we're open source
    internal class UploadDaemon : IDisposable, IUploadDaemon {
        private const string UploadSecret = "f0b4a81c9bd3ded1081b365f7628781f";
        private readonly CustomLevelLoader _customLevelLoader;
        private readonly LeaderboardService _leaderboardService;

        private readonly PlayerDataModel _playerDataModel;

        private readonly PlayerService _playerService;
        private readonly ReplayService _replayService;

        public UploadDaemon(PlayerService playerService, LeaderboardService leaderboardService,
            ReplayService replayService, PlayerDataModel playerDataModel, CustomLevelLoader customLevelLoader) {
            _playerService = playerService;
            _replayService = replayService;
            _leaderboardService = leaderboardService;
            _playerDataModel = playerDataModel;
            _customLevelLoader = customLevelLoader;

            SetupUploader();
            Plugin.Log.Debug("Upload service setup!");
        }

        public void Dispose() {
            Plugin.Log.Info("Upload service succesfully deconstructed");
            StandardLevelScenesTransitionSetupDataSO transitionSetup =
                Resources.FindObjectsOfTypeAll<StandardLevelScenesTransitionSetupDataSO>().FirstOrDefault();
            if (transitionSetup != null) {
                transitionSetup.didFinishEvent -= StandardUpload;
            }
        }

        public event Action<UploadStatus, string> UploadStatusChanged;

        public bool Uploading { get; set; }

        private void SetupUploader() {
            StandardLevelScenesTransitionSetupDataSO transitionSetup =
                Resources.FindObjectsOfTypeAll<StandardLevelScenesTransitionSetupDataSO>().FirstOrDefault();
            MultiplayerLevelScenesTransitionSetupDataSO multiTransitionSetup =
                Resources.FindObjectsOfTypeAll<MultiplayerLevelScenesTransitionSetupDataSO>().FirstOrDefault();
            
            if (!Plugin.ScoreSubmission) {
                
                return;
            }

            transitionSetup.didFinishEvent -= UploadDaemonHelper.ThreeInstance;
            transitionSetup.didFinishEvent -= StandardUpload;
            UploadDaemonHelper.ThreeInstance = StandardUpload;
            transitionSetup.didFinishEvent += StandardUpload;

            multiTransitionSetup.didFinishEvent -= UploadDaemonHelper.FourInstance;
            multiTransitionSetup.didFinishEvent -= MultiplayerUpload;
            UploadDaemonHelper.FourInstance = MultiplayerUpload;
            multiTransitionSetup.didFinishEvent += MultiplayerUpload;
        }

        // Standard uploader
        private void StandardUpload(StandardLevelScenesTransitionSetupDataSO standardLevelScenesTransitionSetupDataSo,
            LevelCompletionResults levelCompletionResults) {
            PreUpload(standardLevelScenesTransitionSetupDataSo.gameMode,
                standardLevelScenesTransitionSetupDataSo.difficultyBeatmap, levelCompletionResults,
                standardLevelScenesTransitionSetupDataSo.practiceSettings != null);
        }

        // Multiplayer uploader
        private void MultiplayerUpload(
            MultiplayerLevelScenesTransitionSetupDataSO multiplayerLevelScenesTransitionSetupDataSo,
            MultiplayerResultsData multiplayerResultsData) {
            if (multiplayerLevelScenesTransitionSetupDataSo.difficultyBeatmap == null) {
                return;
            }

            if (multiplayerResultsData.localPlayerResultData.multiplayerLevelCompletionResults.levelCompletionResults ==
                null) {
                return;
            }

            if (multiplayerResultsData.localPlayerResultData.multiplayerLevelCompletionResults.playerLevelEndReason ==
                MultiplayerLevelCompletionResults.MultiplayerPlayerLevelEndReason.HostEndedLevel) {
                return;
            }

            if (multiplayerResultsData.localPlayerResultData.multiplayerLevelCompletionResults.levelCompletionResults
                    .levelEndStateType != LevelCompletionResults.LevelEndStateType.Cleared) {
                return;
            }

            PreUpload(multiplayerLevelScenesTransitionSetupDataSo.gameMode,
                multiplayerLevelScenesTransitionSetupDataSo.difficultyBeatmap,
                multiplayerResultsData.localPlayerResultData.multiplayerLevelCompletionResults.levelCompletionResults,
                false);
        }

        private void PreUpload(string gameMode, IDifficultyBeatmap difficultyBeatmap,
            LevelCompletionResults levelCompletionResults, bool practicing) {
            try {
                if (Plugin.ReplayState.IsPlaybackEnabled) { return; }

                PracticeViewController practiceViewController =
                    Resources.FindObjectsOfTypeAll<PracticeViewController>().FirstOrDefault();

                if (practiceViewController != null && practiceViewController.isInViewControllerHierarchy) {
                    _replayService.WriteSerializedReplay().RunTask();
                    return;
                }

                if (gameMode != "Solo" && gameMode != "Multiplayer") {
                    return;
                }

                Plugin.Log.Debug(
                    $"Starting upload process for {difficultyBeatmap.level.levelID}:{difficultyBeatmap.level.songName}");
                if (practicing) {
                    // If practice write replay at this point
                    _replayService.WriteSerializedReplay().RunTask();
                    return;
                }

                if (levelCompletionResults.levelEndAction != LevelCompletionResults.LevelEndAction.None) {
                    _replayService.WriteSerializedReplay().RunTask();
                    return;
                }

                if (levelCompletionResults.levelEndStateType != LevelCompletionResults.LevelEndStateType.Cleared) {
                    _replayService.WriteSerializedReplay().RunTask();
                    return;
                }

                Upload(difficultyBeatmap, levelCompletionResults);
            } catch (Exception ex) {
                UploadStatusChanged?.Invoke(UploadStatus.Error, "Failed to upload score, error written to log.");
                Plugin.Log.Error($"Failed to upload score: {ex}");
            }
        }

        //This starts the upload processs
        private async void Upload(IDifficultyBeatmap difficultyBeatmap, LevelCompletionResults levelCompletionResults) {
            if (!(difficultyBeatmap.level is CustomBeatmapLevel)) {
                return;
            }

            EnvironmentInfoSO defaultEnvironment = _customLevelLoader.LoadEnvironmentInfo(null, false);

            IReadonlyBeatmapData beatmapData =
                await difficultyBeatmap.GetBeatmapDataAsync(defaultEnvironment,
                    _playerDataModel.playerData.playerSpecificSettings);

            if (LeaderboardUtils.ContainsV3Stuff(beatmapData)) {
                UploadStatusChanged?.Invoke(UploadStatus.Error, "New note type not supported, not uploading");
                return;
            }

            double maxScore = LeaderboardUtils.OldMaxRawScoreForNumberOfNotes(beatmapData.cuttableNotesCount);
            maxScore *= 1.12;

            if (levelCompletionResults.modifiedScore > maxScore) {
                return;
            }

            try {
                UploadStatusChanged?.Invoke(UploadStatus.Packaging, "Packaging score...");
                ScoreSaberUploadData data =
                    ScoreSaberUploadData.Create(difficultyBeatmap, levelCompletionResults,
                        _playerService.localPlayerInfo, GetVersionHash());
                string scoreData = JsonConvert.SerializeObject(data);

                // TODO: Simplify now that we're open source
                byte[] encodedPassword =
                    new UTF8Encoding().GetBytes(
                        $"{UploadSecret}-{_playerService.localPlayerInfo.playerKey}-{_playerService.localPlayerInfo.playerId}-{UploadSecret}");
                byte[] keyHash = ((HashAlgorithm)CryptoConfig.CreateFromName("MD5")).ComputeHash(encodedPassword);
                string key = BitConverter.ToString(keyHash)
                    .Replace("-", string.Empty)
                    .ToLower();

                string scoreDataHex =
                    BitConverter.ToString(Swap(Encoding.UTF8.GetBytes(scoreData), Encoding.UTF8.GetBytes(key)))
                        .Replace("-", "");
                Seven(data, scoreDataHex, difficultyBeatmap, levelCompletionResults).RunTask();
            } catch (Exception ex) {    
                UploadStatusChanged?.Invoke(UploadStatus.Error, "Failed to upload score, error written to log.");
                Plugin.Log.Error($"Failed to upload score: {ex}");
            }
        }

        private async Task Seven(ScoreSaberUploadData scoreSaberUploadData, string uploadData,
            IDifficultyBeatmap difficultyBeatmap, LevelCompletionResults results) {
            try {
                UploadStatusChanged?.Invoke(UploadStatus.Packaging, "Checking leaderboard ranked status...");

                Leaderboard currentLeaderboard = await _leaderboardService.GetCurrentLeaderboard(difficultyBeatmap);

                if (currentLeaderboard != null) {
                    if (currentLeaderboard.leaderboardInfo.playerScore != null) {
                        if (results.modifiedScore < currentLeaderboard.leaderboardInfo.playerScore.modifiedScore) {
                            UploadStatusChanged?.Invoke(UploadStatus.Error, "Didn't beat score, not uploading.");
                            UploadStatusChanged?.Invoke(UploadStatus.Done, "");
                            Uploading = false;
                            return;
                        }
                    }
                } else {
                    Plugin.Log.Debug("Failed to get leaderboards ranked status");
                }

                bool done = false;
                bool failed = false;
                int attempts = 1;
                UploadStatusChanged?.Invoke(UploadStatus.Packaging, "Packaging replay...");
                byte[] serializedReplay = await _replayService.WriteSerializedReplay();

                // Create http packet
                WWWForm form = new WWWForm();
                form.AddField("data", uploadData);
                if (serializedReplay != null) {
                    Plugin.Log.Debug($"Replay size: {serializedReplay.Length}");
                    form.AddBinaryData("zr", serializedReplay);
                } else {
                    UploadStatusChanged?.Invoke(UploadStatus.Error, "Failed to upload (failed to serialize replay)");
                    done = true;
                    failed = true;
                }

                // Start upload process
                while (!done) {
                    Uploading = true;
                    string response = null;
                    Plugin.Log.Info("Attempting score upload...");
                    UploadStatusChanged?.Invoke(UploadStatus.Uploading, "Uploading score...");
                    try {
                        response = await Plugin.HttpInstance.PostAsync("/game/upload", form);
                    } catch (HttpErrorException httpException) {
                        Plugin.Log.Error(httpException.isScoreSaberError
                            ? $"Failed to upload score: {httpException.scoreSaberError.errorMessage}:{httpException}"
                            : $"Failed to upload score: {httpException.isNetworkError}:{httpException.isHttpError}:{httpException}");
                    } catch (Exception ex) {
                        Plugin.Log.Error($"Failed to upload score: {ex}");
                    }

                    if (!string.IsNullOrEmpty(response)) {
                        if (response.Contains("uploaded")) {
                            done = true;
                        } else {
                            if (response == "banned") {
                                UploadStatusChanged?.Invoke(UploadStatus.Error, "Failed to upload (banned)");
                                done = true;
                                failed = true;
                            }

                            Plugin.Log.Error($"Raw failed response: ${response}");
                        }
                    }

                    if (done) {
                        continue;
                    }

                    if (attempts < 4) {
                        UploadStatusChanged?.Invoke(UploadStatus.Retrying,
                            $"Failed, attempting again ({attempts} of 3 tries...)");
                        attempts++;
                        await Task.Delay(1000);
                    } else {
                        done = true;
                        failed = true;
                    }
                }

                if (!failed) {
                    SaveLocalReplay(scoreSaberUploadData, difficultyBeatmap, serializedReplay);
                    Plugin.Log.Info("Score uploaded!");
                    UploadStatusChanged?.Invoke(UploadStatus.Success, "Score uploaded!");
                } else {
                    UploadStatusChanged?.Invoke(UploadStatus.Error, "Failed to upload score.");
                }

                Uploading = false;
                UploadStatusChanged?.Invoke(UploadStatus.Done, "");
            } catch (Exception) {
                Uploading = false;
                UploadStatusChanged?.Invoke(UploadStatus.Done, "");
            }
        }

        private static void SaveLocalReplay(ScoreSaberUploadData scoreSaberUploadData,
            IDifficultyBeatmap difficultyBeatmap, byte[] replay) {
            if (replay == null) {
                Plugin.Log.Error("Failed to write local replay; replay is null");
                return;
            }

            try {
                if (!Plugin.Settings.saveLocalReplays) {
                    return;
                }

                string replayPath =
                    $@"{Settings.replayPath}\{scoreSaberUploadData.playerId}-{scoreSaberUploadData.songName.ReplaceInvalidChars().Truncate(155)}-{difficultyBeatmap.difficulty.SerializedName()}-{difficultyBeatmap.parentDifficultyBeatmapSet.beatmapCharacteristic.serializedName}-{scoreSaberUploadData.leaderboardId}.dat";
                File.WriteAllBytes(replayPath, replay);
            } catch (Exception ex) {
                Plugin.Log.Error($"Failed to write local replay; {ex}");
            }
        }

        private static byte[] Swap(IReadOnlyList<byte> panda1, IReadOnlyList<byte> panda2) {
            int n1 = 11;
            int n2 = 13;
            int ns = 257;

            for (int I = 0; I <= panda2.Count - 1; I++) {
                ns += ns % (panda2[I] + 1);
            }

            byte[] T = new byte[panda1.Count];
            for (int I = 0; I <= panda1.Count - 1; I++) {
                ns = panda2[I % panda2.Count] + ns;
                n1 = ((ns + 5) * (n1 & 255)) + (n1 >> 8);
                n2 = ((ns + 7) * (n2 & 255)) + (n2 >> 8);
                ns = ((n1 << 8) + n2) & 255;

                T[I] = (byte)(panda1[I] ^ (byte)ns);
            }

            return T;
        }

        private static string GetVersionHash() {
            using (MD5 md5 = MD5.Create()) {
                string versionString = $"{Plugin.Instance.LibVersion}{Application.version}";
                string hash =
                    BitConverter.ToString(md5.ComputeHash(Encoding.UTF8.GetBytes(versionString))).Replace("-", "")
                        .ToLowerInvariant();
                return hash;
            }
        }
    }
}
#endif