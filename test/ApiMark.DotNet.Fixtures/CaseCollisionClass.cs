namespace ApiMark.DotNet.Fixtures;

/// <summary>A class demonstrating case-insensitive member name collision.</summary>
/// <remarks>
///     Used to verify that the generator combines members whose names differ only in case
///     (e.g. field <c>name</c> and property <c>Name</c>) onto a single shared Markdown page
///     so that no two output files collide on case-insensitive file systems.
/// </remarks>
public class CaseCollisionClass
{
    /// <summary>The backing name field.</summary>
    public string name = string.Empty;

    /// <summary>Gets the formatted name.</summary>
    public string Name => name;
}
