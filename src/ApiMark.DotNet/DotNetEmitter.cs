using ApiMark.Core;
using Mono.Cecil;

namespace ApiMark.DotNet;

/// <summary>
///     Emitter that holds pre-parsed .NET assembly data and can write the Markdown
///     documentation tree in either gradual-disclosure or single-file format.
/// </summary>
/// <remarks>
///     Created exclusively by <see cref="DotNetGenerator.Parse"/>.
/// </remarks>
internal sealed class DotNetEmitter : IApiEmitter
{
    /// <summary>Column header label used in all generated Markdown tables for the description column.</summary>
    internal const string DescriptionColumnHeader = "Description";

    /// <summary>Placeholder emitted in description cells and paragraphs when no XML doc summary is available.</summary>
    internal const string NoDescriptionPlaceholder = "*No description provided.*";

    /// <summary>The .NET metadata method name used for all instance and static constructors.</summary>
    internal const string ConstructorMethodName = ".ctor";

    /// <summary>Gets the pre-parsed assembly data used during emit.</summary>
    internal DotNetAstModel Model { get; }

    /// <summary>Initializes a new <see cref="DotNetEmitter"/> with pre-parsed assembly data.</summary>
    /// <param name="model">Pre-parsed assembly data; ownership is transferred to this emitter.</param>
    internal DotNetEmitter(DotNetAstModel model)
    {
        Model = model;
    }

    /// <summary>
    ///     Emits the complete Markdown documentation tree in the format specified by
    ///     <paramref name="config"/>.
    /// </summary>
    /// <param name="factory">Factory for creating per-file Markdown writers. Must not be null.</param>
    /// <param name="config">Output configuration controlling format and heading depth. Must not be null.</param>
    /// <param name="context">Output channel for informational and error messages. Must not be null.</param>
    public void Emit(IMarkdownWriterFactory factory, EmitConfig config, IContext context)
    {
        ArgumentNullException.ThrowIfNull(factory);

        // Dispose the assembly after emit regardless of success or failure
        using (Model.Assembly)
        {
            if (config.Format == OutputFormat.SingleFile)
            {
                new DotNetEmitterSingleFile(this, Model).Emit(factory, config, context);
            }
            else
            {
                new DotNetEmitterGradualDisclosure(this, Model).Emit(factory, config, context);
            }
        }
    }

    // =========================================================================
    // Namespace path helpers
    // =========================================================================

    /// <summary>
    ///     Computes the file-system folder path for a namespace, treating the root namespace as atomic.
    /// </summary>
    /// <param name="namespaceName">The full namespace name.</param>
    /// <param name="rootNamespaces">All root namespaces identified in the assembly.</param>
    /// <returns>
    ///     The folder path string. For a root namespace the entire dotted name is the path segment
    ///     (e.g. <c>ApiMark.DotNet.Fixtures</c>). For a child namespace the root prefix is kept
    ///     and subsequent segments use forward slashes
    ///     (e.g. <c>ApiMark.DotNet.Fixtures/Inner</c>).
    /// </returns>
    internal static string GetNamespaceFolderPath(string namespaceName, IReadOnlyList<string> rootNamespaces)
    {
        var root = rootNamespaces.FirstOrDefault(r =>
            string.Equals(r, namespaceName, StringComparison.Ordinal) ||
            namespaceName.StartsWith(r + ".", StringComparison.Ordinal));

        if (root == null)
        {
            return namespaceName; // fallback: no known root, use full name
        }

        if (string.Equals(namespaceName, root, StringComparison.Ordinal))
        {
            return root; // root namespace: full dotted name is the single path segment
        }

        // Child namespace: keep root intact, replace subsequent dots with slashes
        var suffix = namespaceName.Substring(root.Length); // e.g. ".Inner" or ".Inner.Sub"
        return root + suffix.Replace('.', '/');
    }

    /// <summary>
    ///     Returns all immediate child namespaces of <paramref name="parent"/> from the given set.
    ///     A namespace N is an immediate child when it starts with <c>parent.</c> and the
    ///     remaining portion contains no further dots.
    /// </summary>
    /// <param name="parent">The parent namespace name.</param>
    /// <param name="allNamespaces">All namespace names to search.</param>
    /// <returns>An enumerable of immediate child namespace names.</returns>
    internal static IEnumerable<string> GetImmediateChildNamespaces(string parent, List<string> allNamespaces)
    {
        var prefix = parent + ".";
        return allNamespaces.Where(n =>
            n.StartsWith(prefix, StringComparison.Ordinal) &&
            !n.Substring(prefix.Length).Contains('.'));
    }

    /// <summary>
    ///     Splits a forward-slash-delimited folder path into a parent subfolder and a short name.
    /// </summary>
    /// <param name="path">The full folder path (e.g. <c>ApiMark.DotNet.Fixtures/Inner</c>).</param>
    /// <param name="subFolder">
    ///     The portion before the last slash, or an empty string when there is no slash.
    /// </param>
    /// <param name="shortName">The portion after the last slash, or the whole string when there is no slash.</param>
    internal static void SplitPath(string path, out string subFolder, out string shortName)
    {
        var lastSlash = path.LastIndexOf('/');
        if (lastSlash < 0)
        {
            subFolder = string.Empty;
            shortName = path;
        }
        else
        {
            subFolder = path.Substring(0, lastSlash);
            shortName = path.Substring(lastSlash + 1);
        }
    }

