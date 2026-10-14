// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

namespace ApiMark.DotNet.Fixtures;

/// <summary>A fixture class whose example nests <c>lang</c>-tagged code blocks inside paragraphs.</summary>
/// <example>
/// <para>
/// Configure it in XML:
/// <code lang="xml">
/// &lt;setting name="y" /&gt;
/// </code>
/// </para>
/// <para>Then run it from a shell.</para>
/// <code lang="powershell">
/// Get-Item .
/// </code>
/// </example>
public class LangParaExampleClass
{
    /// <summary>Documents a member whose example has no code element at all.</summary>
    /// <example>
    /// var plain = 1;
    /// </example>
    public void NoCodeElement()
    {
    }
}
