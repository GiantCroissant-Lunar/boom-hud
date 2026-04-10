using System;

#if JSONNET_PLASTIC_EXISTS
using Unity.Plastic.Newtonsoft.Json;
#elif JSONNET_EXISTS
using Newtonsoft.Json;
#endif

namespace DA_Assets.FCU.Model
{
    [Serializable]
    public struct FigmaUser
    {
#if JSONNET_EXISTS || JSONNET_PLASTIC_EXISTS
        [JsonProperty("id")]
#endif
        public string Id { get; set; }
#if JSONNET_EXISTS || JSONNET_PLASTIC_EXISTS
        [JsonProperty("handle")]
#endif
        public string Name { get; set; }
#if JSONNET_EXISTS || JSONNET_PLASTIC_EXISTS
        [JsonProperty("email")]
#endif
        public string Email { get; set; }
#if JSONNET_EXISTS || JSONNET_PLASTIC_EXISTS
        [JsonProperty("img_url")]
#endif
        public string ImgUrl { get; set; }
    }
}