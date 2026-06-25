using ApiMark.Core;
using Mono.Cecil;

namespace ApiMark.DotNet;

/// <summary>Generates Markdown API documentation from a .NET assembly using Mono.Cecil.</summary>
/// <remarks>
///     Implements <see cref="IApiGenerator"/> for C#/.NET assemblies. Reads assembly metadata
///     via Mono.Cecil without loading the target assembly into the current process, which ensures
///     reliable operation against arbitrary build outputs and dependency graphs. Pairs the metadata
///     with developer-authored content from the XML documentation file to produce a gradual-disclosure
///     Markdown tree. Not thread-safe; construct and use one instance per generation run.
/// </remarks>
public sealed class DotNetGenerator : IApiGenerator
{
    /// <summary>Configuration controlling which assembly, XML doc, and visibility filter to use.</summary>
    private readonly DotNetGeneratorOptions _options;

    /// <summary>Initializes a new instance of <see cref="DotNetGenerator"/> with the specified options.</summary>
    /// <remarks>
    ///     No file system access occurs at construction time; all I/O is deferred to <see cref="Parse"/>.
    /// </remarks>
    /// <param name="options">The generator configuration options.</param>
    public DotNetGenerator(DotNetGeneratorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <summary>
    ///     Parses the configured .NET assembly and returns an emitter ready to produce
    ///     Markdown documentation in the requested format.
    /// </summary>
    /// <remarks>
    ///     Opens the assembly via Mono.Cecil, builds an <see cref="XmlDocReader"/> index from
    ///     the XML documentation file, then collects type and namespace data needed for
    ///     subsequent <see cref="IApiEmitter.Emit"/> calls. The
    ///     <see cref="AssemblyDefinition"/> remains open until <see cref="IApiEmitter.Emit"/>
    ///     completes and is then disposed.
    ///     <para>
    ///     The entrypoint <c>api.md</c> lists all namespaces — both root and child — with a
    ///         direct type count column, followed by a file naming and path convention appendix. Each namespace page lists only its immediate child
    ///         namespaces and types, enabling gradual disclosure for AI consumers. Namespace-level
    ///         documentation is sourced from the <c>NamespaceDoc</c> convention: an
    ///         <c>internal static class NamespaceDoc</c> in a namespace carries the namespace
    ///         summary and is excluded from type listings.
    ///     </para>
    /// </remarks>
    /// <param name="context">
    ///     Output channel for informational messages. DotNetGenerator emits parsing progress and
    ///     type-count summary messages via <see cref="IContext.WriteLine"/>. Must not be null.
    /// </param>
    /// <returns>
    ///     An <see cref="IApiEmitter"/> holding all data required to emit documentation
    ///     in any supported <see cref="OutputFormat"/>. The caller must subsequently
    ///     invoke <see cref="IApiEmitter.Emit"/> to write output.
    /// </returns>
    /// <exception cref="FileNotFoundException">Thrown when the assembly file or XML documentation file does not exist.</exception>
    public IApiEmitter Parse(IContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Fail early if the assembly is absent to give the caller an actionable exception
        if (!File.Exists(_options.AssemblyPath))
        {
            throw new FileNotFoundException("Assembly file not found.", _options.AssemblyPath);
        }

        // Fail early if the XML doc is absent rather than producing empty output
        if (!File.Exists(_options.XmlDocPath))
        {
            throw new FileNotFoundException("XML documentation file not found.", _options.XmlDocPath);
        }

        context.WriteLine($"Parsing assembly: {Path.GetFileName(_options.AssemblyPath)}");
        var assembly = AssemblyDefinition.ReadAssembly(_options.AssemblyPath);
        try
        {
            // Build the inheritance chain from assembly metadata so that bare <inheritdoc />
            // elements in the XML doc file can be resolved to their base members.
            // This must be done before constructing XmlDocReader.
            var inheritanceChain = BuildInheritanceChain(assembly);
            var xmlDocs = new XmlDocReader(_options.XmlDocPath, inheritanceChain);

            // Build namespace descriptions from the NamespaceDoc convention before filtering
            // visible types so that NamespaceDoc types are available for summary extraction
            // even though they are excluded from the public type listing
            var namespaceDocTypes = assembly.MainModule.Types
                .Where(DotNetEmitter.IsNamespaceDocCarrier)
                .ToList();
            var namespaceDocTypeSet = namespaceDocTypes.ToHashSet();

            var namespaceDescriptions = namespaceDocTypes
                .GroupBy(t => t.Namespace, StringComparer.Ordinal)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(t => xmlDocs.GetSummary(DotNetEmitter.BuildTypeId(t)))
                        .FirstOrDefault(summary => !string.IsNullOrEmpty(summary)),
                    StringComparer.Ordinal);

            // Collect all types that pass the visibility, obsolete, and compiler-generated filters.
            // Exclude NamespaceDoc types — they are documentation carriers, not user-facing types.
            var visibleTypes = assembly.MainModule.Types
                .Where(t => !DotNetEmitter.IsCompilerGenerated(t))
                .Where(t => !namespaceDocTypeSet.Contains(t))
                .Where(t => !t.IsNested && _options.Visibility switch
                {
                    ApiVisibility.Public => t.IsPublic,
                    ApiVisibility.PublicAndProtected => t.IsPublic,
                    ApiVisibility.All => true,
                    _ => t.IsPublic,
                })
                .Where(t => _options.IncludeObsolete || !DotNetEmitter.IsObsolete(t))
                .ToList();

            // Group by namespace and sort for deterministic output
            var byNamespace = visibleTypes
                .GroupBy(t => t.Namespace)
                .OrderBy(g => g.Key)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<TypeDefinition>)g.OrderBy(t => t.Name).ToList());

