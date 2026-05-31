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

    /// <summary>Validates that CLR primitive type names are simplified to their C# keyword aliases.</summary>
    [Fact]
    public void TypeNameSimplifier_Primitives_RenderLanguageAliases()
    {
        // Arrange: load string, int, bool types from the fixture assembly
        var sampleClass = GetType("SampleClass");
        var greetingMethod = sampleClass.Methods.First(m => m.Name == "GetGreeting");
        var stringParam = greetingMethod.Parameters[0].ParameterType; // System.String

        var arrayClass = GetType("ArrayAndNullableClass");
        var getCountMethod = arrayClass.Methods.First(m => m.Name == "GetCount");
        // int? is Nullable<int>; verify the inner int
        var countReturn = getCountMethod.ReturnType; // Nullable<int>

        // Act
        var stringResult = TypeNameSimplifier.Simplify(stringParam, "ApiMark.DotNet.Fixtures");
        var countResult = TypeNameSimplifier.Simplify(countReturn, "ApiMark.DotNet.Fixtures");

        // Assert: CLR primitive names are replaced by C# aliases
        Assert.Equal("string", stringResult);
        Assert.Equal("int?", countResult); // Nullable<int> → int?
    }

    /// <summary>Validates that array types are rendered using C# bracket notation rather than CLR array syntax.</summary>
    [Fact]
    public void TypeNameSimplifier_ArrayType_ReturnsBracketNotation()
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
    public void TypeNameSimplifier_NullableValueTypes_UseQuestionMarkForm()
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
    public void TypeNameSimplifier_WellKnownNamespaceTypes_RenderWithoutNamespace()
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
    public void TypeNameSimplifier_ContextNamespaceTypes_RenderWithoutSharedPrefix()
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
    public void TypeNameSimplifier_GenericArguments_AreSimplifiedRecursively()
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
}
