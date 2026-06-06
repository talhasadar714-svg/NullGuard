using System;
using NullShield.Core.Enums;

namespace NullShield.Core.Attributes
{
    /// <summary>
    /// Instructs the NullShield compiler generator to inject strict compile-time null-safety guards.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public sealed class NullShieldAttribute : Attribute
    {
        public MitigationStrategy Strategy { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="NullShieldAttribute"/> with the default ThrowException strategy.
        /// </summary>
        public NullShieldAttribute() : this(MitigationStrategy.ThrowException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NullShieldAttribute"/> with a custom mitigation strategy.
        /// </summary>
        public NullShieldAttribute(MitigationStrategy strategy)
        {
            Strategy = strategy;
        }
    }
}