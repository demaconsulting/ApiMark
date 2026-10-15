// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

using ApiMark.Core;
using ApiMark.Core.TestHelpers;
using ApiMark.DotNet;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Xunit;

namespace ApiMark.DotNet.Tests;

/// <summary>Unit tests for <see cref="DotNetEmitter"/>.</summary>
public class DotNetEmitterTests
{
    /// <summary>Builds DotNetGeneratorOptions pointing at the fixture assembly.</summary>
    private static DotNetGeneratorOptions BuildOptions() => new()
    {
        AssemblyPath = FixturePaths.GetFixtureDll(),
        XmlDocPath = FixturePaths.GetFixtureXmlDoc(),
        Visibility = ApiVisibility.Public,
    };

    /// <summary>Validates that passing null to <see cref="DotNetEmitter.Emit"/> throws <see cref="ArgumentNullException"/>.</summary>
    [Fact]
    public void DotNetEmitter_Emit_NullFactory_ThrowsArgumentNullException()
    {
        // Arrange
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => emitter.Emit(null!, new EmitConfig(), new InMemoryContext()));
    }

    /// <summary>Validates that passing a null config to <see cref="DotNetEmitter.Emit"/> throws <see cref="ArgumentNullException"/>.</summary>
    [Fact]
    public void DotNetEmitter_Emit_NullConfig_ThrowsArgumentNullException()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => emitter.Emit(factory, null!, new InMemoryContext()));
    }

    /// <summary>Validates that passing a null context to <see cref="DotNetEmitter.Emit"/> throws <see cref="ArgumentNullException"/>.</summary>
    [Fact]
    public void DotNetEmitter_Emit_NullContext_ThrowsArgumentNullException()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => emitter.Emit(factory, new EmitConfig(), null!));
    }

    /// <summary>Validates that <see cref="OutputFormat.GradualDisclosure"/> produces more than one writer.</summary>
    [Fact]
    public void DotNetEmitter_Emit_GradualDisclosureFormat_ProducesMultipleFiles()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        emitter.Emit(factory, new EmitConfig { Format = OutputFormat.GradualDisclosure }, new InMemoryContext());

        // Assert
        Assert.True(factory.Writers.Count > 1, "GradualDisclosure format must produce more than one writer");
    }

    /// <summary>Validates that <see cref="OutputFormat.SingleFile"/> produces exactly one writer keyed as "api".</summary>
    [Fact]
    public void DotNetEmitter_Emit_SingleFileFormat_ProducesSingleApiFile()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        emitter.Emit(factory, new EmitConfig { Format = OutputFormat.SingleFile }, new InMemoryContext());

        // Assert
        Assert.True(factory.HasWriter("", "api"), "SingleFile format must produce an api writer");
        Assert.Single(factory.Writers);
    }

    /// <summary>Validates that <see cref="DotNetEmitter.GetNamespaceFolderPath"/> returns the full dotted name for a root namespace.</summary>
    [Fact]
    public void DotNetEmitter_GetNamespaceFolderPath_RootNamespace_ReturnsDottedName()
    {
        // Arrange / Act
        var result = DotNetEmitter.GetNamespaceFolderPath("A.B", ["A.B"]);

        // Assert
        Assert.Equal("A.B", result);
    }

    /// <summary>Validates that <see cref="DotNetEmitter.GetNamespaceFolderPath"/> returns slash-separated path for a child namespace.</summary>
    [Fact]
    public void DotNetEmitter_GetNamespaceFolderPath_ChildNamespace_ReturnsSlashSeparated()
    {
        // Arrange / Act
        var result = DotNetEmitter.GetNamespaceFolderPath("A.B.C", ["A.B"]);

        // Assert
        Assert.Equal("A.B/C", result);
    }

    /// <summary>
    ///     Validates that <see cref="DotNetEmitter.ToXmlDocTypeName"/> converts a Cecil-encoded
    ///     type name — including generic instantiations, nested types, and byref parameters —
    ///     to the XML doc ID encoding.
    /// </summary>
    [Theory]
    [InlineData("System.String", "System.String")]
    [InlineData("System.String[]", "System.String[]")]
    [InlineData("System.Collections.Generic.IEnumerable`1<System.String>",
                "System.Collections.Generic.IEnumerable{System.String}")]
    [InlineData("System.Collections.Generic.IReadOnlyDictionary`2<System.String,System.Object>",
                "System.Collections.Generic.IReadOnlyDictionary{System.String,System.Object}")]
    [InlineData("System.Action`1<System.String>", "System.Action{System.String}")]
    [InlineData("Outer/Inner", "Outer.Inner")]
    [InlineData("System.String&", "System.String@")]
    [InlineData("System.Int32&", "System.Int32@")]
    [InlineData("System.Collections.Generic.IEnumerable`1<System.String>&",
                "System.Collections.Generic.IEnumerable{System.String}@")]
    public void DotNetEmitter_ToXmlDocTypeName_ConvertsCecilEncodingToXmlDocId(string cecilFullName, string expected)
    {
        // Act
        var result = DotNetEmitter.ToXmlDocTypeName(cecilFullName);

        // Assert
        Assert.Equal(expected, result);
    }

    /// <summary>Validates that <see cref="DotNetEmitter.GetNamespaceFolderPath"/> returns the full name for an unknown namespace.</summary>
    [Fact]
    public void DotNetEmitter_GetNamespaceFolderPath_UnknownNamespace_ReturnsFullName()
    {
        // Arrange / Act
        var result = DotNetEmitter.GetNamespaceFolderPath("Unknown.Namespace", ["ApiMark.DotNet"]);

        // Assert: namespace that matches no root is returned as its full dotted name
        Assert.Equal("Unknown.Namespace", result);
    }

    /// <summary>Validates generic method type parameters in signatures and display names.</summary>
    /// <param name="methodName">The method name.</param>
    /// <param name="parameterCount">The parameter count used to select the overload.</param>
    /// <param name="generic">Whether to select the generic overload.</param>
    /// <param name="expectedSignature">The expected signature.</param>
    /// <param name="expectedDisplay">The expected display name.</param>
    [Theory]
    [InlineData("Echo", 1, true, "public T Echo<T>(T item)", "Echo<T>(T)")]
    [InlineData("Over", 1, false, "public void Over(int value)", "Over(int)")]
    public void DotNetEmitter_BuildMethodSignature_GenericMethods_ShowTypeParameters(
        string methodName,
        int parameterCount,
        bool generic,
        string expectedSignature,
        string expectedDisplay)
    {
        // Arrange
        using var assembly = AssemblyDefinition.ReadAssembly(FixturePaths.GetFixtureDll());
        var type = assembly.MainModule.Types.First(t => t.Name == "GenericMethodClass");
        var method = type.Methods.Single(m =>
            m.Name == methodName && m.Parameters.Count == parameterCount && m.HasGenericParameters == generic);

        // Act
        var signature = DotNetEmitter.BuildMethodSignature(method, "ApiMark.DotNet.Fixtures");
        var display = DotNetEmitter.BuildMethodDisplayName(method);

        // Assert
        Assert.Equal(expectedSignature, signature);
        Assert.Equal(expectedDisplay, display);
        Assert.Equal(methodName, DotNetEmitter.BuildMethodFileName(method, type));
    }

    /// <summary>Validates generic method where constraints in signatures and display names.</summary>
    /// <param name="methodName">The method name.</param>
    /// <param name="parameterCount">The parameter count used to select the overload.</param>
    /// <param name="expectedSignature">The expected signature.</param>
    /// <param name="expectedDisplay">The expected display name.</param>
    [Theory]
    [InlineData("Map", 1, "public TOut Map<TIn, TOut>(TIn value) where TIn : class, IDisposable where TOut : struct", "Map<TIn, TOut>(TIn)")]
    [InlineData("Create", 0, "public static T Create<T>() where T : class, new()", "Create<T>()")]
    [InlineData("Take", 1, "public void Take<T>(T value) where T : unmanaged", "Take<T>(T)")]
    [InlineData("Over", 1, "public void Over<T>(T value) where T : IComparable<T>", "Over<T>(T)")]
    public void DotNetEmitter_BuildMethodSignature_GenericMethods_ShowConstraints(
        string methodName,
        int parameterCount,
        string expectedSignature,
        string expectedDisplay)
    {
        // Arrange
        using var assembly = AssemblyDefinition.ReadAssembly(FixturePaths.GetFixtureDll());
        var type = assembly.MainModule.Types.First(t => t.Name == "GenericMethodClass");
        var method = type.Methods.Single(m =>
            m.Name == methodName && m.Parameters.Count == parameterCount && m.HasGenericParameters);

        // Act
        var signature = DotNetEmitter.BuildMethodSignature(method, "ApiMark.DotNet.Fixtures");
        var display = DotNetEmitter.BuildMethodDisplayName(method);

        // Assert
        Assert.Equal(expectedSignature, signature);
        Assert.Equal(expectedDisplay, display);
    }

    /// <summary>Validates that source-level record and record struct keywords are rendered.</summary>
    /// <param name="typeName">The fixture type name.</param>
    /// <param name="expected">The expected signature.</param>
    [Theory]
    [InlineData("SealedRecordClass", "public sealed record SealedRecordClass")]
    [InlineData("BaseRecordClass", "public record BaseRecordClass")]
    [InlineData("DerivedRecordClass", "public record DerivedRecordClass : BaseRecordClass")]
    [InlineData("GenericRecordSample`1", "public record GenericRecordSample<T>")]
    [InlineData("RecordStructSample", "public record struct RecordStructSample")]
    [InlineData("ReadOnlyRecordStructSample", "public readonly record struct ReadOnlyRecordStructSample")]
    public void DotNetEmitter_BuildTypeSignature_RecordKinds_RenderRecordKeywords(string typeName, string expected)
    {
        // Arrange
        using var assembly = AssemblyDefinition.ReadAssembly(FixturePaths.GetFixtureDll());
        var type = assembly.MainModule.Types.First(t => t.Name == typeName);

        // Act
        var signature = DotNetEmitter.BuildTypeSignature(type, "ApiMark.DotNet.Fixtures");

        // Assert
        Assert.Equal(expected, signature);
    }

    /// <summary>Validates that readonly and ref struct modifiers are rendered.</summary>
    /// <param name="typeName">The fixture type name.</param>
    /// <param name="expected">The expected signature.</param>
    [Theory]
    [InlineData("ReadOnlyStructSample", "public readonly struct ReadOnlyStructSample")]
    [InlineData("RefStructSample", "public ref struct RefStructSample")]
    [InlineData("ReadOnlyRefStructSample", "public readonly ref struct ReadOnlyRefStructSample")]
    [InlineData("DefaultValuePoint", "public struct DefaultValuePoint")]
    public void DotNetEmitter_BuildTypeSignature_StructKinds_RenderStructModifiers(string typeName, string expected)
    {
        // Arrange
        using var assembly = AssemblyDefinition.ReadAssembly(FixturePaths.GetFixtureDll());
        var type = assembly.MainModule.Types.First(t => t.Name == typeName);

        // Act
        var signature = DotNetEmitter.BuildTypeSignature(type, "ApiMark.DotNet.Fixtures");

        // Assert
        Assert.Equal(expected, signature);
    }

    /// <summary>
    ///     Validates that <see cref="DotNetEmitter.BuildTypeSignature"/> includes the <c>abstract</c>
    ///     modifier for an abstract class that is not sealed.
    /// </summary>
    [Fact]
    public void DotNetEmitter_BuildTypeSignature_AbstractClass_ContainsAbstractModifier()
    {
        // Arrange: load the abstract fixture class from the fixture assembly
        using var assembly = AssemblyDefinition.ReadAssembly(FixturePaths.GetFixtureDll());
        var type = assembly.MainModule.Types.First(t => t.Name == "AbstractFixtureClass");

        // Act
        var signature = DotNetEmitter.BuildTypeSignature(type, "ApiMark.DotNet.Fixtures");

        // Assert: the abstract modifier must be present and no conflicting modifiers appear
        Assert.Contains("abstract ", signature, StringComparison.Ordinal);
        Assert.DoesNotContain("static ", signature, StringComparison.Ordinal);
        Assert.DoesNotContain("sealed ", signature, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that <see cref="DotNetEmitter.BuildTypeSignature"/> includes the <c>sealed</c>
    ///     modifier for a sealed class that is not abstract.
    /// </summary>
    [Fact]
    public void DotNetEmitter_BuildTypeSignature_SealedClass_ContainsSealedModifier()
    {
        // Arrange: load the sealed fixture class from the fixture assembly
        using var assembly = AssemblyDefinition.ReadAssembly(FixturePaths.GetFixtureDll());
        var type = assembly.MainModule.Types.First(t => t.Name == "SealedClass");

        // Act
        var signature = DotNetEmitter.BuildTypeSignature(type, "ApiMark.DotNet.Fixtures");

        // Assert: the sealed modifier must be present and no conflicting modifiers appear
        Assert.Contains("sealed ", signature, StringComparison.Ordinal);
        Assert.DoesNotContain("abstract ", signature, StringComparison.Ordinal);
        Assert.DoesNotContain("static ", signature, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that <see cref="DotNetEmitter.BuildTypeSignature"/> includes the <c>static</c>
    ///     modifier for a static class.
    /// </summary>
    [Fact]
    public void DotNetEmitter_BuildTypeSignature_StaticClass_ContainsStaticModifier()
    {
        // Arrange: load the static fixture class from the fixture assembly
        using var assembly = AssemblyDefinition.ReadAssembly(FixturePaths.GetFixtureDll());
        var type = assembly.MainModule.Types.First(t => t.Name == "StaticFixtureClass");

        // Act
        var signature = DotNetEmitter.BuildTypeSignature(type, "ApiMark.DotNet.Fixtures");

        // Assert: the static modifier must be present and no conflicting modifiers appear
        Assert.Contains("static ", signature, StringComparison.Ordinal);
        Assert.DoesNotContain("abstract ", signature, StringComparison.Ordinal);
        Assert.DoesNotContain("sealed ", signature, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that <see cref="DotNetEmitter.BuildPropertySignature"/> includes the <c>static</c>
    ///     modifier for a static property.
    /// </summary>
    [Fact]
    public void DotNetEmitter_BuildPropertySignature_StaticProperty_ContainsStaticModifier()
    {
        // Arrange: load the Label static property from the fixture assembly
        using var assembly = AssemblyDefinition.ReadAssembly(FixturePaths.GetFixtureDll());
        var type = assembly.MainModule.Types.First(t => t.Name == "StaticFixtureClass");
        var prop = type.Properties.Single(p => p.Name == "Label");

        // Act
        var signature = DotNetEmitter.BuildPropertySignature(prop, "ApiMark.DotNet.Fixtures");

        // Assert
        Assert.Contains("static ", signature, StringComparison.Ordinal);
        Assert.Equal("public static string Label { get; }", signature);
    }

    /// <summary>
    ///     Validates that <see cref="DotNetEmitter.BuildPropertySignature"/> does not include the
    ///     <c>static</c> modifier for an instance property.
    /// </summary>
    [Fact]
    public void DotNetEmitter_BuildPropertySignature_InstanceProperty_DoesNotContainStaticModifier()
    {
        // Arrange: load the instance Name property from the fixture assembly
        using var assembly = AssemblyDefinition.ReadAssembly(FixturePaths.GetFixtureDll());
        var type = assembly.MainModule.Types.First(t => t.Name == "SampleClass");
        var prop = type.Properties.Single(p => p.Name == "Name");

        // Act
        var signature = DotNetEmitter.BuildPropertySignature(prop, "ApiMark.DotNet.Fixtures");

        // Assert
        Assert.DoesNotContain("static ", signature, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that <see cref="DotNetEmitter.BuildFieldSignature"/> appends a double-quoted
    ///     constant value for a <see langword="const"/> <see langword="string"/> field.
    /// </summary>
    [Fact]
    public void DotNetEmitter_BuildFieldSignature_ConstStringField_AppendsQuotedValue()
    {
        // Arrange: load the DefaultName const string field from the fixture assembly
        using var assembly = AssemblyDefinition.ReadAssembly(FixturePaths.GetFixtureDll());
        var type = assembly.MainModule.Types.First(t => t.Name == "SampleClass");
        var field = type.Fields.Single(f => f.Name == "DefaultName");

        // Act
        var signature = DotNetEmitter.BuildFieldSignature(field, "ApiMark.DotNet.Fixtures");

        // Assert
        Assert.Equal("public const string DefaultName = \"default\"", signature);
    }

    /// <summary>
    ///     Validates that <see cref="DotNetEmitter.BuildFieldSignature"/> appends a plain numeric
    ///     literal for a <see langword="const"/> <see langword="int"/> field.
    /// </summary>
    [Fact]
    public void DotNetEmitter_BuildFieldSignature_ConstIntField_AppendsNumericValue()
    {
        // Arrange: load the MaxCount const int field from the fixture assembly
        using var assembly = AssemblyDefinition.ReadAssembly(FixturePaths.GetFixtureDll());
        var type = assembly.MainModule.Types.First(t => t.Name == "SampleClass");
        var field = type.Fields.Single(f => f.Name == "MaxCount");

        // Act
        var signature = DotNetEmitter.BuildFieldSignature(field, "ApiMark.DotNet.Fixtures");

        // Assert
        Assert.Equal("public const int MaxCount = 42", signature);
    }

    /// <summary>
    ///     Validates that <see cref="DotNetEmitter.BuildFieldSignature"/> appends a lowercase
    ///     <c>true</c>/<c>false</c> literal for a <see langword="const"/> <see langword="bool"/> field.
    /// </summary>
    [Fact]
    public void DotNetEmitter_BuildFieldSignature_ConstBoolField_AppendsLowercaseBooleanValue()
    {
        // Arrange: load the IsDefaultEnabled const bool field from the fixture assembly
        using var assembly = AssemblyDefinition.ReadAssembly(FixturePaths.GetFixtureDll());
        var type = assembly.MainModule.Types.First(t => t.Name == "SampleClass");
        var field = type.Fields.Single(f => f.Name == "IsDefaultEnabled");

        // Act
        var signature = DotNetEmitter.BuildFieldSignature(field, "ApiMark.DotNet.Fixtures");

        // Assert
        Assert.Equal("public const bool IsDefaultEnabled = true", signature);
    }

    /// <summary>
    ///     Validates that <see cref="DotNetEmitter.BuildFieldSignature"/> renders a <see langword="const"/>
    ///     <see langword="char"/> field whose value is the NUL character as the escaped <c>'\0'</c> form
    ///     rather than embedding a raw control byte in the signature text.
    /// </summary>
    [Fact]
    public void DotNetEmitter_BuildFieldSignature_ConstCharField_EscapesNulCharacter()
    {
        // Arrange: load the NulSeparator const char field from the fixture assembly
        using var assembly = AssemblyDefinition.ReadAssembly(FixturePaths.GetFixtureDll());
        var type = assembly.MainModule.Types.First(t => t.Name == "SampleClass");
        var field = type.Fields.Single(f => f.Name == "NulSeparator");

        // Act
        var signature = DotNetEmitter.BuildFieldSignature(field, "ApiMark.DotNet.Fixtures");

        // Assert
        Assert.Equal("public const char NulSeparator = '\\0'", signature);
    }

    /// <summary>
    ///     Validates that <see cref="DotNetEmitter.FormatConstantValue"/> escapes every remaining
    ///     C# short-escape control character (backspace, form-feed, vertical tab, bell/alert) in a
    ///     string constant, rather than embedding the raw control bytes in generated documentation
    ///     text. This directly exercises <c>FormatConstantValue</c>/<c>EscapeStringLiteral</c> with
    ///     a crafted value, since the string-literal fixture coverage above only exercises the
    ///     already-covered <c>\n</c>/<c>\r</c>/<c>\t</c> escapes via a plain fixture string.
    /// </summary>
    [Fact]
    public void DotNetEmitter_FormatConstantValue_StringWithControlCharacters_EscapesAllOfThem()
    {
        // Arrange: a string containing bell, backspace, form-feed, and vertical-tab control characters
        const string value = "a\ab\bc\fd\ve";

        // Act
        var formatted = DotNetEmitter.FormatConstantValue(value);

        // Assert
        Assert.Equal("\"a\\ab\\bc\\fd\\ve\"", formatted);
    }

    /// <summary>
    ///     Validates that <see cref="DotNetEmitter.FormatConstantValue"/> falls back to a
    ///     <c>\uXXXX</c> escape for a control character that has no named C# short escape (e.g.
    ///     <c>DEL</c>, <c>0x7f</c>), so no raw control byte is ever embedded in generated
    ///     documentation text.
    /// </summary>
    [Fact]
    public void DotNetEmitter_FormatConstantValue_UnnamedControlCharacter_FallsBackToUnicodeEscape()
    {
        // Arrange: DEL (0x7f) has no named C# short escape
        const string value = "a\u007fb";

        // Act
        var formatted = DotNetEmitter.FormatConstantValue(value);

        // Assert
        Assert.Equal("\"a\\u007fb\"", formatted);
    }

    /// <summary>
    ///     Validates that <see cref="DotNetEmitter.FormatConstantValue"/> falls back to a
    ///     <c>\uXXXX</c> escape for a Unicode C1 control character (<c>U+0080</c>-<c>U+009F</c>),
    ///     not just the ASCII C0/DEL controls, so no raw control byte is ever embedded in
    ///     generated documentation text.
    /// </summary>
    [Fact]
    public void DotNetEmitter_FormatConstantValue_C1ControlCharacter_FallsBackToUnicodeEscape()
    {
        // Arrange: U+0085 (NEL) is a C1 control character with no named C# short escape
        const string value = "a\u0085b";

        // Act
        var formatted = DotNetEmitter.FormatConstantValue(value);

        // Assert
        Assert.Equal("\"a\\u0085b\"", formatted);
    }

    /// <summary>
    ///     Validates that <see cref="DotNetEmitter.BuildFieldSignature"/> renders an enum member's
    ///     explicit value using its underlying numeric constant rather than the enum type name,
    ///     confirming Mono.Cecil stores enum member constants as the underlying primitive type.
    /// </summary>
    [Fact]
    public void DotNetEmitter_BuildFieldSignature_EnumMemberWithExplicitValue_AppendsUnderlyingNumericValue()
    {
        // Arrange: load the Active enum member (explicit "= 0") from the fixture assembly
        using var assembly = AssemblyDefinition.ReadAssembly(FixturePaths.GetFixtureDll());
        var type = assembly.MainModule.Types.First(t => t.Name == "SampleStatus");
        var field = type.Fields.Single(f => f.Name == "Active");

        // Act
        var signature = DotNetEmitter.BuildFieldSignature(field, "ApiMark.DotNet.Fixtures");

        // Assert
        Assert.Equal("public const SampleStatus Active = 0", signature);
    }

    /// <summary>
    ///     Validates that <see cref="DotNetEmitter.BuildMethodSignature"/> renders an <c>out</c>
    ///     parameter with the <c>out</c> keyword and the un-suffixed element type name, rather than
    ///     Cecil's raw byref-marked type name (e.g. <c>ByRefTargetClass&amp;</c>).
    /// </summary>
    [Fact]
    public void DotNetEmitter_BuildMethodSignature_OutParameter_RendersOutKeywordAndPlainTypeName()
    {
        // Arrange
        using var assembly = AssemblyDefinition.ReadAssembly(FixturePaths.GetFixtureDll());
        var type = assembly.MainModule.Types.First(t => t.Name == "ByRefParameterClass");
        var method = type.Methods.Single(m => m.Name == "TryResolve");

        // Act
        var signature = DotNetEmitter.BuildMethodSignature(method, "ApiMark.DotNet.Fixtures");

        // Assert: the out parameter must render as "out ByRefTargetClass value" — never "ByRefTargetClass& value"
        Assert.Contains("out ByRefTargetClass value", signature, StringComparison.Ordinal);
        Assert.DoesNotContain("&", signature, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that <see cref="DotNetEmitter.BuildMethodSignature"/> renders a <c>ref</c>
    ///     parameter with the <c>ref</c> keyword and the un-suffixed element type name.
    /// </summary>
    [Fact]
    public void DotNetEmitter_BuildMethodSignature_RefParameter_RendersRefKeywordAndPlainTypeName()
    {
        // Arrange
        using var assembly = AssemblyDefinition.ReadAssembly(FixturePaths.GetFixtureDll());
        var type = assembly.MainModule.Types.First(t => t.Name == "ByRefParameterClass");
        var method = type.Methods.Single(m => m.Name == "Increment");

        // Act
        var signature = DotNetEmitter.BuildMethodSignature(method, "ApiMark.DotNet.Fixtures");

        // Assert
        Assert.Contains("ref ByRefTargetClass value", signature, StringComparison.Ordinal);
        Assert.DoesNotContain("&", signature, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that <see cref="DotNetEmitter.BuildMethodSignature"/> renders an <c>in</c>
    ///     parameter with the <c>in</c> keyword and the un-suffixed element type name.
    /// </summary>
    [Fact]
    public void DotNetEmitter_BuildMethodSignature_InParameter_RendersInKeywordAndPlainTypeName()
    {
        // Arrange
        using var assembly = AssemblyDefinition.ReadAssembly(FixturePaths.GetFixtureDll());
        var type = assembly.MainModule.Types.First(t => t.Name == "ByRefParameterClass");
        var method = type.Methods.Single(m => m.Name == "Inspect");

        // Act
        var signature = DotNetEmitter.BuildMethodSignature(method, "ApiMark.DotNet.Fixtures");

        // Assert
        Assert.Contains("in ByRefTargetClass value", signature, StringComparison.Ordinal);
        Assert.DoesNotContain("&", signature, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that <see cref="DotNetEmitter.BuildMethodSignature"/> renders a <c>ref</c>-returning
    ///     method with the <c>ref</c> keyword before the return type, rather than silently dropping it
    ///     now that <see cref="TypeNameSimplifier"/> unwraps <see cref="Mono.Cecil.ByReferenceType"/>.
    /// </summary>
    [Fact]
    public void DotNetEmitter_BuildMethodSignature_RefReturningMethod_RendersRefKeywordBeforeReturnType()
    {
        // Arrange
        using var assembly = AssemblyDefinition.ReadAssembly(FixturePaths.GetFixtureDll());
        var type = assembly.MainModule.Types.First(t => t.Name == "ByRefParameterClass");
        var method = type.Methods.Single(m => m.Name == "GetByRef");

        // Act
        var signature = DotNetEmitter.BuildMethodSignature(method, "ApiMark.DotNet.Fixtures");

        // Assert
        Assert.Contains("ref ByRefTargetClass GetByRef", signature, StringComparison.Ordinal);
        Assert.DoesNotContain("&", signature, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that <see cref="DotNetEmitter.GetReturnRefKeyword"/> returns an empty string for
    ///     an ordinary by-value return type.
    /// </summary>
    [Fact]
    public void DotNetEmitter_GetReturnRefKeyword_ByValueReturn_ReturnsEmptyString()
    {
        // Arrange
        using var assembly = AssemblyDefinition.ReadAssembly(FixturePaths.GetFixtureDll());
        var type = assembly.MainModule.Types.First(t => t.Name == "ByRefParameterClass");
        var method = type.Methods.Single(m => m.Name == "TryResolve");

        // Act
        var keyword = DotNetEmitter.GetReturnRefKeyword(method.ReturnType);

        // Assert
        Assert.Equal(string.Empty, keyword);
    }

    /// <summary>
    ///     Validates that <see cref="DotNetEmitter.BuildMethodDisplayName"/> includes the <c>out</c>
    ///     keyword for a byref parameter in the overload heading text.
    /// </summary>
    [Fact]
    public void DotNetEmitter_BuildMethodDisplayName_OutParameter_IncludesOutKeyword()
    {
        // Arrange
        using var assembly = AssemblyDefinition.ReadAssembly(FixturePaths.GetFixtureDll());
        var type = assembly.MainModule.Types.First(t => t.Name == "ByRefParameterClass");
        var method = type.Methods.Single(m => m.Name == "TryResolve");

        // Act
        var displayName = DotNetEmitter.BuildMethodDisplayName(method);

        // Assert
        Assert.Equal("TryResolve(string, out ByRefTargetClass)", displayName);
    }

    /// <summary>
    ///     Validates that <see cref="DotNetEmitter.GetRefKindKeyword"/> returns an empty string for
    ///     an ordinary by-value parameter.
    /// </summary>
    [Fact]
    public void DotNetEmitter_GetRefKindKeyword_ByValueParameter_ReturnsEmptyString()
    {
        // Arrange
        using var assembly = AssemblyDefinition.ReadAssembly(FixturePaths.GetFixtureDll());
        var type = assembly.MainModule.Types.First(t => t.Name == "ByRefParameterClass");
        var method = type.Methods.Single(m => m.Name == "TryResolve");
        var nameParam = method.Parameters.Single(p => p.Name == "name");

        // Act
        var keyword = DotNetEmitter.GetRefKindKeyword(nameParam);

        // Assert
        Assert.Equal(string.Empty, keyword);
    }

    /// <summary>
    ///     Validates that <see cref="DotNetEmitter.IsNamespaceDocCarrier"/> returns
    ///     <see langword="true"/> for the <c>NamespaceDoc</c> carrier class in the fixture assembly.
    /// </summary>
    [Fact]
    public void DotNetEmitter_IsNamespaceDocCarrier_NamespaceDocClass_ReturnsTrue()
    {
        // Arrange: load all types including internal ones from the fixture assembly
        using var assembly = AssemblyDefinition.ReadAssembly(FixturePaths.GetFixtureDll());
        var type = assembly.MainModule.Types.First(t => t.Name == "NamespaceDoc");

        // Act
        var result = DotNetEmitter.IsNamespaceDocCarrier(type);

        // Assert
        Assert.True(result, "NamespaceDoc internal static class must be recognized as a carrier.");
    }

    /// <summary>
    ///     Validates that <see cref="DotNetEmitter.IsNamespaceDocCarrier"/> returns
    ///     <see langword="false"/> for a regular (non-carrier) class.
    /// </summary>
    [Fact]
    public void DotNetEmitter_IsNamespaceDocCarrier_RegularClass_ReturnsFalse()
    {
        // Arrange: SampleClass is a regular public class, not a NamespaceDoc carrier
        using var assembly = AssemblyDefinition.ReadAssembly(FixturePaths.GetFixtureDll());
        var type = assembly.MainModule.Types.First(t => t.Name == "SampleClass");

        // Act
        var result = DotNetEmitter.IsNamespaceDocCarrier(type);

        // Assert
        Assert.False(result, "A regular public class must not be recognized as a NamespaceDoc carrier.");
    }

    /// <summary>Validates that <see cref="DotNetEmitter.BuildPropertyAccessors"/> emits <c>init;</c> for init-only setters.</summary>
    [Fact]
    public void DotNetEmitter_BuildPropertyAccessors_InitOnlySetter_EmitsInit()
    {
        // Arrange
        using var assembly = AssemblyDefinition.ReadAssembly(FixturePaths.GetFixtureDll());
        var type = assembly.MainModule.Types.Single(t => t.Name == "InitPropertyClass");
        var prop = type.Properties.Single(p => p.Name == "InitOnlyProperty");

        // Act
        var result = DotNetEmitter.BuildPropertyAccessors(prop);

        // Assert
        Assert.Contains("init", result, StringComparison.Ordinal);
        Assert.DoesNotContain("set", result, StringComparison.Ordinal);
    }

    /// <summary>Validates that <see cref="DotNetEmitter.BuildPropertyAccessors"/> does not prefix accessors when they share the property's declared accessibility.</summary>
    [Fact]
    public void DotNetEmitter_BuildPropertyAccessors_ProtectedProperty_DoesNotPrefixAccessors()
    {
        // Arrange: load ProtectedProperty from AbstractFixtureClass in the fixture assembly
        using var assembly = AssemblyDefinition.ReadAssembly(FixturePaths.GetFixtureDll());
        var type = assembly.MainModule.Types.First(t => t.Name == "AbstractFixtureClass");
        var prop = type.Properties.Single(p => p.Name == "ProtectedProperty");

        // Act
        var result = DotNetEmitter.BuildPropertyAccessors(prop);

        // Assert: both accessors share the property's protected accessibility — no prefix should be emitted
        Assert.Equal("get; set;", result);
    }

    /// <summary>
    ///     Validates that <see cref="DotNetEmitter.BuildPropertyAccessors"/> uses the most permissive accessor
    ///     to derive the property-level accessibility keyword when getter and setter have different levels.
    /// </summary>
    [Fact]
    public void DotNetEmitter_BuildPropertyAccessors_AsymmetricGetSet_UsesMostPermissiveAccessibility()
    {
        // Arrange: load AsymmetricProperty from AsymmetricAccessorClass; getter is protected, setter is public.
        // The most permissive accessor is the setter (public), so the property-level keyword must be "public"
        // and only the getter should receive an explicit "protected " prefix.
        using var assembly = AssemblyDefinition.ReadAssembly(FixturePaths.GetFixtureDll());
        var type = assembly.MainModule.Types.Single(t => t.Name == "AsymmetricAccessorClass");
        var prop = type.Properties.Single(p => p.Name == "AsymmetricProperty");

        // Act
        var result = DotNetEmitter.BuildPropertyAccessors(prop);

        // Assert: getter must be prefixed with its restricted accessibility; setter must have no prefix
        Assert.Equal("protected get; set;", result);
    }

    /// <summary>
    ///     Validates that <see cref="DotNetEmitter.GetVisibleNestedTypes"/> excludes compiler-generated
    ///     nested types (cached-lambda classes, closures, etc.) even at <see cref="ApiVisibility.All"/>,
    ///     where they would otherwise be included because they are private/internal and carry no
    ///     meaningful documentation, and their names (e.g. <c>&lt;&gt;c</c>) are invalid Windows file names.
    /// </summary>
    [Fact]
    public void DotNetEmitter_GetVisibleNestedTypes_AllVisibility_ExcludesCompilerGeneratedTypes()
    {
        // Arrange
        var options = BuildOptions();
        options.Visibility = ApiVisibility.All;
        var emitter = (DotNetEmitter)new DotNetGenerator(options).Parse(new InMemoryContext());
        using var assembly = AssemblyDefinition.ReadAssembly(FixturePaths.GetFixtureDll());
        var type = assembly.MainModule.Types.Single(t => t.Name == "CompilerGeneratedNestedClass");

        // Sanity check: the compiler did synthesize at least one nested type for the lambdas
        Assert.NotEmpty(type.NestedTypes);

        // Act
        var visibleNestedTypes = emitter.GetVisibleNestedTypes(type).ToList();

        // Assert: none of the synthesized nested types (whose names start with '<') are visible
        Assert.Empty(visibleNestedTypes);
        Assert.DoesNotContain(visibleNestedTypes, t => t.Name.Contains('<', StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates that <see cref="DotNetEmitter.GetVisibleMembers"/> excludes a record's
    ///     compiler-synthesized <c>EqualityContract</c> property — for a sealed record (private,
    ///     visible only at <see cref="ApiVisibility.All"/>), a non-sealed record (protected
    ///     virtual, visible at <see cref="ApiVisibility.PublicAndProtected"/>), and a derived
    ///     record (protected override) — while still including the record's genuine,
    ///     user-declared positional property, and still flagging a hand-written, non-compiler-
    ///     generated property that happens to share the <c>EqualityContract</c> name.
    /// </summary>
    [Fact]
    public void DotNetEmitter_GetVisibleMembers_AllVisibility_ExcludesRecordEqualityContract()
    {
        // Arrange
        var options = BuildOptions();
        options.Visibility = ApiVisibility.All;
        var emitter = (DotNetEmitter)new DotNetGenerator(options).Parse(new InMemoryContext());
        using var assembly = AssemblyDefinition.ReadAssembly(FixturePaths.GetFixtureDll());

        var sealedRecord = assembly.MainModule.Types.Single(t => t.Name == "SealedRecordClass");
        var baseRecord = assembly.MainModule.Types.Single(t => t.Name == "BaseRecordClass");
        var derivedRecord = assembly.MainModule.Types.Single(t => t.Name == "DerivedRecordClass");
        var handWritten = assembly.MainModule.Types.Single(t => t.Name == "HandWrittenEqualityContractClass");

        // Act
        var sealedMembers = emitter.GetVisibleMembers(sealedRecord).ToList();
        var baseMembers = emitter.GetVisibleMembers(baseRecord).ToList();
        var derivedMembers = emitter.GetVisibleMembers(derivedRecord).ToList();
        var handWrittenMembers = emitter.GetVisibleMembers(handWritten).ToList();

        // Assert: EqualityContract is excluded for every record shape...
        Assert.DoesNotContain(sealedMembers, m => m.Name == "EqualityContract");
        Assert.DoesNotContain(baseMembers, m => m.Name == "EqualityContract");
        Assert.DoesNotContain(derivedMembers, m => m.Name == "EqualityContract");

        // ...but the records' genuine positional properties are still visible...
        Assert.Contains(sealedMembers, m => m.Name == "Name");
        Assert.Contains(baseMembers, m => m.Name == "Name");
        Assert.Contains(derivedMembers, m => m.Name == "Extra");

        // ...and a hand-written, non-compiler-generated EqualityContract property is still reported
        Assert.Contains(handWrittenMembers, m => m.Name == "EqualityContract");
    }

    /// <summary>
    ///     Validates that <see cref="DotNetEmitter.GetVisibleMembers"/> excludes a field-like
    ///     event's compiler-generated backing field — which shares the event's exact name (no
    ///     angle brackets) and is only distinguishable via <c>CompilerGeneratedAttribute</c> —
    ///     while still including the event itself.
    /// </summary>
    [Fact]
    public void DotNetEmitter_GetVisibleMembers_AllVisibility_ExcludesFieldLikeEventBackingField()
    {
        // Arrange
        var options = BuildOptions();
        options.Visibility = ApiVisibility.All;
        var emitter = (DotNetEmitter)new DotNetGenerator(options).Parse(new InMemoryContext());
        using var assembly = AssemblyDefinition.ReadAssembly(FixturePaths.GetFixtureDll());
        var type = assembly.MainModule.Types.Single(t => t.Name == "FieldLikeEventClass");

        // Act
        var members = emitter.GetVisibleMembers(type).ToList();

        // Assert: the backing field (a FieldDefinition named "Updated") is excluded...
        Assert.DoesNotContain(members, m => m is FieldDefinition && m.Name == "Updated");

        // ...but the event itself (an EventDefinition named "Updated") is still visible
        Assert.Contains(members, m => m is EventDefinition && m.Name == "Updated");
    }

    /// <summary>
    ///     Validates that <see cref="DotNetEmitter.IsImplicitDefaultConstructor"/> correctly
    ///     distinguishes compiler-synthesized implicit default constructors — including ones on
    ///     types with property initializers, which contribute their own debug sequence points —
    ///     from genuine explicit constructors, including one whose single source statement stores
    ///     more than one field under a single sequence point (a tuple-deconstruction assignment).
    /// </summary>
    [Fact]
    public void DotNetEmitter_IsImplicitDefaultConstructor_DistinguishesImplicitFromExplicit()
    {
        // Arrange — load with debug symbols so the sequence-point heuristic has data to work with
        using var assembly = AssemblyDefinition.ReadAssembly(FixturePaths.GetFixtureDll(), new ReaderParameters
        {
            ReadSymbols = true,
            SymbolReaderProvider = new DefaultSymbolReaderProvider(false),
        });

        var implicitNoInitializers = assembly.MainModule.Types
            .Single(t => t.Name == "ExcludedSampleClass")
            .Methods.Single(m => m.IsConstructor);
        var implicitWithInitializers = assembly.MainModule.Types
            .Single(t => t.Name == "ImplicitDefaultConstructorClass")
            .Methods.Single(m => m.IsConstructor);
        var explicitWithParameter = assembly.MainModule.Types
            .Single(t => t.Name == "OuterClass")
            .Methods.Single(m => m.IsConstructor);
        var explicitEmptyParameterless = assembly.MainModule.Types
            .Single(t => t.Name == "ExplicitEmptyConstructorClass")
            .Methods.Single(m => m.IsConstructor);
        var explicitExpressionBodied = assembly.MainModule.Types
            .Single(t => t.Name == "ExpressionBodiedConstructorClass")
            .Methods.Single(m => m.IsConstructor);
        var explicitTupleDeconstruction = assembly.MainModule.Types
            .Single(t => t.Name == "TupleDeconstructionConstructorClass")
            .Methods.Single(m => m.IsConstructor);
        var explicitExternalProtectedFieldStore = assembly.MainModule.Types
            .Single(t => t.Name == "ExternalProtectedFieldConstructorClass")
            .Methods.Single(m => m.IsConstructor);

        // Act / Assert
        Assert.True(DotNetEmitter.IsImplicitDefaultConstructor(implicitNoInitializers));
        Assert.True(DotNetEmitter.IsImplicitDefaultConstructor(implicitWithInitializers));
        Assert.False(DotNetEmitter.IsImplicitDefaultConstructor(explicitWithParameter));
        Assert.False(DotNetEmitter.IsImplicitDefaultConstructor(explicitEmptyParameterless));
        Assert.False(DotNetEmitter.IsImplicitDefaultConstructor(explicitExpressionBodied));
        Assert.False(DotNetEmitter.IsImplicitDefaultConstructor(explicitTupleDeconstruction));
        Assert.False(DotNetEmitter.IsImplicitDefaultConstructor(explicitExternalProtectedFieldStore));
    }

    /// <summary>
    ///     Validates that <see cref="DotNetEmitter.IsImplicitDefaultConstructor"/> does not throw
    ///     when a constructor stores a field whose declaring-type reference points at an assembly
    ///     that cannot be resolved anywhere — reproducing the scenario reported against an earlier
    ///     revision of this heuristic, where <c>FieldReference.DeclaringType.Resolve()</c> could
    ///     raise <see cref="AssemblyResolutionException"/> and abort documentation-coverage
    ///     checking. A genuinely nonexistent assembly reference (rather than a real external
    ///     fixture assembly) is used so the test is not masked by the host process's own
    ///     dependency-probing, which can resolve real fixture assemblies regardless of the
    ///     directory the inspected module was loaded from.
    /// </summary>
    [Fact]
    public void DotNetEmitter_IsImplicitDefaultConstructor_UnresolvableExternalFieldDoesNotThrow()
    {
        // Arrange — load a real fixture constructor (with genuine debug symbols already attached),
        // then splice in an extra `stfld` targeting a field declared on a fabricated type from an
        // assembly that does not exist anywhere on disk or in the process's dependency graph.
        using var assembly = AssemblyDefinition.ReadAssembly(FixturePaths.GetFixtureDll(), new ReaderParameters
        {
            ReadSymbols = true,
            SymbolReaderProvider = new DefaultSymbolReaderProvider(false),
        });

        var module = assembly.MainModule;
        var constructor = module.Types
            .Single(t => t.Name == "ExplicitEmptyConstructorClass")
            .Methods.Single(m => m.IsConstructor);

        var fakeAssembly = new AssemblyNameReference("ApiMark.NonExistent.Fake.Assembly", new Version(1, 0, 0, 0));
        var fakeDeclaringType = new TypeReference("ApiMark.NonExistent.Fake", "ExternalBase", module, fakeAssembly);
        var fakeField = new FieldReference("FakeField", module.TypeSystem.Int32, fakeDeclaringType);

        var il = constructor.Body.GetILProcessor();
        var firstInstruction = constructor.Body.Instructions[0];
        il.InsertBefore(firstInstruction, il.Create(OpCodes.Ldarg_0));
        il.InsertBefore(firstInstruction, il.Create(OpCodes.Ldc_I4_1));
        il.InsertBefore(firstInstruction, il.Create(OpCodes.Stfld, fakeField));

        // Sanity check: the fabricated declaring type truly cannot be resolved.
        Assert.Throws<AssemblyResolutionException>(() => fakeDeclaringType.Resolve());

        // Act
        var exception = Record.Exception(() => DotNetEmitter.IsImplicitDefaultConstructor(constructor));

        // Assert: no exception, and the explicit constructor is correctly not classified as implicit
        Assert.Null(exception);
        Assert.False(DotNetEmitter.IsImplicitDefaultConstructor(constructor));
    }

    /// <summary>
    ///     Validates that <see cref="DotNetEmitter.IsImplicitDefaultConstructor"/> never classifies
    ///     a bodyless constructor as the compiler-synthesized implicit default constructor, since a
    ///     genuine implicit default constructor always has an IL body.
    /// </summary>
    [Fact]
    public void DotNetEmitter_IsImplicitDefaultConstructor_BodylessConstructor_ReturnsFalse()
    {
        // Arrange — load a real explicit constructor (with genuine debug symbols), then flip its
        // implementation attributes to InternalCall, which Mono.Cecil treats as evidence of no IL
        // body (mirroring a metadata-only/reference-assembly stub or an extern-declared method).
        // This leaves it with zero sequence points, which previously caused it to be misclassified
        // as the implicit default constructor.
        using var assembly = AssemblyDefinition.ReadAssembly(FixturePaths.GetFixtureDll(), new ReaderParameters
        {
            ReadSymbols = true,
            SymbolReaderProvider = new DefaultSymbolReaderProvider(false),
        });

        var constructor = assembly.MainModule.Types
            .Single(t => t.Name == "ExplicitEmptyConstructorClass")
            .Methods.Single(m => m.IsConstructor);

        constructor.ImplAttributes |= MethodImplAttributes.InternalCall;

        // Sanity check: the constructor is now reported as bodyless with no sequence points.
        Assert.False(constructor.HasBody);
        Assert.Empty(constructor.DebugInformation?.SequencePoints ?? []);

        // Act / Assert
        Assert.False(DotNetEmitter.IsImplicitDefaultConstructor(constructor));
    }

    /// <summary>Validates that optional parameter default values are rendered in method signatures.</summary>
    /// <param name="methodName">The fixture method name.</param>
    /// <param name="expected">The expected signature.</param>
    [Theory]
    [InlineData("ToJson", "public string ToJson(bool indented = true)")]
    [InlineData("Format", "public string Format(string format = \"N0\")")]
    [InlineData("Name", "public string Name(string name = null)")]
    [InlineData("Origin", "public int Origin(DefaultValuePoint point = default)")]
    [InlineData("Mode", "public DefaultValueMode Mode(DefaultValueMode mode = DefaultValueMode.Slow)")]
    [InlineData("Optional", "public int Optional(int value = default)")]
    [InlineData("Explicit", "public int Explicit(int value = 7)")]
    [InlineData("Rate", "public decimal Rate(decimal rate = 1.5)")]
    [InlineData("Count", "public int? Count(int? count = null)")]
    [InlineData("Separate", "public char Separate(char separator = ',', long limit = 42)")]
    public void DotNetEmitter_BuildMethodSignature_DefaultValues_RendersDefaults(string methodName, string expected)
    {
        // Arrange
        using var assembly = AssemblyDefinition.ReadAssembly(FixturePaths.GetFixtureDll());
        var type = assembly.MainModule.Types.Single(t => t.Name == "DefaultValueClass");
        var method = type.Methods.Single(m => m.Name == methodName);

        // Act
        var signature = DotNetEmitter.BuildMethodSignature(method, "ApiMark.DotNet.Fixtures");

        // Assert
        Assert.Equal(expected, signature);
    }

    /// <summary>Validates that display names and file names are unchanged by default values.</summary>
    [Fact]
    public void DotNetEmitter_BuildMethodDisplayName_DefaultValues_DoesNotIncludeDefaults()
    {
        // Arrange
        using var assembly = AssemblyDefinition.ReadAssembly(FixturePaths.GetFixtureDll());
        var type = assembly.MainModule.Types.Single(t => t.Name == "DefaultValueClass");
        var method = type.Methods.Single(m => m.Name == "ToJson");

        // Act
        var displayName = DotNetEmitter.BuildMethodDisplayName(method);
        var fileName = DotNetEmitter.BuildMethodFileName(method, type);

        // Assert
        Assert.Equal("ToJson(bool)", displayName);
        Assert.Equal("ToJson", fileName);
    }

    /// <summary>Validates the additional default value shapes: DateTimeConstant, unnamed enum, nullable enum, and special doubles.</summary>
    /// <param name="methodName">The fixture method name.</param>
    /// <param name="expected">The expected signature.</param>
    [Theory]
    [InlineData("When", "public DateTime When(DateTime when = new DateTime(630822816000000000))")]
    [InlineData("Unnamed", "public DefaultValueMode Unnamed(DefaultValueMode mode = (DefaultValueMode)5)")]
    [InlineData("NullableMode", "public DefaultValueMode? NullableMode(DefaultValueMode? mode = DefaultValueMode.Slow)")]
    [InlineData("Special", "public double Special(double a = double.NaN, double b = double.PositiveInfinity, double c = double.NegativeInfinity)")]
    [InlineData("SpecialFloat", "public float SpecialFloat(float a = float.NaN, float b = float.PositiveInfinity, float c = float.NegativeInfinity)")]
    [InlineData("Negative", "public decimal Negative(decimal rate = -0.25)")]
    public void DotNetEmitter_BuildMethodSignature_ExtendedDefaultValues_RendersDefaults(string methodName, string expected)
    {
        // Arrange
        using var assembly = AssemblyDefinition.ReadAssembly(FixturePaths.GetFixtureDll());
        var type = assembly.MainModule.Types.Single(t => t.Name == "DefaultValueExtendedClass");
        var method = type.Methods.Single(m => m.Name == methodName);

        // Act
        var signature = DotNetEmitter.BuildMethodSignature(method, "ApiMark.DotNet.Fixtures");

        // Assert
        Assert.Equal(expected, signature);
    }

    /// <summary>Validates that NaN and infinite floating-point constants render as named members.</summary>
    [Fact]
    public void DotNetEmitter_FormatConstantValue_SpecialFloatingPointValues_RendersNamedMembers()
    {
        // Act / Assert
        Assert.Equal("double.NaN", DotNetEmitter.FormatConstantValue(double.NaN));
        Assert.Equal("double.PositiveInfinity", DotNetEmitter.FormatConstantValue(double.PositiveInfinity));
        Assert.Equal("double.NegativeInfinity", DotNetEmitter.FormatConstantValue(double.NegativeInfinity));
        Assert.Equal("float.NaN", DotNetEmitter.FormatConstantValue(float.NaN));
        Assert.Equal("float.PositiveInfinity", DotNetEmitter.FormatConstantValue(float.PositiveInfinity));
        Assert.Equal("float.NegativeInfinity", DotNetEmitter.FormatConstantValue(float.NegativeInfinity));
        Assert.Equal("1.5", DotNetEmitter.FormatConstantValue(1.5d));
    }

    /// <summary>Creates a synthetic optional parameter (no metadata constant) carrying the given attribute.</summary>
    /// <param name="module">The module used to create type references.</param>
    /// <param name="parameterType">The parameter type.</param>
    /// <param name="attributeNamespace">The attribute namespace.</param>
    /// <param name="attributeName">The attribute type name.</param>
    /// <param name="argumentTypes">The constructor parameter types.</param>
    /// <param name="arguments">The constructor arguments.</param>
    /// <returns>The synthetic parameter.</returns>
    private static ParameterDefinition CreateAttributedOptionalParameter(
        ModuleDefinition module,
        TypeReference parameterType,
        string attributeNamespace,
        string attributeName,
        TypeReference[] argumentTypes,
        object?[] arguments)
    {
        var attributeType = new TypeReference(attributeNamespace, attributeName, module, module.TypeSystem.CoreLibrary);
        var constructor = new MethodReference(".ctor", module.TypeSystem.Void, attributeType) { HasThis = true };
        foreach (var argumentType in argumentTypes)
        {
            constructor.Parameters.Add(new ParameterDefinition(argumentType));
        }

        var attribute = new CustomAttribute(constructor);
        for (var i = 0; i < arguments.Length; i++)
        {
            attribute.ConstructorArguments.Add(new CustomAttributeArgument(argumentTypes[i], arguments[i]));
        }

        var parameter = new ParameterDefinition("value", ParameterAttributes.Optional, parameterType);
        parameter.CustomAttributes.Add(attribute);
        return parameter;
    }

    /// <summary>Validates that a DefaultParameterValue attribute (direct and boxed argument forms) supplies the default.</summary>
    [Fact]
    public void DotNetEmitter_FormatParameterDefault_DefaultParameterValueAttribute_RendersValue()
    {
        // Arrange
        using var module = ModuleDefinition.CreateModule("Synthetic", ModuleKind.Dll);
        var direct = CreateAttributedOptionalParameter(
            module,
            module.TypeSystem.Int32,
            "System.Runtime.InteropServices",
            "DefaultParameterValueAttribute",
            [module.TypeSystem.Object],
            [42]);
        var boxed = CreateAttributedOptionalParameter(
            module,
            module.TypeSystem.String,
            "System.Runtime.InteropServices",
            "DefaultParameterValueAttribute",
            [module.TypeSystem.Object],
            [new CustomAttributeArgument(module.TypeSystem.String, "x")]);

        // Act / Assert
        Assert.Equal("42", DotNetEmitter.FormatParameterDefault(direct));
        Assert.Equal("\"x\"", DotNetEmitter.FormatParameterDefault(boxed));
    }

    /// <summary>Validates that DecimalConstant arguments are converted by bit pattern and never throw.</summary>
    [Fact]
    public void DotNetEmitter_FormatParameterDefault_DecimalConstantAttribute_HandlesSignedAndOutOfRangeArguments()
    {
        // Arrange
        using var module = ModuleDefinition.CreateModule("Synthetic", ModuleKind.Dll);
        var types = new[]
        {
            module.TypeSystem.Byte,
            module.TypeSystem.Byte,
            module.TypeSystem.UInt32,
            module.TypeSystem.UInt32,
            module.TypeSystem.UInt32,
        };
        var intTypes = new[]
        {
            module.TypeSystem.Byte,
            module.TypeSystem.Byte,
            module.TypeSystem.Int32,
            module.TypeSystem.Int32,
            module.TypeSystem.Int32,
        };
        const string attributeName = "DecimalConstantAttribute";
        const string attributeNamespace = "System.Runtime.CompilerServices";

        var negative = CreateAttributedOptionalParameter(
            module, module.TypeSystem.Object, attributeNamespace, attributeName, types, [(byte)2, (byte)1, 0u, 0u, 125u]);
        var signedBits = CreateAttributedOptionalParameter(
            module, module.TypeSystem.Object, attributeNamespace, attributeName, intTypes, [(byte)0, (byte)0, 0, 0, -1]);
        var invalidScale = CreateAttributedOptionalParameter(
            module, module.TypeSystem.Object, attributeNamespace, attributeName, types, [(byte)40, (byte)0, 0u, 0u, 1u]);

        // Act / Assert
        Assert.Equal("-1.25", DotNetEmitter.FormatParameterDefault(negative));
        Assert.Equal("4294967295", DotNetEmitter.FormatParameterDefault(signedBits));
        Assert.Equal("default", DotNetEmitter.FormatParameterDefault(invalidScale));
    }

    /// <summary>Validates that an operator parameter with a default value renders it in the operator signature.</summary>
    [Fact]
    public void DotNetEmitter_BuildOperatorSignature_ParameterWithDefault_RendersDefault()
    {
        // Arrange — C# cannot declare optional operator parameters, so patch the metadata of a fixture operator
        using var assembly = AssemblyDefinition.ReadAssembly(FixturePaths.GetFixtureDll());
        var type = assembly.MainModule.Types.Single(t => t.Name == "OperatorsStruct");
        var method = type.Methods.Single(m => m.Name == "op_Addition");
        method.Parameters[1].Attributes |= ParameterAttributes.Optional | ParameterAttributes.HasDefault;
        method.Parameters[1].Constant = null;

        // Act
        var signature = DotNetEmitter.BuildMethodSignature(method, "ApiMark.DotNet.Fixtures");

        // Assert — a null constant on a struct parameter is the compiler encoding of default(T)
        Assert.Equal(
            "public static OperatorsStruct operator +(OperatorsStruct left, OperatorsStruct right = default)",
            signature);
    }

    /// <summary>Validates the <c>notnull</c> constraint is rendered alongside other generic constraints.</summary>
    /// <param name="methodName">The fixture method name.</param>
    /// <param name="expected">The expected signature.</param>
    [Theory]
    [InlineData("Keep", "public void Keep<T>(T value) where T : notnull")]
    [InlineData("Make", "public T Make<T>() where T : notnull, new()")]
    [InlineData("Allow", "public void Allow<T>(T value)")]
    public void DotNetEmitter_BuildMethodSignature_NotNullConstraint_RendersNotNull(string methodName, string expected)
    {
        // Arrange
        using var assembly = AssemblyDefinition.ReadAssembly(FixturePaths.GetFixtureDll());
        var type = assembly.MainModule.Types.Single(t => t.Name == "DefaultValueExtendedClass");
        var method = type.Methods.Single(m => m.Name == methodName);

        // Act
        var signature = DotNetEmitter.BuildMethodSignature(method, "ApiMark.DotNet.Fixtures");

        // Assert
        Assert.Equal(expected, signature);
    }

    /// <summary>Validates that nullable reference annotations are rendered at each type position of every member kind.</summary>
    /// <param name="typeName">The fixture type name.</param>
    /// <param name="memberName">The member name.</param>
    /// <param name="expected">The expected signature.</param>
    [Theory]
    [InlineData("NullabilityDiagramElement", "Subtitle", "public string? Subtitle { get; set; }")]
    [InlineData("NullabilityDiagramElement", "Title", "public string Title { get; set; }")]
    [InlineData("NullabilityDiagramElement", "StyleFunc", "public Func<NullWire, NullWireStyle?>? StyleFunc { get; set; }")]
    [InlineData("NullabilityDiagramElement", "NonNullStyleFunc", "public Func<NullWire, NullWireStyle?> NonNullStyleFunc { get; set; }")]
    [InlineData("NullabilityDiagramElement", "NullableElements", "public string?[] NullableElements { get; set; }")]
    [InlineData("NullabilityDiagramElement", "NullableArray", "public string[]? NullableArray { get; set; }")]
    [InlineData("NullabilityDiagramElement", "OptionalPoint", "public NullPoint? OptionalPoint { get; set; }")]
    [InlineData("NullabilityDiagramElement", "Cache", "public List<string?>? Cache")]
    [InlineData("NullabilityDiagramElement", "StyleChanged", "public event EventHandler<NullWireStyle?>? StyleChanged")]
    [InlineData("NullabilityDiagramElement", "Style", "public Func<NullWire, NullWireStyle?>? Style(NullWire wire, Dictionary<string, NullWire?>? fallback)")]
    [InlineData("NullabilityDiagramElement", "TryGetStyle", "public bool TryGetStyle(string name, out NullWireStyle? style)")]
    [InlineData("NullabilityMostlyNullableElement", "Third", "public string Third { get; set; }")]
    [InlineData("NullabilityMostlyNullableElement", "Combine", "public string? Combine(string? a, string? b, string c)")]
    [InlineData("NullabilityObliviousElement", "Name", "public string Name { get; set; }")]
    [InlineData("NullabilityObliviousElement", "Echo", "public string Echo(string value)")]
    [InlineData("NullabilityGenericElement`1", "Value", "public T? Value { get; set; }")]
    public void DotNetEmitter_BuildMemberSignature_NullabilityPositions_RendersAnnotations(
        string typeName,
        string memberName,
        string expected)
    {
        // Arrange
        using var assembly = AssemblyDefinition.ReadAssembly(FixturePaths.GetFixtureDll());
        var type = assembly.MainModule.Types.Single(t => t.Name == typeName);
        var member = type.Properties.Cast<IMemberDefinition>()
            .Concat(type.Events)
            .Concat(type.Fields)
            .Concat(type.Methods)
            .First(m => m.Name == memberName);

        // Act
        var signature = DotNetEmitter.BuildMemberSignature(member, "ApiMark.DotNet.Fixtures");

        // Assert
        Assert.Equal(expected, signature);
    }

    /// <summary>Validates that the inherited NullableContext is resolved from the member, type, or defaults to oblivious.</summary>
    [Fact]
    public void DotNetEmitter_ResolveNullableContext_ResolvesInheritedContext()
    {
        // Arrange
        using var assembly = AssemblyDefinition.ReadAssembly(FixturePaths.GetFixtureDll());
        var mostlyNullable = assembly.MainModule.Types.Single(t => t.Name == "NullabilityMostlyNullableElement");
        var oblivious = assembly.MainModule.Types.Single(t => t.Name == "NullabilityObliviousElement");

        // Act
        var nullableContext = DotNetEmitter.ResolveNullableContext(mostlyNullable.Methods.Single(m => m.Name == "Combine"));
        var obliviousContext = DotNetEmitter.ResolveNullableContext(oblivious.Methods.Single(m => m.Name == "Echo"));

        // Assert — the mostly-nullable type is compiled with context 2; the oblivious type has none
        Assert.Equal(2, nullableContext);
        Assert.Equal(0, obliviousContext);
    }

    /// <summary>Validates that CreateMemberTypeNullability reads the primary-type annotation of each member kind.</summary>
    [Fact]
    public void DotNetEmitter_CreateMemberTypeNullability_ReadsPrimaryTypeAnnotation()
    {
        // Arrange
        using var assembly = AssemblyDefinition.ReadAssembly(FixturePaths.GetFixtureDll());
        var type = assembly.MainModule.Types.Single(t => t.Name == "NullabilityDiagramElement");

        // Act
        var nullableProperty = DotNetEmitter.CreateMemberTypeNullability(type.Properties.Single(p => p.Name == "Subtitle"));
        var nonNullProperty = DotNetEmitter.CreateMemberTypeNullability(type.Properties.Single(p => p.Name == "Title"));
        var nullableField = DotNetEmitter.CreateMemberTypeNullability(type.Fields.Single(f => f.Name == "Cache"));

        // Assert — 2 means annotated (nullable), 1 means not annotated
        Assert.Equal(2, nullableProperty.Next());
        Assert.Equal(1, nonNullProperty.Next());
        Assert.Equal(2, nullableField.Next());
    }

    /// <summary>Validates that the implicit record <c>IEquatable&lt;Self&gt;</c> is hidden but explicit interfaces are kept.</summary>
    /// <param name="typeName">The fixture type name.</param>
    /// <param name="expected">The expected signature.</param>
    [Theory]
    [InlineData("SealedRecordClass", "public sealed record SealedRecordClass")]
    [InlineData("RecordWithInterface", "public record RecordWithInterface : IComparable")]
    [InlineData("ExplicitEquatableClass", "public sealed class ExplicitEquatableClass : IEquatable<ExplicitEquatableClass>")]
    public void DotNetEmitter_BuildTypeSignature_SelfEquatable_SuppressedOnlyForRecords(string typeName, string expected)
    {
        // Arrange
        using var assembly = AssemblyDefinition.ReadAssembly(FixturePaths.GetFixtureDll());
        var type = assembly.MainModule.Types.Single(t => t.Name == typeName);

        // Act
        var signature = DotNetEmitter.BuildTypeSignature(type, "ApiMark.DotNet.Fixtures");

        // Assert
        Assert.Equal(expected, signature);
    }

    /// <summary>Validates that a root namespace named like the reserved api index page is moved aside.</summary>
    [Fact]
    public void DotNetEmitter_GetNamespaceFolderPath_NamespaceNamedApi_IsMovedAsideFromApiIndexPage()
    {
        Assert.Equal("api-2", DotNetEmitter.GetNamespaceFolderPath("api", ["api"]));
        Assert.Equal("Api-2", DotNetEmitter.GetNamespaceFolderPath("Api", ["Api"]));
        Assert.Equal("api-2/Inner", DotNetEmitter.GetNamespaceFolderPath("api.Inner", ["api"]));
        Assert.Equal("api.Inner", DotNetEmitter.GetNamespaceFolderPath("api.Inner", ["api.Inner"]));
    }

    /// <summary>Validates that the empty (global) namespace maps to the synthetic global-namespace folder for any root set.</summary>
    [Fact]
    public void DotNetEmitter_GetNamespaceFolderPath_GlobalNamespace_ReturnsGlobalNamespaceFolder()
    {
        Assert.Equal("global-namespace", DotNetEmitter.GetNamespaceFolderPath("", []));
        Assert.Equal("global-namespace", DotNetEmitter.GetNamespaceFolderPath("", [""]));
        Assert.Equal("global-namespace", DotNetEmitter.GetNamespaceFolderPath("", ["", "A"]));
    }

    /// <summary>Validates that the global namespace has no children and is never a child.</summary>
    [Fact]
    public void DotNetEmitter_GetImmediateChildNamespaces_GlobalNamespace_IsSiblingRoot()
    {
        Assert.Empty(DotNetEmitter.GetImmediateChildNamespaces("", ["", "A", "A.B"]));
        Assert.Equal(["A.B"], DotNetEmitter.GetImmediateChildNamespaces("A", ["", "A", "A.B"]));
    }

    /// <summary>Validates the namespace display-name helper.</summary>
    [Fact]
    public void DotNetEmitter_GetNamespaceDisplayName_GlobalNamespace_ReturnsPlaceholder()
    {
        Assert.Equal("(global namespace)", DotNetEmitter.GetNamespaceDisplayName(""));
        Assert.Equal("A.B", DotNetEmitter.GetNamespaceDisplayName("A.B"));
    }

    /// <summary>Validates that the empty namespace is always a root and never hides other roots.</summary>
    [Fact]
    public void DotNetGenerator_ComputeRootNamespaces_GlobalNamespace_IsRootAndDoesNotHideOthers()
    {
        var roots = DotNetGenerator.ComputeRootNamespaces(["", "A", "A.B"]);
        Assert.Equal(["", "A"], roots);
    }

    /// <summary>Finds a member of a fixture type by name across all member kinds.</summary>
    /// <param name="assembly">The loaded fixture assembly.</param>
    /// <param name="typeName">The CLR type name.</param>
    /// <param name="memberName">The member name (the first match wins).</param>
    /// <returns>The member definition.</returns>
    private static IMemberDefinition FindFixtureMember(AssemblyDefinition assembly, string typeName, string memberName)
    {
        var type = assembly.MainModule.Types.Single(t => t.Name == typeName);
        return type.Properties.Cast<IMemberDefinition>()
            .Concat(type.Events)
            .Concat(type.Fields)
            .Concat(type.Methods)
            .First(m => m.Name == memberName);
    }

    /// <summary>
    ///     Validates the exact signature text of void returns, optional/out parameters, tuples, generic
    ///     arguments, and <c>params</c> arrays in nullable-context fixtures (regression for <c>void?</c>,
    ///     missing <c>params</c>, and tuple rendering).
    /// </summary>
    /// <param name="typeName">The fixture type name.</param>
    /// <param name="memberName">The member name.</param>
    /// <param name="expected">The exact expected signature.</param>
    [Theory]
    [InlineData("NullableSignatureMatrix", "Reconnect", "public void Reconnect(MatrixPort? newSource, MatrixPort? newTarget)")]
    [InlineData("NullableSignatureMatrix", "Disconnect", "public void Disconnect(MatrixPort? source)")]
    [InlineData("NullableSignatureMatrix", "NotifyAsync", "public Task NotifyAsync(MatrixPort? port)")]
    [InlineData("NullableSignatureMatrix", "AddAnnotation", "public void AddAnnotation(string text, MatrixRect bounds, string? id = null)")]
    [InlineData("NullableSignatureMatrix", "TryRename", "public bool TryRename(MatrixNode node, string name, out string? error)")]
    [InlineData("NullableSignatureMatrix", "Collect", "public Func<MatrixWire, MatrixStyle?>[] Collect(MatrixWire? wire, object? state, Dictionary<string, object?>? bag)")]
    [InlineData("NullableSignatureMatrix", "GetPair", "public (int Id, string? Name) GetPair(MatrixPort? port)")]
    [InlineData("NullableSignatureMatrix", "GetUnnamed", "public (int, string) GetUnnamed()")]
    [InlineData("NullableSignatureMatrix", "FindPair", "public (int Id, string Name)? FindPair(string? key)")]
    [InlineData("NullableSignatureMatrix", "Take", "public void Take((int X, int Y) point, (string? A, string? B)? optional)")]
    [InlineData("NullableSignatureMatrix", "GetBig", "public (int A, int B, int C, int D, int E, int F, int G, string? H) GetBig()")]
    [InlineData("ParamsClass", ".ctor", "public ParamsClass(string name, params int[] values)")]
    [InlineData("ParamsClass", "Sum", "public int Sum(params int[] values)")]
    [InlineData("ParamsClass", "Join", "public string Join(string separator, params string?[] parts)")]
    [InlineData("ParamsClass", "Log", "public void Log(string format, params object?[] args)")]
    public void DotNetEmitter_BuildMemberSignature_NullableMatrixAndParams_RendersExactSignature(
        string typeName,
        string memberName,
        string expected)
    {
        // Arrange
        using var assembly = AssemblyDefinition.ReadAssembly(FixturePaths.GetFixtureDll());
        var member = FindFixtureMember(assembly, typeName, memberName);

        // Act
        var signature = DotNetEmitter.BuildMemberSignature(member, "ApiMark.DotNet.Fixtures");

        // Assert
        Assert.Equal(expected, signature);
    }

    /// <summary>Validates that a delegate keeps the <c>params</c> keyword on its parameter array.</summary>
    [Fact]
    public void DotNetEmitter_BuildDelegateSignature_ParamsDelegate_RendersParamsKeyword()
    {
        // Arrange
        using var assembly = AssemblyDefinition.ReadAssembly(FixturePaths.GetFixtureDll());
        var type = assembly.MainModule.Types.Single(t => t.Name == "ParamsHandler");

        // Act
        var signature = DotNetEmitter.BuildTypeSignature(type, "ApiMark.DotNet.Fixtures");

        // Assert
        Assert.Equal("public delegate void ParamsHandler(params string[] items)", signature);
    }

    /// <summary>
    ///     Validates the exact virtual/override/abstract/sealed modifiers on methods, properties, and events,
    ///     and that interface members carry none.
    /// </summary>
    /// <param name="typeName">The fixture type name.</param>
    /// <param name="memberName">The member name.</param>
    /// <param name="expected">The exact expected signature.</param>
    [Theory]
    [InlineData("ShapeBase", "Draw", "public abstract void Draw()")]
    [InlineData("ShapeBase", "Resize", "public virtual void Resize(int width, int height)")]
    [InlineData("ShapeBase", "OnResized", "protected virtual void OnResized()")]
    [InlineData("ShapeBase", "Name", "public abstract string Name { get; }")]
    [InlineData("ShapeBase", "Label", "public virtual string Label { get; set; }")]
    [InlineData("ShapeBase", "Changed", "public virtual event EventHandler? Changed")]
    [InlineData("ShapeBase", "Plain", "public void Plain()")]
    [InlineData("ShapeDerived", "Draw", "public override void Draw()")]
    [InlineData("ShapeDerived", "Resize", "public sealed override void Resize(int width, int height)")]
    [InlineData("ShapeDerived", "OnResized", "protected override void OnResized()")]
    [InlineData("ShapeDerived", "Name", "public override string Name { get; }")]
    [InlineData("ShapeDerived", "Label", "public override string Label { get; set; }")]
    [InlineData("ShapeDerived", "Changed", "public override event EventHandler? Changed")]
    [InlineData("ShapeSealed", "Draw", "public override void Draw()")]
    [InlineData("ShapeSealed", "ToString", "public override string ToString()")]
    [InlineData("IShapeContract", "Draw", "public void Draw()")]
    [InlineData("IShapeContract", "Name", "public string Name { get; }")]
    [InlineData("ShapeContractImplementation", "Draw", "public void Draw()")]
    [InlineData("ShapeContractImplementation", "Name", "public string Name { get; }")]
    public void DotNetEmitter_BuildMemberSignature_InheritanceShapes_RendersModifiers(
        string typeName,
        string memberName,
        string expected)
    {
        // Arrange
        using var assembly = AssemblyDefinition.ReadAssembly(FixturePaths.GetFixtureDll());
        var member = FindFixtureMember(assembly, typeName, memberName);

        // Act
        var signature = DotNetEmitter.BuildMemberSignature(member, "ApiMark.DotNet.Fixtures");

        // Assert
        Assert.Equal(expected, signature);
    }

    /// <summary>
    ///     Validates the <c>new</c> modifier on members hiding base members (not on overloads or
    ///     overrides) and the <c>static</c> modifier on events.
    /// </summary>
    /// <param name="typeName">The fixture type name.</param>
    /// <param name="memberName">The member name.</param>
    /// <param name="expected">The exact expected signature.</param>
    [Theory]
    [InlineData("ShapeHiding", "Plain", "public new void Plain()")]
    [InlineData("ShapeHiding", "Label", "public new string Label { get; set; }")]
    [InlineData("ShapeHiding", "Draw", "public override void Draw()")]
    [InlineData("StaticEventHolder", "Happened", "public static event EventHandler? Happened")]
    public void DotNetEmitter_BuildMemberSignature_HidingAndStaticEvent_RendersNewAndStatic(
        string typeName,
        string memberName,
        string expected)
    {
        // Arrange
        using var assembly = AssemblyDefinition.ReadAssembly(FixturePaths.GetFixtureDll());
        var member = FindFixtureMember(assembly, typeName, memberName);

        // Act
        var signature = DotNetEmitter.BuildMemberSignature(member, "ApiMark.DotNet.Fixtures");

        // Assert
        Assert.Equal(expected, signature);
    }

    /// <summary>Validates that a hiding virtual method gets <c>new virtual</c> and an overload gets no <c>new</c>.</summary>
    [Fact]
    public void DotNetEmitter_BuildMethodSignature_HidingVirtualAndOverload_RendersNewOnlyOnHider()
    {
        // Arrange
        using var assembly = AssemblyDefinition.ReadAssembly(FixturePaths.GetFixtureDll());
        var type = assembly.MainModule.Types.Single(t => t.Name == "ShapeHiding");

        // Act
        var hider = DotNetEmitter.BuildMethodSignature(type.Methods.Single(m => m.Name == "Resize" && m.Parameters.Count == 2), "ApiMark.DotNet.Fixtures");
        var overload = DotNetEmitter.BuildMethodSignature(type.Methods.Single(m => m.Name == "Resize" && m.Parameters.Count == 1), "ApiMark.DotNet.Fixtures");

        // Assert
        Assert.Equal("public new virtual void Resize(int width, int height)", hider);
        Assert.Equal("public void Resize(int width)", overload);
    }

    /// <summary>
    ///     Validates that property accessors that are not visible at the configured tier (private or
    ///     internal setters, private getters) are hidden, while visible ones keep their modifiers.
    /// </summary>
    /// <param name="memberName">The property name.</param>
    /// <param name="visibility">The visibility tier.</param>
    /// <param name="expected">The exact expected signature.</param>
    [Theory]
    [InlineData("PrivateSet", ApiVisibility.Public, "public int PrivateSet { get; }")]
    [InlineData("PrivateSet", ApiVisibility.PublicAndProtected, "public int PrivateSet { get; }")]
    [InlineData("PrivateSet", ApiVisibility.All, "public int PrivateSet { get; private set; }")]
    [InlineData("InternalSet", ApiVisibility.Public, "public int InternalSet { get; }")]
    [InlineData("InternalSet", ApiVisibility.PublicAndProtected, "public int InternalSet { get; }")]
    [InlineData("InternalSet", ApiVisibility.All, "public int InternalSet { get; internal set; }")]
    [InlineData("ProtectedSet", ApiVisibility.Public, "public int ProtectedSet { get; }")]
    [InlineData("ProtectedSet", ApiVisibility.PublicAndProtected, "public int ProtectedSet { get; protected set; }")]
    [InlineData("ProtectedInternalSet", ApiVisibility.Public, "public int ProtectedInternalSet { get; }")]
    [InlineData("ProtectedInternalSet", ApiVisibility.PublicAndProtected, "public int ProtectedInternalSet { get; protected internal set; }")]
    [InlineData("InitOnly", ApiVisibility.Public, "public int InitOnly { get; init; }")]
    [InlineData("PrivateGet", ApiVisibility.Public, "public int PrivateGet { set; }")]
    [InlineData("PrivateGet", ApiVisibility.All, "public int PrivateGet { private get; set; }")]
    [InlineData("Plain", ApiVisibility.Public, "public int Plain { get; set; }")]
    public void DotNetEmitter_BuildMemberSignature_AccessorShapes_HidesNonVisibleAccessors(
        string memberName,
        ApiVisibility visibility,
        string expected)
    {
        // Arrange
        using var assembly = AssemblyDefinition.ReadAssembly(FixturePaths.GetFixtureDll());
        var member = FindFixtureMember(assembly, "AccessorShapes", memberName);

        // Act
        var signature = DotNetEmitter.BuildMemberSignature(member, "ApiMark.DotNet.Fixtures", visibility);

        // Assert
        Assert.Equal(expected, signature);
    }
}
