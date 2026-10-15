// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

using ApiMark.Core;
using ApiMark.Core.TestHelpers;
using ApiMark.DotNet;
using Xunit;

namespace ApiMark.DotNet.Tests;

/// <summary>
///     Exact-output tests proving types from referenced assemblies appear with short C# names
///     (never namespace-qualified) in every signature position of gradual-disclosure pages.
/// </summary>
public class DotNetEmitterExternalSignatureTests
{
    /// <summary>The fixture page folder of the type under test.</summary>
    private const string TypeFolder = "ApiMark.DotNet.Fixtures/ExternalSignatureShapes-1";

    /// <summary>Emits the fixture assembly with the external assembly as a reference.</summary>
    /// <returns>The populated factory.</returns>
    private static InMemoryMarkdownWriterFactory Emit()
    {
        var options = new DotNetGeneratorOptions
        {
            AssemblyPath = FixturePaths.GetFixtureDll(),
            XmlDocPath = FixturePaths.GetFixtureXmlDoc(),
            Visibility = ApiVisibility.PublicAndProtected,
            ReferencePaths = [FixturePaths.GetExternalFixtureDll()],
        };
        var factory = new InMemoryMarkdownWriterFactory();
        new DotNetGenerator(options).Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());
        return factory;
    }

    /// <summary>Returns the code blocks of a page.</summary>
    /// <param name="factory">The factory.</param>
    /// <param name="key">The page key.</param>
    /// <returns>The code block texts.</returns>
    private static List<string> CodeBlocks(InMemoryMarkdownWriterFactory factory, string key) =>
        factory.Writers[key].Operations.OfType<SignatureOperation>().Select(c => c.Code).ToList();

    /// <summary>Validates the type signature uses short names for external base, interface and constraint types.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_TypeSignature_UsesShortExternalNames()
    {
        // Arrange / Act
        var blocks = CodeBlocks(Emit(), TypeFolder.Replace("x", "x"));

        // Assert
        var signature = Assert.Single(blocks);
        Assert.Equal(
            "public class ExternalSignatureShapes<T> : ExternalBaseClass, IExternalBaseInterface where T : ExternalBaseClass, new()",
            signature.Replace("\r\n", "\n"));
    }

    /// <summary>Validates member signatures use short external names in parameters, returns, events, fields and constraints.</summary>
    /// <param name="member">The member page name.</param>
    /// <param name="expected">The exact expected signature.</param>
    [Theory]
    [InlineData("Echo", "public ExternalBaseClass? Echo<TItem>(ExternalBaseClass? value, ExternalGenericFormat<TItem> format) where TItem : IExternalBaseInterface")]
    [InlineData("Changed", "public event EventHandler<ExternalGenericFormat<string>>? Changed")]
    [InlineData("Partner", "public ExternalBaseClass? Partner { get; set; }")]
    [InlineData("Format", "public ExternalGenericFormat<ExternalBaseClass>? Format")]
    public void DotNetEmitterGradualDisclosure_Emit_MemberSignatures_UseShortExternalNames(string member, string expected)
    {
        // Arrange / Act
        var blocks = CodeBlocks(Emit(), $"{TypeFolder}/{member}");

        // Assert
        var signature = Assert.Single(blocks);
        Assert.Equal(expected, signature.Replace("\r\n", "\n").Trim());
    }

    /// <summary>Validates no signature anywhere carries the external namespace.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_NoSignatureContainsExternalNamespace()
    {
        // Arrange / Act
        var factory = Emit();

        // Assert
        var offenders = factory.Writers
            .Where(w => w.Key.StartsWith(TypeFolder, StringComparison.Ordinal))
            .SelectMany(w => w.Value.Operations.OfType<SignatureOperation>().Select(c => $"{w.Key}: {c.Code}"))
            .Where(s => s.Contains("ApiMark.DotNet.Fixtures.External", StringComparison.Ordinal))
            .ToList();
        Assert.Empty(offenders);
    }
}
