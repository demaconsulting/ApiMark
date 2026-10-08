// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

using ApiMark.DotNet;
using Mono.Cecil;
using Xunit;

namespace ApiMark.DotNet.Tests;

/// <summary>Unit tests for <see cref="CrefTargetResolver"/>.</summary>
public class CrefTargetResolverTests : IDisposable
{
    private readonly AssemblyDefinition _assembly;
    private readonly CrefTargetResolver _resolver;

    /// <summary>Initializes the test fixture by loading the fixture assembly and building the resolver.</summary>
    public CrefTargetResolverTests()
    {
        _assembly = AssemblyDefinition.ReadAssembly(FixturePaths.GetFixtureDll());
        _resolver = new CrefTargetResolver(_assembly);
    }

    /// <summary>Disposes the loaded assembly after each test.</summary>
    public void Dispose()
    {
        _assembly.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>Validates that a top-level type declared in the assembly resolves by its XML-doc type identifier.</summary>
    [Fact]
    public void CrefTargetResolver_TryResolveType_TopLevelType_ReturnsTrueAndType()
    {
        // Arrange
        var expected = _assembly.MainModule.Types.First(t => t.Name == "SampleClass");
        var crefId = DotNetEmitter.BuildTypeId(expected);

        // Act
        var resolved = _resolver.TryResolveType(crefId, out var type);

        // Assert
        Assert.True(resolved);
        Assert.Same(expected, type);
    }

    /// <summary>Validates that a nested type resolves by its XML-doc type identifier.</summary>
    [Fact]
    public void CrefTargetResolver_TryResolveType_NestedType_ReturnsTrueAndType()
    {
        // Arrange
        var outer = _assembly.MainModule.Types.First(t => t.Name == "OuterClass");
        var expected = outer.NestedTypes.First(t => t.Name == "Inner");
        var crefId = DotNetEmitter.BuildTypeId(expected);

        // Act
        var resolved = _resolver.TryResolveType(crefId, out var type);

        // Assert
        Assert.True(resolved);
        Assert.Same(expected, type);
    }

    /// <summary>Validates that an unknown type identifier (never indexed) fails to resolve.</summary>
    [Fact]
    public void CrefTargetResolver_TryResolveType_UnknownIdentifier_ReturnsFalse()
    {
        // Act
        var resolved = _resolver.TryResolveType("T:Does.Not.Exist", out var type);

        // Assert
        Assert.False(resolved);
        Assert.Null(type);
    }

    /// <summary>
    ///     Validates that an external-assembly type identifier (e.g. a BCL type never declared in
    ///     the indexed assembly) fails to resolve, matching the fallback requirement for external
    ///     crefs.
    /// </summary>
    [Fact]
    public void CrefTargetResolver_TryResolveType_ExternalAssemblyType_ReturnsFalse()
    {
        // Act
        var resolved = _resolver.TryResolveType("T:System.ArgumentNullException", out var type);

        // Assert
        Assert.False(resolved);
        Assert.Null(type);
    }

    /// <summary>Validates that a method declared in the assembly resolves by its XML-doc member identifier.</summary>
    [Fact]
    public void CrefTargetResolver_TryResolveMember_Method_ReturnsTrueAndMember()
    {
        // Arrange
        var type = _assembly.MainModule.Types.First(t => t.Name == "SampleClass");
        var expected = type.Methods.First(m => m.Name == "Reset");
        var crefId = DotNetEmitter.BuildMemberId(expected);

        // Act
        var resolved = _resolver.TryResolveMember(crefId, out var member);

        // Assert
        Assert.True(resolved);
        Assert.Same(expected, member);
    }

    /// <summary>
    ///     Validates that a filtered-by-visibility member (e.g. a private member) is still indexed
    ///     and resolvable — <see cref="CrefTargetResolver"/> applies no visibility filtering; the
    ///     emitted-visibility check is a separate, caller-supplied concern.
    /// </summary>
    [Fact]
    public void CrefTargetResolver_TryResolveMember_PrivateMember_StillResolves()
    {
        // Arrange
        var type = _assembly.MainModule.Types.First(t => t.Name == "ProtectedMembersClass");
        var expected = type.Methods.First(m => m.Name == "PrivateMethod" && m.IsPrivate);
        var crefId = DotNetEmitter.BuildMemberId(expected);

        // Act
        var resolved = _resolver.TryResolveMember(crefId, out var member);

        // Assert
        Assert.True(resolved);
        Assert.Same(expected, member);
    }

    /// <summary>Validates that an unknown member identifier (never indexed) fails to resolve.</summary>
    [Fact]
    public void CrefTargetResolver_TryResolveMember_UnknownIdentifier_ReturnsFalse()
    {
        // Act
        var resolved = _resolver.TryResolveMember("M:Does.Not.Exist", out var member);

        // Assert
        Assert.False(resolved);
        Assert.Null(member);
    }

    /// <summary>
    ///     Validates that a malformed cref string (the compiler's <c>!:</c> "could not resolve"
    ///     prefix) fails to resolve as either a type or a member.
    /// </summary>
    [Fact]
    public void CrefTargetResolver_TryResolveType_MalformedCref_ReturnsFalse()
    {
        // Act
        var resolvedAsType = _resolver.TryResolveType("!:NotAWellFormedCrefString", out var type);
        var resolvedAsMember = _resolver.TryResolveMember("!:NotAWellFormedCrefString", out var member);

        // Assert
        Assert.False(resolvedAsType);
        Assert.Null(type);
        Assert.False(resolvedAsMember);
        Assert.Null(member);
    }

    /// <summary>Validates that a field declared in the assembly resolves by its XML-doc member identifier.</summary>
    [Fact]
    public void CrefTargetResolver_TryResolveMember_Field_ReturnsTrueAndMember()
    {
        // Arrange: find any type in the fixture assembly with a field, to avoid depending on a
        // specific fixture file's internal layout beyond field existence.
        var type = _assembly.MainModule.GetTypes().First(t => t.Fields.Count > 0);
        var expected = type.Fields.First();
        var crefId = DotNetEmitter.BuildMemberId(expected);

        // Act
        var resolved = _resolver.TryResolveMember(crefId, out var member);

        // Assert
        Assert.True(resolved);
        Assert.Same(expected, member);
    }

    /// <summary>
    ///     Validates that a generic method's XML-doc member identifier includes the <c>``N</c>
    ///     arity suffix and resolves to the generic overload specifically, not the non-generic
    ///     overload sharing the same name.
    /// </summary>
    [Fact]
    public void CrefTargetResolver_TryResolveMember_GenericMethod_IncludesArityAndResolvesCorrectOverload()
    {
        // Arrange
        var type = _assembly.MainModule.Types.First(t => t.Name == "CrefLinkingClass");
        var genericMethod = type.Methods.First(m => m.Name == "Identity" && m.HasGenericParameters);
        var nonGenericMethod = type.Methods.First(m => m.Name == "Identity" && !m.HasGenericParameters);
        var genericCrefId = DotNetEmitter.BuildMemberId(genericMethod);
        var nonGenericCrefId = DotNetEmitter.BuildMemberId(nonGenericMethod);

        // Assert: the generic method's ID carries the ``1 arity suffix and the two overloads
        // produce distinct identifiers
        Assert.Contains("``1", genericCrefId);
        Assert.NotEqual(genericCrefId, nonGenericCrefId);

        // Assert: the method's own generic parameter in its parameter list is encoded using
        // XML doc positional notation (``0), matching what a real compiler-generated XML doc
        // file would contain — not the raw Cecil source name ("T")
        Assert.Equal("M:ApiMark.DotNet.Fixtures.CrefLinkingClass.Identity``1(``0)", genericCrefId);

        // Act
        var resolvedGeneric = _resolver.TryResolveMember(genericCrefId, out var resolvedGenericMember);
        var resolvedNonGeneric = _resolver.TryResolveMember(nonGenericCrefId, out var resolvedNonGenericMember);

        // Assert: each identifier resolves to its own distinct overload
        Assert.True(resolvedGeneric);
        Assert.Same(genericMethod, resolvedGenericMember);
        Assert.True(resolvedNonGeneric);
        Assert.Same(nonGenericMethod, resolvedNonGenericMember);
    }

    /// <summary>
    ///     Validates that an indexer's XML-doc member identifier includes its index parameter
    ///     type list (e.g. <c>P:Type.Item(System.Int32)</c>), matching the real compiler-emitted
    ///     XML doc ID, and resolves back to the indexer's <see cref="PropertyDefinition"/>.
    /// </summary>
    [Fact]
    public void CrefTargetResolver_TryResolveMember_Indexer_IncludesParameterListAndResolves()
    {
        // Arrange
        var type = _assembly.MainModule.Types.First(t => t.Name == "CrefLinkingClass");
        var indexer = type.Properties.First(p => p.Name == "Item");
        var crefId = DotNetEmitter.BuildMemberId(indexer);

        // Assert: the ID carries the index parameter type list
        Assert.Equal("P:ApiMark.DotNet.Fixtures.CrefLinkingClass.Item(System.Int32)", crefId);

        // Act
        var resolved = _resolver.TryResolveMember(crefId, out var resolvedMember);

        // Assert
        Assert.True(resolved);
        Assert.Same(indexer, resolvedMember);
    }

    /// <summary>
    ///     Validates that a generic method's own type parameter used as an array element type
    ///     (e.g. <c>T[]</c>) is resolved to XML doc positional notation (<c>``0</c>), not the
    ///     raw Cecil source name, matching real compiler-emitted XML doc IDs.
    /// </summary>
    [Fact]
    public void CrefTargetResolver_TryResolveMember_GenericMethodWithArrayParameter_ResolvesPositionalNotation()
    {
        // Arrange
        var type = _assembly.MainModule.Types.First(t => t.Name == "CrefLinkingClass");
        var method = type.Methods.First(m => m.Name == "IdentityArray");
        var crefId = DotNetEmitter.BuildMemberId(method);

        // Assert: both the array-element parameter and the array-element return type resolve
        // positionally, not to the literal source name "T"
        Assert.Equal("M:ApiMark.DotNet.Fixtures.CrefLinkingClass.IdentityArray``1(``0[])", crefId);

        // Act
        var resolved = _resolver.TryResolveMember(crefId, out var resolvedMember);

        // Assert
        Assert.True(resolved);
        Assert.Same(method, resolvedMember);
    }
}
