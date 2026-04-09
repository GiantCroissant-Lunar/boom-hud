using System;
using UnityEngine.Playables;

namespace BoomHud.Unity.Timeline
{
    [Serializable]
    public sealed class BoomHudMotionClipBehaviour : PlayableBehaviour
    {
        public string ClipId = "intro";

        public double TimeOffsetSeconds;
    }
}
