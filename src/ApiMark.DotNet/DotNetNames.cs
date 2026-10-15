// Copyright (c) DEMA Consulting
// SPDX-License-Identifier: MIT

using System.Runtime.CompilerServices;
using ApiMark.Core;
using Mono.Cecil;

namespace ApiMark.DotNet;

/// <summary>
///     The approved file names of every symbol of a documented .NET module.
/// </summary>
/// <remarks>
///     Every namespace, type (including nested, internal and documentation-carrier types), member page
///     and operators page of the module is registered with a <see cref="SymbolNameRegistry"/> exactly once
///     and resolved up front, independent of the visibility filter, so a symbol has the same file name
///     wherever it is written or linked. The emitters and link resolvers only read names from this map;
///     they never sanitize a name themselves. The rules are documented in
///     <c>docs/design/api-mark-core/file-naming-rules.md</c>.
/// </remarks>
internal sealed class DotNetNames
{
    /// <summary>Name of the synthetic folder and page of the global namespace.</summary>
    private const string GlobalNamespaceName = "global-namespace";

    /// <summary>Name of the reserved assembly index page.</summary>
    private const string ApiPageName = "api";

    /// <summary>Name of the reserved per-type operators page.</summary>
    private const string OperatorsPageName = "operators";

    /// <summary>Per-module name maps.</summary>
    private static readonly ConditionalWeakTable<ModuleDefinition, DotNetNames> ModuleNames = new();

    /// <summary>Name maps registered for a root-namespace list instance (see <see cref="Attach"/>).</summary>
    private static readonly ConditionalWeakTable<object, DotNetNames> ListNames = new();

