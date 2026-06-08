// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

namespace ApiMark.Cpp;

/// <summary>
///     Represents a non-standard external C++ type reference found in the documentation.
/// </summary>
/// <remarks>
///     Instances are collected per generated Markdown file and emitted in the
///     "External Types" section so that readers know which additional headers or
///     libraries they must include to use the documented API. Types in the <c>std::</c>
///     namespace and C++ primitives are excluded; only types with an explicit namespace
///     that is not <c>std</c> are recorded here.
/// </remarks>
/// <param name="TypeString">The type name as it appears in the documentation table.</param>
/// <param name="Namespace">The C++ namespace that declares the type, using <c>::</c> separators.</param>
internal sealed record CppExternalTypeInfo(string TypeString, string Namespace)
    : IComparable<CppExternalTypeInfo>
{
    /// <summary>
    ///     Compares this instance to <paramref name="other"/> by type string,
    ///     enabling deterministic alphabetical sorting of the External Types table.
    /// </summary>
    /// <param name="other">The other instance to compare against, or <see langword="null"/>.</param>
    /// <returns>
    ///     A negative value when this instance sorts before <paramref name="other"/>,
    ///     zero when equal, or a positive value when it sorts after.
    /// </returns>
    public int CompareTo(CppExternalTypeInfo? other) =>
        StringComparer.Ordinal.Compare(TypeString, other?.TypeString);

    /// <summary>Returns <see langword="true"/> when <paramref name="left"/> sorts before <paramref name="right"/>.</summary>
    public static bool operator <(CppExternalTypeInfo left, CppExternalTypeInfo right) =>
        left.CompareTo(right) < 0;

    /// <summary>Returns <see langword="true"/> when <paramref name="left"/> sorts before or equal to <paramref name="right"/>.</summary>
    public static bool operator <=(CppExternalTypeInfo left, CppExternalTypeInfo right) =>
        left.CompareTo(right) <= 0;

    /// <summary>Returns <see langword="true"/> when <paramref name="left"/> sorts after <paramref name="right"/>.</summary>
    public static bool operator >(CppExternalTypeInfo left, CppExternalTypeInfo right) =>
        left.CompareTo(right) > 0;

    /// <summary>Returns <see langword="true"/> when <paramref name="left"/> sorts after or equal to <paramref name="right"/>.</summary>
    public static bool operator >=(CppExternalTypeInfo left, CppExternalTypeInfo right) =>
        left.CompareTo(right) >= 0;
}
