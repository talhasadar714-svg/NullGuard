using System;
using NullShield.Core.Attributes;
using NullShield.Core.Enums;

namespace NullShield.Sample
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== NullShield Security Shield Test ===");

            try
            {
                // Test 1: Sending valid data
                SaveUser("Talha Sadar", "talha@example.com");
                Console.WriteLine("Test 1 Passed: Valid user processed successfully.\n");

                // Test 2: Sending null data (Source Generator must intercept!)
                Console.WriteLine("Test 2 Triggered: Sending null username...");
                SaveUser(null!, "test@example.com");
            }
            catch (ArgumentNullException ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n[SHIELD INTERCEPTED] Caught Expected Exception (Method Guard):");
                Console.WriteLine(ex.Message);
                Console.ResetColor();
            }

            try
            {
                // Test 3: Primary constructor null data
                Console.WriteLine("\nTest 3 Triggered: Instantiating OrderService with null orderId...");
                var service = new OrderService(null!, "Test description");
            }
            catch (ArgumentNullException ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n[SHIELD INTERCEPTED] Caught Expected Exception (Primary Constructor Guard):");
                Console.WriteLine(ex.Message);
                Console.ResetColor();
            }
        }

        [NullShield(MitigationStrategy.ThrowException)]
        public static void SaveUser(string username, string email)
        {
            // Metot adi SaveUser oldugu icin jenerator otomatik olarak bu sinifi uretti:
            NullShield_Guard_Program_SaveUser.ValidateParameters(username, email);

            Console.WriteLine($"[Method Body] Processing database action for: {username} ({email})");
        }
    }

    // Phase 2: Primary Constructor Test
    public partial class OrderService([NotNull] string orderId, string optionalDesc)
    {
        public void Process()
        {
            Console.WriteLine($"Processing order: {orderId}");
        }
    }

    // Phase 3: Analyzer Tests
    public class AnalyzerTests
    {
        // Should trigger NS1001: Manual null check can be simplified
        public void LegacySaveUser(string name)
        {
            ArgumentNullException.ThrowIfNull(name);
            Console.WriteLine(name);
        }

        // Should trigger NS1000: Redundant [NotNull] on nullable reference type
        public void NullableUser([NotNull] string? name)
        {
            Console.WriteLine(name);
        }
    }
}