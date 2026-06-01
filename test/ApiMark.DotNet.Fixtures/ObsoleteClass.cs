namespace ApiMark.DotNet.Fixtures;

#pragma warning disable CS0612, CS0618 // Obsolete attribute is intentional for testing obsolete member filtering
/// <summary>An obsolete class for testing obsolete member filtering.</summary>
[Obsolete("This class is obsolete.")]
public class ObsoleteClass
{
    /// <summary>An old method that should no longer be used.</summary>
    public void OldMethod()
    {
        // Intentionally empty for testing purposes
    }
}
#pragma warning restore CS0612, CS0618
