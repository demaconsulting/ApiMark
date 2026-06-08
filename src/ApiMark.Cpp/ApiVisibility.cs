namespace ApiMark.Cpp;

/// <summary>Specifies which class members are included in the generated C++ API documentation.</summary>
/// <remarks>
///     Mirrors <c>ApiMark.DotNet.ApiVisibility</c> with identical ordinal values so that the
///     two enums can be round-tripped via integer cast in <c>ApiMark.Tool</c> without introducing
///     a cross-project dependency from <c>ApiMark.Cpp</c> to <c>ApiMark.DotNet</c>.
/// </remarks>
public enum ApiVisibility
{
    /// <summary>Include only public members.</summary>
    Public,

    /// <summary>Include public and protected members.</summary>
    PublicAndProtected,

    /// <summary>Include all members regardless of access modifier.</summary>
    All,
}