    // =========================================================================
    // Visibility filtering
    // =========================================================================

    /// <summary>
    ///     Returns <c>true</c> when <paramref name="type"/> satisfies the visibility
    ///     setting in <see cref="DotNetAstModel.Options"/>.
    /// </summary>
    /// <param name="type">The type definition to test.</param>
    /// <returns><c>true</c> when the type is visible under the current visibility setting.</returns>
    internal bool IsTypeVisible(TypeDefinition type)
    {
        // Nested types are not emitted as top-level pages
        if (type.IsNested)
        {
            return false;
        }

        return Model.Options.Visibility switch
        {
            ApiVisibility.Public => type.IsPublic,
            ApiVisibility.PublicAndProtected => type.IsPublic || type.IsNestedFamily || type.IsNestedFamilyOrAssembly,
            ApiVisibility.All => true,
            _ => type.IsPublic,
        };
    }

    /// <summary>
    ///     Returns the nested types of <paramref name="type"/> that satisfy the visibility
    ///     setting in <see cref="DotNetAstModel.Options"/>, ordered by name.
    /// </summary>
    /// <remarks>
    ///     Nested-type visibility is tested with the <c>IsNested*</c> flags rather than the
    ///     top-level <c>IsPublic</c> flag because Cecil assigns separate flags to each
    ///     nested-access level. Ordering by name ensures deterministic output regardless of
    ///     metadata table order.
    /// </remarks>
    /// <param name="type">The declaring type whose nested types are to be filtered.</param>
    /// <returns>
    ///     An ordered enumerable of nested <see cref="TypeDefinition"/> instances that pass
    ///     the current visibility filter.
    /// </returns>
    internal IEnumerable<TypeDefinition> GetVisibleNestedTypes(TypeDefinition type)
    {
        return type.NestedTypes
            .Where(t => Model.Options.Visibility switch
            {
                ApiVisibility.Public => t.IsNestedPublic,
                ApiVisibility.PublicAndProtected => t.IsNestedPublic || t.IsNestedFamily || t.IsNestedFamilyOrAssembly,
                ApiVisibility.All => true,
                _ => t.IsNestedPublic,
            })
            .OrderBy(t => t.Name, StringComparer.Ordinal);
    }

