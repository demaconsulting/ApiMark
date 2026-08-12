namespace SampleLib;

/// <summary>A sample class used as a fixture for ApiMark.MSBuild enforce-docs package integration tests.</summary>
public class SampleClass
{
    /// <summary>Gets the name of this sample instance.</summary>
    public string Name { get; } = "sample";

    // Intentionally undocumented public member - exercises --enforce-docs at the Public tier
    // so the package test can prove the ApiMarkEnforceDocs/ApiMarkEnforceDocsSeverity MSBuild
    // properties actually reach ApiMark.Tool through the .targets file.
    public string Undocumented { get; } = "undocumented";
}
