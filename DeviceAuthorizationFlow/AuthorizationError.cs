using Newtonsoft.Json;

namespace DeviceAuthorizationFlow
{
    internal class AuthorizationError
    {
        [JsonProperty]
        public string Name { get; private set; }

        [JsonProperty]
        public string Description { get; private set; }

        [JsonConstructor]
        public AuthorizationError(string error, string error_description)
        {
            Name = error;
            Description = error_description;
        }

        public override string ToString()
        {
            return $"Error Response from server: {Name} with description: {Description}";
        }
    }
}
