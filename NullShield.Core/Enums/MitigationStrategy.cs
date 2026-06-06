using System;

namespace NullShield.Core.Enums
{
    /// <summary>
    /// Defines the action taken when a null value is detected by NullShield.
    /// </summary>
    public enum MitigationStrategy
    {
        /// <summary>
        /// Throws an ArgumentNullException immediately (Default professional behavior).
        /// </summary>
        ThrowException = 0,

        /// <summary>
        /// Assigns a safe, non-null default value instead of failing.
        /// </summary>
        FallbackToDefault = 1,

        /// <summary>
        /// Gently logs the issue to the diagnostics/console and bypasses the execution safely.
        /// </summary>
        LogAndBypass = 2
    }
}