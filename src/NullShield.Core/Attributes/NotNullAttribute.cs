using System;

namespace NullShield.Core.Attributes
{
    /// <summary>
    /// Instructs the NullShield compiler generator to inject strict compile-time null-safety guards
    /// for this specific parameter. Particularly useful for Primary Constructors.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, Inherited = false, AllowMultiple = false)]
    public sealed class NotNullAttribute : Attribute
    {
    }
}
