namespace ApiMark.DotNet;

/// <summary>Configuration options for <see cref="DotNetGenerator"/>.</summary>
public sealed class DotNetGeneratorOptions
{
    /// <summary>Gets or sets the path to the .NET assembly to document.</summary>
    public string AssemblyPath { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the path to the XML documentation file produced alongside the assembly.
    ///     <see cref="DotNetGenerator"/> throws <see cref="FileNotFoundException"/> if this file does not exist.
    /// </summary>
    public string XmlDocPath { get; set; } = string.Empty;

    /// <summary>Gets or sets which members are visible in the generated output. Defaults to <see cref="ApiVisibility.Public"/>.</summary>
    public ApiVisibility Visibility { get; set; } = ApiVisibility.Public;

    /// <summary>Gets or sets a value indicating whether obsolete members are included. Defaults to <c>false</c>.</summary>
    public bool IncludeObsolete { get; set; }
}
