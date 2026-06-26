// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

// Polyfill required to use C# 9+ init-only setters when targeting netstandard2.0.
// The compiler emits modreq(IsExternalInit) for init accessors; this declaration
// satisfies the compiler for legacy targets.

// ReSharper disable CheckNamespace
#pragma warning disable SA1403 // File may only contain a single namespace
#pragma warning disable SA1649 // File name should match first type name

namespace System.Runtime.CompilerServices;

/// <summary>Polyfill marker type that allows C# 9+ init-only property setters on netstandard2.0 targets.</summary>
internal static class IsExternalInit { }

#pragma warning restore SA1649
#pragma warning restore SA1403
