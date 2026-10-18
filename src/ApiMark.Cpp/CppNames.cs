using ApiMark.Core;
using ApiMark.Cpp.CppAst;

namespace ApiMark.Cpp;

/// <summary>
///     The approved file names of every symbol of a documented C++ library.
/// </summary>
/// <remarks>
///     Every namespace, class (including nested classes), enum, type alias, free-function page, class-member
///     page and operators page is registered with a <see cref="SymbolNameRegistry"/> exactly once and
///     resolved up front, independent of the visibility filter, so a symbol has the same file name wherever
///     it is written or linked. The emitters and the link resolver only read names from this map; they never
///     sanitize a name themselves. Overloaded functions and constructors share one page named after the
///     function. A class, enum, alias and function of the same name compete for the namespace folder in this
///     order: class, enum, alias, function; the first keeps the plain name and the others receive a numeric
///     suffix (<c>-2</c>, <c>-3</c>). The rules are documented in <c>docs/design/api-mark-core/file-naming-rules.md</c>.
/// </remarks>
internal sealed class CppNames
{
    /// <summary>Name of the reserved library index page.</summary>
    internal const string ApiPageName = "api";

    /// <summary>Name of the reserved per-namespace and per-class operators page.</summary>
    internal const string OperatorsPageName = "operators";

    /// <summary>The resolved registry.</summary>
    private readonly SymbolNameRegistry _registry = new();

    /// <summary>Initializes a new instance of the <see cref="CppNames"/> class.</summary>
    private CppNames()
    {
    }

    /// <summary>Gets the key of the reserved library index page.</summary>
    internal static string ApiKey => "R:" + ApiPageName;

    /// <summary>Gets the registry that holds the approved names.</summary>
    internal SymbolNameRegistry Registry => _registry;

    /// <summary>Builds and resolves the names of every declaration in <paramref name="namespaces"/>.</summary>
    /// <param name="namespaces">The namespace declarations of the library, by namespace key.</param>
    /// <returns>The resolved names.</returns>
    internal static CppNames Build(IReadOnlyDictionary<string, CppEmitter.NamespaceDeclarations> namespaces)
    {
        ArgumentNullException.ThrowIfNull(namespaces);

        var names = new CppNames();
        var registry = names._registry;
        registry.Register(ApiKey, null, ApiPageName, SymbolKind.Reserved);

        foreach (var (nsKey, decls) in namespaces)
        {
            var key = NamespaceKey(nsKey);
            registry.Register(key, null, nsKey, SymbolKind.Namespace);
            registry.Register(OperatorsKey(key), key, OperatorsPageName, SymbolKind.Reserved);

            foreach (var cls in decls.Classes)
            {
                RegisterClass(registry, key, cls);
            }

            foreach (var name in decls.Enums.Select(en => en.Name))
            {
                RegisterOnce(registry, EnumKey(key, name), key, name, SymbolKind.Type);
            }

            foreach (var name in decls.TypeAliases.Select(alias => alias.Name))
            {
                RegisterOnce(registry, AliasKey(key, name), key, name, SymbolKind.Type);
            }

            foreach (var name in decls.FreeFunctions.Where(f => !CppEmitter.IsOperatorName(f.Name)).Select(f => f.Name))
            {
                RegisterOnce(registry, FunctionKey(key, name), key, name, SymbolKind.Type);
            }
        }

        registry.Resolve();
        return names;
    }

    /// <summary>Gets the registry key of a namespace.</summary>
    /// <param name="nsKey">The namespace key used by the emitters.</param>
    /// <returns>The key.</returns>
    internal static string NamespaceKey(string nsKey) => "N:" + nsKey;

    /// <summary>Gets the registry key of a class.</summary>
    /// <param name="parentKey">The key of the namespace or enclosing class.</param>
    /// <param name="name">The class name.</param>
    /// <returns>The key.</returns>
    internal static string ClassKey(string parentKey, string name) => $"{parentKey}/T1:{name}";

    /// <summary>Gets the registry key of an enum.</summary>
    /// <param name="namespaceKey">The registry key of the namespace.</param>
    /// <param name="name">The enum name.</param>
    /// <returns>The key.</returns>
    internal static string EnumKey(string namespaceKey, string name) => $"{namespaceKey}/T2:{name}";

    /// <summary>Gets the registry key of a type alias.</summary>
    /// <param name="parentKey">The key of the namespace or class that declares the alias.</param>
    /// <param name="name">The alias name.</param>
    /// <returns>The key.</returns>
    internal static string AliasKey(string parentKey, string name) => $"{parentKey}/T3:{name}";

    /// <summary>Gets the registry key of the page of a free function (shared by all overloads).</summary>
    /// <param name="namespaceKey">The registry key of the namespace.</param>
    /// <param name="name">The function name.</param>
    /// <returns>The key.</returns>
    internal static string FunctionKey(string namespaceKey, string name) => $"{namespaceKey}/T4:{name}";

    /// <summary>Gets the registry key of a class-member page (shared by all overloads of one name).</summary>
    /// <param name="classKey">The registry key of the class.</param>
    /// <param name="memberName">The member name; constructors use the class name.</param>
    /// <returns>The key.</returns>
    internal static string MemberKey(string classKey, string memberName) => $"{classKey}/M:{memberName}";

    /// <summary>Gets the registry key of the operators page of a namespace or class.</summary>
    /// <param name="ownerKey">The registry key of the namespace or class.</param>
    /// <returns>The key.</returns>
    internal static string OperatorsKey(string ownerKey) => $"{ownerKey}/R:{OperatorsPageName}";

