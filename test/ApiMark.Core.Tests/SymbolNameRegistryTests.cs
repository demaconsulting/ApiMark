using ApiMark.Core;
using Xunit;

namespace ApiMark.Core.Tests;

/// <summary>Verifies <see cref="SymbolNameRegistry"/> assigns unique, predictable names before any file is written.</summary>
public class SymbolNameRegistryTests
{
    /// <summary>Builds and resolves a registry from a list of (key, parent, raw, kind) tuples.</summary>
    /// <param name="symbols">The symbols to register, in the given order.</param>
    /// <returns>The resolved registry.</returns>
    private static SymbolNameRegistry Resolved(params (string Key, string? Parent, string Raw, SymbolKind Kind)[] symbols)
    {
        var registry = new SymbolNameRegistry();
        foreach (var (key, parent, raw, kind) in symbols)
        {
            registry.Register(key, parent, raw, kind);
        }

        registry.Resolve();
        return registry;
    }

    /// <summary>Validates that a unique name is kept unchanged and its path includes the parent folder.</summary>
    [Fact]
    public void SymbolNameRegistry_Resolve_UniqueNames_AreUnchanged()
    {
        // Arrange / Act
        var registry = Resolved(
            ("ns", null, "A", SymbolKind.Namespace),
            ("t", "ns", "Widget{2}", SymbolKind.Type),
            ("m", "t", "Run", SymbolKind.Member));

        // Assert
        Assert.Equal("A", registry.GetPath("ns"));
        Assert.Equal("A/Widget{2}", registry.GetPath("t"));
        Assert.Equal("A/Widget{2}/Run", registry.GetPath("m"));
        Assert.Equal("A/Widget{2}", registry.GetDirectory("m"));
        Assert.Equal(string.Empty, registry.GetDirectory("ns"));
        Assert.Equal("Widget{2}", registry.GetName("t"));
    }

    /// <summary>Validates that of two case-twin names the lowest ordinal key keeps the plain name and the other gets -2.</summary>
    [Fact]
    public void SymbolNameRegistry_Resolve_CaseTwins_LowestKeyWinsAndOtherGetsSuffix()
    {
        // Arrange / Act
        var registry = Resolved(
            ("B.item", null, "item", SymbolKind.Type),
            ("A.Item", null, "Item", SymbolKind.Type));

        // Assert
        Assert.Equal("Item", registry.GetName("A.Item"));
        Assert.Equal("item-2", registry.GetName("B.item"));
    }

    /// <summary>Validates that a tier 0 identity name beats a tier 1 transformed name that collides with it.</summary>
    [Fact]
    public void SymbolNameRegistry_Resolve_IdentityName_BeatsTransformedName()
    {
        // Arrange / Act (the transformed name sorts first by key but loses the tier)
        var registry = Resolved(
            ("a", null, "a:b", SymbolKind.Type),
            ("z", null, "a_b", SymbolKind.Type));

        // Assert
        Assert.Equal("a_b", registry.GetName("z"));
        Assert.Equal("a_b-2", registry.GetName("a"));
    }

    /// <summary>
    ///     Validates the sanitizer collision cases of the earlier review: literal braces never collide with
    ///     escaped angle brackets, and names that look like escapes or hashes are never merged.
    /// </summary>
    [Fact]
    public void SymbolNameRegistry_Resolve_BracesBracketsAndEscapeLookalikes_AreDistinctAndExact()
    {
        // Arrange / Act
        var registry = Resolved(
            ("k1", null, "A<B>", SymbolKind.Type),
            ("k2", null, "A{B}", SymbolKind.Type),
            ("k3", null, "A_B_", SymbolKind.Type),
            ("k4", null, "A_B_-2", SymbolKind.Type),
            ("k5", null, "Foo_1a2b3c4d", SymbolKind.Type),
            ("k6", null, "Foo?", SymbolKind.Type),
            ("k7", null, "Foo*", SymbolKind.Type));

        // Assert: literal braces are kept; identity names win; transformed twins take the next free suffix
        Assert.Equal("A{B}", registry.GetName("k2"));
        Assert.Equal("A_B_", registry.GetName("k3"));
        Assert.Equal("A_B_-2", registry.GetName("k4"));
        Assert.Equal("A_B_-3", registry.GetName("k1"));
        Assert.Equal("Foo_1a2b3c4d", registry.GetName("k5"));
        Assert.Equal("Foo_", registry.GetName("k6"));
        Assert.Equal("Foo_-2", registry.GetName("k7"));
        var paths = registry.GetAllPaths();
        Assert.Equal(paths.Count, paths.Select(p => p.ToLowerInvariant()).Distinct().Count());
    }

