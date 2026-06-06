// Copyright (c) NullShield Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

// Polyfill for System.Runtime.CompilerServices.IsExternalInit.
// C# 9+ records use 'init' accessors which rely on this type.
// It is natively available in .NET 5+ but absent in netstandard2.0.
// Declaring it here (in the correct namespace) satisfies the compiler without any
// additional NuGet dependency and without affecting the public API surface.
// The 'internal' visibility ensures it does not conflict with the real type if the
// consuming SDK provides it later.
namespace System.Runtime.CompilerServices
{
    using System.ComponentModel;

    /// <summary>
    /// Reserved for use by the compiler. Enables C# 9 init-only setters on netstandard2.0.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static class IsExternalInit { }
}