    /// <summary>Gets the approved page path (forward slashes, no extension) of a symbol.</summary>
    /// <param name="key">The registry key.</param>
    /// <returns>The path relative to the output root.</returns>
    internal string Path(string key) => _registry.GetPath(key);

    /// <summary>Gets the approved directory that holds the page of a symbol.</summary>
    /// <param name="key">The registry key.</param>
    /// <returns>The directory relative to the output root; empty for the root.</returns>
    internal string Directory(string key) => _registry.GetDirectory(key);

    /// <summary>Gets the approved file name (without extension) of a symbol.</summary>
    /// <param name="key">The registry key.</param>
    /// <returns>The file name.</returns>
    internal string Name(string key) => _registry.GetName(key);

    /// <summary>Gets the relative link destination from the page directory of one symbol to another symbol's page.</summary>
    /// <param name="fromDirectory">The directory of the page that holds the link.</param>
    /// <param name="targetKey">The registry key of the target symbol.</param>
    /// <returns>A forward-slash relative path ending in <c>.md</c>.</returns>
    internal string Link(string fromDirectory, string targetKey) =>
        LinkPath.Relative(fromDirectory, _registry.GetPath(targetKey) + ".md");

    /// <summary>
    ///     Builds the map from fully qualified C++ type names to approved page paths used by
    ///     <see cref="CppTypeLinkResolver"/>.
    /// </summary>
    /// <param name="namespaces">The namespace declarations of the library, by namespace key.</param>
    /// <returns>The known intra-library types.</returns>
    internal Dictionary<string, string> BuildKnownTypes(IReadOnlyDictionary<string, CppEmitter.NamespaceDeclarations> namespaces)
    {
        var known = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (nsKey, decls) in namespaces)
        {
            var display = nsKey.Replace(".", "::", StringComparison.Ordinal);
            var key = NamespaceKey(nsKey);
            foreach (var cls in decls.Classes)
            {
                AddClass(known, display, key, cls);
            }

            foreach (var name in decls.Enums.Select(en => en.Name))
            {
                known.TryAdd($"{display}::{name}", Path(EnumKey(key, name)));
            }

            foreach (var name in decls.TypeAliases.Select(alias => alias.Name))
            {
                known.TryAdd($"{display}::{name}", Path(AliasKey(key, name)));
            }
        }

        return known;
    }

    /// <summary>Adds a class, its scoped aliases and its nested classes to the known-type map.</summary>
    /// <param name="known">The map to extend.</param>
    /// <param name="parentDisplay">The qualified display name of the parent.</param>
    /// <param name="parentKey">The registry key of the parent.</param>
    /// <param name="cls">The class.</param>
    private void AddClass(Dictionary<string, string> known, string parentDisplay, string parentKey, CppClass cls)
    {
        var display = $"{parentDisplay}::{TemplateFreeName(cls.Name)}";
        var key = ClassKey(parentKey, cls.Name);
        known.TryAdd(display, Path(key));

        foreach (var name in cls.TypeAliases.Select(alias => alias.Name))
        {
            known.TryAdd($"{display}::{name}", Path(AliasKey(key, name)));
        }

        foreach (var nested in cls.NestedClasses)
        {
            AddClass(known, display, key, nested);
        }
    }

    /// <summary>
    ///     Gets a type name without its template arguments, the form in which the link resolver looks
    ///     types up (it strips template arguments from the references it resolves).
    /// </summary>
    /// <param name="name">The declared type name.</param>
    /// <returns>The name before the first <c>&lt;</c>.</returns>
    private static string TemplateFreeName(string name)
    {
        var lt = name.IndexOf('<', StringComparison.Ordinal);
        return lt >= 0 ? name[..lt] : name;
    }

    /// <summary>Registers a symbol unless the same key was already registered (for example an overload).</summary>
    /// <param name="registry">The registry.</param>
    /// <param name="key">The symbol key.</param>
    /// <param name="parentKey">The parent key.</param>
    /// <param name="rawName">The raw name; template angle brackets are written as braces.</param>
    /// <param name="kind">The symbol kind.</param>
    private static void RegisterOnce(SymbolNameRegistry registry, string key, string parentKey, string rawName, SymbolKind kind)
    {
        if (!registry.Contains(key))
        {
            registry.Register(key, parentKey, rawName.Replace('<', '{').Replace('>', '}'), kind);
        }
    }

    /// <summary>Registers a class, its operators page, its member pages, its aliases and its nested classes.</summary>
    /// <param name="registry">The registry.</param>
    /// <param name="parentKey">The key of the namespace or enclosing class.</param>
    /// <param name="cls">The class.</param>
    private static void RegisterClass(SymbolNameRegistry registry, string parentKey, CppClass cls)
    {
        var key = ClassKey(parentKey, cls.Name);
        if (registry.Contains(key))
        {
            return;
        }

        RegisterOnce(registry, key, parentKey, cls.Name, SymbolKind.Type);
        registry.Register(OperatorsKey(key), key, OperatorsPageName, SymbolKind.Reserved);

        foreach (var name in cls.TypeAliases.Select(alias => alias.Name))
        {
            RegisterOnce(registry, AliasKey(key, name), key, name, SymbolKind.Type);
        }

        foreach (var nested in cls.NestedClasses)
        {
            RegisterClass(registry, key, nested);
        }

        foreach (var name in cls.Members
            .Where(m => m.IsConstructor || !CppEmitter.IsOperatorName(m.Name))
            .Select(m => CppEmitter.GetMemberBaseName(m, cls.Name)))
        {
            RegisterOnce(registry, MemberKey(key, name), key, name, SymbolKind.Member);
        }

        foreach (var name in cls.Fields.Select(field => field.Name))
        {
            RegisterOnce(registry, MemberKey(key, name), key, name, SymbolKind.Member);
        }
    }
}