    /// <summary>Validates that a namespace keeps the plain name when a type claims the same name.</summary>
    [Fact]
    public void SymbolNameRegistry_Resolve_NamespaceAndType_NamespaceWins()
    {
        // Arrange / Act
        var registry = Resolved(
            ("root", null, "A", SymbolKind.Namespace),
            ("A.B", "root", "B", SymbolKind.Namespace),
            ("A.B(type)", "root", "B", SymbolKind.Type),
            ("A.B.C", "A.B", "C", SymbolKind.Type));

        // Assert
        Assert.Equal("A/B", registry.GetPath("A.B"));
        Assert.Equal("A/B-2", registry.GetPath("A.B(type)"));
        Assert.Equal("A/B/C", registry.GetPath("A.B.C"));
    }

    /// <summary>Validates that a displaced parent carries its children along to its new folder.</summary>
    [Fact]
    public void SymbolNameRegistry_Resolve_DisplacedParent_ChildrenFollow()
    {
        // Arrange / Act
        var registry = Resolved(
            ("n1", null, "N", SymbolKind.Namespace),
            ("n1.b", "n1", "b", SymbolKind.Type),
            ("n1.B", "n1", "B", SymbolKind.Type),
            ("n1.b.m", "n1.b", "Run", SymbolKind.Member),
            ("n1.B.m", "n1.B", "Run", SymbolKind.Member));

        // Assert
        Assert.Equal("N/B/Run", registry.GetPath("n1.B.m"));
        Assert.Equal("N/b-2/Run", registry.GetPath("n1.b.m"));
    }

    /// <summary>Validates that a reserved structural page always keeps its name.</summary>
    [Fact]
    public void SymbolNameRegistry_Resolve_ReservedName_KeepsNameAndDisplacesSymbol()
    {
        // Arrange / Act
        var registry = Resolved(
            ("reserved", null, "api", SymbolKind.Reserved),
            ("ns", null, "API", SymbolKind.Namespace),
            ("t", null, "Aa", SymbolKind.Type));

        // Assert
        Assert.Equal("api", registry.GetName("reserved"));
        Assert.Equal("API-2", registry.GetName("ns"));
        Assert.Equal("Aa", registry.GetName("t"));
    }

    /// <summary>Validates that the numeric suffix skips names that are already taken.</summary>
    [Fact]
    public void SymbolNameRegistry_Resolve_SuffixCandidateTaken_UsesNextNumber()
    {
        // Arrange / Act
        var registry = Resolved(
            ("1", null, "Foo", SymbolKind.Type),
            ("2", null, "foo", SymbolKind.Type),
            ("3", null, "FOO", SymbolKind.Type),
            ("4", null, "Foo-2", SymbolKind.Type));

        // Assert
        Assert.Equal("Foo", registry.GetName("1"));
        Assert.Equal("Foo-2", registry.GetName("4"));
        Assert.Equal("FOO-4", registry.GetName("3"));
        Assert.Equal("foo-3", registry.GetName("2"));
    }

    /// <summary>Validates that the result does not depend on the registration order.</summary>
    [Fact]
    public void SymbolNameRegistry_Resolve_RegistrationOrder_DoesNotMatter()
    {
        // Arrange
        var symbols = new (string Key, string? Parent, string Raw, SymbolKind Kind)[]
        {
            ("ns", null, "N", SymbolKind.Namespace),
            ("a", "ns", "Foo", SymbolKind.Type),
            ("b", "ns", "foo", SymbolKind.Type),
            ("c", "ns", "FOO", SymbolKind.Member),
            ("d", "ns", "Foo{1}", SymbolKind.Type),
            ("e", "ns", "foo{1}", SymbolKind.Type),
        };

        // Act
        var forward = Resolved(symbols);
        var reversed = Resolved(symbols.Reverse().OrderBy(s => s.Parent == null ? 0 : 1).ToArray());

        // Assert
        foreach (var (key, _, _, _) in symbols)
        {
            Assert.Equal(forward.GetPath(key), reversed.GetPath(key));
        }
    }

