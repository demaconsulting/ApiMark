using ApiMark.Core;
using ApiMark.Vhdl.VhdlAst;

namespace ApiMark.Vhdl;

/// <summary>
///     The approved file names of every entity, package, and subprogram of a documented VHDL library.
/// </summary>
/// <remarks>
///     Every declaration is registered with a <see cref="SymbolNameRegistry"/> and resolved up front, so the
///     emitter only reads approved names. Entities and packages share the output root together with the
///     reserved <c>api</c> index page; an entity wins the plain name over a package of the same name (VHDL
///     identifiers are case-insensitive, so <c>Api</c> and <c>API</c> are the same name), and the package
///     receives a numeric suffix (<c>-2</c>). Subprogram pages live in the folder of their package;
///     overloads each get their own page in declaration order (<c>name</c>, <c>name-2</c>, ...).
///     The rules are documented in <c>docs/design/api-mark-core/file-naming-rules.md</c>.
/// </remarks>
internal sealed class VhdlNames
{
    /// <summary>Name of the reserved library index page.</summary>
    internal const string ApiPageName = "api";

    /// <summary>The resolved registry.</summary>
    private readonly SymbolNameRegistry _registry = new();

    /// <summary>The registry keys of the declarations, by declaration instance.</summary>
    private readonly Dictionary<object, string> _keys = new(ReferenceEqualityComparer.Instance);

    /// <summary>Initializes a new instance of the <see cref="VhdlNames"/> class.</summary>
    private VhdlNames()
    {
    }

    /// <summary>Gets the registry that holds the approved names.</summary>
    internal SymbolNameRegistry Registry => _registry;

    /// <summary>Builds and resolves the names of every declaration in <paramref name="fileModels"/>.</summary>
    /// <param name="fileModels">The parsed file models of the library.</param>
    /// <returns>The resolved names.</returns>
    internal static VhdlNames Build(IReadOnlyList<VhdlFileModel> fileModels)
    {
        ArgumentNullException.ThrowIfNull(fileModels);

        var names = new VhdlNames();
        var registry = names._registry;
        registry.Register("R:" + ApiPageName, null, ApiPageName, SymbolKind.Reserved);

        foreach (var entity in fileModels.SelectMany(f => f.Entities))
        {
            names.RegisterUnique(registry, entity, "T1:" + entity.Name, null, entity.Name, SymbolKind.Type);
        }

        foreach (var pkg in fileModels.SelectMany(f => f.Packages))
        {
            var pkgKey = names.RegisterUnique(registry, pkg, "T2:" + pkg.Name, null, pkg.Name, SymbolKind.Type);
            for (var i = 0; i < pkg.Subprograms.Count; i++)
            {
                var s = pkg.Subprograms[i];
                names.RegisterUnique(registry, s, $"{pkgKey}/M:{s.Name}/{i:D6}", pkgKey, s.Name, SymbolKind.Member);
            }
        }

        registry.Resolve();
        return names;
    }

    /// <summary>Gets the page path (no extension) of the reserved api index page.</summary>
    /// <returns>The approved path.</returns>
    internal string ApiPath() => _registry.GetPath("R:" + ApiPageName);

    /// <summary>Gets the approved page path (no extension) of an entity, package, or subprogram.</summary>
    /// <param name="declaration">The declaration.</param>
    /// <returns>The path relative to the output root.</returns>
    internal string Path(object declaration) => _registry.GetPath(_keys[declaration]);

    /// <summary>Gets the approved file name (no extension) of an entity, package, or subprogram.</summary>
    /// <param name="declaration">The declaration.</param>
    /// <returns>The file name.</returns>
    internal string Name(object declaration) => _registry.GetName(_keys[declaration]);

    /// <summary>Gets the approved directory that holds the page of a declaration.</summary>
    /// <param name="declaration">The declaration.</param>
    /// <returns>The directory relative to the output root; empty for the root.</returns>
    internal string Directory(object declaration) => _registry.GetDirectory(_keys[declaration]);

    /// <summary>Gets the relative link destination from one page directory to a declaration's page.</summary>
    /// <param name="fromDirectory">The directory of the page that holds the link.</param>
    /// <param name="declaration">The target declaration.</param>
    /// <returns>A forward-slash relative path ending in <c>.md</c>.</returns>
    internal string Link(string fromDirectory, object declaration) =>
        LinkPath.Relative(fromDirectory, Path(declaration) + ".md");

    /// <summary>Registers a declaration under a key made unique when the same declaration name occurs twice.</summary>
    /// <param name="registry">The registry.</param>
    /// <param name="declaration">The declaration instance.</param>
    /// <param name="key">The preferred key.</param>
    /// <param name="parentKey">The parent key, or <see langword="null"/> for the root.</param>
    /// <param name="rawName">The raw name.</param>
    /// <param name="kind">The symbol kind.</param>
    /// <returns>The key that was registered.</returns>
    private string RegisterUnique(
        SymbolNameRegistry registry,
        object declaration,
        string key,
        string? parentKey,
        string rawName,
        SymbolKind kind)
    {
        var unique = key;
        var n = 2;
        while (registry.Contains(unique))
        {
            unique = $"{key}#{n}";
            n++;
        }

        registry.Register(unique, parentKey, rawName, kind);
        _keys[declaration] = unique;
        return unique;
    }
}
