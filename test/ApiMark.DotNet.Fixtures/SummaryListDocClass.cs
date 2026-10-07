// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

namespace ApiMark.DotNet.Fixtures;

/// <summary>
/// A fixture class for testing bullet list rendering inside a <c>&lt;summary&gt;</c> element.
/// <list type="bullet">
/// <item><description>First summary bullet item.</description></item>
/// <item><description>Second summary bullet item.</description></item>
/// <item><description>Third summary bullet item.</description></item>
/// </list>
/// Trailing summary prose after the list.
/// </summary>
public static class SummaryBulletListDocClass
{
}

/// <summary>
/// A fixture class for testing numbered list rendering inside a <c>&lt;summary&gt;</c> element.
/// <list type="number">
/// <item><description>First summary numbered item.</description></item>
/// <item><description>Second summary numbered item.</description></item>
/// <item><description>Third summary numbered item.</description></item>
/// </list>
/// Trailing summary prose after the list.
/// </summary>
public static class SummaryNumberListDocClass
{
    /// <summary>
    /// A fixture method for testing numbered list rendering inside a member's
    /// <c>&lt;summary&gt;</c> element.
    /// <list type="number">
    /// <item><description>First member numbered item.</description></item>
    /// <item><description>Second member numbered item.</description></item>
    /// <item><description>Third member numbered item.</description></item>
    /// </list>
    /// Trailing member summary prose after the list.
    /// </summary>
    public static void DoNumberedWork()
    {
    }
}

/// <summary>
/// A fixture class for testing table list rendering inside a <c>&lt;summary&gt;</c> element.
/// <list type="table">
/// <listheader>
/// <term>Format</term>
/// <description>Behavior</description>
/// </listheader>
/// <item>
/// <term>SingleFile</term>
/// <description>Writes all output to a single <c>api.md</c> file.</description>
/// </item>
/// <item>
/// <term>GradualDisclosure</term>
/// <description>Writes one page per namespace and type.</description>
/// </item>
/// </list>
/// Trailing summary prose after the list.
/// </summary>
public static class SummaryTableListDocClass
{
}
