using ApiMark.Core;
using Mono.Cecil;

namespace ApiMark.DotNet;

/// <summary>Generates Markdown API documentation from a .NET assembly using Mono.Cecil.</summary>
public sealed class DotNetGenerator : IApiGenerator
{
    /// <summary>Configuration controlling which assembly, XML doc, and visibility filter to use.</summary>
    private readonly DotNetGeneratorOptions _options;

    /// <summary>Initializes a new instance of <see cref="DotNetGenerator"/> with the specified options.</summary>
    /// <param name="options">The generator configuration options.</param>
    public DotNetGenerator(DotNetGeneratorOptions options)
    {
        _options = options;
    }

    /// <summary>Generates API documentation into the provided writer factory.</summary>
    /// <param name="factory">The markdown writer factory used to create output files.</param>
    /// <exception cref="FileNotFoundException">Thrown when the XML documentation file does not exist.</exception>
    public void Generate(IMarkdownWriterFactory factory)
    {
        // Fail early if the XML doc is absent rather than producing empty output
        if (!File.Exists(_options.XmlDocPath))
        {
            throw new FileNotFoundException("XML documentation file not found.", _options.XmlDocPath);
        }

        var xmlDocs = new XmlDocReader(_options.XmlDocPath);
        var assembly = AssemblyDefinition.ReadAssembly(_options.AssemblyPath);

        // Collect all types that pass the visibility, obsolete, and compiler-generated filters
        var visibleTypes = assembly.MainModule.Types
            .Where(t => !IsCompilerGenerated(t))
            .Where(t => IsTypeVisible(t))
            .Where(t => _options.IncludeObsolete || !IsObsolete(t))
            .ToList();

        // Group by namespace and sort for deterministic output
        var byNamespace = visibleTypes
            .GroupBy(t => t.Namespace)
            .OrderBy(g => g.Key)
            .ToList();

        // Write the top-level assembly index page
        using var apiWriter = factory.CreateMarkdown("", "api");
        apiWriter.WriteHeading(1, assembly.Name.Name);

        var nsHeaders = new[] { "Namespace", "Description" };
        var nsRows = byNamespace.Select(g =>
        {
            var nsName = g.Key;
            var link = $"{nsName}/{nsName}.md";
            return new[] { $"[{nsName}]({link})", string.Empty };
        });
        apiWriter.WriteTable(nsHeaders, nsRows);

        // Write one namespace page and one type page per namespace group
        foreach (var nsGroup in byNamespace)
        {
            var namespaceName = nsGroup.Key;
            var nsTypes = nsGroup.OrderBy(t => t.Name).ToList();

            using var nsWriter = factory.CreateMarkdown(namespaceName, namespaceName);
            nsWriter.WriteHeading(1, namespaceName);

            var typeHeaders = new[] { "Type", "Description" };
            var typeRows = nsTypes.Select(t =>
            {
                var typeMemberId = BuildTypeId(t);
                var summary = xmlDocs.GetSummary(typeMemberId) ?? string.Empty;
                var link = $"{t.Name}.md";
                return new[] { $"[{t.Name}]({link})", summary };
            });
            nsWriter.WriteTable(typeHeaders, typeRows);

            foreach (var type in nsTypes)
            {
                WriteTypePage(factory, namespaceName, type, xmlDocs);
            }
        }
    }