            var allNamespaces = byNamespace.Keys.OrderBy(n => n).ToList();
            context.WriteLine($"Found {visibleTypes.Count} types across {allNamespaces.Count} namespace(s).");

            // Root namespaces: those not prefixed by any other namespace present in the assembly
            var rootNamespaces = allNamespaces
                .Where(n => !allNamespaces.Any(
                    other => !string.Equals(other, n, StringComparison.Ordinal) &&
                             n.StartsWith(other + ".", StringComparison.Ordinal)))
                .OrderBy(n => n)
                .ToList();

            var resolver = new TypeLinkResolver(rootNamespaces);

            return new DotNetEmitter(new DotNetAstModel(
                assembly,
                xmlDocs,
                allNamespaces,
                byNamespace,
                rootNamespaces,
                namespaceDescriptions,
                resolver,
                _options));
        }
        catch
        {
            assembly.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     Builds a map of XML doc member ID to ordered list of base member IDs by walking
    ///     all types in the assembly and resolving virtual overrides and interface implementations.
    /// </summary>
    /// <remarks>
    ///     The map is used by <see cref="XmlDocReader"/> to resolve bare <c>&lt;inheritdoc /&gt;</c>
    ///     elements that carry no <c>cref</c> attribute. For each entry, the candidate list is ordered
    ///     so that the overridden base-class member (if any) appears before interface members.
    ///     <para>
    ///         Limitation: complex generic method signatures may not produce a correct XML doc ID
    ///         because generic parameter constraints and arity information in Mono.Cecil's FullName
    ///         representation differs from the XML doc format in some edge cases. Simple non-generic
    ///         methods, properties, and events are handled correctly.
    ///     </para>
    /// </remarks>
    /// <param name="assembly">The assembly to inspect.</param>
    /// <returns>Read-only dictionary mapping derived member IDs to their base member ID lists.</returns>
    private static IReadOnlyDictionary<string, IReadOnlyList<string>> BuildInheritanceChain(AssemblyDefinition assembly)
    {
        var chain = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var type in assembly.MainModule.GetTypes())
        {
            BuildTypeInheritanceEntries(type, chain);
        }

        // Project to the read-only interface expected by XmlDocReader
        return chain.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlyList<string>)kv.Value.AsReadOnly(),
            StringComparer.Ordinal);
    }

    /// <summary>
    ///     Populates inheritance entries for all members of <paramref name="type"/> into
    ///     <paramref name="chain"/>.
    /// </summary>
    /// <param name="type">The type whose members to inspect.</param>
    /// <param name="chain">The chain dictionary to populate.</param>
    private static void BuildTypeInheritanceEntries(TypeDefinition type, Dictionary<string, List<string>> chain)
    {
        // Methods (excluding constructors, static members, and compiler-generated accessors)
        foreach (var method in type.Methods)
        {
            if (method.IsConstructor || method.IsStatic || method.IsSpecialName)
            {
                continue;
            }

            var targets = CollectMethodInheritanceTargets(method, type);
            if (targets.Count > 0)
            {
                chain[DotNetEmitter.BuildMethodId(method)] = targets;
            }
        }

        // Properties (accessor overrides and interface implementations)
        foreach (var property in type.Properties)
        {
            var targets = CollectPropertyInheritanceTargets(property, type);
            if (targets.Count > 0)
            {
                chain[DotNetEmitter.BuildMemberId(property)] = targets;
            }
        }

        // Events (accessor overrides and interface implementations)
        foreach (var ev in type.Events)
        {
            var targets = CollectEventInheritanceTargets(ev, type);
            if (targets.Count > 0)
            {
                chain[DotNetEmitter.BuildMemberId(ev)] = targets;
            }
        }
    }

    /// <summary>
    ///     Collects base member IDs for <paramref name="method"/>, including explicit overrides,
    ///     implicit base-class virtual overrides, and implicit interface implementations.
    /// </summary>
    /// <param name="method">The method to inspect.</param>
    /// <param name="type">The declaring type.</param>
    /// <returns>Ordered list of base member IDs; empty when the method does not override anything.</returns>
    private static List<string> CollectMethodInheritanceTargets(MethodDefinition method, TypeDefinition type)
    {
        var targets = new List<string>();

        // Explicit overrides (covers explicit interface implementations such as IFoo.Bar())
        foreach (var overrideRef in method.Overrides)
        {
            var targetId = BuildMethodIdFromReference(overrideRef);
            if (!string.IsNullOrEmpty(targetId))
            {
                targets.Add(targetId);
            }
        }

        // Implicit virtual override — the method occupies a slot from a base-class virtual
        if (method.IsVirtual && !method.IsNewSlot && type.BaseType != null)
        {
            try
            {
                var baseTypeDef = type.BaseType.Resolve();
                if (baseTypeDef != null)
                {
                    var baseMethod = FindMatchingMethodDefinition(baseTypeDef, method);
                    if (baseMethod != null)
                    {
                        // Base class override takes priority — insert at front
                        targets.Insert(0, DotNetEmitter.BuildMethodId(baseMethod));
                    }
                }
            }
            catch (AssemblyResolutionException)
            {
                // Base type is in an external assembly — skip base-class override mapping
            }
        }

        // Implicit interface implementations — method name and signature match an interface member
        foreach (var iface in type.Interfaces)
        {
            try
            {
                var ifaceType = iface.InterfaceType.Resolve();
                if (ifaceType == null)
                {
                    continue;
                }

                var ifaceMethod = FindMatchingMethodDefinition(ifaceType, method);
                if (ifaceMethod != null)
                {
                    var ifaceId = DotNetEmitter.BuildMethodId(ifaceMethod);
                    if (!targets.Contains(ifaceId, StringComparer.Ordinal))
                    {
                        targets.Add(ifaceId);
                    }
                }
            }
            catch (AssemblyResolutionException)
            {
                // Interface is in an external assembly — skip
            }
        }

        return targets;
    }

    /// <summary>
    ///     Collects base property IDs for <paramref name="property"/>, including explicit
    ///     accessor overrides and implicit interface property implementations.
    /// </summary>
    /// <param name="property">The property to inspect.</param>
    /// <param name="type">The declaring type.</param>
    /// <returns>Ordered list of base property IDs; empty when the property does not override anything.</returns>
    private static List<string> CollectPropertyInheritanceTargets(PropertyDefinition property, TypeDefinition type)
    {
        var targets = new List<string>();

        // Explicit accessor overrides map back to the owning interface property
        if (property.GetMethod != null)
        {
            foreach (var overrideRef in property.GetMethod.Overrides)
            {
                var propId = MapAccessorReferenceToPropertyId(overrideRef, "get_");
                if (propId != null && !targets.Contains(propId, StringComparer.Ordinal))
                {
                    targets.Add(propId);
                }
            }
        }

        if (property.SetMethod != null)
        {
            foreach (var overrideRef in property.SetMethod.Overrides)
            {
                var propId = MapAccessorReferenceToPropertyId(overrideRef, "set_");
                if (propId != null && !targets.Contains(propId, StringComparer.Ordinal))
                {
                    targets.Add(propId);
                }
            }
        }

        // Implicit interface implementation — property name and type match an interface property
        foreach (var iface in type.Interfaces)
        {
            try
            {
                var ifaceType = iface.InterfaceType.Resolve();
                if (ifaceType == null)
                {
                    continue;
                }

                var ifaceProp = ifaceType.Properties.FirstOrDefault(p =>
                    string.Equals(p.Name, property.Name, StringComparison.Ordinal) &&
                    string.Equals(
                        p.PropertyType.FullName,
                        property.PropertyType.FullName,
                        StringComparison.Ordinal));

                if (ifaceProp != null)
                {
                    var ifacePropId = $"P:{ifaceProp.DeclaringType.FullName.Replace('/', '.')}.{ifaceProp.Name}";
                    if (!targets.Contains(ifacePropId, StringComparer.Ordinal))
                    {
                        targets.Add(ifacePropId);
                    }
                }
            }
            catch (AssemblyResolutionException)
            {
                // Interface is in an external assembly — skip
            }
        }

        return targets;
    }

    /// <summary>
    ///     Collects base event IDs for <paramref name="ev"/>, including explicit accessor overrides
    ///     and implicit interface event implementations.
    /// </summary>
    /// <param name="ev">The event to inspect.</param>
    /// <param name="type">The declaring type.</param>
    /// <returns>Ordered list of base event IDs; empty when the event does not override anything.</returns>
    private static List<string> CollectEventInheritanceTargets(EventDefinition ev, TypeDefinition type)
    {
        var targets = new List<string>();

        // Explicit accessor overrides map back to the owning interface event
        if (ev.AddMethod != null)
        {
            foreach (var overrideRef in ev.AddMethod.Overrides)
            {
                var evId = MapAccessorReferenceToEventId(overrideRef, "add_");
                if (evId != null && !targets.Contains(evId, StringComparer.Ordinal))
                {
                    targets.Add(evId);
                }
            }
        }

        if (ev.RemoveMethod != null)
        {
            foreach (var overrideRef in ev.RemoveMethod.Overrides)
            {
                var evId = MapAccessorReferenceToEventId(overrideRef, "remove_");
                if (evId != null && !targets.Contains(evId, StringComparer.Ordinal))
                {
                    targets.Add(evId);
                }
            }
        }

        // Implicit interface implementation — event name and type match an interface event
        foreach (var iface in type.Interfaces)
        {
            try
            {
                var ifaceType = iface.InterfaceType.Resolve();
                if (ifaceType == null)
                {
                    continue;
                }

                var ifaceEvent = ifaceType.Events.FirstOrDefault(e =>
                    string.Equals(e.Name, ev.Name, StringComparison.Ordinal) &&
                    string.Equals(
                        e.EventType.FullName,
                        ev.EventType.FullName,
                        StringComparison.Ordinal));

                if (ifaceEvent != null)
                {
                    var ifaceEventId = $"E:{ifaceEvent.DeclaringType.FullName.Replace('/', '.')}.{ifaceEvent.Name}";
                    if (!targets.Contains(ifaceEventId, StringComparer.Ordinal))
                    {
                        targets.Add(ifaceEventId);
                    }
                }
            }
            catch (AssemblyResolutionException)
            {
                // Interface is in an external assembly — skip
            }
        }

        return targets;
    }

    /// <summary>
    ///     Finds a method in <paramref name="searchType"/> that matches <paramref name="method"/>
    ///     by name and parameter count and types. Returns <c>null</c> when no match is found.
    /// </summary>
    /// <remarks>
    ///     Parameter matching uses <see cref="TypeReference.FullName"/> for comparison.
    ///     Generic parameter representations may differ in some edge cases; this is a known
    ///     limitation for complex generic signatures.
    /// </remarks>
    /// <param name="searchType">The type to search.</param>
    /// <param name="method">The method whose signature to match.</param>
    /// <returns>The matching <see cref="MethodDefinition"/>, or <c>null</c>.</returns>
    private static MethodDefinition? FindMatchingMethodDefinition(TypeDefinition searchType, MethodDefinition method)
    {
        return searchType.Methods.FirstOrDefault(m =>
            !m.IsSpecialName &&
            string.Equals(m.Name, method.Name, StringComparison.Ordinal) &&
            m.Parameters.Count == method.Parameters.Count &&
            m.Parameters.Zip(method.Parameters, (a, b) =>
                    string.Equals(a.ParameterType.FullName, b.ParameterType.FullName, StringComparison.Ordinal))
                .All(match => match));
    }

    /// <summary>
    ///     Builds an XML doc method identifier string from a <see cref="MethodReference"/>.
    /// </summary>
    /// <param name="methodRef">The method reference to convert.</param>
    /// <returns>The XML doc member identifier (e.g. <c>M:Namespace.Type.Method(ParamType)</c>).</returns>
    private static string BuildMethodIdFromReference(MethodReference methodRef)
    {
        var typeName = methodRef.DeclaringType.FullName.Replace('/', '.');

        // XML doc format uses #ctor for constructors; IL metadata uses .ctor
        var methodName = string.Equals(methodRef.Name, ".ctor", StringComparison.Ordinal) ? "#ctor" : methodRef.Name;

        if (!methodRef.HasParameters)
        {
            return $"M:{typeName}.{methodName}";
        }

        var paramList = string.Join(",", methodRef.Parameters.Select(p => DotNetEmitter.ToXmlDocTypeName(p.ParameterType.FullName)));

        if (methodRef.Name is "op_Implicit" or "op_Explicit")
        {
            return $"M:{typeName}.{methodName}({paramList})~{DotNetEmitter.ToXmlDocTypeName(methodRef.ReturnType.FullName)}";
        }

        return $"M:{typeName}.{methodName}({paramList})";
    }

    /// <summary>
    ///     Maps an accessor method reference (getter or setter) to its owning property's XML doc ID.
    ///     Returns <c>null</c> when the accessor name does not start with <paramref name="prefix"/>.
    /// </summary>
    /// <param name="accessorRef">The accessor method reference.</param>
    /// <param name="prefix">The accessor prefix to strip (<c>get_</c> or <c>set_</c>).</param>
    /// <returns>The property XML doc ID, or <c>null</c>.</returns>
    private static string? MapAccessorReferenceToPropertyId(MethodReference accessorRef, string prefix)
    {
        if (!accessorRef.Name.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var propertyName = accessorRef.Name[prefix.Length..];
        var typeName = accessorRef.DeclaringType.FullName.Replace('/', '.');
        return $"P:{typeName}.{propertyName}";
    }

    /// <summary>
    ///     Maps an accessor method reference (add or remove) to its owning event's XML doc ID.
    ///     Returns <c>null</c> when the accessor name does not start with <paramref name="prefix"/>.
    /// </summary>
    /// <param name="accessorRef">The accessor method reference.</param>
    /// <param name="prefix">The accessor prefix to strip (<c>add_</c> or <c>remove_</c>).</param>
    /// <returns>The event XML doc ID, or <c>null</c>.</returns>
    private static string? MapAccessorReferenceToEventId(MethodReference accessorRef, string prefix)
    {
        if (!accessorRef.Name.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var eventName = accessorRef.Name[prefix.Length..];
        var typeName = accessorRef.DeclaringType.FullName.Replace('/', '.');
        return $"E:{typeName}.{eventName}";
    }
}

/// <summary>Specifies which members are included in the generated API documentation.</summary>
public enum ApiVisibility
{
    /// <summary>Include only public members.</summary>
    Public,

    /// <summary>Include public and protected members.</summary>
    PublicAndProtected,

    /// <summary>Include all members regardless of access modifier.</summary>
    All,
}

/// <summary>Configuration options for <see cref="DotNetGenerator"/>.</summary>
public sealed class DotNetGeneratorOptions
{
    /// <summary>Gets or sets the path to the .NET assembly to document.</summary>
    public string AssemblyPath { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the path to the XML documentation file produced alongside the assembly.
    ///     <see cref="DotNetGenerator"/> throws <see cref="FileNotFoundException"/> if this file does not exist.
    /// </summary>
    public string XmlDocPath { get; set; } = string.Empty;

    /// <summary>Gets or sets which members are visible in the generated output. Defaults to <see cref="ApiVisibility.Public"/>.</summary>
    public ApiVisibility Visibility { get; set; } = ApiVisibility.Public;

    /// <summary>Gets or sets a value indicating whether obsolete members are included. Defaults to <c>false</c>.</summary>
    public bool IncludeObsolete { get; set; }
}
