namespace TinyShop.PaymentService.Application.Payments;

/// <summary>Creates transaction IDs: "txn_" followed by 32 lowercase hex digits.</summary>
internal static class TransactionIds
{
    public const string Prefix = "txn_";

    // A random GUID: 122 random bits, so collisions are practically impossible without any
    // coordination between instances. The prefix makes the ID's kind obvious in logs.
    public static string New() => Prefix + Guid.NewGuid().ToString("N");
}
