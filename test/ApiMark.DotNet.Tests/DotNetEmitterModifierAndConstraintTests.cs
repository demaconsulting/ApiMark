// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

using ApiMark.Core;
using ApiMark.Core.TestHelpers;
using ApiMark.DotNet;
using Xunit;

namespace ApiMark.DotNet.Tests;

/// <summary>
///     Exact-output tests for the heuristically recovered <c>new</c> modifier (all member kinds)
///     and the recoverable generic constraints (<c>notnull</c>, <c>unmanaged</c>, <c>struct</c>,
///     <c>class</c>, <c>class?</c>, <c>new()</c>).
/// </summary>
public class DotNetEmitterModifierAndConstraintTests
{
    /// <summary>The page folder of the fixture namespace.</summary>
    private const string Folder = "ApiMark.DotNet.Fixtures/Modifiers";

    /// <summary>Emits the fixture assembly at the <see cref="ApiVisibility.All"/> tier.</summary>
    /// <returns>The populated factory.</returns>
    private static InMemoryMarkdownWriterFactory Emit()
    {
        var options = new DotNetGeneratorOptions
        {
            AssemblyPath = FixturePaths.GetFixtureDll(),
            XmlDocPath = FixturePaths.GetFixtureXmlDoc(),
            Visibility = ApiVisibility.All,
        };
        var factory = new InMemoryMarkdownWriterFactory();
        new DotNetGenerator(options).Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());
        return factory;
    }

    /// <summary>Returns the single signature of a page.</summary>
    /// <param name="factory">The factory.</param>
    /// <param name="key">The page key.</param>
    /// <returns>The signature with single-line formatting.</returns>
    private static string Signature(InMemoryMarkdownWriterFactory factory, string key)
    {
        var page = factory.Writers.TryGetValue(key, out var writer) ? writer : null;
        Assert.True(page != null, $"Missing page '{key}'");
        return page!.Operations.OfType<SignatureOperation>().Single().Code.Replace("\r\n", "\n").Replace('\n', ' ');
    }

    /// <summary>Validates <c>new</c> is rendered exactly where a member hides an accessible base member.</summary>
    /// <param name="member">The member page name.</param>
    /// <param name="expected">The exact expected signature.</param>
    [Theory]
    [InlineData("Konst", "public new const int Konst = 2")]
    [InlineData("Value", "public new int Value")]
    [InlineData("Prop", "public new string Prop")]
    [InlineData("Run", "public new int Run")]
    [InlineData("Evt", "public new int Evt")]
    [InlineData("Prot", "public new static int Prot")]
    [InlineData("Inner", "public new int Inner")]
    [InlineData("hidden", "public int hidden")]
    [InlineData("FieldHiddenByMethod", "public new void FieldHiddenByMethod()")]
    [InlineData("MethodHiddenByProperty", "public new int MethodHiddenByProperty { get; set; }")]
    [InlineData("Unrelated", "public int Unrelated")]
    public void DotNetEmitterGradualDisclosure_Emit_NewModifier_RenderedForEveryHidingMemberKind(string member, string expected)
    {
        // Arrange / Act
        var signature = Signature(Emit(), $"{Folder}/NewDerived/{member}");

        // Assert
        Assert.Equal(expected, signature);
    }

    /// <summary>Validates the base type itself never shows <c>new</c>.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_NewModifier_BaseTypeMembersHaveNone()
    {
        // Arrange / Act
        var factory = Emit();

        // Assert
        Assert.Equal("public int Value", Signature(factory, $"{Folder}/NewBase/Value"));
        Assert.Equal("public event EventHandler? Evt", Signature(factory, $"{Folder}/NewBase/Evt"));
    }

    /// <summary>Validates every recoverable generic constraint is rendered in canonical order.</summary>
    /// <param name="method">The method page name.</param>
    /// <param name="expected">The exact expected signature.</param>
    [Theory]
    [InlineData("NotNullOnly", "public void NotNullOnly<T>(T value) where T : notnull")]
    [InlineData("UnmanagedOnly", "public void UnmanagedOnly<T>(T value) where T : unmanaged")]
    [InlineData("StructOnly", "public void StructOnly<T>(T value) where T : struct")]
    [InlineData("ClassOnly", "public void ClassOnly<T>(T value) where T : class")]
    [InlineData("NullableClassOnly", "public void NullableClassOnly<T>(T value) where T : class?")]
    [InlineData("ClassAndNew", "public void ClassAndNew<T>() where T : class, new()")]
    [InlineData("NotNullAndNew", "public void NotNullAndNew<T>() where T : notnull, new()")]
    [InlineData("TwoParameters", "public void TwoParameters<TKey, TValue>() where TKey : notnull where TValue : struct")]
    [InlineData("ClassAndInterface", "public void ClassAndInterface<T>() where T : class, IDisposable")]
    [InlineData("NullableInterface", "public void NullableInterface<T>() where T : IDisposable?")]
    [InlineData("Unconstrained", "public void Unconstrained<T>(T value)")]
    public void DotNetEmitterGradualDisclosure_Emit_GenericConstraints_RenderedForEveryRecoverableShape(string method, string expected)
    {
        // Arrange / Act
        var signature = Signature(Emit(), $"{Folder}/ConstraintShapes/{method}");

        // Assert
        Assert.Equal(expected, signature);
    }
}
