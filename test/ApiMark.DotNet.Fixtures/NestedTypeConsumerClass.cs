// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace ApiMark.DotNet.Fixtures;

/// <summary>
///     A fixture class that references nested types (<see cref="OuterClass.Inner"/> and
///     <see cref="TwoLevelNestedClass.Middle.Inner"/>) as a method parameter type, a method
///     return type, a field type, and a generic type argument — used to verify that links to
///     nested types resolve to the correct page from a completely different type's page.
/// </summary>
public class NestedTypeConsumerClass
{
    /// <summary>A field whose type is the nested type <see cref="OuterClass.Inner"/>.</summary>
    public OuterClass.Inner? Field;

    /// <summary>Returns an instance of the nested type <see cref="OuterClass.Inner"/>.</summary>
    /// <returns>An <see cref="OuterClass.Inner"/> instance, or <see langword="null"/>.</returns>
    public OuterClass.Inner? GetInner() => Field;

    /// <summary>Accepts an instance of the nested type <see cref="OuterClass.Inner"/>.</summary>
    /// <param name="inner">The nested type instance to process.</param>
    public void Process(OuterClass.Inner inner) => Field = inner;

    /// <summary>Returns a list whose generic type argument is the nested type <see cref="OuterClass.Inner"/>.</summary>
    /// <returns>A list of <see cref="OuterClass.Inner"/> instances.</returns>
    public List<OuterClass.Inner> GetInnerList() => [];

    /// <summary>Returns an instance of the two-levels-deep nested type <see cref="TwoLevelNestedClass.Middle.Inner"/>.</summary>
    /// <returns>A <see cref="TwoLevelNestedClass.Middle.Inner"/> instance.</returns>
    public TwoLevelNestedClass.Middle.Inner GetTwoLevelNested() => new(0);
}
