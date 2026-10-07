// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

// [GeneratedRegex] requires the regex source generator, which targets the modern
// System.Text.RegularExpressions.Generator APIs unavailable on netstandard2.0.
#if !NETSTANDARD2_0
using System.Text.RegularExpressions;

namespace ApiMark.DotNet.Fixtures;

/// <summary>
///     A class declaring a <c>[GeneratedRegex]</c> partial method, used to verify
///     <c>--enforce-docs</c> accepts the method's <c>&lt;remarks&gt;</c> as documentation. The
///     regex source generator attaches its own <c>&lt;remarks&gt;</c> (describing the compiled
///     pattern) to the generated partial-method implementation, which silently replaces the
///     <c>&lt;summary&gt;</c> written here on the defining partial declaration in the compiled
///     XML documentation — so the method ends up with only a <c>&lt;remarks&gt;</c> at runtime.
/// </summary>
public sealed partial class GeneratedRegexClass
{
    /// <summary>Matches a sequence of one or more digits.</summary>
    [GeneratedRegex(@"\d+")]
    public static partial Regex DigitsRegex();
}
#endif
