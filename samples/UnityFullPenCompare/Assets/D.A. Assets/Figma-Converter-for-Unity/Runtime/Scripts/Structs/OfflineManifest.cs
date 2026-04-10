using System;

namespace DA_Assets.FCU.Model
{
    [Serializable]
    public struct OfflineManifest
    {
        public float ExportScale;
        public string ImageFormat;
        public bool IsValid => ExportScale > 0f && !string.IsNullOrWhiteSpace(ImageFormat);
    }
}