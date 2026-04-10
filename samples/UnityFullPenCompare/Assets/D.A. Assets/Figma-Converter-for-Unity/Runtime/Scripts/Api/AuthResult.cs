#if JSONNET_PLASTIC_EXISTS
using Unity.Plastic.Newtonsoft.Json;
#elif JSONNET_EXISTS
using Newtonsoft.Json;
#endif

namespace DA_Assets.FCU.Model
{
    public struct AuthResult
    {
#if JSONNET_EXISTS || JSONNET_PLASTIC_EXISTS
        [JsonProperty("access_token")]
#endif
        public string AccessToken { get; set; }
#if JSONNET_EXISTS || JSONNET_PLASTIC_EXISTS
        [JsonProperty("expires_in")]
#endif
        public string ExpiresIn { get; set; }
#if JSONNET_EXISTS || JSONNET_PLASTIC_EXISTS
        [JsonProperty("refresh_token")]
#endif
        public string RefreshToken { get; set; }
    }
}