    /// <summary>
    ///     Returns <c>true</c> when <paramref name="member"/> satisfies the visibility
    ///     setting in <see cref="DotNetAstModel.Options"/>.
    /// </summary>
    /// <param name="member">The member to test.</param>
    /// <returns><c>true</c> when the member is visible under the current visibility setting.</returns>
    internal bool IsMemberVisible(IMemberDefinition member)
    {
        return Model.Options.Visibility switch
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
    internal static bool IsMemberPublic(IMemberDefinition member) => member switch
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
    internal static bool IsMemberPublicOrProtected(IMemberDefinition member) => member switch
    {
        MethodDefinition m => m.IsPublic || m.IsFamily || m.IsFamilyOrAssembly,
        PropertyDefinition p => IsPropertyPublicOrProtected(p),
        FieldDefinition f => f.IsPublic || f.IsFamily || f.IsFamilyOrAssembly,
        EventDefinition e => (e.AddMethod?.IsPublic ?? false) || (e.AddMethod?.IsFamily ?? false) || (e.AddMethod?.IsFamilyOrAssembly ?? false),
        _ => false,
    };

    /// <summary>Returns <c>true</c> when <paramref name="p"/> has a public or protected getter or setter.</summary>
    /// <param name="p">The property to inspect.</param>
    /// <returns><c>true</c> when at least one accessor is publicly or protected-family accessible.</returns>
    internal static bool IsPropertyPublicOrProtected(PropertyDefinition p)
    {
        var getterVisible = p.GetMethod != null && (p.GetMethod.IsPublic || p.GetMethod.IsFamily || p.GetMethod.IsFamilyOrAssembly);
        var setterVisible = p.SetMethod != null && (p.SetMethod.IsPublic || p.SetMethod.IsFamily || p.SetMethod.IsFamilyOrAssembly);
        return getterVisible || setterVisible;
    }

    /// <summary>
    ///     Enumerates all members of <paramref name="type"/> that pass the current visibility,
    ///     obsolete, and compiler-generated filters.
    /// </summary>
    /// <param name="type">The type whose members to inspect.</param>
    /// <returns>An enumerable of visible member definitions.</returns>
    internal IEnumerable<IMemberDefinition> GetVisibleMembers(TypeDefinition type)
    {
        // Methods: exclude special-name accessors (property getters/setters, event add/remove)
        // but always include constructors
        foreach (var method in type.Methods
            .Where(m => !IsSpecialNameNonConstructor(m) && !IsCompilerGenerated(m) && ShouldIncludeMember(m)))
        {
            yield return method;
        }

        foreach (var prop in type.Properties.Where(ShouldIncludeMember))
        {
            yield return prop;
        }

        // Fields: skip compiler-generated backing fields (names contain angle brackets) and
        // the compiler-generated enum backing field named "value__" that does not appear
        // in source and has no meaningful documentation
        foreach (var field in type.Fields
            .Where(f => f.Name != "value__" && !IsCompilerGeneratedField(f) && ShouldIncludeMember(f)))
        {
            yield return field;
        }

        foreach (var evt in type.Events.Where(ShouldIncludeMember))
        {
            yield return evt;
        }
    }

    /// <summary>Returns <c>true</c> when <paramref name="member"/> passes both visibility and obsolete filters.</summary>
    /// <param name="member">The member to evaluate.</param>
    /// <returns><c>true</c> when the member is visible and not filtered out by the obsolete setting.</returns>
    internal bool ShouldIncludeMember(IMemberDefinition member) =>
        IsMemberVisible(member) && (Model.Options.IncludeObsolete || !IsObsolete(member));

    // =========================================================================
    // Member kind / name helpers
    // =========================================================================

    /// <summary>Returns <c>true</c> when <paramref name="method"/> is a C# user-defined operator overload.</summary>
    /// <param name="method">The method to test.</param>
    /// <returns><c>true</c> when the method has the special-name flag and a name starting with <c>op_</c>.</returns>
    internal static bool IsOperator(MethodDefinition method) =>
        method.IsSpecialName && method.Name.StartsWith("op_", StringComparison.Ordinal);

    /// <summary>Returns <c>true</c> when <paramref name="method"/> is a special-name accessor that is not a constructor or operator.</summary>
    /// <param name="method">The method to test.</param>
    /// <returns><c>true</c> for property getters/setters and event add/remove methods.</returns>
    internal static bool IsSpecialNameNonConstructor(MethodDefinition method) =>
        method.IsSpecialName && method.Name != ConstructorMethodName && !IsOperator(method);

    /// <summary>Returns <c>true</c> when <paramref name="field"/> is a compiler-generated backing field.</summary>
    /// <param name="field">The field to test.</param>
    /// <returns><c>true</c> when the field name contains angle brackets (compiler-generated backing fields).</returns>
    internal static bool IsCompilerGeneratedField(FieldDefinition field) =>
        field.Name.Contains('<') || field.Name.Contains('>');

    /// <summary>Returns the display name for a member as it should appear in documentation tables.</summary>
    /// <param name="member">The member whose display name to compute.</param>
    /// <returns>The display name string (constructor uses the declaring type's simple name).</returns>
    internal static string GetMemberDisplayName(IMemberDefinition member)
    {
        return member switch
        {
            MethodDefinition m => BuildMethodDisplayName(m),
            PropertyDefinition p => p.Name,
            FieldDefinition f => f.Name,
            EventDefinition e => e.Name,
            _ => member.Name,
        };
    }

    /// <summary>
    ///     Returns the <see cref="TypeReference"/> representing the type of a member, used to
    ///     linkify the type column in documentation tables.
    /// </summary>
    /// <remarks>
    ///     Constructors have no meaningful return type and return <see langword="null"/> so that
    ///     callers can omit the type column from constructor rows.
    /// </remarks>
    /// <param name="member">The member whose type reference to retrieve.</param>
    /// <returns>
    ///     The type reference for properties, fields, events, and non-constructor methods;
    ///     <see langword="null"/> for constructors and unrecognized member kinds.
    /// </returns>
    internal static TypeReference? GetMemberTypeRef(IMemberDefinition member) => member switch
    {
        MethodDefinition m when m.Name == ConstructorMethodName => null,
        MethodDefinition m => m.ReturnType,
        PropertyDefinition p => p.PropertyType,
        FieldDefinition f => f.FieldType,
        EventDefinition e => e.EventType,
        _ => null,
    };

    /// <summary>
    ///     Returns the file-system-safe name used to form the member page path.
    ///     Constructors use the declaring type name; all others use the member name directly.
    /// </summary>
    /// <param name="member">The member whose file name to compute.</param>
    /// <param name="declaringType">The type that declares <paramref name="member"/>.</param>
    /// <returns>A string suitable for use as a file name (without extension).</returns>
    internal static string GetSanitizedMemberFileName(IMemberDefinition member, TypeDefinition declaringType)
    {
        return member switch
        {
            MethodDefinition m => BuildMethodFileName(m, declaringType),
            PropertyDefinition p => p.Name,
            FieldDefinition f => f.Name,
            EventDefinition e => e.Name,
            _ => member.Name,
        };
    }

    // =========================================================================
    // XML doc identifiers
    // =========================================================================

    /// <summary>Builds the XML doc member identifier for a type (e.g. <c>T:Namespace.TypeName</c>).</summary>
    /// <param name="type">The type definition.</param>
    /// <returns>The XML doc member identifier string.</returns>
    internal static string BuildTypeId(TypeDefinition type) =>
        $"T:{type.FullName.Replace('/', '.')}";

    /// <summary>Builds the XML doc member identifier for an arbitrary member.</summary>
    /// <param name="member">The member definition.</param>
    /// <returns>The XML doc member identifier string.</returns>
    internal static string BuildMemberId(IMemberDefinition member) => member switch
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
    internal static string BuildMethodId(MethodDefinition method)
    {
        var typeName = method.DeclaringType.FullName.Replace('/', '.');

        // XML doc format uses #ctor for constructors; IL metadata uses .ctor
        var methodName = method.Name == ConstructorMethodName ? "#ctor" : method.Name;

        // XML doc format includes parenthesized parameter list only when parameters exist
        if (!method.HasParameters)
        {
            return $"M:{typeName}.{methodName}";
        }

        // Normalize nested-type separators: Cecil uses '/' in FullName (e.g. Outer/Inner)
        // but XML doc IDs always use '.' (e.g. Outer.Inner)
        var paramList = string.Join(",", method.Parameters.Select(p => p.ParameterType.FullName.Replace('/', '.')));

        // Conversion operators carry a ~ReturnType suffix in the XML doc member ID
        // (e.g. M:Type.op_Implicit(SourceType)~TargetType) that distinguishes overloads
        // with the same source type but different target types
        if (method.Name is "op_Implicit" or "op_Explicit")
        {
            // Normalize nested-type separators: Cecil uses '/' in FullName (e.g. OuterClass/Inner)
            // but XML doc IDs always use '.' (e.g. OuterClass.Inner)
            return $"M:{typeName}.{methodName}({paramList})~{method.ReturnType.FullName.Replace('/', '.')}";
        }

        return $"M:{typeName}.{methodName}({paramList})";
    }

    // =========================================================================
    // Signature builders
    // =========================================================================

    /// <summary>Returns <see langword="true"/> when <paramref name="type"/> is a delegate type.</summary>
    /// <remarks>
    ///     The C# compiler compiles every <c>delegate</c> declaration into a sealed class that
    ///     inherits <c>System.MulticastDelegate</c>. Testing the base type name is therefore the
    ///     reliable way to detect delegates from compiled metadata.
    /// </remarks>
    /// <param name="type">The type to test.</param>
    /// <returns>
    ///     <see langword="true"/> when <paramref name="type"/> derives directly from
    ///     <c>System.MulticastDelegate</c>.
    /// </returns>
    internal static bool IsDelegate(TypeDefinition type) =>
        type.BaseType?.FullName == "System.MulticastDelegate";

    /// <summary>
    ///     Builds the source-level <c>public delegate</c> signature for a delegate type,
    ///     derived from the compiler-injected <c>Invoke</c> method's return type and parameters.
    /// </summary>
    /// <remarks>
    ///     The C# compiler injects three methods into every delegate type: <c>Invoke</c>,
    ///     <c>BeginInvoke</c>, and <c>EndInvoke</c>. <c>Invoke</c> carries the exact signature
    ///     the developer wrote in source, so it is used here to reconstruct the canonical
    ///     <c>public delegate ReturnType Name(params)</c> form.
    /// </remarks>
    /// <param name="type">The delegate type (must satisfy <see cref="IsDelegate"/>).</param>
    /// <param name="contextNamespace">Used to simplify type names in the signature.</param>
    /// <returns>A string of the form <c>public delegate void ServiceEvent(DateTime timestamp, ...)</c>.</returns>
    internal static string BuildDelegateSignature(TypeDefinition type, string contextNamespace)
    {
        var invoke = type.Methods.FirstOrDefault(m => m.Name == "Invoke");
        if (invoke == null)
        {
            // Malformed delegate — fall back to a void declaration without parameters
            return $"public delegate void {StripArity(type.Name)}()";
        }

        var returnType = TypeNameSimplifier.Simplify(invoke.ReturnType, contextNamespace);
        var name = StripArity(type.Name);
        if (type.HasGenericParameters)
        {
            var args = string.Join(", ", type.GenericParameters.Select(p => p.Name));
            name = $"{name}<{args}>";
        }

        var parameters = string.Join(", ", invoke.Parameters.Select(p =>
            $"{TypeNameSimplifier.Simplify(p.ParameterType, contextNamespace)} {p.Name}"));

        return $"public delegate {returnType} {name}({parameters})";
    }

    /// <summary>Builds a human-readable C# declaration signature for a type definition.</summary>
    /// <remarks>
    ///     Base types <c>System.Object</c>, <c>System.ValueType</c>, <c>System.Enum</c>, and
    ///     <c>System.MulticastDelegate</c> are suppressed because they are implicit compiler-assigned
    ///     roots that carry no information for readers; showing them would add noise to every class,
    ///     struct, enum, and delegate signature. The <paramref name="contextNamespace"/> is forwarded
    ///     to <see cref="TypeNameSimplifier"/> so that types declared in the same namespace are
    ///     rendered without their namespace prefix, keeping signatures concise.
    ///     <para>
    ///         Delegate types are detected and dispatched to <see cref="BuildDelegateSignature"/>
    ///         before the class/struct/enum/interface switch so the output always shows the
    ///         source-level <c>delegate</c> keyword rather than <c>sealed class</c>.
    ///     </para>
    /// </remarks>
    /// <param name="type">The type definition to represent.</param>
    /// <param name="contextNamespace">Used to simplify base type and interface names in the signature.</param>
    /// <returns>
    ///     A string of the form <c>public class Name</c>, <c>public interface Name&lt;T&gt;</c>,
    ///     <c>public delegate void Name(params)</c>, or
    ///     <c>public class Name : BaseClass, IInterface</c> when direct inheritance is present.
    /// </returns>
    internal static string BuildTypeSignature(TypeDefinition type, string contextNamespace)
    {
        // Delegates are compiled to sealed classes deriving from MulticastDelegate.
        // Detect them before the generic class/struct/enum/interface switch so that
        // the output shows the original source-level delegate syntax.
        if (IsDelegate(type))
        {
            return BuildDelegateSignature(type, contextNamespace);
        }

        var keyword = type switch
        {
            { IsInterface: true } => "interface",
            { IsEnum: true } => "enum",
            { IsValueType: true } => "struct",
            _ => "class",
        };

        var classModifier = string.Empty;
        if (keyword == "class")
        {
            if (type.IsAbstract && type.IsSealed)
            {
                classModifier = "static ";
            }
            else if (type.IsSealed)
            {
                classModifier = "sealed ";
            }
        }

        var name = StripArity(type.Name);
        if (type.HasGenericParameters)
        {
            var args = string.Join(", ", type.GenericParameters.Select(p => p.Name));
            name = $"{name}<{args}>";
        }

        // Collect direct base class (excluding well-known root types) and directly declared interfaces
        // so the signature shows inheritance at a glance without opening the source file
        var bases = new List<string>();
        if (type.BaseType != null)
        {
            var baseName = type.BaseType.FullName;

            // Skip the standard implicit base types that carry no information for readers
            if (baseName is not ("System.Object" or "System.ValueType" or "System.Enum" or "System.MulticastDelegate"))
            {
                bases.Add(TypeNameSimplifier.Simplify(type.BaseType, contextNamespace));
            }
        }

        // Include all directly declared interfaces — transitive inheritance is omitted
        foreach (var interfaceRef in type.Interfaces)
        {
            bases.Add(TypeNameSimplifier.Simplify(interfaceRef.InterfaceType, contextNamespace));
        }

        if (bases.Count > 0)
        {
            name = $"{name} : {string.Join(", ", bases)}";
        }

        return $"public {classModifier}{keyword} {name}";
    }

    /// <summary>Dispatches to the appropriate signature builder based on the runtime member type.</summary>
    /// <param name="member">The member to represent.</param>
    /// <param name="contextNamespace">Used to simplify type names in the signature.</param>
    /// <returns>A human-readable C# declaration signature string.</returns>
    internal static string BuildMemberSignature(IMemberDefinition member, string contextNamespace)
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
    internal static string BuildMethodSignature(MethodDefinition method, string contextNamespace)
    {
        if (IsOperator(method))
        {
            return BuildOperatorSignature(method, contextNamespace);
        }

        var returnType = TypeNameSimplifier.Simplify(
            method.ReturnType,
            contextNamespace,
            HasNullableAnnotation(method.MethodReturnType.CustomAttributes));
        var isExtensionMethod = IsExtensionMethod(method);

        // Use the declaring type name for constructors rather than ConstructorMethodName
        var name = method.Name == ConstructorMethodName
            ? StripArity(method.DeclaringType.Name)
            : method.Name;

        var parameters = string.Join(", ", method.Parameters.Select((p, index) =>
        {
            var receiverPrefix = isExtensionMethod && index == 0 ? "this " : string.Empty;
            var paramType = TypeNameSimplifier.Simplify(
                p.ParameterType,
                contextNamespace,
                HasNullableAnnotation(p.CustomAttributes));
            return $"{receiverPrefix}{paramType} {p.Name}";
        }));

        var accessibility = GetAccessibilityKeyword(method);
        var staticModifier = method.IsStatic && method.Name != ConstructorMethodName ? " static" : string.Empty;
        return method.Name == ConstructorMethodName
            ? $"{accessibility} {name}({parameters})"
            : $"{accessibility}{staticModifier} {returnType} {name}({parameters})";
    }

    /// <summary>Builds a human-readable C# property declaration signature.</summary>
    /// <param name="prop">The property definition.</param>
    /// <param name="contextNamespace">Used to simplify the property type name.</param>
    /// <returns>The property signature string.</returns>
    internal static string BuildPropertySignature(PropertyDefinition prop, string contextNamespace)
    {
        var typeName = TypeNameSimplifier.Simplify(
            prop.PropertyType,
            contextNamespace,
            HasNullableAnnotation(prop.CustomAttributes));
        var accessibility = GetAccessibilityKeyword(prop.GetMethod ?? prop.SetMethod!);
        var accessors = BuildPropertyAccessors(prop);
        return $"{accessibility} {typeName} {prop.Name} {{ {accessors} }}";
    }

    /// <summary>Builds the accessor portion of a property signature (e.g. <c>get; internal set;</c>).</summary>
    /// <param name="prop">The property definition.</param>
    /// <returns>The accessor string to embed in the property signature.</returns>
    internal static string BuildPropertyAccessors(PropertyDefinition prop)
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
    internal static string BuildFieldSignature(FieldDefinition field, string contextNamespace)
    {
        var typeName = TypeNameSimplifier.Simplify(
            field.FieldType,
            contextNamespace,
            HasNullableAnnotation(field.CustomAttributes));

        // Determine modifier(s) from compile-time flags; literals imply IsStatic so they
        // must be tested before the IsStatic arm to avoid showing "static const"
        var modifiers = field switch
        {
            { IsLiteral: true } => "const",
            { IsStatic: true, IsInitOnly: true } => "static readonly",
            { IsStatic: true } => "static",
            { IsInitOnly: true } => "readonly",
            _ => string.Empty,
        };

        var gap = modifiers.Length > 0 ? " " : string.Empty;
        return $"{GetAccessibilityKeyword(field)} {modifiers}{gap}{typeName} {field.Name}";
    }

    /// <summary>Builds a human-readable C# event declaration signature.</summary>
    /// <param name="evt">The event definition.</param>
    /// <param name="contextNamespace">Used to simplify the event type name.</param>
    /// <returns>The event signature string.</returns>
    internal static string BuildEventSignature(EventDefinition evt, string contextNamespace)
    {
        var typeName = TypeNameSimplifier.Simplify(
            evt.EventType,
            contextNamespace,
            HasNullableAnnotation(evt.CustomAttributes));
        return $"{GetAccessibilityKeyword(evt)} event {typeName} {evt.Name}";
    }

    /// <summary>Returns the C# accessibility keyword for a method (e.g. <c>"public"</c> or <c>"protected"</c>).</summary>
    /// <param name="method">The method whose accessibility to report.</param>
    /// <returns>The lowercase C# accessibility keyword string.</returns>
    internal static string GetAccessibilityKeyword(MethodDefinition method) => method switch
    {
        { IsPublic: true } => "public",
        { IsFamilyOrAssembly: true } => "protected internal",
        { IsFamily: true } => "protected",
        { IsAssembly: true } => "internal",
        _ => "private",
    };

    /// <summary>Returns the C# accessibility keyword for a field (e.g. <c>"public"</c> or <c>"protected"</c>).</summary>
    /// <remarks>
    ///     Mirrors <see cref="GetAccessibilityKeyword(MethodDefinition)"/> for fields so that
    ///     <see cref="BuildFieldSignature"/> can render the correct modifier rather than always
    ///     hard-coding <c>"public"</c>.
    /// </remarks>
    /// <param name="field">The field whose accessibility to report.</param>
    /// <returns>The lowercase C# accessibility keyword string.</returns>
    internal static string GetAccessibilityKeyword(FieldDefinition field) => field switch
    {
        { IsPublic: true } => "public",
        { IsFamilyOrAssembly: true } => "protected internal",
        { IsFamily: true } => "protected",
        { IsAssembly: true } => "internal",
        _ => "private",
    };

    /// <summary>Returns the C# accessibility keyword for an event, derived from its add-accessor (e.g. <c>"public"</c> or <c>"protected"</c>).</summary>
    /// <remarks>
    ///     Mirrors <see cref="GetAccessibilityKeyword(MethodDefinition)"/> for events so that
    ///     <see cref="BuildEventSignature"/> can render the correct modifier rather than always
    ///     hard-coding <c>"public"</c>. The add-accessor's accessibility governs the event's
    ///     declared visibility.
    /// </remarks>
    /// <param name="evt">The event whose accessibility to report.</param>
    /// <returns>The lowercase C# accessibility keyword string, or <c>"private"</c> when no add-accessor is present.</returns>
    internal static string GetAccessibilityKeyword(EventDefinition evt) =>
        evt.AddMethod != null ? GetAccessibilityKeyword(evt.AddMethod) : "private";

    internal static string BuildMethodDisplayName(MethodDefinition method)
    {
        var baseName = GetMethodGroupName(method);
        var parameters = string.Join(", ", method.Parameters.Select(p =>
            TypeNameSimplifier.Simplify(p.ParameterType, method.DeclaringType.Namespace)));
        return $"{baseName}({parameters})";
    }

    internal static string BuildMethodFileName(MethodDefinition method, TypeDefinition declaringType)
    {
        return method.Name == ConstructorMethodName
            ? StripArity(declaringType.Name)
            : method.Name;
    }

    internal static string GetMethodGroupDisplayName(MethodDefinition method, int overloadCount)
    {
        var baseName = GetMethodGroupName(method);
        return overloadCount > 1 ? $"{baseName} ({overloadCount} overloads)" : baseName;
    }

    internal static string GetMethodGroupName(MethodDefinition method)
    {
        if (method.Name == ConstructorMethodName)
        {
            return StripArity(method.DeclaringType.Name);
        }

        if (IsOperator(method))
        {
            return GetOperatorCSharpName(method);
        }

        return method.Name;
    }

    /// <summary>
    ///     Returns the C# source-level name for an operator method, suitable for use as a heading
    ///     and display name in documentation (e.g. <c>operator +</c> or <c>implicit operator double</c>).
    /// </summary>
    /// <param name="method">The operator method. Must satisfy <see cref="IsOperator"/>.</param>
    /// <returns>
    ///     For conversion operators, returns <c>implicit operator {ReturnType}</c> or
    ///     <c>explicit operator {ReturnType}</c>. For all others, returns <c>operator {symbol}</c>.
    /// </returns>
    internal static string GetOperatorCSharpName(MethodDefinition method)
    {
        var conversionKeyword = method.Name switch
        {
            "op_Implicit" => "implicit",
            "op_Explicit" => "explicit",
            _ => null,
        };

        if (conversionKeyword != null)
        {
            var returnType = TypeNameSimplifier.Simplify(method.ReturnType, method.DeclaringType.Namespace);
            return $"{conversionKeyword} operator {returnType}";
        }

        return $"operator {GetOperatorSymbol(method.Name)}";
    }

    /// <summary>
    ///     Maps an IL operator method name (e.g. <c>op_Addition</c>) to its C# source-level
    ///     symbol (e.g. <c>+</c>). Returns the IL name unchanged when no mapping is defined.
    /// </summary>
    /// <param name="ilName">The IL operator method name.</param>
    /// <returns>The C# symbol string, or <paramref name="ilName"/> when unmapped.</returns>
    internal static string GetOperatorSymbol(string ilName) => ilName switch
    {
        "op_Addition" => "+",
        "op_Subtraction" => "-",
        "op_Multiply" => "*",
        "op_Division" => "/",
        "op_Modulus" => "%",
        "op_BitwiseAnd" => "&",
        "op_BitwiseOr" => "|",
        "op_ExclusiveOr" => "^",
        "op_LeftShift" => "<<",
        "op_RightShift" => ">>",
        "op_Equality" => "==",
        "op_Inequality" => "!=",
        "op_LessThan" => "<",
        "op_GreaterThan" => ">",
        "op_LessThanOrEqual" => "<=",
        "op_GreaterThanOrEqual" => ">=",
        "op_UnaryNegation" => "-",
        "op_UnaryPlus" => "+",
        "op_Increment" => "++",
        "op_Decrement" => "--",
        "op_OnesComplement" => "~",
        "op_LogicalNot" => "!",
        "op_True" => "true",
        "op_False" => "false",
        _ => ilName,
    };

    /// <summary>Builds a C# operator declaration signature for an operator method.</summary>
    /// <param name="method">The operator method. Must satisfy <see cref="IsOperator"/>.</param>
    /// <param name="contextNamespace">Used to simplify type names in parameters and return type.</param>
    /// <returns>
    ///     For conversion operators: <c>public static implicit operator T(U v)</c>.
    ///     For all others: <c>public static T operator +(T left, T right)</c>.
    /// </returns>
    internal static string BuildOperatorSignature(MethodDefinition method, string contextNamespace)
    {
        var accessibility = GetAccessibilityKeyword(method);
        var returnType = TypeNameSimplifier.Simplify(
            method.ReturnType,
            contextNamespace,
            HasNullableAnnotation(method.MethodReturnType.CustomAttributes));
        var parameters = string.Join(", ", method.Parameters.Select(p =>
        {
            var paramType = TypeNameSimplifier.Simplify(
                p.ParameterType,
                contextNamespace,
                HasNullableAnnotation(p.CustomAttributes));
            return $"{paramType} {p.Name}";
        }));

        return method.Name switch
        {
            "op_Implicit" => $"{accessibility} static implicit operator {returnType}({parameters})",
            "op_Explicit" => $"{accessibility} static explicit operator {returnType}({parameters})",
            _ => $"{accessibility} static {returnType} operator {GetOperatorSymbol(method.Name)}({parameters})",
        };
    }

    // =========================================================================
    // Nullable / compiler attribute helpers
    // =========================================================================

    /// <summary>
    ///     Returns <see langword="true"/> when the member's primary type (return type, property
    ///     type, field type, or event type) carries a <c>NullableAttribute(2)</c> annotation on
    ///     its outermost position, indicating a C# 8+ nullable reference type.
    /// </summary>
    /// <remarks>
    ///     Mono.Cecil stores nullable-reference annotations on the containing member (method return
    ///     parameter, property, field, or event) rather than on the <see cref="TypeReference"/>
    ///     itself. This method reads the correct <see cref="ICustomAttributeProvider"/> for each
    ///     member kind and delegates to <see cref="HasNullableAnnotation"/>.
    /// </remarks>
    internal static bool IsMemberTypeNullableAnnotated(IMemberDefinition member) => member switch
    {
        MethodDefinition m when m.Name != ConstructorMethodName
            => HasNullableAnnotation(m.MethodReturnType.CustomAttributes),
        PropertyDefinition p => HasNullableAnnotation(p.CustomAttributes),
        FieldDefinition f => HasNullableAnnotation(f.CustomAttributes),
        EventDefinition e => HasNullableAnnotation(e.CustomAttributes),
        _ => false,
    };

    /// <summary>
    ///     Returns <see langword="true"/> when <paramref name="attrs"/> contains a
    ///     <c>System.Runtime.CompilerServices.NullableAttribute</c> whose first (or only)
    ///     byte argument is <c>2</c>, which is the compiler-emitted encoding for a nullable
    ///     reference type annotation (<c>?</c>).
    /// </summary>
    /// <remarks>
    ///     The attribute has two constructor forms: <c>NullableAttribute(byte)</c> for simple
    ///     types and <c>NullableAttribute(byte[])</c> for composite types (arrays, generics).
    ///     In the composite form, byte index 0 represents the outermost type's nullability.
    /// </remarks>
    internal static bool HasNullableAnnotation(IEnumerable<CustomAttribute> attrs)
    {
        var nullableAttr = attrs.FirstOrDefault(
            a => a.AttributeType.FullName == "System.Runtime.CompilerServices.NullableAttribute");
        if (nullableAttr == null || nullableAttr.ConstructorArguments.Count == 0)
        {
            return false;
        }

        var arg = nullableAttr.ConstructorArguments[0];

        // Single-byte form: NullableAttribute(byte)
        if (arg.Value is byte b)
        {
            return b == 2;
        }

        // Byte-array form: NullableAttribute(byte[]) — index 0 is the outermost type
        if (arg.Value is CustomAttributeArgument[] arr && arr.Length > 0 && arr[0].Value is byte first)
        {
            return first == 2;
        }

        return false;
    }

    /// <summary>
    ///     Returns <c>true</c> when <paramref name="method"/> is an extension method
    ///     (static and carries <c>ExtensionAttribute</c>).
    /// </summary>
    /// <param name="method">The method definition to inspect.</param>
    /// <returns><c>true</c> when the method is an extension method.</returns>
    internal static bool IsExtensionMethod(MethodDefinition method) =>
        method.IsStatic && method.CustomAttributes.Any(attribute =>
            attribute.AttributeType.FullName == "System.Runtime.CompilerServices.ExtensionAttribute");

    /// <summary>
    ///     Returns <c>true</c> when <paramref name="provider"/> carries a
    ///     <c>CompilerGeneratedAttribute</c>, indicating it was synthesized by the compiler.
    /// </summary>
    /// <param name="provider">The metadata element to inspect.</param>
    /// <returns><c>true</c> when the element is compiler-generated.</returns>
    internal static bool IsCompilerGenerated(ICustomAttributeProvider provider) =>
        provider.CustomAttributes.Any(a =>
            a.AttributeType.FullName == "System.Runtime.CompilerServices.CompilerGeneratedAttribute");

    /// <summary>
    ///     Returns <c>true</c> when <paramref name="type"/> is a compiler-generated
    ///     type (name contains angle brackets, or carries CompilerGeneratedAttribute).
    /// </summary>
    /// <param name="type">The type definition to inspect.</param>
    /// <returns><c>true</c> when the type is compiler-generated.</returns>
    internal static bool IsCompilerGenerated(TypeDefinition type) =>
        type.Name.Contains('<') || type.Name.Contains('>') || IsCompilerGenerated((ICustomAttributeProvider)type);

    /// <summary>
    ///     Returns <c>true</c> when <paramref name="type"/> is a <c>NamespaceDoc</c>
    ///     carrier — an internal static class used to attach XML documentation to a namespace.
    /// </summary>
    /// <param name="type">The type definition to inspect.</param>
    /// <returns><c>true</c> when the type is a namespace documentation carrier.</returns>
    internal static bool IsNamespaceDocCarrier(TypeDefinition type) =>
        type.Name == "NamespaceDoc" && type.IsClass && type.IsAbstract && type.IsSealed && type.IsNotPublic;

    /// <summary>
    ///     Returns <c>true</c> when <paramref name="provider"/> carries an
    ///     <c>ObsoleteAttribute</c>.
    /// </summary>
    /// <param name="provider">The metadata element to inspect.</param>
    /// <returns><c>true</c> when the element is marked obsolete.</returns>
    internal static bool IsObsolete(ICustomAttributeProvider provider) =>
        provider.CustomAttributes.Any(a => a.AttributeType.FullName == "System.ObsoleteAttribute");

    // =========================================================================
    // Name formatting helpers
    // =========================================================================

    /// <summary>Removes the generic arity suffix (e.g. <c>`1</c>) from a type name.</summary>
    /// <param name="name">The raw type name that may contain a backtick arity suffix.</param>
    /// <returns>The name without the arity suffix.</returns>
    internal static string StripArity(string name)
    {
        var tick = name.IndexOf('`');
        return tick >= 0 ? name.Substring(0, tick) : name;
    }

    /// <summary>
    ///     Converts the IL backtick arity suffix to a plain numeric suffix for file-system-safe names.
    ///     For example, <c>Foo`2</c> becomes <c>Foo2</c>; <c>Foo</c> is unchanged.
    /// </summary>
    /// <param name="name">The raw IL type name that may contain a backtick arity suffix.</param>
    /// <returns>The name with the backtick removed but the arity count preserved.</returns>
    internal static string FlattenArity(string name) => TypeNameSimplifier.FlattenArity(name);
}
