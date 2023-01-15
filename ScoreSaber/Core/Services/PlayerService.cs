#region

using IPA.Utilities;
using Newtonsoft.Json;
using Oculus.Platform;
using Oculus.Platform.Models;
using ScoreSaber.Core.Data;
using ScoreSaber.Core.Data.Models;
using ScoreSaber.Core.Data.Wrappers;
using ScoreSaber.Extensions;
using Steamworks;
using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

#endregion

namespace ScoreSaber.Core.Services {
    internal class PlayerService {
        public enum LoginStatus {
            Info = 0,
            Error = 1,
            Success = 2
        }

        public PlayerService() {
            Plugin.Log.Debug("PlayerService Setup!");
        }

        public LocalPlayerInfo localPlayerInfo { get; set; }
        public LoginStatus loginStatus { get; set; }
        public event Action<LoginStatus, string> LoginStatusChanged;

        public void ChangeLoginStatus(LoginStatus _loginStatus, string status) {
            loginStatus = _loginStatus;
            LoginStatusChanged?.Invoke(loginStatus, status);
        }

        public void GetLocalPlayerInfo() {
            if (localPlayerInfo == null) {
                if (File.Exists(Path.Combine(UnityGame.InstallPath, "Beat Saber_Data", "Plugins", "x86_64",
                        "steam_api64.dll"))) {
                    GetLocalPlayerInfo1().RunTask();
                } else {
                    GetLocalPlayerInfo2();
                }
            }
        }

        private async Task GetLocalPlayerInfo1() {
            ChangeLoginStatus(LoginStatus.Info, "Signing into ScoreSaber...");

            int attempts = 1;

            while (attempts < 4) {
                LocalPlayerInfo steamInfo = await GetLocalSteamInfo();
                if (steamInfo != null) {
                    bool authenticated = await AuthenticateWithScoreSaber(steamInfo);
                    if (authenticated) {
                        localPlayerInfo = steamInfo;
                        string successText = "Sucessfully signed into ScoreSaber!";
                        if (localPlayerInfo.playerId == PlayerIDs.Denyah) {
                            successText = "Wagwan piffting wots ur bbm pin?";
                        }

                        ChangeLoginStatus(LoginStatus.Success, successText);
                        break;
                    }

                    ChangeLoginStatus(LoginStatus.Error, $"Failed, attempting again ({attempts} of 3 tries...)");
                    attempts++;
                    await Task.Delay(4000);
                } else {
                    Plugin.Log.Error("Steamworks is not initialized!");
                    ChangeLoginStatus(LoginStatus.Error, "Failed to authenticate! Error getting steam info");
                    break;
                }
            }

            if (loginStatus != LoginStatus.Success) {
                ChangeLoginStatus(LoginStatus.Error,
                    "Failed to authenticate with ScoreSaber! Please restart your game");
            }
        }

        private void GetLocalPlayerInfo2() {
            ChangeLoginStatus(LoginStatus.Info, "Signing into ScoreSaber...");

            Users.GetLoggedInUser().OnComplete(delegate(Message<User> loggedInMessage) {
                if (!loggedInMessage.IsError) {
                    Users.GetLoggedInUserFriends().OnComplete(delegate(Message<UserList> friendsMessage) {
                        if (!friendsMessage.IsError) {
                            Users.GetUserProof().OnComplete(delegate(Message<UserProof> userProofMessage) {
                                if (!userProofMessage.IsError) {
                                    Users.GetAccessToken().OnComplete(async delegate(Message<string> authTokenMessage) {
                                        string playerId = loggedInMessage.Data.ID.ToString();
                                        string playerName = loggedInMessage.Data.OculusID;
                                        string friends = playerId + ",";
                                        string nonce = userProofMessage.Data.Value + "," + authTokenMessage.Data;
                                        LocalPlayerInfo oculusInfo =
                                            new LocalPlayerInfo(playerId, playerName, friends, "1", nonce);
                                        bool authenticated = await AuthenticateWithScoreSaber(oculusInfo);
                                        if (authenticated) {
                                            localPlayerInfo = oculusInfo;
                                            ChangeLoginStatus(LoginStatus.Success,
                                                "Sucessfully signed into ScoreSaber!");
                                        } else {
                                            ChangeLoginStatus(LoginStatus.Error,
                                                "Failed to authenticate with ScoreSaber! Please restart your game");
                                        }
                                    });
                                } else {
                                    ChangeLoginStatus(LoginStatus.Error,
                                        "Failed to authenticate! Error getting oculus info");
                                }
                            });
                        } else {
                            ChangeLoginStatus(LoginStatus.Error, "Failed to authenticate! Error getting oculus info");
                        }
                    });
                } else {
                    ChangeLoginStatus(LoginStatus.Error, "Failed to authenticate! Error getting oculus info");
                }
            });
        }

