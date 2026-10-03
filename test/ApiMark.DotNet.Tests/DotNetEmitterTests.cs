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
}
