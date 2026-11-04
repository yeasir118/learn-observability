namespace TinyShop.Testing;

/// <summary>
/// A connection string to a port nothing listens on: "database unreachable" for a second copy of
/// the app, without stopping the container the other tests share.
/// </summary>
public static class UnreachableDatabase
{
    public static string ConnectionString(string database) =>
        // 127.0.0.1, not localhost: on Windows, localhost tries IPv6 first and each refused
        // connection attempt costs about two seconds.
        $"Host=127.0.0.1;Port={ClosedPort.Get()};Database={database};Username=x;Password=x";
}
