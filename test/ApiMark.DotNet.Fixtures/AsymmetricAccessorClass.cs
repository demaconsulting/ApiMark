// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

namespace ApiMark.DotNet.Fixtures;

/// <summary>A class with an asymmetric property accessor for testing property-level accessibility selection.</summary>
/// <remarks>
///     Used by <c>DotNetEmitter_BuildPropertyAccessors_AsymmetricGetSet_UsesMostPermissiveAccessibility</c>
///     to verify that <c>BuildPropertyAccessors</c> derives the property-level accessibility keyword from
///     the most permissive accessor rather than always defaulting to the getter.
///     The property is declared <c>public</c>, but its getter is restricted to <c>protected</c>; the setter
///     is <c>public</c> (no explicit modifier, matching the property level). Correct output must therefore
///     use <c>public</c> as the property keyword and prefix only the getter with <c>protected</c>.
/// </remarks>
public class AsymmetricAccessorClass
{
    /// <summary>Gets (protected) or sets (public) the value used to exercise asymmetric accessor rendering.</summary>
    public int AsymmetricProperty { protected get; set; }
}
