#if JSONNET_PLASTIC_EXISTS
using Unity.Plastic.Newtonsoft.Json;
#elif JSONNET_EXISTS
using Newtonsoft.Json;
#endif

using System.Collections.Generic;

namespace DA_Assets.FCU
{
    public struct FontRoot
    {
#if JSONNET_EXISTS || JSONNET_PLASTIC_EXISTS
        [JsonProperty("kind")]
#endif
        public string Kind { get; set; }
#if JSONNET_EXISTS || JSONNET_PLASTIC_EXISTS
        [JsonProperty("items")]
#endif
        public List<FontItem> Items { get; set; }
    }
}

