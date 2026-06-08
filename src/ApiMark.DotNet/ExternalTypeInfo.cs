// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

namespace ApiMark.DotNet;

/// <summary>
///     Represents a non-standard external type reference found in the documentation.
/// </summary>
/// <remarks>
///     Instances are collected per generated Markdown file and emitted in the
///     "External Types" section so that readers know which additional packages or
///     headers they must include to use the documented API. System-namespace types
///     and C# primitives are excluded; only types whose namespace does not start
///     with "System" and are not CLR value-type primitives are recorded here.
/// </remarks>
/// <param name="SimplifiedName">The simplified type name as it appears in the documentation table.</param>
/// <param name="Namespace">The .NET namespace that declares the type.</param>
internal sealed record ExternalTypeInfo(string SimplifiedName, string Namespace)
    : IComparable<ExternalTypeInfo>
{
    /// <summary>
    ///     Compares this instance to <paramref name="other"/> by simplified name,
    ///     enabling deterministic alphabetical sorting of the External Types table.
    /// </summary>
    /// <param name="other">The other instance to compare against, or <see langword="null"/>.</param>
    /// <returns>
    ///     A negative value when this instance sorts before <paramref name="other"/>,
    ///     zero when equal, or a positive value when it sorts after.
    /// </returns>
    public int CompareTo(ExternalTypeInfo? other) =>
        StringComparer.Ordinal.Compare(SimplifiedName, other?.SimplifiedName);

    /// <summary>Returns <see langword="true"/> when <paramref name="left"/> sorts before <paramref name="right"/>.</summary>
    public static bool operator <(ExternalTypeInfo left, ExternalTypeInfo right) =>
        left.CompareTo(right) < 0;

    /// <summary>Returns <see langword="true"/> when <paramref name="left"/> sorts before or equal to <paramref name="right"/>.</summary>
    public static bool operator <=(ExternalTypeInfo left, ExternalTypeInfo right) =>
        left.CompareTo(right) <= 0;

    /// <summary>Returns <see langword="true"/> when <paramref name="left"/> sorts after <paramref name="right"/>.</summary>
    public static bool operator >(ExternalTypeInfo left, ExternalTypeInfo right) =>
        left.CompareTo(right) > 0;

    /// <summary>Returns <see langword="true"/> when <paramref name="left"/> sorts after or equal to <paramref name="right"/>.</summary>
    public static bool operator >=(ExternalTypeInfo left, ExternalTypeInfo right) =>
        left.CompareTo(right) >= 0;
}
