// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

namespace ApiMark.DotNet.Fixtures;

/// <summary>
///     A fixture class exercising every <c>&lt;see cref&gt;</c>/<c>&lt;seealso cref&gt;</c>
///     resolution outcome exercised by the cross-reference linking feature: a real intra-assembly
///     type link, a real intra-assembly member link, a fallback for a member filtered out by
///     visibility, a fallback for an external framework type, and a fallback for a malformed
///     cref string.
/// </summary>
/// <remarks>See also <seealso cref="SampleClass"/>.</remarks>
public class CrefLinkingClass
{
    /// <summary>
    ///     References another in-assembly, publicly visible type:
    ///     <see cref="SampleClass"/>.
    /// </summary>
    public void ReferencesVisibleType()
    {
    }

    /// <summary>
    ///     References another in-assembly, publicly visible member:
    ///     <see cref="SampleClass.Reset"/>.
    /// </summary>
    public void ReferencesVisibleMember()
    {
    }

    /// <summary>
    ///     References a member filtered out of the generated documentation by the active
    ///     visibility setting: <see cref="ProtectedMembersClass.PrivateMethod(int)"/>.
    /// </summary>
    public void ReferencesFilteredMember()
    {
    }

    /// <summary>
    ///     References an external framework type: <see cref="ArgumentNullException"/>.
    /// </summary>
    public void ReferencesExternalType()
    {
    }

    /// <summary>
    ///     References a malformed, unresolvable cref string:
    ///     <see cref="!:NotAWellFormedCrefString"/>.
    /// </summary>
    public void ReferencesMalformedCref()
    {
    }

    /// <summary>
    ///     References a generic method overload by its own XML-doc <c>``N</c> arity suffix:
    ///     <see cref="Identity{T}(T)"/>.
    /// </summary>
    public void ReferencesGenericMethod()
    {
    }

    /// <summary>A generic method with its own type parameter, distinct from a non-generic overload of the same name.</summary>
    /// <typeparam name="T">The type of <paramref name="value"/>.</typeparam>
    /// <param name="value">The value to return unchanged.</param>
    /// <returns><paramref name="value"/>, unchanged.</returns>
    public T Identity<T>(T value) => value;

    /// <summary>A non-generic overload sharing its name with <see cref="Identity{T}(T)"/>.</summary>
    /// <param name="value">The value to return unchanged.</param>
    /// <returns><paramref name="value"/>, unchanged.</returns>
    public int Identity(int value) => value;

    /// <summary>An indexer, whose XML doc ID must carry its index parameter type list.</summary>
    /// <param name="index">The index to read.</param>
    /// <returns>Always <c>0</c>.</returns>
    public int this[int index] => 0;

    /// <summary>
    ///     A generic method taking its own type parameter as an array element type, whose XML
    ///     doc ID must resolve <c>T</c> positionally even when wrapped in an array type.
    /// </summary>
    /// <typeparam name="T">The array element type.</typeparam>
    /// <param name="values">The array to return unchanged.</param>
    /// <returns><paramref name="values"/>, unchanged.</returns>
    public T[] IdentityArray<T>(T[] values) => values;
}
