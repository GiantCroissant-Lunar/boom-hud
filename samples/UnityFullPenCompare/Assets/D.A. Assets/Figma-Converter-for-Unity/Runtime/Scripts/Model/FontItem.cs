#if JSONNET_PLASTIC_EXISTS
using Unity.Plastic.Newtonsoft.Json;
#elif JSONNET_EXISTS
using Newtonsoft.Json;
#endif


using System.Collections.Generic;

namespace DA_Assets.FCU
{
    public struct FontItem
    {
#if JSONNET_EXISTS || JSONNET_PLASTIC_EXISTS
        [JsonProperty("family")]
#endif
        public string Family { get; set; }
#if JSONNET_EXISTS || JSONNET_PLASTIC_EXISTS
        [JsonProperty("variants")]
#endif
        public List<string> Variants { get; set; }
#if JSONNET_EXISTS || JSONNET_PLASTIC_EXISTS
        [JsonProperty("subsets")]
#endif
        public List<string> Subsets { get; set; }
#if JSONNET_EXISTS || JSONNET_PLASTIC_EXISTS
        [JsonProperty("version")]
#endif
        public string Version { get; set; }
#if JSONNET_EXISTS || JSONNET_PLASTIC_EXISTS
        [JsonProperty("lastModified")]
#endif
        public string LastModified { get; set; }
#if JSONNET_EXISTS || JSONNET_PLASTIC_EXISTS
        [JsonProperty("files")]
#endif
        public Dictionary<string, string> Files { get; set; }
#if JSONNET_EXISTS || JSONNET_PLASTIC_EXISTS
        [JsonProperty("category")]
#endif
        public string Category { get; set; }
#if JSONNET_EXISTS || JSONNET_PLASTIC_EXISTS
        [JsonProperty("kind")]
#endif
        public string Kind { get; set; }
#if JSONNET_EXISTS || JSONNET_PLASTIC_EXISTS
        [JsonProperty("menu")]
#endif
        public string Menu { get; set; }
    }
}
