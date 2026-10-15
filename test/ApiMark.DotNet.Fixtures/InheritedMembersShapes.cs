// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

using ApiMark.DotNet.Fixtures.External;

namespace ApiMark.DotNet.Fixtures;

/// <summary>The root of a three-level hierarchy used to verify inherited-member listings.</summary>
public class InheritedGrandBase
{
    /// <summary>Gets or sets the grand-base value.</summary>
    public int GrandValue { get; set; }

    /// <summary>Runs the grand-base operation.</summary>
    public void GrandMethod()
    {
    }

    /// <summary>Runs the shared operation (overridden by the middle type).</summary>
    public virtual void Shared()
    {
    }
}

/// <summary>The middle of the hierarchy; it overrides <c>Shared</c> and adds its own method.</summary>
public class InheritedMiddle : InheritedGrandBase
{
    /// <summary>Runs the middle operation.</summary>
    public void MiddleMethod()
    {
    }

    /// <summary>Runs the overridden shared operation.</summary>
    public override void Shared()
    {
    }
}

/// <summary>The leaf of the hierarchy; it declares one member and inherits the rest.</summary>
public class InheritedLeaf : InheritedMiddle
{
    /// <summary>Runs the leaf operation.</summary>
    public void LeafMethod()
    {
    }
}

/// <summary>A class that derives from a base in another assembly and declares nothing itself.</summary>
public class InheritedFromExternal : ExternalBaseClass
{
}

/// <summary>
///     A class whose summary refers to <see cref="ExternalBaseClass"/>, to
///     <see cref="ExternalBaseClass.DescribeBase"/> and to <see cref="System.String"/>.
/// </summary>
public class ExternalCrefClass
{
}

/// <summary>A base with members at every access level, used to verify inherited-member visibility tiers.</summary>
public class InheritedTierBase
{
    /// <summary>Runs the public operation.</summary>
    public void PublicOp()
    {
    }

    /// <summary>Runs the protected operation.</summary>
    protected void ProtectedOp()
    {
    }

    /// <summary>Runs the protected-internal operation.</summary>
    protected internal void ProtectedInternalOp()
    {
    }

    /// <summary>Runs the private-protected operation.</summary>
    private protected void PrivateProtectedOp()
    {
    }

    /// <summary>Runs the internal operation.</summary>
    internal void InternalOp()
    {
    }

    /// <summary>Runs the private operation.</summary>
    private void PrivateOp()
    {
    }

    /// <summary>A protected field.</summary>
    protected int ProtectedField;
}

/// <summary>A derived type that adds nothing, so every visible base member is inherited.</summary>
public class InheritedTierDerived : InheritedTierBase
{
}

/// <summary>An abstract root declaring one abstract method and one abstract property.</summary>
public abstract class InheritedAbstractBase
{
    /// <summary>Computes a value.</summary>
    /// <returns>The value.</returns>
    public abstract int Compute();

    /// <summary>Gets the name.</summary>
    public abstract string Name { get; }

    /// <summary>Runs a concrete operation.</summary>
    public void Concrete()
    {
    }
}

/// <summary>An abstract middle type that implements only <c>Compute</c>.</summary>
public abstract class InheritedAbstractMid : InheritedAbstractBase
{
    /// <inheritdoc/>
    public override int Compute() => 1;
}

/// <summary>A leaf that implements the remaining abstract property.</summary>
public class InheritedAbstractLeaf : InheritedAbstractMid
{
    /// <inheritdoc/>
    public override string Name => "leaf";
}

/// <summary>A generic base type.</summary>
/// <typeparam name="T">The item type.</typeparam>
public class InheritedGenericBase<T>
{
    /// <summary>Takes an item.</summary>
    /// <param name="item">The item.</param>
    public void Take(T item)
    {
    }
}

/// <summary>A type deriving from a constructed generic base.</summary>
public class InheritedFromGeneric : InheritedGenericBase<string>
{
}

/// <summary>A host for a nested base class.</summary>
public class InheritedNestedHost
{
    /// <summary>A nested base class.</summary>
    public class NestedBase
    {
        /// <summary>Runs the nested operation.</summary>
        public void NestedOp()
        {
        }
    }
}

/// <summary>A type deriving from a nested base class.</summary>
public class InheritedFromNested : InheritedNestedHost.NestedBase
{
}

/// <summary>A type overriding one member of an external base and inheriting the rest.</summary>
public class InheritedFromExternalOverride : ExternalBaseClass
{
    /// <inheritdoc/>
    public override string DescribeBase() => "override";
}
