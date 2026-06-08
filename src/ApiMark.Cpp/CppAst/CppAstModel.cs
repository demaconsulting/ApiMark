namespace ApiMark.Cpp.CppAst;

/// <summary>Specifies the access level of a C++ class member.</summary>
public enum CppAccessibility
{
    /// <summary>The member is publicly accessible.</summary>
    Public,

    /// <summary>The member is accessible to derived classes.</summary>
    Protected,

    /// <summary>The member is accessible only within the declaring class.</summary>
    Private,
}

/// <summary>Records the source file and line where a C++ declaration appears.</summary>
/// <param name="File">Absolute path to the source file.</param>
/// <param name="Line">One-based line number within the source file.</param>
public record CppSourceLocation(string File, int Line);

/// <summary>Holds a parameter name and its documentation text extracted from a Doxygen comment.</summary>
/// <param name="Name">The exact parameter name as it appears in the function signature.</param>
/// <param name="Description">The trimmed, normalized description text from the corresponding <c>@param</c> tag.</param>
public record CppParamDoc(string Name, string Description);

/// <summary>
///     Holds the structured documentation extracted from a Doxygen comment block attached
///     to a C++ declaration.
/// </summary>
/// <remarks>
///     All text fields are pre-normalized: leading and trailing whitespace has been removed
///     and embedded newlines have been collapsed to single spaces where appropriate.
///     <see langword="null"/> means no documentation was found for that slot.
/// </remarks>
/// <param name="Summary">
///     The brief description from <c>@brief</c>, or the first plain paragraph when
///     <c>@brief</c> is absent.
/// </param>
/// <param name="Details">
///     Extended description from a <c>@details</c> or <c>@remarks</c> block, preserved
///     with internal whitespace intact.
/// </param>
/// <param name="Params">One entry for each <c>@param</c> tag found on the declaration.</param>
/// <param name="Returns">Return description from a <c>@return</c> or <c>@returns</c> tag.</param>
public record CppDocComment(
    string? Summary,
    string? Details,
    IReadOnlyList<CppParamDoc> Params,
    string? Returns);

/// <summary>Names a base type in a C++ class inheritance list.</summary>
/// <param name="Name">
///     The display name of the base type as clang reports it in <c>type.qualType</c>,
///     e.g. <c>"Shape"</c> or <c>"std::enable_shared_from_this&lt;Foo&gt;"</c>.
/// </param>
public record CppBaseType(string Name);

/// <summary>
///     Names a single template type parameter of a class or function template.
/// </summary>
/// <param name="Name">
///     The parameter name as declared, e.g. <c>"T"</c> or <c>"U"</c>. Non-type
///     template parameters also appear here with their declared name.
/// </param>
public record CppTemplateParam(string Name);

/// <summary>Represents a single enumerator constant declared inside a C++ enum.</summary>
/// <param name="Name">The enumerator name, e.g. <c>"Active"</c>.</param>
/// <param name="Doc">Doxygen documentation attached to this value, or <see langword="null"/> when absent.</param>
public record CppEnumValue(string Name, CppDocComment? Doc);

/// <summary>Represents a single parameter in a C++ function or method signature.</summary>
/// <param name="Name">The parameter name as declared in the header.</param>
/// <param name="TypeName">
///     The parameter type as clang reports it in <c>type.qualType</c>,
///     e.g. <c>"const std::string &amp;"</c>.
/// </param>
public record CppParameter(string Name, string TypeName);

/// <summary>Represents a field (data member) of a C++ class or struct.</summary>
/// <param name="Name">The field name.</param>
/// <param name="TypeName">
///     The field type as clang reports it in <c>type.qualType</c>.
/// </param>
/// <param name="Accessibility">The access level of the field.</param>
/// <param name="IsStatic">
///     <see langword="true"/> when the field is a static class member variable.
/// </param>
/// <param name="IsDeprecated">
///     <see langword="true"/> when the field carries a <c>[[deprecated]]</c> attribute.
/// </param>
/// <param name="Location">Source location of the field declaration, or <see langword="null"/> when unavailable.</param>
/// <param name="Doc">Doxygen documentation attached to this field, or <see langword="null"/> when absent.</param>
public record CppField(
    string Name,
    string TypeName,
    CppAccessibility Accessibility,
    bool IsStatic,
    bool IsDeprecated,
    CppSourceLocation? Location,
    CppDocComment? Doc);

