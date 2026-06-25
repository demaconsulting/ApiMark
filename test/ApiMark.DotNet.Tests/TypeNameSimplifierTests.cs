using ApiMark.DotNet;
using Mono.Cecil;
using Xunit;

namespace ApiMark.DotNet.Tests;

/// <summary>Unit tests for <see cref="TypeNameSimplifier"/>.</summary>
public class TypeNameSimplifierTests
{
    /// <summary>Fixture assembly shared across all tests in this class.</summary>
    private static readonly AssemblyDefinition FixtureAssembly =
        AssemblyDefinition.ReadAssembly(FixturePaths.GetFixtureDll());

    /// <summary>Resolves a type by simple name from the fixture assembly.</summary>
    /// <param name="name">The simple (unqualified) type name to look up.</param>
    /// <returns>The matching <see cref="TypeDefinition"/>.</returns>
    private static TypeDefinition GetType(string name) =>
        FixtureAssembly.MainModule.Types.First(t => t.Name == name);

    /// <summary>Validates that all 16 CLR primitive type names are simplified to their C# keyword aliases.</summary>
    [Theory]
    [InlineData("System.Boolean", "bool")]
    [InlineData("System.Byte", "byte")]
    [InlineData("System.SByte", "sbyte")]
    [InlineData("System.Int16", "short")]
    [InlineData("System.UInt16", "ushort")]
    [InlineData("System.Int32", "int")]
    [InlineData("System.UInt32", "uint")]
    [InlineData("System.Int64", "long")]
    [InlineData("System.UInt64", "ulong")]
    [InlineData("System.Single", "float")]
    [InlineData("System.Double", "double")]
    [InlineData("System.Decimal", "decimal")]
    [InlineData("System.Char", "char")]
    [InlineData("System.String", "string")]
    [InlineData("System.Object", "object")]
    [InlineData("System.Void", "void")]
    public void TypeNameSimplifier_Simplify_Primitives_RenderLanguageAliases(string clrName, string expected)
    {
        // Arrange: construct a TypeReference from the CLR full name using the fixture module
        var parts = clrName.Split('.');
        var ns = string.Join('.', parts[..^1]);
        var name = parts[^1];
        var typeRef = new TypeReference(ns, name, FixtureAssembly.MainModule, null);

        // Act
        var result = TypeNameSimplifier.Simplify(typeRef, "ApiMark.DotNet.Fixtures");

        // Assert: CLR primitive name is replaced by the C# keyword alias
        Assert.Equal(expected, result);
    }

    /// <summary>Validates that array types are rendered using C# bracket notation rather than CLR array syntax.</summary>
    [Fact]
    public void TypeNameSimplifier_Simplify_ArrayType_ReturnsBracketNotation()
    {
        // Arrange: GetNames() returns string[]
        var arrayClass = GetType("ArrayAndNullableClass");
        var getNamesMethod = arrayClass.Methods.First(m => m.Name == "GetNames");
        var returnType = getNamesMethod.ReturnType;

        // Act
        var result = TypeNameSimplifier.Simplify(returnType, "ApiMark.DotNet.Fixtures");

        // Assert
        Assert.Equal("string[]", result);
    }

    /// <summary>Validates that <c>Nullable&lt;T&gt;</c> value types are rendered as <c>T?</c>.</summary>
    [Fact]
    public void TypeNameSimplifier_Simplify_NullableValueTypes_UseQuestionMarkForm()
    {
        // Arrange: GetCount() returns int? (Nullable<int>)
        var arrayClass = GetType("ArrayAndNullableClass");
        var getCountMethod = arrayClass.Methods.First(m => m.Name == "GetCount");
        var returnType = getCountMethod.ReturnType;

        // Act
        var result = TypeNameSimplifier.Simplify(returnType, "ApiMark.DotNet.Fixtures");

        // Assert: Nullable<int> renders as int?
        Assert.Equal("int?", result);
    }

    /// <summary>Validates that types from well-known namespaces are rendered without their namespace prefix.</summary>
    [Fact]
    public void TypeNameSimplifier_Simplify_WellKnownNamespaceTypes_RenderWithoutNamespace()
    {
        // Arrange: GetList() returns List<string> (System.Collections.Generic.List)
        var arrayClass = GetType("ArrayAndNullableClass");
        var getListMethod = arrayClass.Methods.First(m => m.Name == "GetList");
        var returnType = getListMethod.ReturnType;

        // Act
        var result = TypeNameSimplifier.Simplify(returnType, "ApiMark.DotNet.Fixtures");

        // Assert: System.Collections.Generic prefix is stripped
        Assert.Equal("List<string>", result);
    }

    /// <summary>Validates that types in the same namespace as the context drop their shared namespace prefix.</summary>
    [Fact]
    public void TypeNameSimplifier_Simplify_ContextNamespaceTypes_RenderWithoutSharedPrefix()
    {
        // Arrange: get a type from ApiMark.DotNet.Fixtures namespace and use same namespace as context
        var sampleClass = GetType("SampleClass");
        // SampleClass is in ApiMark.DotNet.Fixtures — when context is same namespace, just the name
        var typeRef = (TypeReference)sampleClass;

        // Act: context namespace matches the type's namespace
        var result = TypeNameSimplifier.Simplify(typeRef, "ApiMark.DotNet.Fixtures");

        // Assert: shared namespace prefix is stripped
        Assert.Equal("SampleClass", result);
    }

