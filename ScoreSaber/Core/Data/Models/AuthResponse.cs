#region

using Newtonsoft.Json;

#endregion

namespace ScoreSaber.Core.Data.Models {
    internal class AuthResponse {
        [JsonProperty("a")] internal string a { get; set; }

        [JsonProperty("e")] internal string e { get; set; }
    }
}