/// <summary>Represents a method, constructor, or free function in the C++ AST.</summary>
/// <remarks>
///     Both class members (methods, constructors) and namespace-level free functions
///     use this record. Callers can distinguish them by checking
///     <see cref="IsConstructor"/> and the enclosing <see cref="CppClass"/> or
///     <see cref="CppNamespaceDecl"/>.
/// </remarks>
/// <param name="Name">The function or method name.</param>
/// <param name="ReturnTypeName">
///     The return type display name. For constructors this is always <c>"void"</c>.
/// </param>
/// <param name="Parameters">Ordered list of declared parameters (excludes the implicit variadic <c>...</c>).</param>
/// <param name="Accessibility">The access level; always <see cref="CppAccessibility.Public"/> for free functions.</param>
/// <param name="IsStatic"><see langword="true"/> when the method has the <c>static</c> storage class.</param>
/// <param name="IsVirtual"><see langword="true"/> when the method is declared <c>virtual</c>.</param>
/// <param name="IsConstructor"><see langword="true"/> when the declaration is a constructor.</param>
/// <param name="IsVariadic"><see langword="true"/> when the function is variadic (ends with <c>...</c>).</param>
/// <param name="IsDeprecated">
///     <see langword="true"/> when the function carries a <c>[[deprecated]]</c> attribute.
/// </param>
/// <param name="Location">Source location of the declaration, or <see langword="null"/> when unavailable.</param>
/// <param name="Doc">Doxygen documentation attached to this function, or <see langword="null"/> when absent.</param>
public record CppFunction(
    string Name,
    string ReturnTypeName,
    IReadOnlyList<CppParameter> Parameters,
    CppAccessibility Accessibility,
    bool IsStatic,
    bool IsVirtual,
    bool IsConstructor,
    bool IsVariadic,
    bool IsDeprecated,
    CppSourceLocation? Location,
    CppDocComment? Doc);

/// <summary>Represents a C++ class or struct declaration.</summary>
/// <remarks>
///     Template classes carry their type parameters in <see cref="TemplateParams"/>; non-template
///     classes have an empty list. <see cref="Members"/> contains all constructors and methods;
///     callers use <see cref="CppFunction.IsConstructor"/> to distinguish them.
/// </remarks>
/// <param name="Name">The unqualified class name.</param>
/// <param name="BaseTypes">Direct base classes, in declaration order.</param>
/// <param name="TemplateParams">Template type parameters; empty for non-template classes.</param>
/// <param name="Members">All constructors and methods declared in the class body, in declaration order.</param>
/// <param name="Fields">All data member fields declared in the class body, in declaration order.</param>
/// <param name="IsDeprecated">
///     <see langword="true"/> when the class carries a <c>[[deprecated]]</c> attribute.
/// </param>
/// <param name="IsFinal">
///     <see langword="true"/> when the class is marked <c>final</c> and cannot be used as a base class.
/// </param>
/// <param name="Location">Source location of the class declaration, or <see langword="null"/> when unavailable.</param>
/// <param name="Doc">Doxygen documentation attached to this class, or <see langword="null"/> when absent.</param>
public record CppClass(
    string Name,
    IReadOnlyList<CppBaseType> BaseTypes,
    IReadOnlyList<CppTemplateParam> TemplateParams,
    IReadOnlyList<CppFunction> Members,
    IReadOnlyList<CppField> Fields,
    bool IsDeprecated,
    bool IsFinal,
    CppSourceLocation? Location,
    CppDocComment? Doc);

/// <summary>Represents a C++ enum declaration (both scoped <c>enum class</c> and plain <c>enum</c>).</summary>
/// <param name="Name">The unqualified enum name.</param>
/// <param name="Values">All enumerator constants in declaration order.</param>
/// <param name="IsDeprecated">
///     <see langword="true"/> when the enum carries a <c>[[deprecated]]</c> attribute.
/// </param>
/// <param name="Location">Source location of the enum declaration, or <see langword="null"/> when unavailable.</param>
/// <param name="Doc">Doxygen documentation attached to this enum, or <see langword="null"/> when absent.</param>
public record CppEnum(
    string Name,
    IReadOnlyList<CppEnumValue> Values,
    bool IsDeprecated,
    CppSourceLocation? Location,
    CppDocComment? Doc);

/// <summary>
///     Groups all owned declarations contributed by a single C++ namespace (or the global namespace).
/// </summary>
/// <remarks>
///     Multiple translation units may open the same namespace; the parser merges their contributions
///     into a single <see cref="CppNamespaceDecl"/>. The global (unnamed) namespace is represented
///     with <see cref="QualifiedName"/> equal to an empty string.
/// </remarks>
/// <param name="QualifiedName">
///     The fully-qualified C++ namespace name using <c>::</c> separators (e.g. <c>"mylib::rendering"</c>),
///     or an empty string for the global namespace.
/// </param>
/// <param name="Classes">All owned class and struct declarations contributed to this namespace.</param>
/// <param name="FreeFunctions">All owned free function declarations contributed to this namespace.</param>
/// <param name="Enums">All owned enum declarations contributed to this namespace.</param>
/// <param name="Doc">Doxygen documentation attached to the namespace, or <see langword="null"/> when absent.</param>
public record CppNamespaceDecl(
    string QualifiedName,
    IReadOnlyList<CppClass> Classes,
    IReadOnlyList<CppFunction> FreeFunctions,
    IReadOnlyList<CppEnum> Enums,
    CppDocComment? Doc);

/// <summary>Encapsulates the complete parsed result returned by <see cref="ClangAstParser"/>.</summary>
/// <param name="Namespaces">
///     All namespaces that contain at least one owned declaration. The global namespace
///     appears with <see cref="CppNamespaceDecl.QualifiedName"/> equal to an empty string.
/// </param>
/// <param name="Errors">
///     Error and warning messages emitted by clang to standard error during parsing.
///     A non-empty list does not necessarily mean the output is unusable — clang may emit
///     warnings for system or third-party headers while successfully parsing owned headers.
/// </param>
public record CppCompilationResult(
    IReadOnlyList<CppNamespaceDecl> Namespaces,
    IReadOnlyList<string> Errors);