    /// <summary>Validates that generic type arguments are recursively simplified along with their container type.</summary>
    [Fact]
    public void TypeNameSimplifier_Simplify_GenericArguments_AreSimplifiedRecursively()
    {
        // Arrange: GetAsync() returns Task<bool>
        var arrayClass = GetType("ArrayAndNullableClass");
        var getAsyncMethod = arrayClass.Methods.First(m => m.Name == "GetAsync");
        var returnType = getAsyncMethod.ReturnType;

        // Act
        var result = TypeNameSimplifier.Simplify(returnType, "ApiMark.DotNet.Fixtures");

        // Assert: Task from System.Threading.Tasks is simplified; bool is a C# alias
        Assert.Equal("Task<bool>", result);
    }

    /// <summary>Validates that a non-annotated reference type does not spuriously receive a <c>?</c> suffix.</summary>
    [Fact]
    public void TypeNameSimplifier_Simplify_NonAnnotatedReferenceType_DoesNotAppendQuestionMark()
    {
        // Arrange: a plain reference type (string) in the fixture assembly
        // Non-nullable reference types must NOT receive a spurious ? suffix.
        // The TypeNameSimplifier checks NullableAttribute on TypeDefinition;
        // a bare string TypeReference carries no NullableAttribute so no ? is appended.
        var sampleClass = GetType("SampleClass");
        var nameProperty = sampleClass.Properties.First(p => p.Name == "Name");
        var propertyType = nameProperty.PropertyType; // System.String (not nullable at type level)

        // Act
        var result = TypeNameSimplifier.Simplify(propertyType, "ApiMark.DotNet.Fixtures");

        // Assert: non-annotated reference type does not get a spurious ? suffix
        Assert.Equal("string", result);
    }

    /// <summary>Validates that a reference type passed with <c>isNullableAnnotated: true</c> receives a <c>?</c> suffix.</summary>
    [Fact]
    public void TypeNameSimplifier_Simplify_NullableAnnotatedReferenceType_AppendsQuestionMark()
    {
        // Arrange: use a plain string TypeReference and simulate a NullableAttribute(2) annotation.
        // In production, DotNetGenerator detects NullableAttribute on the enclosing member and passes
        // isNullableAnnotated: true because Mono.Cecil stores the annotation on the member, not the type.
        var sampleClass = GetType("SampleClass");
        var nameProperty = sampleClass.Properties.First(p => p.Name == "Name");
        var propertyType = nameProperty.PropertyType; // System.String

        // Act: pass isNullableAnnotated = true to exercise Rule 7
        var result = TypeNameSimplifier.Simplify(propertyType, "ApiMark.DotNet.Fixtures", isNullableAnnotated: true);

        // Assert: the ? suffix is appended because string is a reference type
        Assert.Equal("string?", result);
    }

    /// <summary>Validates that passing <c>null</c> as the context namespace does not throw.</summary>
    [Fact]
    public void TypeNameSimplifier_Simplify_NullContextNamespace_DoesNotThrow()
    {
        // Arrange: any type reference
        var typeRef = FixtureAssembly.MainModule.TypeSystem.String;

        // Act: passing null as contextNamespace must not throw — safe fallback expected
        var result = TypeNameSimplifier.Simplify(typeRef, null!);

        // Assert: a non-null result is returned
        Assert.NotNull(result);
    }

    /// <summary>Validates that a multi-dimensional array type is rendered with the correct rank suffix.</summary>
    [Fact]
    public void TypeNameSimplifier_Simplify_MultiDimensionalArray_AppendsRankSuffix()
    {
        // Arrange: construct a rank-2 int array (int[,]) using Mono.Cecil
        var rank2Array = new ArrayType(FixtureAssembly.MainModule.TypeSystem.Int32, 2);

        // Act
        var result = TypeNameSimplifier.Simplify(rank2Array, "");

        // Assert: rank-2 int array must simplify to "int[,]"
        Assert.Equal("int[,]", result);
    }

    /// <summary>
    ///     Validates that a type in a namespace nested under the context namespace has the
    ///     shared prefix stripped, leaving only the unshared suffix prepended to the type name.
    /// </summary>
    [Fact]
    public void TypeNameSimplifier_Simplify_ContextNamespaceTypes_NestedNamespace_StripsSharedPrefix()
    {
        // Arrange: context = "A.B", type namespace = "A.B.C", type name = "FooType"
        // Expected: the shared "A.B." prefix is stripped, leaving "C.FooType"
        var typeRef = new TypeReference("A.B.C", "FooType", FixtureAssembly.MainModule, null);

        // Act
        var result = TypeNameSimplifier.Simplify(typeRef, "A.B");

        // Assert: shared "A.B." prefix is stripped; only "C.FooType" remains
        Assert.Equal("C.FooType", result);
    }
}
