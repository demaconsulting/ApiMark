namespace ApiMark.DotNet.Fixtures;

/// <summary>A class with methods overloaded by scalar vs array parameter types.</summary>
public static class IntVsIntArrayClass
{
    /// <summary>Processes a single integer value.</summary>
    /// <param name="value">The integer value to process.</param>
    public static void Process(int value)
    {
        // Fixture: intentional empty implementation — exercises scalar vs array overload documentation
    }

    /// <summary>Processes an array of integer values.</summary>
    /// <param name="values">The integer values to process.</param>
    public static void Process(int[] values)
    {
        // Fixture: intentional empty implementation — exercises scalar vs array overload documentation
    }
}
