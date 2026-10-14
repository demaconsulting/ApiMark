// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

namespace ApiMark.DotNet.Fixtures;

/// <summary>A fixture class whose examples use the optional <c>lang</c> attribute on code blocks.</summary>
public class LangExampleClass
{
    /// <summary>Shows a configuration snippet.</summary>
    /// <param name="value">The value.</param>
    /// <example>
    /// Configure it in XML:
    /// <code lang="xml">
    /// &lt;setting name="x" /&gt;
    /// </code>
    /// Or in a shell:
    /// <code lang="powershell">
    /// Get-Item .
    /// </code>
    /// Or with an invalid language that must fall back:
    /// <code lang="bad lang!">
    /// plain
    /// </code>
    /// Or with no language:
    /// <code>
    /// var x = 1;
    /// </code>
    /// </example>
    public void Configure(int value)
    {
    }
}
