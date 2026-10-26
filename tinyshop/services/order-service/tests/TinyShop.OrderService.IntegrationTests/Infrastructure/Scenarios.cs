namespace TinyShop.OrderService.IntegrationTests.Infrastructure;

/// <summary>
/// The fake dependencies decide how to behave from the IDs in the request, not from shared
/// mutable state, so tests running in parallel against one app never interfere.
/// Any ID not listed here is a user or product that exists and a payment that is approved.
/// </summary>
public static class Scenarios
{
    /// <summary>User IDs that make the fake user-service misbehave.</summary>
    public static class User
    {
        public const long NotFound = 9_404;
        public const long BadRequest = 9_400;
        public const long ServerError = 9_500;
        public const long Unreachable = 9_502;
        public const long Timeout = 9_504;
    }

    /// <summary>Product IDs that make the fake product-service misbehave.</summary>
    public static class Product
    {
        /// <summary>The contract's example product (contracts.md §3).</summary>
        public const long Keyboard = 1;
        public const long KeyboardPriceCents = 10_000;

        public const long NotFound = 9_404;
        public const long BadRequest = 9_400;
        public const long ServerError = 9_500;
        public const long Unreachable = 9_502;
        public const long Timeout = 9_504;
        public const long InvalidBody = 9_200;

        /// <summary>Every other product costs this.</summary>
        public const long DefaultPriceCents = 2_500;
    }

    /// <summary>User IDs that make the fake payment-service misbehave (the users themselves exist).</summary>
    public static class Payment
    {
        public const long Declined = 8_001;
        public const long DeclineReason = 8_002;
        public const long Unavailable = 8_503;
        public const long Internal = 8_500;
        public const long InvalidArgument = 8_400;
        public const long DeadlineExceeded = 8_504;

        /// <summary>Runs the factory's "during payment" hook (cutting the database), then approves.</summary>
        public const long DatabaseDownDuringPayment = 8_600;
    }

    /// <summary>
    /// A user ID no other test uses: their orders list contains only what this test created.
    /// Above every scenario ID, so it always exists and always pays.
    /// </summary>
    public static long NewUserId() => Random.Shared.NextInt64(100_000, 1_000_000_000_000);
}
