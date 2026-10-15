// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

using ApiMark.DotNet.Fixtures.External.Nested;

namespace ApiMark.DotNet.Fixtures;

/// <summary>
///     Refers to nested types of an external assembly: <see cref="NestedHost.Inner"/>,
///     <see cref="NestedHost.Inner.Run"/>, <see cref="NestedHost.Deep.Leaf"/> and
///     <see cref="NestedGenericHost{T}.Inner{U}"/>.
/// </summary>
public class NestedExternalConsumer
{
    /// <summary>Gets an external nested instance.</summary>
    public NestedHost.Inner? Inner { get; }

    /// <summary>Gets an external generic nested instance.</summary>
    public NestedGenericHost<int>.Inner<string>? GenericInner { get; }
}