    /// <summary>
    ///     Writes the Markdown page for a single type, including a members table and
    ///     links to complex member pages.
    /// </summary>
    /// <param name="factory">Factory for creating output writers.</param>
    /// <param name="namespaceName">The namespace that owns <paramref name="type"/>.</param>
    /// <param name="type">The type whose page is being written.</param>
    /// <param name="xmlDocs">XML documentation index for summary and remarks lookups.</param>
    private void WriteTypePage(
        IMarkdownWriterFactory factory,
        string namespaceName,
        TypeDefinition type,
        XmlDocReader xmlDocs)
    {
        using var typeWriter = factory.CreateMarkdown(namespaceName, type.Name);
        typeWriter.WriteHeading(2, type.Name);

        // Emit the C# declaration signature so readers can see the type kind and modifiers
        var typeSignature = BuildTypeSignature(type);
        typeWriter.WriteSignature("csharp", typeSignature);

        var typeMemberId = BuildTypeId(type);

        var typeSummary = xmlDocs.GetSummary(typeMemberId);
        if (!string.IsNullOrEmpty(typeSummary))
        {
            typeWriter.WriteParagraph(typeSummary);
        }

        var typeRemarks = xmlDocs.GetRemarks(typeMemberId);
        if (!string.IsNullOrEmpty(typeRemarks))
        {
            typeWriter.WriteParagraph(typeRemarks);
        }

        // Collect visible members: constructors first, then alphabetically
        var members = GetVisibleMembers(type)
            .OrderBy(m => m.Name == ".ctor" ? 0 : 1)
            .ThenBy(m => m.Name)
            .ToList();

        if (members.Count == 0)
        {
            return;
        }

        var memberHeaders = new[] { "Member", "Type", "Description" };
        var inlineRows = new List<string[]>();

        // Emit a link row for complex members (own page) and a plain row for simple ones
        foreach (var member in members)
        {
            var memberId = BuildMemberId(member);
            var memberSummary = xmlDocs.GetSummary(memberId) ?? string.Empty;
            var memberTypeName = GetMemberTypeName(member, namespaceName);
            var memberDisplayName = GetMemberDisplayName(member);
            var sanitizedName = GetSanitizedMemberFileName(member, type);

            if (IsComplex(member, xmlDocs))
            {
                WriteMemberPage(factory, namespaceName, type, member, xmlDocs, memberId);
                var memberLink = $"{type.Name}/{sanitizedName}.md";
                inlineRows.Add(new[] { $"[{memberDisplayName}]({memberLink})", memberTypeName, memberSummary });
            }
            else
            {
                inlineRows.Add(new[] { memberDisplayName, memberTypeName, memberSummary });
            }
        }

        if (inlineRows.Count > 0)
        {
            typeWriter.WriteTable(memberHeaders, inlineRows);
        }
    }

    /// <summary>
    ///     Writes the detailed Markdown page for a single complex member, including
    ///     signature, parameters, returns, exceptions, remarks, and examples.
    /// </summary>
    /// <param name="factory">Factory for creating output writers.</param>
    /// <param name="namespaceName">The namespace that owns the declaring type.</param>
    /// <param name="type">The type that declares <paramref name="member"/>.</param>
    /// <param name="member">The member whose page is being written.</param>
    /// <param name="xmlDocs">XML documentation index.</param>
    /// <param name="memberId">Pre-computed XML doc member identifier for <paramref name="member"/>.</param>
    private void WriteMemberPage(
        IMarkdownWriterFactory factory,
        string namespaceName,
        TypeDefinition type,
        IMemberDefinition member,
        XmlDocReader xmlDocs,
        string memberId)
    {
        var sanitizedName = GetSanitizedMemberFileName(member, type);
        using var memberWriter = factory.CreateMarkdown($"{namespaceName}/{type.Name}", sanitizedName);

        var displayName = GetMemberDisplayName(member);
        memberWriter.WriteHeading(3, displayName);

        var signature = BuildMemberSignature(member, namespaceName);
        memberWriter.WriteSignature("csharp", signature);

        var summary = xmlDocs.GetSummary(memberId);
        if (!string.IsNullOrEmpty(summary))
        {
            memberWriter.WriteParagraph(summary);
        }

        // Emit a parameter table when the member is a method with at least one parameter
        if (member is MethodDefinition method && method.HasParameters)
        {
            var paramDocs = xmlDocs.GetParams(memberId);
            var paramHeaders = new[] { "Parameter", "Type", "Description" };
            var paramRows = method.Parameters.Select(p =>
            {
                var desc = paramDocs.FirstOrDefault(pd => pd.Name == p.Name).Description ?? string.Empty;
                var typeName = TypeNameSimplifier.Simplify(p.ParameterType, namespaceName);
                return new[] { p.Name, typeName, desc };
            });
            memberWriter.WriteTable(paramHeaders, paramRows);
        }

        var returns = xmlDocs.GetReturns(memberId);
        if (!string.IsNullOrEmpty(returns))
        {
            memberWriter.WriteParagraph($"**Returns:** {returns}");
        }

        // Emit exception table when documented exceptions exist
        var exceptions = xmlDocs.GetExceptions(memberId);
        if (exceptions.Count > 0)
        {
            var exHeaders = new[] { "Exception", "Description" };
            var exRows = exceptions.Select(e => new[] { e, string.Empty });
            memberWriter.WriteTable(exHeaders, exRows);
        }

        var remarks = xmlDocs.GetRemarks(memberId);
        if (!string.IsNullOrEmpty(remarks))
        {
            memberWriter.WriteParagraph(remarks);
        }

        var example = xmlDocs.GetExample(memberId);
        if (!string.IsNullOrEmpty(example))
        {
            memberWriter.WriteCodeBlock("csharp", example);
        }
    }