    /// <summary>Validates that resolving twice gives the same names (idempotent inputs) and the second call throws.</summary>
    [Fact]
    public void SymbolNameRegistry_Resolve_CalledTwice_Throws()
    {
        // Arrange
        var registry = Resolved(("a", null, "A", SymbolKind.Type));

        // Act / Assert
        Assert.Throws<InvalidOperationException>(registry.Resolve);
    }

    /// <summary>Validates that no name can be read before the registry is resolved.</summary>
    [Fact]
    public void SymbolNameRegistry_GetName_BeforeResolve_Throws()
    {
        // Arrange
        var registry = new SymbolNameRegistry();
        registry.Register("a", null, "A", SymbolKind.Type);

        // Act / Assert
        Assert.Throws<InvalidOperationException>(() => registry.GetName("a"));
        Assert.Throws<InvalidOperationException>(() => registry.GetPath("a"));
        Assert.Throws<InvalidOperationException>(() => registry.GetDirectory("a"));
        Assert.Throws<InvalidOperationException>(registry.GetAllPaths);
    }

    /// <summary>Validates that no symbol can be registered after the registry is resolved.</summary>
    [Fact]
    public void SymbolNameRegistry_Register_AfterResolve_Throws()
    {
        // Arrange
        var registry = Resolved(("a", null, "A", SymbolKind.Type));

        // Act / Assert
        Assert.Throws<InvalidOperationException>(() => registry.Register("b", null, "B", SymbolKind.Type));
    }

    /// <summary>Validates the registration error cases.</summary>
    [Fact]
    public void SymbolNameRegistry_Register_InvalidInput_Throws()
    {
        // Arrange
        var registry = new SymbolNameRegistry();
        registry.Register("a", null, "A", SymbolKind.Type);

        // Act / Assert
        Assert.Throws<InvalidOperationException>(() => registry.Register("a", null, "A", SymbolKind.Type));
        Assert.Throws<InvalidOperationException>(() => registry.Register("b", "missing", "B", SymbolKind.Type));
        Assert.Throws<ArgumentNullException>(() => registry.Register(null!, null, "B", SymbolKind.Type));
        Assert.Throws<ArgumentNullException>(() => registry.Register("c", null, null!, SymbolKind.Type));
    }

    /// <summary>Validates that an unknown key is rejected after resolution.</summary>
    [Fact]
    public void SymbolNameRegistry_GetName_UnknownKey_Throws()
    {
        // Arrange
        var registry = Resolved(("a", null, "A", SymbolKind.Type));

        // Act / Assert
        Assert.False(registry.Contains("zzz"));
        Assert.True(registry.Contains("a"));
        Assert.Throws<InvalidOperationException>(() => registry.GetName("zzz"));
    }

    /// <summary>Validates that unicode-equivalent names collide after normalization.</summary>
    [Fact]
    public void SymbolNameRegistry_Resolve_CanonicallyEquivalentNames_AreDistinguished()
    {
        // Arrange / Act
        var registry = Resolved(
            ("a", null, "caf\u00e9", SymbolKind.Type),
            ("b", null, "cafe\u0301", SymbolKind.Type));

        // Assert
        Assert.Equal("caf\u00e9", registry.GetName("a"));
        Assert.Equal("caf\u00e9-2", registry.GetName("b"));
    }

    /// <summary>Validates that every resolved path of a large generated symbol set is legal and unique ignoring case.</summary>
    [Fact]
    public void SymbolNameRegistry_Resolve_GeneratedSymbolSet_PathsAreLegalAndCaseInsensitivelyUnique()
    {
        // Arrange
        var random = new Random(12345);
        var alphabet = new[] { "a", "A", "b", "B", "con", "CON", ":", "{1}", ".", "x y", "-2", "api", "Item", "item" };
        var registry = new SymbolNameRegistry();
        var parents = new List<string?> { null };
        for (var i = 0; i < 2000; i++)
        {
            var raw = string.Concat(Enumerable.Range(0, random.Next(1, 4)).Select(_ => alphabet[random.Next(alphabet.Length)]));
            var parent = parents[random.Next(parents.Count)];
            var key = $"k{i}";
            registry.Register(key, parent, raw, (SymbolKind)random.Next(1, 4));
            parents.Add(key);
        }

        // Act
        registry.Resolve();
        var paths = registry.GetAllPaths();

        // Assert
        Assert.Equal(paths.Count, paths.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(paths, p => Assert.All(p.Split('/'), s => Assert.True(FileNameRules.IsLegalSegment(s), s)));
    }
}
