namespace ApiMark.DotNet.Fixtures.Modifiers;

/// <summary>Methods exercising every recoverable generic constraint shape.</summary>
public class ConstraintShapes
{
    /// <summary>A notnull constraint.</summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <param name="value">The value.</param>
    public void NotNullOnly<T>(T value)
        where T : notnull
    {
    }

    /// <summary>An unmanaged constraint.</summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <param name="value">The value.</param>
    public void UnmanagedOnly<T>(T value)
        where T : unmanaged
    {
    }

    /// <summary>A struct constraint.</summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <param name="value">The value.</param>
    public void StructOnly<T>(T value)
        where T : struct
    {
    }

    /// <summary>A class constraint.</summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <param name="value">The value.</param>
    public void ClassOnly<T>(T value)
        where T : class
    {
    }

    /// <summary>A nullable class constraint.</summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <param name="value">The value.</param>
    public void NullableClassOnly<T>(T value)
        where T : class?
    {
    }

    /// <summary>A class plus new() constraint.</summary>
    /// <typeparam name="T">The type.</typeparam>
    public void ClassAndNew<T>()
        where T : class, new()
    {
    }

    /// <summary>A notnull plus new() constraint.</summary>
    /// <typeparam name="T">The type.</typeparam>
    public void NotNullAndNew<T>()
        where T : notnull, new()
    {
    }

    /// <summary>Independent constraints on two parameters.</summary>
    /// <typeparam name="TKey">The key.</typeparam>
    /// <typeparam name="TValue">The value.</typeparam>
    public void TwoParameters<TKey, TValue>()
        where TKey : notnull
        where TValue : struct
    {
    }

    /// <summary>A class plus interface constraint.</summary>
    /// <typeparam name="T">The type.</typeparam>
    public void ClassAndInterface<T>()
        where T : class, IDisposable
    {
    }

    /// <summary>A nullable interface constraint.</summary>
    /// <typeparam name="T">The type.</typeparam>
    public void NullableInterface<T>()
        where T : IDisposable?
    {
    }

    /// <summary>An unconstrained parameter, which must produce no where clause.</summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <param name="value">The value.</param>
    public void Unconstrained<T>(T value)
    {
    }
}