    /// <summary>
    ///     Determines whether <paramref name="member"/> warrants its own page based on
    ///     the complexity rules: parameters, exception docs, multi-line remarks, examples,
    ///     or asymmetric get/set accessors.
    /// </summary>
    /// <param name="member">The member to evaluate.</param>
    /// <param name="xmlDocs">XML documentation index used for doc-driven complexity checks.</param>
    /// <returns><c>true</c> when the member should get a dedicated page.</returns>
    private static bool IsComplex(IMemberDefinition member, XmlDocReader xmlDocs)
    {
        var memberId = BuildMemberId(member);

        if (member is MethodDefinition method && method.HasParameters)
        {
            return true;
        }

        if (xmlDocs.GetExceptions(memberId).Count > 0)
        {
            return true;
        }

        if (xmlDocs.IsMultiLineRemarks(memberId))
        {
            return true;
        }

        if (xmlDocs.GetExample(memberId) != null)
        {
            return true;
        }

        if (member is PropertyDefinition prop && HasAsymmetricAccessors(prop))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Returns <c>true</c> when <paramref name="prop"/> has both a getter and a setter
    ///     but they expose different access levels (e.g. <c>public get; internal set;</c>).
    /// </summary>
    /// <param name="prop">The property to inspect.</param>
    /// <returns><c>true</c> when getter and setter have different access levels.</returns>
    private static bool HasAsymmetricAccessors(PropertyDefinition prop)
    {
        if (prop.GetMethod == null || prop.SetMethod == null)
        {
            return false;
        }

        return GetAccessLevel(prop.GetMethod) != GetAccessLevel(prop.SetMethod);
    }

    /// <summary>Returns a numeric access level for a method, where higher values are more permissive.</summary>
    /// <param name="method">The method to evaluate.</param>
    /// <returns>An integer from 0 (private) to 4 (public).</returns>
    private static int GetAccessLevel(MethodDefinition method)
    {
        if (method.IsPublic)
        {
            return 4;
        }

        if (method.IsFamilyOrAssembly)
        {
            return 3;
        }

        if (method.IsFamily)
        {
            return 2;
        }

        if (method.IsAssembly)
        {
            return 1;
        }

        return 0;
    }

    /// <summary>
    ///     Returns <c>true</c> when <paramref name="type"/> satisfies the visibility
    ///     setting in <see cref="_options"/>.
    /// </summary>
    /// <param name="type">The type definition to test.</param>
    /// <returns><c>true</c> when the type is visible under the current visibility setting.</returns>
    private bool IsTypeVisible(TypeDefinition type)
    {
        // Nested types are not emitted as top-level pages
        if (type.IsNested)
        {
            return false;
        }

        return _options.Visibility switch
        {
            ApiVisibility.Public => type.IsPublic,
            ApiVisibility.PublicAndProtected => type.IsPublic || type.IsNestedFamily || type.IsNestedFamilyOrAssembly,
            ApiVisibility.All => true,
            _ => type.IsPublic,
        };
    }

    /// <summary>
    ///     Returns <c>true</c> when <paramref name="member"/> satisfies the visibility
    ///     setting in <see cref="_options"/>.
    /// </summary>
    /// <param name="member">The member to test.</param>
    /// <returns><c>true</c> when the member is visible under the current visibility setting.</returns>
    private bool IsMemberVisible(IMemberDefinition member)
    {
        return _options.Visibility switch
        {
            ApiVisibility.Public => IsMemberPublic(member),
            ApiVisibility.PublicAndProtected => IsMemberPublicOrProtected(member),
            ApiVisibility.All => true,
            _ => IsMemberPublic(member),
        };
    }

    /// <summary>Returns <c>true</c> when <paramref name="member"/> is publicly accessible.</summary>
    /// <param name="member">The member to test.</param>
    /// <returns><c>true</c> when the member is public.</returns>
    private static bool IsMemberPublic(IMemberDefinition member) => member switch
    {
        MethodDefinition m => m.IsPublic,
        PropertyDefinition p => (p.GetMethod?.IsPublic ?? false) || (p.SetMethod?.IsPublic ?? false),
        FieldDefinition f => f.IsPublic,
        EventDefinition e => e.AddMethod?.IsPublic ?? false,
        _ => false,
    };

    /// <summary>Returns <c>true</c> when <paramref name="member"/> is public or protected.</summary>
    /// <param name="member">The member to test.</param>
    /// <returns><c>true</c> when the member is public or protected.</returns>
    private static bool IsMemberPublicOrProtected(IMemberDefinition member) => member switch
    {
        MethodDefinition m => m.IsPublic || m.IsFamily || m.IsFamilyOrAssembly,
        PropertyDefinition p => (p.GetMethod != null && (p.GetMethod.IsPublic || p.GetMethod.IsFamily)) ||
                                 (p.SetMethod != null && (p.SetMethod.IsPublic || p.SetMethod.IsFamily)),
        FieldDefinition f => f.IsPublic || f.IsFamily || f.IsFamilyOrAssembly,
        EventDefinition e => (e.AddMethod?.IsPublic ?? false) || (e.AddMethod?.IsFamily ?? false),
        _ => false,
    };

    /// <summary>
    ///     Enumerates all members of <paramref name="type"/> that pass the current visibility,
    ///     obsolete, and compiler-generated filters.
    /// </summary>
    /// <param name="type">The type whose members to inspect.</param>
    /// <returns>An enumerable of visible member definitions.</returns>
    private IEnumerable<IMemberDefinition> GetVisibleMembers(TypeDefinition type)
    {
        // Methods: exclude special-name accessors (property getters/setters, event add/remove)
        // but always include constructors
        foreach (var method in type.Methods)
        {
            if (method.IsSpecialName && method.Name != ".ctor")
            {
                continue;
            }

            if (IsCompilerGenerated(method))
            {
                continue;
            }

            if (!IsMemberVisible(method))
            {
                continue;
            }

            if (!_options.IncludeObsolete && IsObsolete(method))
            {
                continue;
            }

            yield return method;
        }

        foreach (var prop in type.Properties)
        {
            if (!IsMemberVisible(prop))
            {
                continue;
            }

            if (!_options.IncludeObsolete && IsObsolete(prop))
            {
                continue;
            }

            yield return prop;
        }

        // Fields: skip compiler-generated backing fields (names contain angle brackets)
        foreach (var field in type.Fields)
        {
            if (field.Name.Contains('<') || field.Name.Contains('>'))
            {
                continue;
            }

            if (!IsMemberVisible(field))
            {
                continue;
            }

            if (!_options.IncludeObsolete && IsObsolete(field))
            {
                continue;
            }

            yield return field;
        }

        foreach (var evt in type.Events)
        {
            if (!IsMemberVisible(evt))
            {
                continue;
            }

            if (!_options.IncludeObsolete && IsObsolete(evt))
            {
                continue;
            }

            yield return evt;
        }
    }

    /// <summary>Builds the XML doc member identifier for a type (e.g. <c>T:Namespace.TypeName</c>).</summary>
    /// <param name="type">The type definition.</param>
    /// <returns>The XML doc member identifier string.</returns>
    private static string BuildTypeId(TypeDefinition type) =>
        $"T:{type.FullName.Replace('/', '.')}";

    /// <summary>Builds the XML doc member identifier for an arbitrary member.</summary>
    /// <param name="member">The member definition.</param>
    /// <returns>The XML doc member identifier string.</returns>
    private static string BuildMemberId(IMemberDefinition member) => member switch
    {
        MethodDefinition m => BuildMethodId(m),
        PropertyDefinition p => $"P:{p.DeclaringType.FullName.Replace('/', '.')}.{p.Name}",
        FieldDefinition f => $"F:{f.DeclaringType.FullName.Replace('/', '.')}.{f.Name}",
        EventDefinition e => $"E:{e.DeclaringType.FullName.Replace('/', '.')}.{e.Name}",
        _ => string.Empty,
    };

    /// <summary>Builds the XML doc member identifier for a method, including parameter type list when present.</summary>
    /// <param name="method">The method definition.</param>
    /// <returns>The XML doc member identifier string.</returns>
    private static string BuildMethodId(MethodDefinition method)
    {
        var typeName = method.DeclaringType.FullName.Replace('/', '.');
        var methodName = method.Name;

        // XML doc format includes parenthesized parameter list only when parameters exist
        if (!method.HasParameters)
        {
            return $"M:{typeName}.{methodName}";
        }

        var paramList = string.Join(",", method.Parameters.Select(p => p.ParameterType.FullName));
        return $"M:{typeName}.{methodName}({paramList})";
    }

    /// <summary>Builds a human-readable C# declaration signature for a type definition.</summary>
    /// <param name="type">The type definition to represent.</param>
    /// <returns>A string of the form <c>public class Name</c> or <c>public interface Name&lt;T&gt;</c>.</returns>
    private static string BuildTypeSignature(TypeDefinition type)
    {
        var keyword = type.IsInterface ? "interface"
            : type.IsEnum ? "enum"
            : type.IsValueType ? "struct"
            : "class";

        var name = StripArity(type.Name);
        if (type.HasGenericParameters)
        {
            var args = string.Join(", ", type.GenericParameters.Select(p => p.Name));
            name = $"{name}<{args}>";
        }

        return $"public {keyword} {name}";
    }

    /// <summary>Dispatches to the appropriate signature builder based on the runtime member type.</summary>
    /// <param name="member">The member to represent.</param>
    /// <param name="contextNamespace">Used to simplify type names in the signature.</param>
    /// <returns>A human-readable C# declaration signature string.</returns>
    private string BuildMemberSignature(IMemberDefinition member, string contextNamespace)
    {
        return member switch
        {
            MethodDefinition m => BuildMethodSignature(m, contextNamespace),
            PropertyDefinition p => BuildPropertySignature(p, contextNamespace),
            FieldDefinition f => BuildFieldSignature(f, contextNamespace),
            EventDefinition e => BuildEventSignature(e, contextNamespace),
            _ => member.Name,
        };
    }

    /// <summary>Builds a human-readable C# method declaration signature.</summary>
    /// <param name="method">The method definition.</param>
    /// <param name="contextNamespace">Used to simplify type names in parameters and return type.</param>
    /// <returns>The method signature string.</returns>
    private string BuildMethodSignature(MethodDefinition method, string contextNamespace)
    {
        var returnType = TypeNameSimplifier.Simplify(method.ReturnType, contextNamespace);

        // Use the declaring type name for constructors rather than ".ctor"
        var name = method.Name == ".ctor"
            ? StripArity(method.DeclaringType.Name)
            : method.Name;

        var parameters = string.Join(", ", method.Parameters.Select(p =>
            $"{TypeNameSimplifier.Simplify(p.ParameterType, contextNamespace)} {p.Name}"));

        var accessibility = GetAccessibilityKeyword(method);
        return method.Name == ".ctor"
            ? $"{accessibility} {name}({parameters})"
            : $"{accessibility} {returnType} {name}({parameters})";
    }

    /// <summary>Builds a human-readable C# property declaration signature.</summary>
    /// <param name="prop">The property definition.</param>
    /// <param name="contextNamespace">Used to simplify the property type name.</param>
    /// <returns>The property signature string.</returns>
    private string BuildPropertySignature(PropertyDefinition prop, string contextNamespace)
    {
        var typeName = TypeNameSimplifier.Simplify(prop.PropertyType, contextNamespace);
        var accessibility = GetAccessibilityKeyword(prop.GetMethod ?? prop.SetMethod!);
        var accessors = BuildPropertyAccessors(prop);
        return $"{accessibility} {typeName} {prop.Name} {{ {accessors} }}";
    }

    /// <summary>Builds the accessor portion of a property signature (e.g. <c>get; internal set;</c>).</summary>
    /// <param name="prop">The property definition.</param>
    /// <returns>The accessor string to embed in the property signature.</returns>
    private static string BuildPropertyAccessors(PropertyDefinition prop)
    {
        var parts = new List<string>();

        if (prop.GetMethod != null)
        {
            // Only prefix the getter when it is less permissive than the property's declared access
            var prefix = prop.GetMethod.IsPublic ? string.Empty : $"{GetAccessibilityKeyword(prop.GetMethod)} ";
            parts.Add($"{prefix}get;");
        }

        if (prop.SetMethod != null)
        {
            var prefix = prop.SetMethod.IsPublic ? string.Empty : $"{GetAccessibilityKeyword(prop.SetMethod)} ";
            parts.Add($"{prefix}set;");
        }

        return string.Join(" ", parts);
    }

    /// <summary>Builds a human-readable C# field declaration signature.</summary>
    /// <param name="field">The field definition.</param>
    /// <param name="contextNamespace">Used to simplify the field type name.</param>
    /// <returns>The field signature string.</returns>
    private string BuildFieldSignature(FieldDefinition field, string contextNamespace)
    {
        var typeName = TypeNameSimplifier.Simplify(field.FieldType, contextNamespace);
        var modifiers = field.IsStatic
            ? (field.IsLiteral ? "const" : "static")
            : "readonly";
        return $"public {modifiers} {typeName} {field.Name}";
    }

    /// <summary>Builds a human-readable C# event declaration signature.</summary>
    /// <param name="evt">The event definition.</param>
    /// <param name="contextNamespace">Used to simplify the event type name.</param>
    /// <returns>The event signature string.</returns>
    private string BuildEventSignature(EventDefinition evt, string contextNamespace)
    {
        var typeName = TypeNameSimplifier.Simplify(evt.EventType, contextNamespace);
        return $"public event {typeName} {evt.Name}";
    }

    /// <summary>Returns the C# accessibility keyword for a method (e.g. <c>"public"</c> or <c>"protected"</c>).</summary>
    /// <param name="method">The method whose accessibility to report.</param>
    /// <returns>The lowercase C# accessibility keyword string.</returns>
    private static string GetAccessibilityKeyword(MethodDefinition method)
    {
        if (method.IsPublic)
        {
            return "public";
        }

        if (method.IsFamilyOrAssembly)
        {
            return "protected internal";
        }

        if (method.IsFamily)
        {
            return "protected";
        }

        if (method.IsAssembly)
        {
            return "internal";
        }

        return "private";
    }

    /// <summary>Returns the display name for a member as it should appear in documentation tables.</summary>
    /// <param name="member">The member whose display name to compute.</param>
    /// <returns>The display name string (constructor uses the declaring type's simple name).</returns>
    private static string GetMemberDisplayName(IMemberDefinition member)
    {
        return member switch
        {
            MethodDefinition m when m.Name == ".ctor" => StripArity(m.DeclaringType.Name),
            MethodDefinition m => m.Name,
            PropertyDefinition p => p.Name,
            FieldDefinition f => f.Name,
            EventDefinition e => e.Name,
            _ => member.Name,
        };
    }

    /// <summary>Returns the simplified C# type name for a member as it should appear in a type column.</summary>
    /// <param name="member">The member whose type name to compute.</param>
    /// <param name="contextNamespace">Used to simplify the type name.</param>
    /// <returns>The simplified type name string.</returns>
    private string GetMemberTypeName(IMemberDefinition member, string contextNamespace)
    {
        return member switch
        {
            MethodDefinition m => TypeNameSimplifier.Simplify(m.ReturnType, contextNamespace),
            PropertyDefinition p => TypeNameSimplifier.Simplify(p.PropertyType, contextNamespace),
            FieldDefinition f => TypeNameSimplifier.Simplify(f.FieldType, contextNamespace),
            EventDefinition e => TypeNameSimplifier.Simplify(e.EventType, contextNamespace),
            _ => string.Empty,
        };
    }

    /// <summary>
    ///     Returns the file-system-safe name used to form the member page path.
    ///     Constructors use the declaring type name; all others use the member name directly.
    /// </summary>
    /// <param name="member">The member whose file name to compute.</param>
    /// <param name="declaringType">The type that declares <paramref name="member"/>.</param>
    /// <returns>A string suitable for use as a file name (without extension).</returns>
    private static string GetSanitizedMemberFileName(IMemberDefinition member, TypeDefinition declaringType)
    {
        return member switch
        {
            MethodDefinition m when m.Name == ".ctor" => StripArity(declaringType.Name),
            MethodDefinition m => m.Name,
            PropertyDefinition p => p.Name,
            FieldDefinition f => f.Name,
            EventDefinition e => e.Name,
            _ => member.Name,
        };
    }

    /// <summary>
    ///     Returns <c>true</c> when <paramref name="provider"/> carries a
    ///     <c>CompilerGeneratedAttribute</c>, indicating it was synthesized by the compiler.
    /// </summary>
    /// <param name="provider">The metadata element to inspect.</param>
    /// <returns><c>true</c> when the element is compiler-generated.</returns>
    private static bool IsCompilerGenerated(ICustomAttributeProvider provider) =>
        provider.CustomAttributes.Any(a =>
            a.AttributeType.FullName == "System.Runtime.CompilerServices.CompilerGeneratedAttribute");

    /// <summary>
    ///     Returns <c>true</c> when <paramref name="type"/> is a compiler-generated
    ///     type (name contains angle brackets, or carries CompilerGeneratedAttribute).
    /// </summary>
    /// <param name="type">The type definition to inspect.</param>
    /// <returns><c>true</c> when the type is compiler-generated.</returns>
    private static bool IsCompilerGenerated(TypeDefinition type) =>
        type.Name.Contains('<') || type.Name.Contains('>') || IsCompilerGenerated((ICustomAttributeProvider)type);

    /// <summary>
    ///     Returns <c>true</c> when <paramref name="provider"/> carries an
    ///     <c>ObsoleteAttribute</c>.
    /// </summary>
    /// <param name="provider">The metadata element to inspect.</param>
    /// <returns><c>true</c> when the element is marked obsolete.</returns>
    private static bool IsObsolete(ICustomAttributeProvider provider) =>
        provider.CustomAttributes.Any(a => a.AttributeType.FullName == "System.ObsoleteAttribute");

    /// <summary>Removes the generic arity suffix (e.g. <c>`1</c>) from a type name.</summary>
    /// <param name="name">The raw type name that may contain a backtick arity suffix.</param>
    /// <returns>The name without the arity suffix.</returns>
    private static string StripArity(string name)
    {
        var tick = name.IndexOf('`');
        return tick >= 0 ? name.Substring(0, tick) : name;
    }
}
