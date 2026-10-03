// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

namespace ApiMark.DotNet.Fixtures;

/// <summary>
///     A class with no explicit constructor of its own, relying on the C# compiler's implicit
///     parameterless constructor — mirroring a typical options/settings class with multiple
///     property initializers, used to verify <c>--enforce-docs</c> does not flag the implicit
///     constructor as undocumented.
/// </summary>
public sealed class ImplicitDefaultConstructorClass
{
    /// <summary>Gets or sets the host URL.</summary>
    public string Host { get; set; } = "http://localhost";

    /// <summary>Gets or sets the port.</summary>
    public int Port { get; set; } = 8080;
}

/// <summary>
///     A class with an explicit, empty parameterless constructor — the real-world "trivial
///     explicit constructor" case that implicit-constructor detection must NOT exempt from
///     <c>--enforce-docs</c>, since CS1591 itself would flag it.
/// </summary>
public sealed class ExplicitEmptyConstructorClass
{
    /// <summary>Initializes a new instance of the <see cref="ExplicitEmptyConstructorClass"/> class.</summary>
    public ExplicitEmptyConstructorClass()
    {
    }
}

/// <summary>
///     A class with an explicit, expression-bodied parameterless constructor whose entire body
///     is a single field assignment — an edge case that implicit-constructor detection must NOT
///     exempt from <c>--enforce-docs</c>, since CS1591 itself would flag it.
/// </summary>
public sealed class ExpressionBodiedConstructorClass
{
    /// <summary>The bar value.</summary>
    public int Bar;

    /// <summary>Initializes a new instance of the <see cref="ExpressionBodiedConstructorClass"/> class.</summary>
    public ExpressionBodiedConstructorClass() => Bar = 1;
}

/// <summary>
///     A class with an explicit, expression-bodied parameterless constructor whose single
///     source statement stores more than one field (a tuple-deconstruction assignment) — an
///     edge case that implicit-constructor detection must NOT exempt from
///     <c>--enforce-docs</c>, since a single compiler-emitted sequence point here covers
///     multiple <c>stfld</c> instructions, which a naive aggregate-count comparison could
///     mistake for one initializer per sequence point.
/// </summary>
public sealed class TupleDeconstructionConstructorClass
{
    /// <summary>The first value.</summary>
    public int A;

    /// <summary>The second value.</summary>
    public int B;

    /// <summary>
    ///     Initializes a new instance of the <see cref="TupleDeconstructionConstructorClass"/> class.
    /// </summary>
    public TupleDeconstructionConstructorClass() => (A, B) = (1, 2);
}
