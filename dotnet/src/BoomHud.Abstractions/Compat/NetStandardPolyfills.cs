#if NETSTANDARD2_1
// Minimal polyfills so BoomHud.Abstractions can also compile for netstandard2.1 (Unity scripting
// runtime). Guarded to NETSTANDARD2_1 only; net8.0/net9.0 use the in-box framework types.
//
// Notes:
//   * IsExternalInit is already provided by the plate-shared include in Directory.Build.props.
//   * System.Index / System.Range already exist in netstandard2.1, so they are NOT polyfilled here.
//   * DateOnly / TimeOnly are net6+. They are only referenced by generated JSON converters in the
//     Motion DTO (dead weight on this target — Unity never serializes Motion). These compile-only
//     shims give the converters something to bind against.

using System.Globalization;

namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = false)]
    internal sealed class RequiredMemberAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = false)]
    internal sealed class CompilerFeatureRequiredAttribute : Attribute
    {
        public CompilerFeatureRequiredAttribute(string featureName) => FeatureName = featureName;

        public string FeatureName { get; }

        public bool IsOptional { get; init; }

        public const string RefStructs = nameof(RefStructs);
        public const string RequiredMembers = nameof(RequiredMembers);
    }
}

namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(AttributeTargets.Constructor, AllowMultiple = false, Inherited = false)]
    internal sealed class SetsRequiredMembersAttribute : Attribute
    {
    }
}

namespace System
{
    public readonly struct DateOnly
    {
        private readonly DateTime _value;

        private DateOnly(DateTime value) => _value = value.Date;

        public static DateOnly Parse(string s) => new DateOnly(DateTime.Parse(s, CultureInfo.InvariantCulture));

        public string ToString(string? format)
            => _value.ToString(format ?? "yyyy-MM-dd", CultureInfo.InvariantCulture);

        public override string ToString() => ToString("yyyy-MM-dd");
    }

    public readonly struct TimeOnly
    {
        private readonly DateTime _value;

        private TimeOnly(DateTime value) => _value = value;

        public static TimeOnly Parse(string s) => new TimeOnly(DateTime.Parse(s, CultureInfo.InvariantCulture));

        public string ToString(string? format)
            => _value.ToString(format ?? "HH:mm:ss.fff", CultureInfo.InvariantCulture);

        public override string ToString() => ToString("HH:mm:ss.fff");
    }
}
#endif
