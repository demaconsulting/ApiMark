// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using ApiMark.DotNet;
using Mono.Cecil;
using Xunit;

namespace ApiMark.DotNet.Tests;

/// <summary>Unit tests for <see cref="TypeLinkResolver"/>.</summary>
public class TypeLinkResolverTests : IDisposable
{
    private readonly AssemblyDefinition _assembly;

    /// <summary>Initializes the test fixture by loading the fixture assembly.</summary>
    public TypeLinkResolverTests()
    {
        _assembly = AssemblyDefinition.ReadAssembly(FixturePaths.GetFixtureDll());
    }

    /// <summary>Disposes the loaded assembly after each test.</summary>
    public void Dispose()
    {
        _assembly.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>Validates that a null type reference returns an empty string.</summary>
    [Fact]
    public void TypeLinkResolver_Linkify_NullTypeRef_ReturnsEmptyString()
    {
        // Arrange
        var resolver = new TypeLinkResolver(["ApiMark.DotNet.Fixtures"]);
        var externalTypes = new HashSet<ExternalTypeInfo>();

        // Act
        var result = resolver.Linkify(null!, "", "ApiMark.DotNet.Fixtures", externalTypes);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    /// <summary>Validates that <c>System.Int32</c> is resolved to the C# alias "int".</summary>
    [Fact]
    public void TypeLinkResolver_Linkify_Int32_ReturnsCSharpAlias()
    {
        // Arrange
        var resolver = new TypeLinkResolver(["ApiMark.DotNet.Fixtures"]);
        var externalTypes = new HashSet<ExternalTypeInfo>();
        var typeRef = _assembly.MainModule.TypeSystem.Int32;

        // Act
        var result = resolver.Linkify(typeRef, "", "ApiMark.DotNet.Fixtures", externalTypes);

        // Assert
        Assert.Equal("int", result);
    }

    /// <summary>Validates that <c>System.String</c> is resolved to the C# alias "string".</summary>
    [Fact]
    public void TypeLinkResolver_Linkify_StringType_ReturnsCSharpAlias()
    {
        // Arrange
        var resolver = new TypeLinkResolver(["ApiMark.DotNet.Fixtures"]);
        var externalTypes = new HashSet<ExternalTypeInfo>();
        var typeRef = _assembly.MainModule.TypeSystem.String;

        // Act
        var result = resolver.Linkify(typeRef, "", "ApiMark.DotNet.Fixtures", externalTypes);

        // Assert
        Assert.Equal("string", result);
    }

    /// <summary>Validates that an intra-assembly type generates a Markdown link when generateLinks is true.</summary>
    [Fact]
    public void TypeLinkResolver_Linkify_GenerateLinksTrue_IntraAssemblyType_ReturnsMarkdownLink()
    {
        // Arrange
        var resolver = new TypeLinkResolver(["ApiMark.DotNet.Fixtures"], generateLinks: true);
        var externalTypes = new HashSet<ExternalTypeInfo>();
        var typeDef = _assembly.MainModule.Types.First(t => t.Name == "SampleClass");

        // Act
        var result = resolver.Linkify(typeDef, "ApiMark.DotNet.Fixtures", "ApiMark.DotNet.Fixtures", externalTypes);

        // Assert
        Assert.Contains("[", result, StringComparison.Ordinal);
    }

    /// <summary>Validates that an intra-assembly type returns plain text when generateLinks is false.</summary>
    [Fact]
    public void TypeLinkResolver_Linkify_GenerateLinksFalse_IntraAssemblyType_ReturnsPlainText()
    {
        // Arrange
        var resolver = new TypeLinkResolver(["ApiMark.DotNet.Fixtures"], generateLinks: false);
        var externalTypes = new HashSet<ExternalTypeInfo>();
        var typeDef = _assembly.MainModule.Types.First(t => t.Name == "SampleClass");

        // Act
        var result = resolver.Linkify(typeDef, "ApiMark.DotNet.Fixtures", "ApiMark.DotNet.Fixtures", externalTypes);

        // Assert
        Assert.DoesNotContain("[", result, StringComparison.Ordinal);
    }

    /// <summary>Validates that a nullable generic type parameter (<c>T?</c>) appends a <c>?</c> suffix.</summary>
    [Fact]
    public void TypeLinkResolver_Linkify_NullableGenericParameter_AppendsQuestionMark()
    {
        // Arrange: obtain the T generic parameter from SampleGenericClass<T>
        var resolver = new TypeLinkResolver(["ApiMark.DotNet.Fixtures"]);
        var externalTypes = new HashSet<ExternalTypeInfo>();
        var genericClass = _assembly.MainModule.Types.First(t => t.Name == "SampleGenericClass`1");
        var typeParam = genericClass.GenericParameters[0]; // T

        // Act: linkify with isNullableAnnotated = true to simulate T?
        var result = resolver.Linkify(typeParam, "", "ApiMark.DotNet.Fixtures", externalTypes, isNullableAnnotated: true);

        // Assert: the result must be "T?" — the plain parameter name plus the nullability marker
        Assert.Equal("T?", result);
    }

    /// <summary>
    ///     Validates that a non-System external type reference is rendered as plain text
    ///     and tracked in the caller-supplied external type set.
    /// </summary>
    [Fact]
    public void TypeLinkResolver_Linkify_ExternalNonSystemType_ReturnsPlainNameAndTracksExternalType()
    {
        // Arrange: create a resolver and a synthetic external TypeReference whose namespace
        // does not start with "System" and whose scope is an AssemblyNameReference (external)
        var resolver = new TypeLinkResolver(["ApiMark.DotNet.Fixtures"]);
        var externalTypes = new HashSet<ExternalTypeInfo>();
        var assemblyRef = new AssemblyNameReference("Acme.Widgets", new Version(1, 0));
        var externalTypeRef = new TypeReference("Acme.Widgets", "Widget", _assembly.MainModule, assemblyRef);

        // Act
        var result = resolver.Linkify(externalTypeRef, "", "ApiMark.DotNet.Fixtures", externalTypes);

        // Assert: plain name returned and the type is recorded in the external types set
        Assert.Multiple(
            () => Assert.Equal("Widget", result),
            () => Assert.Single(externalTypes),
            () => Assert.Contains(
                new ExternalTypeInfo("Widget", "Acme.Widgets"),
                externalTypes));
    }

    /// <summary>
    ///     Validates that an array type reference renders with the <c>[]</c> array rank suffix
    ///     appended to the element type name.
    /// </summary>
    [Fact]
    public void TypeLinkResolver_Linkify_ArrayType_AppendsArraySuffix()
    {
        // Arrange: wrap System.String in an ArrayType to represent string[]
        var resolver = new TypeLinkResolver(["ApiMark.DotNet.Fixtures"]);
        var externalTypes = new HashSet<ExternalTypeInfo>();
        var stringArrayType = new ArrayType(_assembly.MainModule.TypeSystem.String);

        // Act
        var result = resolver.Linkify(stringArrayType, "", "ApiMark.DotNet.Fixtures", externalTypes);

        // Assert: the result is "string[]" — element alias plus array suffix
        Assert.EndsWith("[]", result, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a generic instance type reference renders with angle-bracket notation
    ///     showing the resolved type arguments.
    /// </summary>
    [Fact]
    public void TypeLinkResolver_Linkify_GenericType_RendersTypeArguments()
    {
        // Arrange: get the return type of ArrayAndNullableClass.GetList() which is List<string>
        var resolver = new TypeLinkResolver(["ApiMark.DotNet.Fixtures"]);
        var externalTypes = new HashSet<ExternalTypeInfo>();
        var arrayClass = _assembly.MainModule.Types.First(t => t.Name == "ArrayAndNullableClass");
        var getListMethod = arrayClass.Methods.First(m => m.Name == "GetList");
        var genericReturnType = getListMethod.ReturnType; // List<string>

        // Act
        var result = resolver.Linkify(genericReturnType, "", "ApiMark.DotNet.Fixtures", externalTypes);

        // Assert: the result contains angle-bracket notation for the type arguments
        Assert.Contains("\\<", result, StringComparison.Ordinal);
        Assert.Contains("\\>", result, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a <c>Nullable&lt;int&gt;</c> generic instance is resolved to the
    ///     inner C# alias with a <c>?</c> suffix (i.e., <c>"int?"</c>).
    /// </summary>
    [Fact]
    public void TypeLinkResolver_Linkify_NullableValueType_ReturnsInnerAliasWithQuestionMark()
    {
        // Arrange: construct Nullable<int> as a GenericInstanceType
        var resolver = new TypeLinkResolver(["ApiMark.DotNet.Fixtures"]);
        var externalTypes = new HashSet<ExternalTypeInfo>();
        var nullableType = new GenericInstanceType(_assembly.MainModule.ImportReference(typeof(Nullable<>)));
        nullableType.GenericArguments.Add(_assembly.MainModule.TypeSystem.Int32);

        // Act
        var result = resolver.Linkify(nullableType, "", "ApiMark.DotNet.Fixtures", externalTypes);

        // Assert: Nullable<int> must render as "int?" — not "Nullable<int>"
        Assert.Equal("int?", result);
    }

    /// <summary>
    ///     Validates that a type from a System sub-namespace (e.g. <c>System.IO.Stream</c>) is
    ///     returned as plain text and is not added to the external types accumulator.
    /// </summary>
    [Fact]
    public void TypeLinkResolver_Linkify_SystemNamespaceExternalType_ReturnsPlainTextAndDoesNotTrack()
    {
        // Arrange: construct a synthetic TypeReference for System.IO.Stream from an external assembly scope
        var resolver = new TypeLinkResolver(["ApiMark.DotNet.Fixtures"]);
        var externalTypes = new HashSet<ExternalTypeInfo>();
        var assemblyRef = new AssemblyNameReference("System.Runtime", new Version(8, 0));
        var typeRef = new TypeReference("System.IO", "Stream", _assembly.MainModule, assemblyRef);

        // Act
        var result = resolver.Linkify(typeRef, "", "ApiMark.DotNet.Fixtures", externalTypes);

        // Assert: plain name returned; System types are not tracked as external dependencies
        Assert.Equal("Stream", result);
        Assert.Empty(externalTypes);
    }

    /// <summary>
    ///     Validates that a multi-dimensional array type reference renders with the correct
    ///     rank suffix appended to the element type name (e.g., <c>[,]</c> for rank-2).
    /// </summary>
    [Fact]
    public void TypeLinkResolver_Linkify_MultiDimensionalArrayType_AppendsRankSuffix()
    {
        // Arrange: wrap System.Int32 in a rank-2 ArrayType to represent int[,]
        var resolver = new TypeLinkResolver(["ApiMark.DotNet.Fixtures"]);
        var externalTypes = new HashSet<ExternalTypeInfo>();
        var rank2ArrayType = new ArrayType(_assembly.MainModule.TypeSystem.Int32, 2);

        // Act
        var result = resolver.Linkify(rank2ArrayType, "", "ApiMark.DotNet.Fixtures", externalTypes);

        // Assert: rank-2 int array must produce "int[,]"
        Assert.Equal("int[,]", result);
    }

    /// <summary>
    ///     Validates that a nullable intra-assembly type reference produces a Markdown link
    ///     with a <c>?</c> suffix when <paramref name="isNullableAnnotated"/> is <see langword="true"/>.
    /// </summary>
    [Fact]
    public void TypeLinkResolver_Linkify_NullableIntraAssemblyType_ReturnsLinkWithQuestionMark()
    {
        // Arrange
        var resolver = new TypeLinkResolver(["ApiMark.DotNet.Fixtures"], generateLinks: true);
        var externalTypes = new HashSet<ExternalTypeInfo>();
        var typeDef = _assembly.MainModule.Types.First(t => t.Name == "SampleClass");

        // Act
        var result = resolver.Linkify(typeDef, "ApiMark.DotNet.Fixtures", "ApiMark.DotNet.Fixtures", externalTypes, isNullableAnnotated: true);

        // Assert: the result must contain a Markdown link AND end with "?"
        Assert.Contains("[", result, StringComparison.Ordinal);
        Assert.EndsWith("?", result, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a byref parameter type (Cecil's <see cref="ByReferenceType"/>, used for
    ///     <c>ref</c>/<c>out</c>/<c>in</c> parameters) resolves to a Markdown link using the
    ///     un-suffixed element type name — never a dead link to a page named with a trailing <c>&amp;</c>.
    /// </summary>
    [Fact]
    public void TypeLinkResolver_Linkify_ByReferenceType_ReturnsLinkWithPlainTypeName()
    {
        // Arrange: construct a byref wrapper around SampleClass, as Cecil does for ref/out/in parameters
        var resolver = new TypeLinkResolver(["ApiMark.DotNet.Fixtures"], generateLinks: true);
        var externalTypes = new HashSet<ExternalTypeInfo>();
        var typeDef = _assembly.MainModule.Types.First(t => t.Name == "SampleClass");
        var byRefType = new ByReferenceType(typeDef);

        // Act
        var result = resolver.Linkify(byRefType, "ApiMark.DotNet.Fixtures", "ApiMark.DotNet.Fixtures", externalTypes);

        // Assert: link target and text must not contain "&" — the real page is SampleClass.md
        Assert.Equal("[SampleClass](SampleClass.md)", result);
    }

    /// <summary>
    ///     Validates that an array type reference with <paramref name="isNullableAnnotated"/> set to
    ///     <see langword="true"/> produces a result ending with <c>[]?</c> — the array rank suffix
    ///     followed by the nullable marker.
    /// </summary>
    [Fact]
    public void TypeLinkResolver_Linkify_NullableArrayType_AppendsArraySuffixThenQuestionMark()
    {
        // Arrange: wrap System.String in an ArrayType to represent string[]
        var resolver = new TypeLinkResolver(["ApiMark.DotNet.Fixtures"]);
        var externalTypes = new HashSet<ExternalTypeInfo>();
        var stringArrayType = new ArrayType(_assembly.MainModule.TypeSystem.String);

        // Act
        var result = resolver.Linkify(stringArrayType, "", "ApiMark.DotNet.Fixtures", externalTypes, isNullableAnnotated: true);

        // Assert: nullable string array must render as "string[]?"
        Assert.EndsWith("[]?", result, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a nested type used as a field type (<c>NestedTypeConsumerClass.Field</c>,
    ///     typed as <see cref="OuterClass.Inner"/>) resolves to the nested type's own page under
    ///     the declaring type's folder, rather than a broken link derived from an empty namespace.
    /// </summary>
    [Fact]
    public void TypeLinkResolver_Linkify_NestedTypeAsFieldType_ResolvesToNestedTypePage()
    {
        // Arrange
        var resolver = new TypeLinkResolver(["ApiMark.DotNet.Fixtures"], generateLinks: true);
        var externalTypes = new HashSet<ExternalTypeInfo>();
        var consumer = _assembly.MainModule.Types.First(t => t.Name == "NestedTypeConsumerClass");
        var fieldType = consumer.Fields.First(f => f.Name == "Field").FieldType;

        // Act
        var result = resolver.Linkify(fieldType, "ApiMark.DotNet.Fixtures", "ApiMark.DotNet.Fixtures", externalTypes);

        // Assert: the link target must point at OuterClass/Inner.md, not Inner.md
        Assert.Equal("[Inner](OuterClass/Inner.md)", result);
    }

    /// <summary>
    ///     Validates that a nested type used as a method return type
    ///     (<c>NestedTypeConsumerClass.GetInner()</c>, returning <see cref="OuterClass.Inner"/>)
    ///     resolves to the nested type's own page.
    /// </summary>
    [Fact]
    public void TypeLinkResolver_Linkify_NestedTypeAsReturnType_ResolvesToNestedTypePage()
    {
        // Arrange
        var resolver = new TypeLinkResolver(["ApiMark.DotNet.Fixtures"], generateLinks: true);
        var externalTypes = new HashSet<ExternalTypeInfo>();
        var consumer = _assembly.MainModule.Types.First(t => t.Name == "NestedTypeConsumerClass");
        var returnType = consumer.Methods.First(m => m.Name == "GetInner").ReturnType;

        // Act
        var result = resolver.Linkify(returnType, "ApiMark.DotNet.Fixtures", "ApiMark.DotNet.Fixtures", externalTypes);

        // Assert
        Assert.Equal("[Inner](OuterClass/Inner.md)", result);
    }

    /// <summary>
    ///     Validates that a nested type used as a method parameter type
    ///     (<c>NestedTypeConsumerClass.Process(OuterClass.Inner)</c>) resolves to the nested
    ///     type's own page.
    /// </summary>
    [Fact]
    public void TypeLinkResolver_Linkify_NestedTypeAsParameterType_ResolvesToNestedTypePage()
    {
        // Arrange
        var resolver = new TypeLinkResolver(["ApiMark.DotNet.Fixtures"], generateLinks: true);
        var externalTypes = new HashSet<ExternalTypeInfo>();
        var consumer = _assembly.MainModule.Types.First(t => t.Name == "NestedTypeConsumerClass");
        var parameterType = consumer.Methods.First(m => m.Name == "Process").Parameters[0].ParameterType;

        // Act
        var result = resolver.Linkify(parameterType, "ApiMark.DotNet.Fixtures", "ApiMark.DotNet.Fixtures", externalTypes);

        // Assert
        Assert.Equal("[Inner](OuterClass/Inner.md)", result);
    }

    /// <summary>
    ///     Validates that a nested type used as a generic type argument
    ///     (<c>NestedTypeConsumerClass.GetInnerList()</c>, returning <c>List&lt;OuterClass.Inner&gt;</c>)
    ///     resolves the generic argument to the nested type's own page.
    /// </summary>
    [Fact]
    public void TypeLinkResolver_Linkify_NestedTypeAsGenericArgument_ResolvesToNestedTypePage()
    {
        // Arrange
        var resolver = new TypeLinkResolver(["ApiMark.DotNet.Fixtures"], generateLinks: true);
        var externalTypes = new HashSet<ExternalTypeInfo>();
        var consumer = _assembly.MainModule.Types.First(t => t.Name == "NestedTypeConsumerClass");
        var returnType = consumer.Methods.First(m => m.Name == "GetInnerList").ReturnType;

        // Act
        var result = resolver.Linkify(returnType, "ApiMark.DotNet.Fixtures", "ApiMark.DotNet.Fixtures", externalTypes);

        // Assert: the generic argument link must resolve to OuterClass/Inner.md within the List<> notation
        Assert.Contains("(OuterClass/Inner.md)", result, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a type nested two levels deep (<see cref="TwoLevelNestedClass.Middle.Inner"/>)
    ///     resolves to a page path that includes every ancestor folder in order
    ///     (<c>TwoLevelNestedClass/Middle/Inner</c>).
    /// </summary>
    [Fact]
    public void TypeLinkResolver_Linkify_TwoLevelsDeepNestedType_ResolvesFullAncestorPath()
    {
        // Arrange
        var resolver = new TypeLinkResolver(["ApiMark.DotNet.Fixtures"], generateLinks: true);
        var externalTypes = new HashSet<ExternalTypeInfo>();
        var outer = _assembly.MainModule.Types.First(t => t.Name == "TwoLevelNestedClass");
        var middle = outer.NestedTypes.First(t => t.Name == "Middle");
        var inner = middle.NestedTypes.First(t => t.Name == "Inner");

        // Act
        var result = resolver.Linkify(inner, "ApiMark.DotNet.Fixtures", "ApiMark.DotNet.Fixtures", externalTypes);

        // Assert
        Assert.Equal("[Inner](TwoLevelNestedClass/Middle/Inner.md)", result);
    }

    /// <summary>
    ///     Validates that linking to a nested type from within its own parent type's page produces
    ///     a short relative path (just the nested type's file name), while linking to the same
    ///     nested type from a completely different type's page produces a path prefixed with the
    ///     parent type's folder.
    /// </summary>
    [Fact]
    public void TypeLinkResolver_Linkify_NestedType_FromParentPageVsOtherPage_ProducesDifferentRelativePaths()
    {
        // Arrange
        var resolver = new TypeLinkResolver(["ApiMark.DotNet.Fixtures"], generateLinks: true);
        var externalTypes = new HashSet<ExternalTypeInfo>();
        var outer = _assembly.MainModule.Types.First(t => t.Name == "OuterClass");
        var inner = outer.NestedTypes.First(t => t.Name == "Inner");

        // Act: link from within OuterClass's own page (currentFolder is the OuterClass folder itself,
        // since nested type pages live one level below their parent's folder context)
        var fromParentPage = resolver.Linkify(inner, "ApiMark.DotNet.Fixtures/OuterClass", "ApiMark.DotNet.Fixtures", externalTypes);

        // Act: link from a completely different type's page at the namespace root
        var fromOtherPage = resolver.Linkify(inner, "ApiMark.DotNet.Fixtures", "ApiMark.DotNet.Fixtures", externalTypes);

        // Assert: the parent-page-relative link is shorter and does not repeat the "OuterClass" folder segment
        Assert.Equal("[Inner](Inner.md)", fromParentPage);
        Assert.Equal("[Inner](OuterClass/Inner.md)", fromOtherPage);
    }

    /// <summary>
    ///     Validates that <see cref="TypeLinkResolver.LinkifyResolvedType"/> produces a Markdown
    ///     link wrapping the supplied display text when link generation is enabled.
    /// </summary>
    [Fact]
    public void TypeLinkResolver_LinkifyResolvedType_GenerateLinksTrue_ReturnsMarkdownLink()
    {
        // Arrange
        var resolver = new TypeLinkResolver(["ApiMark.DotNet.Fixtures"], generateLinks: true);
        var type = _assembly.MainModule.Types.First(t => t.Name == "SampleClass");

        // Act
        var result = resolver.LinkifyResolvedType(type, "SampleClass", "ApiMark.DotNet.Fixtures");

        // Assert
        Assert.Equal("[SampleClass](SampleClass.md)", result);
    }

    /// <summary>
    ///     Validates that <see cref="TypeLinkResolver.LinkifyResolvedType"/> returns the display
    ///     text unchanged when link generation is disabled (single-file mode).
    /// </summary>
    [Fact]
    public void TypeLinkResolver_LinkifyResolvedType_GenerateLinksFalse_ReturnsDisplayTextUnchanged()
    {
        // Arrange
        var resolver = new TypeLinkResolver(["ApiMark.DotNet.Fixtures"], generateLinks: false);
        var type = _assembly.MainModule.Types.First(t => t.Name == "SampleClass");

        // Act
        var result = resolver.LinkifyResolvedType(type, "SampleClass", "ApiMark.DotNet.Fixtures");

        // Assert
        Assert.Equal("SampleClass", result);
    }

    /// <summary>
    ///     Validates that <see cref="TypeLinkResolver.LinkifyResolvedMember"/> produces a Markdown
    ///     link using the supplied member page index when link generation is enabled and the
    ///     member has an entry.
    /// </summary>
    [Fact]
    public void TypeLinkResolver_LinkifyResolvedMember_FoundInIndex_ReturnsMarkdownLink()
    {
        // Arrange
        var resolver = new TypeLinkResolver(["ApiMark.DotNet.Fixtures"], generateLinks: true);
        var type = _assembly.MainModule.Types.First(t => t.Name == "SampleClass");
        var method = type.Methods.First(m => m.Name == "Reset");
        var memberId = DotNetEmitter.BuildMemberId(method);
        var memberPageIndex = new Dictionary<string, string> { [memberId] = "SampleClass/Reset" };

        // Act
        var result = resolver.LinkifyResolvedMember(method, "SampleClass.Reset", "", memberPageIndex);

        // Assert
        Assert.Equal("[SampleClass.Reset](SampleClass/Reset.md)", result);
    }

    /// <summary>
    ///     Validates that <see cref="TypeLinkResolver.LinkifyResolvedMember"/> returns the display
    ///     text unchanged when the member has no entry in the member page index (for example, it
    ///     was filtered out of the generated documentation).
    /// </summary>
    [Fact]
    public void TypeLinkResolver_LinkifyResolvedMember_NotInIndex_ReturnsDisplayTextUnchanged()
    {
        // Arrange
        var resolver = new TypeLinkResolver(["ApiMark.DotNet.Fixtures"], generateLinks: true);
        var type = _assembly.MainModule.Types.First(t => t.Name == "SampleClass");
        var method = type.Methods.First(m => m.Name == "Reset");
        var memberPageIndex = new Dictionary<string, string>();

        // Act
        var result = resolver.LinkifyResolvedMember(method, "SampleClass.Reset", "ApiMark.DotNet.Fixtures", memberPageIndex);

        // Assert
        Assert.Equal("SampleClass.Reset", result);
    }

    /// <summary>
    ///     Validates that <see cref="TypeLinkResolver.LinkifyResolvedMember"/> returns the display
    ///     text unchanged when link generation is disabled (single-file mode), regardless of
    ///     whether the member has an entry in the member page index.
    /// </summary>
    [Fact]
    public void TypeLinkResolver_LinkifyResolvedMember_GenerateLinksFalse_ReturnsDisplayTextUnchanged()
    {
        // Arrange
        var resolver = new TypeLinkResolver(["ApiMark.DotNet.Fixtures"], generateLinks: false);
        var type = _assembly.MainModule.Types.First(t => t.Name == "SampleClass");
        var method = type.Methods.First(m => m.Name == "Reset");
        var memberId = DotNetEmitter.BuildMemberId(method);
        var memberPageIndex = new Dictionary<string, string> { [memberId] = "SampleClass/Reset" };

        // Act
        var result = resolver.LinkifyResolvedMember(method, "SampleClass.Reset", "ApiMark.DotNet.Fixtures", memberPageIndex);

        // Assert
        Assert.Equal("SampleClass.Reset", result);
    }

    /// <summary>Creates a synthetic type reference scoped to an external (non-documented) assembly.</summary>
    /// <param name="ns">The type namespace.</param>
    /// <param name="name">The CLR type name (including any arity suffix).</param>
    /// <param name="assemblyName">The simple name of the referenced assembly.</param>
    /// <returns>The synthetic type reference.</returns>
    private TypeReference MakeExternalType(string ns, string name, string assemblyName = "Acme.Widgets") =>
        new(ns, name, _assembly.MainModule, new AssemblyNameReference(assemblyName, new Version(1, 0)));

    /// <summary>Builds a resolver whose single mapping points <c>Acme.Widgets</c> at <paramref name="apiPath"/>.</summary>
    /// <param name="apiPath">The mapped API root.</param>
    /// <param name="generateLinks">Whether link generation is enabled.</param>
    /// <param name="comparer">Comparer for the caller-supplied dictionary.</param>
    /// <returns>The resolver.</returns>
    private static TypeLinkResolver MakeMappedResolver(
        string apiPath,
        bool generateLinks = true,
        StringComparer? comparer = null) =>
        new(
            ["ApiMark.DotNet.Fixtures"],
            generateLinks: generateLinks,
            externalLinks: new Dictionary<string, TypeLinkResolver.ExternalLinkTarget>(comparer ?? StringComparer.Ordinal)
            {
                ["Acme.Widgets"] = new(apiPath, ["Acme.Widgets"]),
            });

    /// <summary>Builds a cursor from explicit per-position nullable flags.</summary>
    /// <param name="flags">The flags in pre-order.</param>
    /// <returns>The cursor.</returns>
    private NullabilityCursor MakeCursor(params byte[] flags)
    {
        var module = _assembly.MainModule;
        var attributeType = new TypeReference(
            "System.Runtime.CompilerServices", "NullableAttribute", module, module.TypeSystem.CoreLibrary);
        var ctor = new MethodReference(".ctor", module.TypeSystem.Void, attributeType) { HasThis = true };
        var attribute = new CustomAttribute(ctor);
        var elements = flags.Select(f => new CustomAttributeArgument(module.TypeSystem.Byte, f)).ToArray();
        attribute.ConstructorArguments.Add(new CustomAttributeArgument(new ArrayType(module.TypeSystem.Byte), elements));
        return NullabilityCursor.ForAttributes([attribute], 0);
    }

    /// <summary>Validates that a type from an unmapped assembly stays plain text and is tracked as external.</summary>
    [Fact]
    public void TypeLinkResolver_Linkify_UnmappedAssembly_ReturnsPlainTextAndTracksType()
    {
        // Arrange: mapping exists only for a different assembly
        var resolver = MakeMappedResolver("../Other/api");
        var externalTypes = new HashSet<ExternalTypeInfo>();
        var type = MakeExternalType("Example.Gadgets", "Gadget", "Example.Gadgets");

        // Act
        var result = resolver.Linkify(type, "", "ApiMark.DotNet.Fixtures", externalTypes);

        // Assert
        Assert.Equal("Gadget", result);
        Assert.Contains(new ExternalTypeInfo("Gadget", "Example.Gadgets"), externalTypes);
    }

    /// <summary>Validates that an http(s) mapping produces an absolute link that is not made relative.</summary>
    [Fact]
    public void TypeLinkResolver_Linkify_MappedHttpsApiPath_ReturnsAbsoluteLinkAndSkipsTracking()
    {
        // Arrange
        var resolver = MakeMappedResolver("https://docs.example.com/widgets/api/");
        var externalTypes = new HashSet<ExternalTypeInfo>();

        // Act
        var result = resolver.Linkify(MakeExternalType("Acme.Widgets", "Widget"), "A/B", "ApiMark.DotNet.Fixtures", externalTypes);

        // Assert
        Assert.Equal("[Widget](https://docs.example.com/widgets/api/Acme.Widgets/Widget.md)", result);
        Assert.Empty(externalTypes);
    }

    /// <summary>Validates that a relative mapping climbs out of the current folder and normalizes <c>./</c> and backslashes.</summary>
    [Fact]
    public void TypeLinkResolver_Linkify_MappedRelativeApiPath_ClimbsOutOfCurrentFolder()
    {
        // Arrange
        var resolver = MakeMappedResolver(".\\Other\\api");
        var externalTypes = new HashSet<ExternalTypeInfo>();

        // Act
        var result = resolver.Linkify(MakeExternalType("Acme.Widgets", "Widget"), "A/B", "ApiMark.DotNet.Fixtures", externalTypes);

        // Assert
        Assert.Equal("[Widget](../../Other/api/Acme.Widgets/Widget.md)", result);
    }

    /// <summary>Validates that assembly-name lookup is case-insensitive even for a case-sensitive caller dictionary.</summary>
    [Fact]
    public void TypeLinkResolver_Linkify_CaseSensitiveCallerDictionary_StillMatchesCaseInsensitively()
    {
        // Arrange: reference scope name differs in case from the mapping key
        var resolver = MakeMappedResolver("../Other/api", comparer: StringComparer.Ordinal);
        var externalTypes = new HashSet<ExternalTypeInfo>();
        var type = MakeExternalType("Acme.Widgets", "Widget", "ACME.WIDGETS");

        // Act
        var result = resolver.Linkify(type, "", "ApiMark.DotNet.Fixtures", externalTypes);

        // Assert
        Assert.Equal("[Widget](../Other/api/Acme.Widgets/Widget.md)", result);
    }

    /// <summary>Validates that a mapping is ignored when link generation is disabled.</summary>
    [Fact]
    public void TypeLinkResolver_Linkify_MappedAssemblyWithGenerateLinksFalse_ReturnsPlainTextAndTracksType()
    {
        // Arrange
        var resolver = MakeMappedResolver("../Other/api", generateLinks: false);
        var externalTypes = new HashSet<ExternalTypeInfo>();

        // Act
        var result = resolver.Linkify(MakeExternalType("Acme.Widgets", "Widget"), "", "ApiMark.DotNet.Fixtures", externalTypes);

        // Assert
        Assert.Equal("Widget", result);
        Assert.Contains(new ExternalTypeInfo("Widget", "Acme.Widgets"), externalTypes);
    }

    /// <summary>Validates that a mapped generic instance links its container and linkifies its arguments.</summary>
    [Fact]
    public void TypeLinkResolver_Linkify_MappedGenericType_LinksContainerWithArguments()
    {
        // Arrange
        var resolver = MakeMappedResolver("../Other/api");
        var externalTypes = new HashSet<ExternalTypeInfo>();
        var generic = new GenericInstanceType(MakeExternalType("Acme.Widgets", "Box`1"));
        generic.GenericArguments.Add(_assembly.MainModule.TypeSystem.Int32);

        // Act
        var result = resolver.Linkify(generic, "", "ApiMark.DotNet.Fixtures", externalTypes);

        // Assert
        Assert.Equal("[Box](../Other/api/Acme.Widgets/Box1.md)\\<int\\>", result);
        Assert.Empty(externalTypes);
    }

    /// <summary>Validates that a mapped nested type links to its nested page path.</summary>
    [Fact]
    public void TypeLinkResolver_Linkify_MappedNestedType_LinksToNestedPagePath()
    {
        // Arrange
        var resolver = MakeMappedResolver("../Other/api");
        var externalTypes = new HashSet<ExternalTypeInfo>();
        var outer = MakeExternalType("Acme.Widgets", "Outer");
        var inner = new TypeReference(string.Empty, "Inner", _assembly.MainModule, outer.Scope) { DeclaringType = outer };

        // Act
        var result = resolver.Linkify(inner, "", "ApiMark.DotNet.Fixtures", externalTypes);

        // Assert
        Assert.Equal("[Inner](../Other/api/Acme.Widgets/Outer/Inner.md)", result);
    }

    /// <summary>Validates that rooted and <c>file:</c> mappings produce absolute <c>file:</c> links, never relative ones.</summary>
    [Theory]
    [InlineData("/docs/api", "[Widget](file:///docs/api/Acme.Widgets/Widget.md)")]
    [InlineData("file:///docs/api", "[Widget](file:///docs/api/Acme.Widgets/Widget.md)")]
    public void TypeLinkResolver_Linkify_RootedOrFileApiPath_ReturnsAbsoluteFileLink(string apiPath, string expected)
    {
        // Arrange
        var resolver = MakeMappedResolver(apiPath);
        var externalTypes = new HashSet<ExternalTypeInfo>();

        // Act
        var result = resolver.Linkify(MakeExternalType("Acme.Widgets", "Widget"), "A", "ApiMark.DotNet.Fixtures", externalTypes);

        // Assert
        Assert.Equal(expected, result);
    }

    /// <summary>Validates that characters that would break a Markdown link target are percent-encoded.</summary>
    [Fact]
    public void TypeLinkResolver_Linkify_ApiPathWithSpacesAndParentheses_EscapesLinkTarget()
    {
        // Arrange
        var resolver = MakeMappedResolver("../My Lib (v2)/api");
        var externalTypes = new HashSet<ExternalTypeInfo>();

        // Act
        var result = resolver.Linkify(MakeExternalType("Acme.Widgets", "Widget"), "", "ApiMark.DotNet.Fixtures", externalTypes);

        // Assert
        Assert.Equal("[Widget](../My%20Lib%20%28v2%29/api/Acme.Widgets/Widget.md)", result);
    }

    /// <summary>Validates that per-position flags annotate the outer generic type and its argument independently.</summary>
    [Fact]
    public void TypeLinkResolver_Linkify_CursorGenericArgumentFlags_AnnotatesEachPosition()
    {
        // Arrange: List<string> with flags [outer=2, argument=2]
        var resolver = new TypeLinkResolver(["ApiMark.DotNet.Fixtures"]);
        var listType = new GenericInstanceType(_assembly.MainModule.ImportReference(typeof(List<>)));
        listType.GenericArguments.Add(_assembly.MainModule.TypeSystem.String);

        // Act
        var result = resolver.Linkify(listType, "", "ApiMark.DotNet.Fixtures", new HashSet<ExternalTypeInfo>(), MakeCursor(2, 2));

        // Assert
        Assert.Equal("List\\<string?\\>?", result);
    }

    /// <summary>Validates that array element and array flags are consumed in pre-order.</summary>
    [Fact]
    public void TypeLinkResolver_Linkify_CursorArrayFlags_AnnotatesElementNotArray()
    {
        // Arrange: string?[] — array not annotated (1), element annotated (2)
        var resolver = new TypeLinkResolver(["ApiMark.DotNet.Fixtures"]);
        var arrayType = new ArrayType(_assembly.MainModule.TypeSystem.String);

        // Act
        var result = resolver.Linkify(arrayType, "", "ApiMark.DotNet.Fixtures", new HashSet<ExternalTypeInfo>(), MakeCursor(1, 2));

        // Assert
        Assert.Equal("string?[]", result);
    }

    /// <summary>Validates that value types never receive <c>?</c> from the annotation flag, even via the legacy boolean overload.</summary>
    [Fact]
    public void TypeLinkResolver_Linkify_AnnotatedValueType_DoesNotAppendQuestionMark()
    {
        // Arrange
        var resolver = new TypeLinkResolver(["ApiMark.DotNet.Fixtures"]);
        var intType = _assembly.MainModule.TypeSystem.Int32;

        // Act
        var viaCursor = resolver.Linkify(intType, "", "ApiMark.DotNet.Fixtures", new HashSet<ExternalTypeInfo>(), MakeCursor(2));
        var viaBool = resolver.Linkify(intType, "", "ApiMark.DotNet.Fixtures", new HashSet<ExternalTypeInfo>(), isNullableAnnotated: true);

        // Assert
        Assert.Equal("int", viaCursor);
        Assert.Equal("int", viaBool);
    }

    /// <summary>Validates that an exhausted flag array falls back to oblivious (no annotation) for later positions.</summary>
    [Fact]
    public void TypeLinkResolver_Linkify_CursorExhausted_LaterPositionsUnannotated()
    {
        // Arrange: List<string> with the legacy single-position cursor (outer annotated only)
        var resolver = new TypeLinkResolver(["ApiMark.DotNet.Fixtures"]);
        var listType = new GenericInstanceType(_assembly.MainModule.ImportReference(typeof(List<>)));
        listType.GenericArguments.Add(_assembly.MainModule.TypeSystem.String);

        // Act
        var result = resolver.Linkify(
            listType, "", "ApiMark.DotNet.Fixtures", new HashSet<ExternalTypeInfo>(), NullabilityCursor.ForOutermostFlag(true));

        // Assert
        Assert.Equal("List\\<string\\>?", result);
    }

    /// <summary>Validates that a byref wrapper consumes no flag, so the first flag applies to the element type.</summary>
    [Fact]
    public void TypeLinkResolver_Linkify_CursorWithByReference_ByRefConsumesNoFlag()
    {
        // Arrange
        var resolver = new TypeLinkResolver(["ApiMark.DotNet.Fixtures"]);
        var byRefString = new ByReferenceType(_assembly.MainModule.TypeSystem.String);

        // Act
        var result = resolver.Linkify(byRefString, "", "ApiMark.DotNet.Fixtures", new HashSet<ExternalTypeInfo>(), MakeCursor(2));

        // Assert
        Assert.Equal("string?", result);
    }

    /// <summary>Validates that a reference to a global-namespace type links to its global-namespace page.</summary>
    [Fact]
    public void TypeLinkResolver_Linkify_GlobalNamespaceType_LinksToGlobalNamespacePage()
    {
        // Arrange
        var resolver = new TypeLinkResolver(["", "ApiMark.DotNet.Fixtures"]);
        var typeRef = new TypeReference("", "GlobalNamespaceClass", _assembly.MainModule, _assembly.MainModule);

        // Act
        var result = resolver.Linkify(typeRef, "", "", new HashSet<ExternalTypeInfo>());

        // Assert
        Assert.Equal("[GlobalNamespaceClass](global-namespace/GlobalNamespaceClass.md)", result);
    }
}
