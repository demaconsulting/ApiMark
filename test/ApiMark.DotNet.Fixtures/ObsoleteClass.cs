namespace ApiMark.DotNet.Fixtures;

/// <summary>An obsolete class for testing obsolete member filtering.</summary>
[Obsolete("This class is obsolete.")]
public class ObsoleteClass
{
    /// <summary>An old method that should no longer be used.</summary>
    public void OldMethod() { }
}
