using System.Runtime.InteropServices;

namespace ApiMark.DotNet;

/// <summary>
///     Provides the platform-aware <see cref="StringComparer"/> used whenever two file-system
///     paths need to be compared or deduplicated within <c>ApiMark.DotNet</c>.
/// </summary>
/// <remarks>
///     Both <see cref="DotNetGenerator"/> (deduplicating reference-path search directories before
///     seeding a Mono.Cecil <c>DefaultAssemblyResolver</c>) and <see cref="ExternalXmlDocResolver"/>
///     (keying its per-reference-path documentation cache) need the same case-sensitivity rule for
///     path comparisons. Extracting the rule here avoids the two classes drifting out of sync and
///     avoids unconditionally using <see cref="StringComparer.OrdinalIgnoreCase"/>, which would
///     incorrectly deduplicate or conflate two genuinely distinct paths on a case-sensitive file
///     system (e.g. <c>/tmp/Lib</c> and <c>/tmp/lib</c> on Linux). This mirrors the equivalent
///     helper in <c>ApiMark.Cpp.CppEmitter</c>; the logic is duplicated there rather than shared
///     because <c>ApiMark.DotNet</c> does not reference <c>ApiMark.Cpp</c> and introducing a
///     cross-project/shared-assembly dependency purely for this one small helper would be a larger
///     change than the bug fix it supports.
/// </remarks>
internal static class FileSystemPathComparer
{
    /// <summary>
    ///     Returns the <see cref="StringComparer"/> appropriate for file-system path comparisons
    ///     on the current platform.
    /// </summary>
    /// <remarks>
    ///     Linux file systems are case-sensitive, so <see cref="StringComparer.Ordinal"/> is used
    ///     there to avoid incorrectly treating paths that differ only in case as duplicates.
    ///     Windows and macOS default to case-insensitive file systems, so
    ///     <see cref="StringComparer.OrdinalIgnoreCase"/> is used on those platforms.
    /// </remarks>
    public static StringComparer Comparer =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            ? StringComparer.Ordinal
            : StringComparer.OrdinalIgnoreCase;
}
