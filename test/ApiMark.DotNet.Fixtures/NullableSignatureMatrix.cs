// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

namespace ApiMark.DotNet.Fixtures;

/// <summary>A reference type used as a nullable port parameter.</summary>
public class MatrixPort
{
}

/// <summary>A reference type used as a wire argument.</summary>
public class MatrixWire
{
}

/// <summary>A reference type used as a nullable style result.</summary>
public class MatrixStyle
{
}

/// <summary>A reference type used as a node parameter.</summary>
public class MatrixNode
{
}

/// <summary>A value type used as a bounds parameter.</summary>
public struct MatrixRect
{
    /// <summary>The width of the rectangle.</summary>
    public int Width;
}

/// <summary>
///     A class whose members are mostly nullable so that the compiler selects nullable context 2, covering
///     void returns, optional parameters, <c>out</c> parameters, generic arguments, and tuples.
/// </summary>
public class NullableSignatureMatrix
{
    /// <summary>Reconnects the ports.</summary>
    /// <param name="newSource">The new source port.</param>
    /// <param name="newTarget">The new target port.</param>
    public void Reconnect(MatrixPort? newSource, MatrixPort? newTarget)
    {
    }

    /// <summary>Disconnects a port.</summary>
    /// <param name="source">The source port.</param>
    public void Disconnect(MatrixPort? source)
    {
    }

    /// <summary>Notifies about a port asynchronously.</summary>
    /// <param name="port">The port.</param>
    /// <returns>A completed task.</returns>
    public Task NotifyAsync(MatrixPort? port) => Task.CompletedTask;

    /// <summary>Adds an annotation.</summary>
    /// <param name="text">The annotation text.</param>
    /// <param name="bounds">The annotation bounds.</param>
    /// <param name="id">The optional identifier.</param>
    public void AddAnnotation(string text, MatrixRect bounds, string? id = null)
    {
    }

    /// <summary>Attempts to rename a node.</summary>
    /// <param name="node">The node.</param>
    /// <param name="name">The new name.</param>
    /// <param name="error">The error text when renaming fails.</param>
    /// <returns><see langword="true"/> when the rename succeeded.</returns>
    public bool TryRename(MatrixNode node, string name, out string? error)
    {
        error = null;
        return true;
    }

    /// <summary>Collects style functions.</summary>
    /// <param name="wire">The wire.</param>
    /// <param name="state">The state object.</param>
    /// <param name="bag">The property bag.</param>
    /// <returns>An array of style functions.</returns>
    public Func<MatrixWire, MatrixStyle?>[] Collect(MatrixWire? wire, object? state, Dictionary<string, object?>? bag) =>
        Array.Empty<Func<MatrixWire, MatrixStyle?>>();

    /// <summary>Gets a named tuple.</summary>
    /// <param name="port">The port.</param>
    /// <returns>The pair.</returns>
    public (int Id, string? Name) GetPair(MatrixPort? port) => (0, null);

    /// <summary>Gets an unnamed tuple.</summary>
    /// <returns>The pair.</returns>
    public (int, string) GetUnnamed() => (0, string.Empty);

    /// <summary>Finds a nullable tuple.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The pair, or <see langword="null"/>.</returns>
    public (int Id, string Name)? FindPair(string? key) => null;

    /// <summary>Takes tuple parameters.</summary>
    /// <param name="point">The point.</param>
    /// <param name="optional">The optional labels.</param>
    public void Take((int X, int Y) point, (string? A, string? B)? optional)
    {
    }

    /// <summary>Gets an eight-element tuple.</summary>
    /// <returns>The tuple.</returns>
    public (int A, int B, int C, int D, int E, int F, int G, string? H) GetBig() => (0, 0, 0, 0, 0, 0, 0, null);
}
