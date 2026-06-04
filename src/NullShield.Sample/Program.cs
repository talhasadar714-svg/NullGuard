// ============================================================================
// NullShield Sample — Demonstrates the generator in action
// ============================================================================

using NullShield.Core.Attributes;
using NullShield.Core.Enums;

// -------------------------------------------------------
// 1) Class-level attribute on a partial class
//    The generator should produce a .g.cs placeholder for this target.
// -------------------------------------------------------
[NullShield(MitigationStrategy.DefaultInstance | MitigationStrategy.TraceAndSkip)]
public partial class OrderService
{
    public void Submit(string? orderId, int quantity)
    {
        Console.WriteLine($"Submitting order {orderId} × {quantity}");
    }

    public void Cancel(string? orderId)
    {
        Console.WriteLine($"Cancelling order {orderId}");
    }
}

// -------------------------------------------------------
// 2) Method-level attribute
// -------------------------------------------------------
public partial class PaymentProcessor
{
    [NullShield(MitigationStrategy.ShortCircuit)]
    public void Charge(string? cardToken, decimal amount)
    {
        Console.WriteLine($"Charging {amount:C} to {cardToken}");
    }
}

// -------------------------------------------------------
// 3) Diagnostic test: non-partial class → expect NS0003 error
//    Uncomment the lines below to see the compiler error.
// -------------------------------------------------------
// [NullShield(MitigationStrategy.DefaultInstance)]
// public class NotPartialService
// {
//     public void Run() { }
// }

// -------------------------------------------------------
// 4) Diagnostic test: MitigationStrategy.None → expect NS0001 warning
//    Uncomment the lines below to see the compiler warning.
// -------------------------------------------------------
// [NullShield((MitigationStrategy)0)]
// public partial class NoOpService
// {
//     public void Run() { }
// }

// -------------------------------------------------------
// Main — run the sample
// -------------------------------------------------------
public class Program
{
    public static void Main()
    {
        Console.WriteLine("=== NullShield Sample ===");
        Console.WriteLine();

        var orders = new OrderService();
        orders.Submit("ORD-001", 3);
        orders.Submit(null, 1);     // Would be guarded in Phase 3
        orders.Cancel("ORD-002");

        Console.WriteLine();

        var payments = new PaymentProcessor();
        payments.Charge("tok_visa_4242", 99.95m);
        payments.Charge(null, 50.00m); // Would be short-circuited in Phase 3

        Console.WriteLine();
        Console.WriteLine("Build succeeded — generator is active!");
        Console.WriteLine("Check obj/Generated/ for emitted .g.cs placeholder files.");
    }
}
