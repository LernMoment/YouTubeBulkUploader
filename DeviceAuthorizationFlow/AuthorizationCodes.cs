using Newtonsoft.Json;
using System;

namespace DeviceAuthorizationFlow
{
    public class AuthorizationCodes
    {
        [JsonProperty]
        public string UserCode { get; private set; }

        [JsonProperty]
        public string VerificationUrl { get; private set; }

        [JsonProperty]
        public string DeviceCode { get; private set; }

        [JsonProperty]
        public int ExpiresInSeconds { get; private set; }

        [JsonProperty]
        public int PollingInterval { get; private set; }

        public AuthorizationCodes(string userCode, string url)
        {
            UserCode = userCode;
            VerificationUrl = url;
        }

        [JsonConstructor]
        public AuthorizationCodes(string user_code, string verification_url, string device_code, int expires_in, int interval)
        {
            UserCode = user_code;
            VerificationUrl = verification_url;
            DeviceCode = device_code;
            ExpiresInSeconds = expires_in;
            PollingInterval = interval;
        }

        public override string ToString()
        {
            return $"VerificationUrl: {VerificationUrl} " + Environment.NewLine + $"UserCode: {UserCode}";
        }
    }
}
