namespace ApiMark.DotNet.Fixtures.External;

/// <summary>The top of a two-level external hierarchy whose levels live in the same assembly.</summary>
public class ExternalChainTop
{
    /// <summary>Runs the top-level operation.</summary>
    public void TopOp()
    {
        // Intentionally empty: only the declaration shape matters to the tests
    }
}

/// <summary>The middle of the external hierarchy; its base is declared in this same assembly.</summary>
public class ExternalChainMiddle : ExternalChainTop
{
    /// <summary>Runs the middle operation.</summary>
    public void MiddleOp()
    {
        // Intentionally empty: only the declaration shape matters to the tests
    }
}
