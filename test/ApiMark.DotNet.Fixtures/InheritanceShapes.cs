// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

#pragma warning disable CS0067 // The event is never used — declared only to exercise signature rendering.

namespace ApiMark.DotNet.Fixtures;

/// <summary>An abstract base class declaring abstract, virtual, and protected virtual members.</summary>
public abstract class ShapeBase
{
    /// <summary>Draws the shape.</summary>
    public abstract void Draw();

    /// <summary>Resizes the shape.</summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    public virtual void Resize(int width, int height)
    {
    }

    /// <summary>Called after a resize.</summary>
    protected virtual void OnResized()
    {
    }

    /// <summary>Gets the shape name.</summary>
    public abstract string Name { get; }

    /// <summary>Gets or sets the label.</summary>
    public virtual string Label { get; set; } = string.Empty;

    /// <summary>Occurs when the shape changes.</summary>
    public virtual event EventHandler? Changed;

    /// <summary>A plain non-virtual method.</summary>
    public void Plain()
    {
    }
}

/// <summary>A concrete shape that overrides members of <see cref="ShapeBase"/>.</summary>
public class ShapeDerived : ShapeBase
{
    /// <inheritdoc/>
    public override void Draw()
    {
    }

    /// <summary>Resizes the derived shape and prevents further overriding.</summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    public sealed override void Resize(int width, int height)
    {
    }

    /// <summary>Called after a resize of the derived shape.</summary>
    protected override void OnResized()
    {
    }

    /// <summary>Gets the shape name.</summary>
    public override string Name => "derived";

    /// <summary>Gets or sets the label.</summary>
    public override string Label
    {
        get => base.Label;
        set => base.Label = value;
    }

    /// <summary>Occurs when the derived shape changes.</summary>
    public override event EventHandler? Changed;
}

/// <summary>A sealed shape; its overrides are not additionally marked <c>sealed</c>.</summary>
public sealed class ShapeSealed : ShapeBase
{
    /// <summary>Draws the sealed shape.</summary>
    public override void Draw()
    {
    }

    /// <summary>Gets the shape name.</summary>
    public override string Name => "sealed";

    /// <summary>Returns the text form of the shape.</summary>
    /// <returns>The text.</returns>
    public override string ToString() => Name;
}

/// <summary>An interface whose members must render without inheritance modifiers.</summary>
public interface IShapeContract
{
    /// <summary>Draws the shape.</summary>
    void Draw();

    /// <summary>Gets the shape name.</summary>
    string Name { get; }
}

/// <summary>An implicit implementation of <see cref="IShapeContract"/>.</summary>
public class ShapeContractImplementation : IShapeContract
{
    /// <summary>Draws the shape.</summary>
    public void Draw()
    {
    }

    /// <summary>Gets the shape name.</summary>
    public string Name => "contract";
}

/// <summary>A shape that hides non-virtual and virtual members of <see cref="ShapeBase"/> with <c>new</c>.</summary>
public class ShapeHiding : ShapeBase
{
    /// <summary>Draws the hiding shape.</summary>
    public override void Draw()
    {
    }

    /// <summary>Gets the shape name.</summary>
    public override string Name => "hiding";

    /// <summary>Hides the plain method.</summary>
    public new void Plain()
    {
    }

    /// <summary>Hides the resize method with a new virtual slot.</summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    public new virtual void Resize(int width, int height)
    {
    }

    /// <summary>An overload that does not hide anything.</summary>
    /// <param name="width">The width.</param>
    public void Resize(int width)
    {
    }

    /// <summary>Hides the label property.</summary>
    public new string Label { get; set; } = string.Empty;
}

/// <summary>A class declaring a static event.</summary>
public class StaticEventHolder
{
    /// <summary>Occurs when something static happens.</summary>
    public static event EventHandler? Happened;
}
