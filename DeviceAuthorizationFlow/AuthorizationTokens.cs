using Newtonsoft.Json;
using System;

namespace DeviceAuthorizationFlow
{
    public class AuthorizationTokens
    {
        [JsonProperty]
        public string AccessToken { get; private set; }

        [JsonProperty]
        public string RefreshToken { get; private set; }

        [JsonProperty]
        public int ExpiresInSeconds { get; private set; }

        [JsonProperty]
        public string TokenType { get; private set; }

        [JsonConstructor]
        public AuthorizationTokens(string access_token, string refresh_token, int expires_in, string token_type)
        {
            AccessToken = access_token;
            RefreshToken = refresh_token;
            ExpiresInSeconds = expires_in;
            TokenType = token_type;
        }
    }
}
