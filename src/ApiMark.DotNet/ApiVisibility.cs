namespace ApiMark.DotNet;

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