        private async Task<LocalPlayerInfo> GetLocalSteamInfo() {
            await TaskEx.WaitUntil(() => SteamManager.Initialized);

            string authToken = (await new SteamPlatformUserModel().GetUserAuthToken()).token;

            LocalPlayerInfo steamInfo = await Task.Run(() => {
                CSteamID steamID = SteamUser.GetSteamID();
                string playerId = steamID.m_SteamID.ToString();
                string playerName = SteamFriends.GetPersonaName();
                string friends = playerId + ",";
                for (int i = 0; i < SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagAll); i++) {
                    CSteamID friendSteamId = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate);
                    if (friendSteamId.m_SteamID.ToString() != "0") {
                        friends = friends + friendSteamId.m_SteamID + ",";
                    }
                }

                friends = friends.Remove(friends.Length - 1);
                return new LocalPlayerInfo(playerId, playerName, friends, "0", authToken);
            });


            return steamInfo;
        }

        private async Task<bool> AuthenticateWithScoreSaber(LocalPlayerInfo playerInfo) {
            if (Plugin.HttpInstance.PersistentRequestHeaders.ContainsKey("Cookies")) {
                Plugin.HttpInstance.PersistentRequestHeaders.Remove("Cookies");
            }

            WWWForm form = new WWWForm();
            form.AddField("at", playerInfo.authType);
            form.AddField("playerId", playerInfo.playerId);
            form.AddField("nonce", playerInfo.playerNonce);
            form.AddField("friends", playerInfo.playerFriends);
            form.AddField("name", playerInfo.playerName);

            try {
                string response = await Plugin.HttpInstance.PostAsync("/game/auth", form);
                AuthResponse authResponse = JsonConvert.DeserializeObject<AuthResponse>(response);
                playerInfo.playerKey = authResponse.a;
                playerInfo.serverKey = authResponse.e;

                Plugin.HttpInstance.PersistentRequestHeaders.Add("Cookies", $"connect.sid={playerInfo.serverKey}");
                return true;
            } catch (Exception ex) {
                Plugin.Log.Error($"Failed user authentication: {ex.Message}");
                return false;
            }
        }

        public async Task<PlayerInfo> GetPlayerInfo(string playerId, bool full) {
            string url = $"/player/{playerId}";

            if (full) {
                url += "/full";
            } else {
                url += "/basic";
            }

            string response = await Plugin.HttpInstance.GetAsync(url);
            PlayerInfo playerStats = JsonConvert.DeserializeObject<PlayerInfo>(response);
            return playerStats;
        }

        public async Task<byte[]> GetReplayData(IDifficultyBeatmap level, int leaderboardId, ScoreMap scoreMap) {
            if (scoreMap.hasLocalReplay) {
                string replayPath = GetReplayPath(scoreMap.parent.songHash, level.difficulty.SerializedName(),
                    level.parentDifficultyBeatmapSet.beatmapCharacteristic.serializedName,
                    scoreMap.score.leaderboardPlayerInfo.id, level.level.songName);
                if (replayPath != null) {
                    return File.ReadAllBytes(replayPath);
                }
            }

            byte[] response = await Plugin.HttpInstance.DownloadAsync(
                $"/game/telemetry/downloadReplay?playerId={scoreMap.score.leaderboardPlayerInfo.id}&leaderboardId={leaderboardId}");

            if (response != null) {
                return response;
            }

            throw new Exception("Failed to download replay");
        }

        private string GetReplayPath(string levelId, string difficultyName, string characteristic, string playerId,
            string songName) {
            songName = songName.ReplaceInvalidChars().Truncate(155);

            string path =
                $@"{Settings.replayPath}\{playerId}-{songName}-{difficultyName}-{characteristic}-{levelId}.dat";
            if (File.Exists(path)) {
                return path;
            }

            string legacyPath = $@"{Settings.replayPath}\{playerId}-{songName}-{levelId}.dat";
            if (File.Exists(legacyPath)) {
                return legacyPath;
            }

            return null;
        }
    }
}