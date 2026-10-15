// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

namespace ApiMark.DotNet.Fixtures.Naming;

/// <summary>A non-generic type named <c>Foo</c>.</summary>
public class Foo
{
    /// <summary>Gets a value.</summary>
    public int Value { get; }
}

/// <summary>A non-generic type whose name ends in a digit and must not collide with <c>Foo&lt;T&gt;</c>.</summary>
public class Foo1
{
    /// <summary>Gets a value.</summary>
    public int Value { get; }
}

/// <summary>A one-parameter generic type.</summary>
/// <typeparam name="T">The value type.</typeparam>
public class Foo<T>
{
    /// <summary>Gets a value.</summary>
    public T? Value { get; }

    /// <summary>Links to <see cref="Foo1"/>, <see cref="Foo"/> and <see cref="Foo{T1,T2}"/>.</summary>
    /// <param name="other">The other value.</param>
    public void Link(Foo1 other)
    {
    }
}

/// <summary>A two-parameter generic type.</summary>
/// <typeparam name="T1">The first type.</typeparam>
/// <typeparam name="T2">The second type.</typeparam>
public class Foo<T1, T2> : Foo<T1>
{
    /// <summary>Gets a second value.</summary>
    public T2? Second { get; }
}

/// <summary>A generic outer type hosting a generic nested type.</summary>
/// <typeparam name="T">The outer type parameter.</typeparam>
public class Outer<T>
{
    /// <summary>A generic nested type.</summary>
    /// <typeparam name="U">The nested type parameter.</typeparam>
    public class Inner<U>
    {
        /// <summary>Gets a value.</summary>
        public U? Value { get; }
    }
}
