// netstandard2.1 lacks IReadOnlySet<T> (added in .NET 5). Alias the read-only string-set members so
// the net8/9 public API is unchanged (IReadOnlySet<string>) while the netstandard2.1 build — the one
// Unity consumes — falls back to the wider IReadOnlyCollection<string>. No single consumer sees both
// targets, so this keeps existing net8/9 implementers (backend capability manifests) compiling as-is.
#if NETSTANDARD2_1
global using StringSet = System.Collections.Generic.IReadOnlyCollection<string>;
#else
global using StringSet = System.Collections.Generic.IReadOnlySet<string>;
#endif
