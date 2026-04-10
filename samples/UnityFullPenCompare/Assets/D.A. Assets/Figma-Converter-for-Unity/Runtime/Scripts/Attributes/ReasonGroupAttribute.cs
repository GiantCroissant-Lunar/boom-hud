using System;

namespace DA_Assets.FCU
{
    [AttributeUsage(AttributeTargets.Field)]
    public class ReasonGroupAttribute : Attribute
    {
        public string Group { get; }
        public ReasonGroupAttribute(string group) => Group = group;
    }
}
