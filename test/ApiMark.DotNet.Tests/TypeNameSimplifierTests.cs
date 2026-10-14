using System.Reflection;
using ApiMark.DotNet;
using Mono.Cecil;
using Xunit;
using GenericParameterAttributes = Mono.Cecil.GenericParameterAttributes;
using MethodAttributes = Mono.Cecil.MethodAttributes;
using ParameterAttributes = Mono.Cecil.ParameterAttributes;
using TypeAttributes = Mono.Cecil.TypeAttributes;

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

    /// <summary>
    ///     Validates that a byref parameter type (Cecil's <see cref="ByReferenceType"/>, used for
    ///     <c>ref</c>/<c>out</c>/<c>in</c> parameters) is unwrapped to its element type name rather
    ///     than rendering the raw trailing <c>&amp;</c> marker.
    /// </summary>
    [Fact]
    public void TypeNameSimplifier_Simplify_ByReferenceType_UnwrapsToElementTypeName()
    {
        // Arrange: construct a byref wrapper around SampleClass, as Cecil does for ref/out/in parameters
        var sampleClass = GetType("SampleClass");
        var byRefType = new ByReferenceType(sampleClass);

        // Act
        var result = TypeNameSimplifier.Simplify(byRefType, "ApiMark.DotNet.Fixtures");

        // Assert: the "&" marker must not appear in the simplified name
        Assert.Equal("SampleClass", result);
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

    // =========================================================================
    // Nullability oracle: compare against System.Reflection.NullabilityInfoContext
    // =========================================================================

    /// <summary>Fixture types whose members are compared against the reflection oracle.</summary>
    private static readonly string[] OracleTypeNames =
    [
        "NullabilityDiagramElement",
        "NullabilityMostlyNullableElement",
        "NullabilityObliviousElement",
        "ArrayAndNullableClass",
    ];

    /// <summary>Renders a reflection type using <see cref="NullabilityInfo"/> in the same style as <see cref="TypeNameSimplifier"/>.</summary>
    /// <param name="type">The reflection type.</param>
    /// <param name="info">The nullability info for the type position.</param>
    /// <returns>The rendered type name.</returns>
    private static string RenderOracle(Type type, NullabilityInfo info)
    {
        if (type.IsByRef)
        {
            return RenderOracle(type.GetElementType()!, info.ElementType ?? info);
        }

        var question = info.ReadState == NullabilityState.Nullable && !type.IsGenericParameter ? "?" : string.Empty;
        if (type.IsArray)
        {
            return RenderOracle(type.GetElementType()!, info.ElementType!) + "[]" + question;
        }

        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying != null)
        {
            return RenderOracle(underlying, info) + "?";
        }

        if (type.IsGenericType)
        {
            var name = type.Name.Substring(0, type.Name.IndexOf('`'));
            var args = string.Join(", ", type.GetGenericArguments().Select((a, i) => RenderOracle(a, info.GenericTypeArguments[i])));
            return $"{name}<{args}>{(type.IsValueType ? string.Empty : question)}";
        }

        var simple = type == typeof(string) ? "string" : type == typeof(int) ? "int" : type == typeof(bool) ? "bool" : type.Name;
        return simple + (type.IsValueType ? string.Empty : question);
    }

    /// <summary>Validates that nullable flags rendered for every fixture member agree with the reflection oracle.</summary>
    [Fact]
    public void TypeNameSimplifier_Simplify_NullabilityFixtures_MatchReflectionOracle()
    {
        // Arrange
        var context = new NullabilityInfoContext();
        var reflectionAssembly = typeof(Fixtures.SampleClass).Assembly;
        const string ns = "ApiMark.DotNet.Fixtures";
        var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        var mismatches = new List<string>();
        var compared = 0;

        void Compare(string where, string expected, string actual)
        {
            compared++;
            if (expected != actual)
            {
                mismatches.Add($"{where}: expected '{expected}' but was '{actual}'");
            }
        }

        foreach (var typeName in OracleTypeNames)
        {
            var cecilType = FixtureAssembly.MainModule.Types.Single(t => t.Name == typeName);
            var reflectionType = reflectionAssembly.GetTypes().Single(t => t.Name == typeName);

            foreach (var prop in reflectionType.GetProperties(flags))
            {
                var cecilProp = cecilType.Properties.Single(p => p.Name == prop.Name);
                Compare(
                    $"{typeName}.{prop.Name}",
                    RenderOracle(prop.PropertyType, context.Create(prop)),
                    TypeNameSimplifier.Simplify(cecilProp.PropertyType, ns, DotNetEmitter.CreateNullability(cecilProp.CustomAttributes, cecilProp)));
            }

            foreach (var field in reflectionType.GetFields(flags))
            {
                var cecilField = cecilType.Fields.Single(f => f.Name == field.Name);
                Compare(
                    $"{typeName}.{field.Name}",
                    RenderOracle(field.FieldType, context.Create(field)),
                    TypeNameSimplifier.Simplify(cecilField.FieldType, ns, DotNetEmitter.CreateNullability(cecilField.CustomAttributes, cecilField)));
            }

            foreach (var evt in reflectionType.GetEvents(flags))
            {
                var cecilEvent = cecilType.Events.Single(e => e.Name == evt.Name);
                Compare(
                    $"{typeName}.{evt.Name}",
                    RenderOracle(evt.EventHandlerType!, context.Create(evt)),
                    TypeNameSimplifier.Simplify(cecilEvent.EventType, ns, DotNetEmitter.CreateNullability(cecilEvent.CustomAttributes, cecilEvent)));
            }

            foreach (var method in reflectionType.GetMethods(flags).Where(m => !m.IsSpecialName))
            {
                var cecilMethod = cecilType.Methods.Single(m => m.Name == method.Name);
                Compare(
                    $"{typeName}.{method.Name} return",
                    RenderOracle(method.ReturnType, context.Create(method.ReturnParameter)),
                    TypeNameSimplifier.Simplify(cecilMethod.ReturnType, ns, DotNetEmitter.CreateNullability(cecilMethod.MethodReturnType.CustomAttributes, cecilMethod)));

                foreach (var parameter in method.GetParameters())
                {
                    var cecilParameter = cecilMethod.Parameters.Single(p => p.Name == parameter.Name);
                    Compare(
                        $"{typeName}.{method.Name}({parameter.Name})",
                        RenderOracle(parameter.ParameterType, context.Create(parameter)),
                        TypeNameSimplifier.Simplify(cecilParameter.ParameterType, ns, DotNetEmitter.CreateNullability(cecilParameter.CustomAttributes, cecilMethod)));
                }
            }
        }

        // Assert
        Assert.True(compared > 30, $"Expected the oracle to compare many positions but compared {compared}.");
        Assert.True(mismatches.Count == 0, string.Join(Environment.NewLine, mismatches));
    }

    /// <summary>Validates the nullable annotations on generic arguments of a function-typed property.</summary>
    [Fact]
    public void TypeNameSimplifier_Simplify_FuncWithNullableResult_AnnotatesInnerAndOuter()
    {
        // Arrange
        var type = GetType("NullabilityDiagramElement");
        var method = type.Methods.Single(m => m.Name == "Style");

        // Act
        var result = TypeNameSimplifier.Simplify(
            method.ReturnType,
            "ApiMark.DotNet.Fixtures",
            DotNetEmitter.CreateNullability(method.MethodReturnType.CustomAttributes, method));

        // Assert
        Assert.Equal("Func<NullWire, NullWireStyle?>?", result);
    }

    /// <summary>Validates that nullable annotations on generic type parameters are rendered (reflection reports unconstrained parameters as always nullable, so no oracle).</summary>
    [Fact]
    public void TypeNameSimplifier_Simplify_GenericParameterAnnotations_RenderQuestionMarks()
    {
        // Arrange
        var type = GetType("NullabilityGenericElement`1");
        string Render(string name)
        {
            var p = type.Properties.Single(x => x.Name == name);
            return TypeNameSimplifier.Simplify(p.PropertyType, "ApiMark.DotNet.Fixtures", DotNetEmitter.CreateNullability(p.CustomAttributes, p));
        }

        // Act / Assert
        Assert.Equal("T?", Render("Value"));
        Assert.Equal("List<T>", Render("Items"));
        Assert.Equal("List<T?>?", Render("MaybeItems"));
    }

    // =========================================================================
    // Synthesized Cecil types (no fixture dependency for the type shapes under test)
    // =========================================================================

    /// <summary>Synthesized module that owns every type reference created by the synthesized-type tests.</summary>
    private static readonly ModuleDefinition SynthModule =
        ModuleDefinition.CreateModule("TypeNameSimplifierSynth", ModuleKind.Dll);

    /// <summary>Creates a synthesized nullability attribute with a single byte argument.</summary>
    /// <param name="attributeName">The simple attribute type name.</param>
    /// <param name="value">The byte argument.</param>
    /// <returns>The custom attribute.</returns>
    private static CustomAttribute MakeByteAttribute(string attributeName, byte value)
    {
        var attrType = new TypeReference("System.Runtime.CompilerServices", attributeName, SynthModule, SynthModule.TypeSystem.CoreLibrary);
        var ctor = new MethodReference(".ctor", SynthModule.TypeSystem.Void, attrType);
        var attribute = new CustomAttribute(ctor);
        attribute.ConstructorArguments.Add(new CustomAttributeArgument(SynthModule.TypeSystem.Byte, value));
        return attribute;
    }

    /// <summary>Creates a synthesized <c>NullableAttribute</c> with a per-position byte array.</summary>
    /// <param name="flags">The per-position flags.</param>
    /// <returns>The custom attribute.</returns>
    private static CustomAttribute MakeFlagArrayAttribute(params byte[] flags)
    {
        var attrType = new TypeReference("System.Runtime.CompilerServices", "NullableAttribute", SynthModule, SynthModule.TypeSystem.CoreLibrary);
        var ctor = new MethodReference(".ctor", SynthModule.TypeSystem.Void, attrType);
        var attribute = new CustomAttribute(ctor);
        var elements = flags.Select(f => new CustomAttributeArgument(SynthModule.TypeSystem.Byte, f)).ToArray();
        attribute.ConstructorArguments.Add(new CustomAttributeArgument(new ArrayType(SynthModule.TypeSystem.Byte), elements));
        return attribute;
    }

    /// <summary>Creates <c>List&lt;string&gt;</c> as a generic instance type.</summary>
    /// <returns>The generic instance type reference.</returns>
    private static GenericInstanceType MakeListOfString()
    {
        var list = new TypeReference("System.Collections.Generic", "List`1", SynthModule, SynthModule.TypeSystem.CoreLibrary);
        var instance = new GenericInstanceType(list);
        instance.GenericArguments.Add(SynthModule.TypeSystem.String);
        return instance;
    }

    /// <summary>Creates a synthesized class with one method returning <paramref name="returnType"/> and taking one parameter of the same type.</summary>
    /// <param name="returnType">The return and parameter type.</param>
    /// <param name="typeContext">The type-level NullableContext byte, or <see langword="null"/> for none.</param>
    /// <param name="methodContext">The method-level NullableContext byte, or <see langword="null"/> for none.</param>
    /// <returns>The synthesized method.</returns>
    private static MethodDefinition MakeMethod(TypeReference returnType, byte? typeContext, byte? methodContext)
    {
        var type = new TypeDefinition("Synth", "Holder", TypeAttributes.Public | TypeAttributes.Class, SynthModule.TypeSystem.Object);
        SynthModule.Types.Add(type);
        var method = new MethodDefinition("M", MethodAttributes.Public, returnType);
        method.Parameters.Add(new ParameterDefinition("p", ParameterAttributes.None, returnType));
        type.Methods.Add(method);
        if (typeContext.HasValue)
        {
            type.CustomAttributes.Add(MakeByteAttribute("NullableContextAttribute", typeContext.Value));
        }

        if (methodContext.HasValue)
        {
            method.CustomAttributes.Add(MakeByteAttribute("NullableContextAttribute", methodContext.Value));
        }

        return method;
    }

    /// <summary>Validates that a by-reference type with a nullable flag unwraps to the element type and still applies the <c>?</c> suffix, without the byref consuming a flag.</summary>
    [Fact]
    public void TypeNameSimplifier_Simplify_ByReferenceWithNullability_AnnotatesElementType()
    {
        // Arrange: ref string? with a single flag of 2; ref List<string?> with flags [1, 2]
        var byRefString = new ByReferenceType(SynthModule.TypeSystem.String);
        var byRefList = new ByReferenceType(MakeListOfString());
        var listCursor = NullabilityCursor.ForAttributes([MakeFlagArrayAttribute(1, 2)], 0);

        // Act
        var simple = TypeNameSimplifier.Simplify(byRefString, "Synth", NullabilityCursor.ForOutermostFlag(true));
        var generic = TypeNameSimplifier.Simplify(byRefList, "Synth", listCursor);

        // Assert
        Assert.Equal("string?", simple);
        Assert.Equal("List<string?>", generic);
    }

    /// <summary>Validates that nullable flags are applied per position on a multi-dimensional array (element and array positions).</summary>
    [Fact]
    public void TypeNameSimplifier_Simplify_MultiDimensionalArrayWithNullability_AnnotatesPerPosition()
    {
        // Arrange: string[,] with flags in pre-order [array, element]
        var array = new ArrayType(SynthModule.TypeSystem.String, 2);

        // Act
        var nullableArray = TypeNameSimplifier.Simplify(array, "Synth", NullabilityCursor.ForAttributes([MakeFlagArrayAttribute(2, 1)], 0));
        var nullableElement = TypeNameSimplifier.Simplify(array, "Synth", NullabilityCursor.ForAttributes([MakeFlagArrayAttribute(1, 2)], 0));

        // Assert
        Assert.Equal("string[,]?", nullableArray);
        Assert.Equal("string?[,]", nullableElement);
    }

    /// <summary>Validates that a struct-constrained generic parameter never receives a <c>?</c> suffix even when the inherited flag is 2, while an unconstrained one does.</summary>
    [Fact]
    public void TypeNameSimplifier_Simplify_StructConstrainedGenericParameter_InheritedFlagTwo_NoQuestionMark()
    {
        // Arrange
        var owner = new TypeDefinition("Synth", "Generic`2", TypeAttributes.Public | TypeAttributes.Class, SynthModule.TypeSystem.Object);
        var structParameter = new GenericParameter("TStruct", owner)
        {
            Attributes = GenericParameterAttributes.NotNullableValueTypeConstraint,
        };
        var plainParameter = new GenericParameter("TPlain", owner);
        var inherited = NullabilityCursor.ForAttributes([], 2);

        // Act
        var structResult = TypeNameSimplifier.Simplify(structParameter, "Synth", inherited);
        var plainResult = TypeNameSimplifier.Simplify(plainParameter, "Synth", NullabilityCursor.ForAttributes([], 2));

        // Assert
        Assert.Equal("TStruct", structResult);
        Assert.Equal("TPlain?", plainResult);
    }

    /// <summary>Validates that the type-level <c>NullableContext</c> is inherited by members without their own attribute.</summary>
    [Fact]
    public void TypeNameSimplifier_Simplify_NullableContextOnType_IsInherited()
    {
        // Arrange: context 2 on the type, none on the method
        var method = MakeMethod(SynthModule.TypeSystem.String, typeContext: 2, methodContext: null);

        // Act
        var returnType = TypeNameSimplifier.Simplify(method.ReturnType, "Synth", DotNetEmitter.CreateNullability(method.MethodReturnType.CustomAttributes, method));
        var parameter = TypeNameSimplifier.Simplify(method.Parameters[0].ParameterType, "Synth", DotNetEmitter.CreateNullability(method.Parameters[0].CustomAttributes, method));

        // Assert
        Assert.Equal("string?", returnType);
        Assert.Equal("string?", parameter);
    }

    /// <summary>Validates that a method-level <c>NullableContext</c> overrides the type-level one.</summary>
    [Fact]
    public void TypeNameSimplifier_Simplify_NullableContextOnMethod_OverridesTypeContext()
    {
        // Arrange: type says annotated (2), method says not annotated (1)
        var method = MakeMethod(SynthModule.TypeSystem.String, typeContext: 2, methodContext: 1);

        // Act
        var result = TypeNameSimplifier.Simplify(method.ReturnType, "Synth", DotNetEmitter.CreateNullability(method.MethodReturnType.CustomAttributes, method));

        // Assert
        Assert.Equal("string", result);
    }

    /// <summary>Validates that an explicit <c>NullableAttribute</c> on the position overrides the inherited context.</summary>
    [Fact]
    public void TypeNameSimplifier_Simplify_ExplicitNullableAttribute_OverridesContext()
    {
        // Arrange: context 2, explicit single-byte attribute 1 on the parameter
        var method = MakeMethod(SynthModule.TypeSystem.String, typeContext: 2, methodContext: null);
        var parameter = method.Parameters[0];
        parameter.CustomAttributes.Add(MakeByteAttribute("NullableAttribute", 1));

        // Act
        var result = TypeNameSimplifier.Simplify(parameter.ParameterType, "Synth", DotNetEmitter.CreateNullability(parameter.CustomAttributes, method));

        // Assert
        Assert.Equal("string", result);
    }

    /// <summary>Validates that an exhausted per-position flag array falls back to the context default for remaining positions.</summary>
    [Fact]
    public void TypeNameSimplifier_Simplify_ExhaustedFlagArray_UsesContextDefault()
    {
        // Arrange: List<string> needs two flags but only one is supplied; context default is 2
        var cursor = NullabilityCursor.ForAttributes([MakeFlagArrayAttribute(1)], 2);

        // Act
        var result = TypeNameSimplifier.Simplify(MakeListOfString(), "Synth", cursor);

        // Assert: outer uses flag 1 (no suffix); the argument falls back to context 2
        Assert.Equal("List<string?>", result);
    }

    /// <summary>Validates the exact output when the context namespace is null.</summary>
    [Fact]
    public void TypeNameSimplifier_Simplify_NullContextNamespace_ReturnsExactNames()
    {
        // Arrange
        var custom = new TypeReference("A.B", "Foo`1", SynthModule, null);

        // Act / Assert: primitives alias, other types reduce to the simple name without arity
        Assert.Equal("string", TypeNameSimplifier.Simplify(SynthModule.TypeSystem.String, null!));
        Assert.Equal("Foo", TypeNameSimplifier.Simplify(custom, null!));
        Assert.Equal("List<string>", TypeNameSimplifier.Simplify(MakeListOfString(), null!));
    }

    /// <summary>Validates that an empty (global) context namespace strips nothing and does not throw.</summary>
    [Fact]
    public void TypeNameSimplifier_Simplify_GlobalContextNamespace_DoesNotStripOrThrow()
    {
        // Arrange
        var global = new TypeReference("", "GlobalType", SynthModule, null);
        var namespaced = new TypeReference("A.B", "Foo", SynthModule, null);

        // Act / Assert
        Assert.Equal("GlobalType", TypeNameSimplifier.Simplify(global, ""));
        Assert.Equal("Foo", TypeNameSimplifier.Simplify(namespaced, ""));
        Assert.Equal("List<string>", TypeNameSimplifier.Simplify(MakeListOfString(), ""));
    }
}
