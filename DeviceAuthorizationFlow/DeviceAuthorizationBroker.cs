using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Util.Store;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DeviceAuthorizationFlow
{
    public class DeviceAuthorizationBroker : IDisposable
    {
        private HttpClient client;
        private AuthorizationCodes codes;
        private readonly ClientSecrets secrets;
        private IEnumerable<string> scopes;

        public DeviceAuthorizationBroker(string clientSecretsJsonFileName)
        {
            client = new HttpClient();
            secrets = ReadSecretsFromFile(clientSecretsJsonFileName);
            codes = null;
        }

        public async Task<UserCredential> TryUsingStoredAuthenticationInfos(IDataStore store)
        {
            UserCredential credential = null;

            GoogleAuthorizationCodeFlow.Initializer initializer = new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = secrets,
                DataStore = store
            };
            GoogleAuthorizationCodeFlow flow = new GoogleAuthorizationCodeFlow(initializer);

            TokenResponse token = await flow.LoadTokenAsync(Environment.UserName, CancellationToken.None);

            if (token != null)
            {
                token = await RefreshTokenIfRequired(flow, token);

                credential = new UserCredential(flow, Environment.UserName, token);
            }

            return credential;
        }

        private static async Task<TokenResponse> RefreshTokenIfRequired(GoogleAuthorizationCodeFlow flow, TokenResponse token)
        {
            if (token.IsExpired(flow.Clock))
            {
                token = await flow.RefreshTokenAsync(
                    Environment.UserName,
                    token.RefreshToken,
                    CancellationToken.None);
            }

            return token;
        }

        public async Task<AuthorizationCodes> RequestCodesAsync(IEnumerable<string> scopes)
        {
            this.scopes = scopes;

            string requestedScopes = string.Join(" ", scopes);
            string json = await RequestDeviceAndUserCodesAsync(secrets.ClientId, requestedScopes);
            codes = ExtractCodes(json);

            return codes;
        }

        public async Task<UserCredential> WaitForAuthorizedCredentialsAsync(IDataStore store)
        {
            UserCredential credentials = null;

            AuthorizationTokens tokens = await PollForAuthorizationTokens();
            if (await IsAccessTokenValidAsync(tokens))
            {
                var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
                {
                    ClientSecrets = secrets,
                    Scopes = scopes,
                    DataStore = store
                });

                var token = new TokenResponse
                {
                    AccessToken = tokens.AccessToken,
                    RefreshToken = tokens.RefreshToken
                };

                credentials = new UserCredential(flow, Environment.UserName, token);
            }

            return credentials;
        }

        private async Task<bool> IsAccessTokenValidAsync(AuthorizationTokens tokens)
        {
            bool result = false;

            string validatedTokenDescription = await RequestTokenDescription(tokens.AccessToken);
            Console.WriteLine($"Validated Token decsription: {validatedTokenDescription}");

            string authorizedId = JObject.Parse(validatedTokenDescription)["aud"].ToString();
            string authorizedScope = JObject.Parse(validatedTokenDescription)["scope"].ToString();
            if (authorizedId.Equals(secrets.ClientId) && authorizedScope.Equals(string.Join(" ", scopes)))
            {
                result = true;
            }

            return result;
        }

        private async Task<AuthorizationTokens> PollForAuthorizationTokens()
        {
            AuthorizationTokens token = null;
            int elapsedTime = 0;

            while (elapsedTime < codes.ExpiresInSeconds)
            {
                await Task.Delay(TimeSpan.FromSeconds(codes.PollingInterval));
                string json = await RequestUserAuthorizationState(secrets.ClientId, secrets.ClientSecret);
                if (IsErrorResponse(json))
                {
                    AuthorizationError err = JsonConvert.DeserializeObject<AuthorizationError>(json);
                    Console.WriteLine($"Error-Response: {err}");
                }
                else
                {
                    token = JsonConvert.DeserializeObject<AuthorizationTokens>(json);
                    break;
                }

                // TODO: JS, Muss diese Berechnung genauer sein?
                // diese Berechnung ist nicht genau, weil wir die Zeit warten und dann noch
                // die Zeit verbrauchen für die Anfrage zum Server.
                elapsedTime += codes.PollingInterval;
            }

            return token;
        }

        private AuthorizationCodes ExtractCodes(string jsonString)
        {
            return JsonConvert.DeserializeObject<AuthorizationCodes>(jsonString);
        }

        private async Task<string> RequestTokenDescription(string accessToken)
        {
            var requestParameters = new List<KeyValuePair<string, string>>()
            {
                new KeyValuePair<string, string>("access_token", accessToken)
            };

            HttpResponseMessage response = await SendRequestAsync(requestParameters, "https://www.googleapis.com/oauth2/v3/tokeninfo");
            return await ExtractResponseAsync(response);
        }

        private async Task<string> RequestDeviceAndUserCodesAsync(string clientId, string scope)
        {
            var requestParameters = new List<KeyValuePair<string, string>>()
            {
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("scope", scope)
            };

            HttpResponseMessage response = await SendRequestAsync(requestParameters, "https://accounts.google.com/o/oauth2/device/code");
            return await ExtractResponseAsync(response);
        }

        private async Task<string> RequestUserAuthorizationState(string clientId, string clientSecret)
        {
            var requestParameters = new List<KeyValuePair<string, string>>()
            {
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("client_secret", clientSecret),
                new KeyValuePair<string, string>("code", codes.DeviceCode),
                new KeyValuePair<string, string>("grant_type", "http://oauth.net/grant_type/device/1.0")
            };

            HttpResponseMessage response = await SendRequestAsync(requestParameters, "https://www.googleapis.com/oauth2/v4/token");
            
            // even an error response is a success in this case!
            return await response.Content.ReadAsStringAsync();
        }

        private async Task<string> RequestRefreshedAccessToken(string refreshToken, string clientId, string clientSecret)
        {
            var requestParameters = new List<KeyValuePair<string, string>>()
            {
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("client_secret", clientSecret),
                new KeyValuePair<string, string>("refresh_token", refreshToken),
                new KeyValuePair<string, string>("grant_type", "refresh_token")
            };

            HttpResponseMessage response = await SendRequestAsync(requestParameters, "https://www.googleapis.com/oauth2/v4/token");

            return await ExtractResponseAsync(response);
        }

        private static async Task<string> ExtractResponseAsync(HttpResponseMessage response)
        {
            string jsonString = string.Empty;

            if (response.IsSuccessStatusCode)
            {
                jsonString = await response.Content.ReadAsStringAsync();
            }
            else
            {
                throw new System.Exception($"Request did not suceed: {response.ToString()}");
            }

            return jsonString;
        }

        private async Task<HttpResponseMessage> SendRequestAsync(IEnumerable<KeyValuePair<string, string>> requestParameters, string url)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new FormUrlEncodedContent(requestParameters)
            };

            return await client.SendAsync(req);
        }

        private bool IsErrorResponse(string json)
        {
            return JObject.Parse(json)["error"] != null;
        }

        private static ClientSecrets ReadSecretsFromFile(string fileNameAndPath)
        {
            string secretsString = File.ReadAllText(fileNameAndPath);
            JObject o = JObject.Parse(secretsString);
            string id = o.SelectToken("installed.client_id").ToString();
            string secret = o.SelectToken("installed.client_secret").ToString();

            ClientSecrets secrets = new ClientSecrets() { ClientId = id, ClientSecret = secret };
            return secrets;
        }

        public void Dispose()
        {
            client?.Dispose();
        }
    }
}
