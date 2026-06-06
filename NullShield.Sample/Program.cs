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
                Console.WriteLine($"\n[SHIELD INTERCEPTED] Caught Expected Exception:");
                Console.WriteLine(ex.Message);
                Console.ResetColor();
            }

            Console.ReadLine();
        }

        [NullShield(MitigationStrategy.ThrowException)]
        public static void SaveUser(string username, string email)
        {
            // Metot adi SaveUser oldugu icin jenerator otomatik olarak bu sinifi uretti:
            NullShield_Guard_Program_SaveUser.ValidateParameters(username, email);

            Console.WriteLine($"[Method Body] Processing database action for: {username} ({email})");
        }
    }
}