    /// <summary>C# keyword aliases used to spell type arguments of explicit interface implementations.</summary>
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.Ordinal)
    {
        ["System.Boolean"] = "bool",
        ["System.Byte"] = "byte",
        ["System.SByte"] = "sbyte",
        ["System.Char"] = "char",
        ["System.Decimal"] = "decimal",
        ["System.Double"] = "double",
        ["System.Single"] = "float",
        ["System.Int16"] = "short",
        ["System.UInt16"] = "ushort",
        ["System.Int32"] = "int",
        ["System.UInt32"] = "uint",
        ["System.Int64"] = "long",
        ["System.UInt64"] = "ulong",
        ["System.Object"] = "object",
        ["System.String"] = "string",
        ["System.IntPtr"] = "nint",
        ["System.UIntPtr"] = "nuint",
    };

    /// <summary>The resolved registry.</summary>
    private readonly SymbolNameRegistry _registry = new();

    /// <summary>Initializes a new instance of the <see cref="DotNetNames"/> class.</summary>
    private DotNetNames()
    {
    }

    /// <summary>Gets the registry that holds the approved names.</summary>
    internal SymbolNameRegistry Registry => _registry;

    /// <summary>Returns the approved names of <paramref name="module"/>, building them on first use.</summary>
    /// <param name="module">The documented module.</param>
    /// <returns>The resolved name map.</returns>
    internal static DotNetNames For(ModuleDefinition module) => ModuleNames.GetValue(module, Build);

    /// <summary>
    ///     Associates <paramref name="names"/> with a root-namespace list so that namespace paths can be
    ///     looked up from the list the emitters already pass around.
    /// </summary>
    /// <param name="rootNamespaces">The root-namespace list instance of the documented assembly.</param>
    /// <param name="names">The approved names of the same assembly.</param>
    internal static void Attach(object rootNamespaces, DotNetNames names)
    {
        ListNames.Remove(rootNamespaces);
        ListNames.Add(rootNamespaces, names);
    }

    /// <summary>Looks up the names attached to a root-namespace list.</summary>
    /// <param name="rootNamespaces">The root-namespace list instance.</param>
    /// <returns>The attached names, or <see langword="null"/> when none were attached.</returns>
    internal static DotNetNames? Find(object rootNamespaces) =>
        ListNames.TryGetValue(rootNamespaces, out var names) ? names : null;

    /// <summary>Returns the approved folder path of a namespace, or <see langword="null"/> when unregistered.</summary>
    /// <param name="namespaceName">The full namespace name (empty for the global namespace).</param>
    /// <returns>The path without extension.</returns>
    internal string? TryGetNamespacePath(string namespaceName) =>
        _registry.Contains(NamespaceKey(namespaceName)) ? _registry.GetPath(NamespaceKey(namespaceName)) : null;

    /// <summary>Returns the approved file name of a type definition, or <see langword="null"/> when unregistered.</summary>
    /// <param name="type">The type definition.</param>
    /// <returns>The name without extension.</returns>
    internal string? TryGetTypeName(TypeDefinition type) =>
        _registry.Contains(TypeKey(type)) ? _registry.GetName(TypeKey(type)) : null;

    /// <summary>Returns the approved file name of a member page, or <see langword="null"/> when unregistered.</summary>
    /// <param name="member">The member.</param>
    /// <param name="declaringType">The declaring type.</param>
    /// <returns>The name without extension.</returns>
    internal string? TryGetMemberName(IMemberDefinition member, TypeDefinition declaringType)
    {
        var key = MemberKey(declaringType, RawMemberName(member, declaringType));
        return _registry.Contains(key) ? _registry.GetName(key) : null;
    }

    /// <summary>Returns the predictable file name of a type that is not part of the documented module.</summary>
    /// <param name="cecilName">The Cecil type name, possibly with a backtick arity.</param>
    /// <returns>The escaped name with the arity in braces.</returns>
    internal static string PredictableTypeName(string cecilName) =>
        FileNameRules.Escape(ArityBraces(cecilName), out _);

    /// <summary>Converts a Cecil name such as <c>Foo`2</c> to its registry form <c>Foo{2}</c>.</summary>
    /// <param name="name">The Cecil name.</param>
    /// <returns>The name with the arity in braces.</returns>
    internal static string ArityBraces(string name)
    {
        var tick = name.IndexOf('`');
        if (tick < 0 || tick == name.Length - 1)
        {
            return name;
        }

        var digits = name[(tick + 1)..];
        return digits.All(char.IsAsciiDigit) ? $"{name[..tick]}{{{digits}}}" : name;
    }

    /// <summary>Computes the raw (unescaped) member page name that the registry receives.</summary>
    /// <param name="member">The member.</param>
    /// <param name="declaringType">The declaring type.</param>
    /// <returns>The raw name; constructors use the type name without arity.</returns>
    internal static string RawMemberName(IMemberDefinition member, TypeDefinition declaringType)
    {
        if (member is MethodDefinition { Name: DotNetEmitter.ConstructorMethodName })
        {
            return DotNetEmitter.StripArity(declaringType.Name);
        }

        var raw = member.Name;
        var interfaceType = ExplicitInterface(member);
        var dot = LastTopLevelDot(raw);
        if (dot < 0)
        {
            return raw;
        }

        var simple = raw[(dot + 1)..];
        return interfaceType == null ? simple : $"{InterfaceName(interfaceType)}.{simple}";
    }

    /// <summary>Builds the registry for <paramref name="module"/>.</summary>
    /// <param name="module">The module.</param>
    /// <returns>The resolved names.</returns>
    private static DotNetNames Build(ModuleDefinition module)
    {
        var names = new DotNetNames();
        var registry = names._registry;
        registry.Register("R:" + ApiPageName, null, ApiPageName, SymbolKind.Reserved);

        var types = module.Types.Where(t => !DotNetEmitter.IsCompilerGenerated(t)).ToList();
        var namespaces = types.Select(t => t.Namespace).Distinct(StringComparer.Ordinal).ToList();
        var roots = DotNetGenerator.ComputeRootNamespaces(namespaces);
        foreach (var ns in namespaces)
        {
            RegisterNamespace(registry, ns, roots);
        }

        foreach (var type in types)
        {
            RegisterType(registry, type, NamespaceKey(type.Namespace));
        }

        registry.Resolve();
        return names;
    }

    /// <summary>Registers a namespace and the folder-only namespaces above it.</summary>
    /// <param name="registry">The registry.</param>
    /// <param name="ns">The namespace.</param>
    /// <param name="roots">The root namespaces of the module.</param>
    private static void RegisterNamespace(SymbolNameRegistry registry, string ns, IReadOnlyList<string> roots)
    {
        if (ns.Length == 0)
        {
            registry.Register(NamespaceKey(ns), null, GlobalNamespaceName, SymbolKind.Namespace);
            return;
        }

        var root = roots.FirstOrDefault(r => ns == r || ns.StartsWith(r + ".", StringComparison.Ordinal)) ?? ns;
        if (!registry.Contains(NamespaceKey(root)))
        {
            registry.Register(NamespaceKey(root), null, root, SymbolKind.Namespace);
        }

        var parent = root;
        foreach (var segment in ns.Length > root.Length ? ns[(root.Length + 1)..].Split('.') : [])
        {
            var current = parent + "." + segment;
            if (!registry.Contains(NamespaceKey(current)))
            {
                registry.Register(NamespaceKey(current), NamespaceKey(parent), segment, SymbolKind.Namespace);
            }

            parent = current;
        }
    }

    /// <summary>Registers a type, its operators page, its member pages and its nested types.</summary>
    /// <param name="registry">The registry.</param>
    /// <param name="type">The type.</param>
    /// <param name="parentKey">The key of the namespace or declaring type.</param>
    private static void RegisterType(SymbolNameRegistry registry, TypeDefinition type, string parentKey)
    {
        var key = TypeKey(type);
        registry.Register(key, parentKey, ArityBraces(type.Name), SymbolKind.Type);
        registry.Register("O:" + type.FullName, key, OperatorsPageName, SymbolKind.Reserved);

        foreach (var member in DotNetEmitter.GetCandidateMembers(type))
        {
            if (member is MethodDefinition method && DotNetEmitter.IsOperator(method))
            {
                continue;
            }

            var raw = RawMemberName(member, type);
            var memberKey = MemberKey(type, raw);
            if (!registry.Contains(memberKey))
            {
                registry.Register(memberKey, key, raw, SymbolKind.Member);
            }
        }

        foreach (var nested in type.NestedTypes.Where(t => !DotNetEmitter.IsCompilerGenerated(t)))
        {
            RegisterType(registry, nested, key);
        }
    }

    /// <summary>Gets the registry key of a namespace.</summary>
    /// <param name="ns">The namespace.</param>
    /// <returns>The key.</returns>
    private static string NamespaceKey(string ns) => "N:" + ns;

    /// <summary>Gets the registry key of a type.</summary>
    /// <param name="type">The type.</param>
    /// <returns>The key.</returns>
    private static string TypeKey(TypeDefinition type) => "T:" + type.FullName;

    /// <summary>Gets the registry key of a member page.</summary>
    /// <param name="type">The declaring type.</param>
    /// <param name="rawName">The raw page name.</param>
    /// <returns>The key.</returns>
    private static string MemberKey(TypeDefinition type, string rawName) => "M:" + type.FullName + "|" + rawName;

    /// <summary>Finds the interface of an explicit interface implementation.</summary>
    /// <param name="member">The member.</param>
    /// <returns>The implemented interface type, or <see langword="null"/>.</returns>
    private static TypeReference? ExplicitInterface(IMemberDefinition member)
    {
        var method = member switch
        {
            MethodDefinition m => m,
            PropertyDefinition p => p.GetMethod ?? p.SetMethod,
            EventDefinition e => e.AddMethod,
            _ => null,
        };

        return method is { HasOverrides: true } ? method.Overrides[0].DeclaringType : null;
    }

    /// <summary>Finds the last dot that is not inside angle brackets.</summary>
    /// <param name="name">The raw name.</param>
    /// <returns>The index, or -1.</returns>
    private static int LastTopLevelDot(string name)
    {
        var depth = 0;
        var result = -1;
        for (var i = 0; i < name.Length; i++)
        {
            switch (name[i])
            {
                case '<':
                    depth++;
                    break;
                case '>':
                    depth--;
                    break;
                case '.' when depth == 0:
                    result = i;
                    break;
            }
        }

        return result;
    }

    /// <summary>Spells an interface for an explicit-implementation page name, e.g. <c>IFoo{int}</c>.</summary>
    /// <param name="type">The interface type.</param>
    /// <returns>The name without namespace; type arguments in braces joined by <c>-</c>.</returns>
    private static string InterfaceName(TypeReference type)
    {
        if (type is not GenericInstanceType instance)
        {
            return ArityBraces(type.Name);
        }

        var name = DotNetEmitter.StripArity(instance.ElementType.Name);
        if (instance.GenericArguments.All(a => a is GenericParameter))
        {
            return $"{name}{{{instance.GenericArguments.Count}}}";
        }

        return $"{name}{{{string.Join('-', instance.GenericArguments.Select(ArgumentName))}}}";
    }

    /// <summary>Spells one type argument.</summary>
    /// <param name="type">The argument.</param>
    /// <returns>The spelling.</returns>
    private static string ArgumentName(TypeReference type) => type switch
    {
        GenericParameter p => p.Name,
        ArrayType a => $"Array{{{ArgumentName(a.ElementType)}}}",
        GenericInstanceType g => $"{DotNetEmitter.StripArity(g.ElementType.Name)}{{{string.Join('-', g.GenericArguments.Select(ArgumentName))}}}",
        _ => Aliases.TryGetValue(type.FullName, out var alias) ? alias : ArityBraces(type.Name),
    };
}
