using System.Net;
using System.Net.Sockets;

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
        $"Host=127.0.0.1;Port={GetClosedPort()};Database={database};Username=x;Password=x";

    /// <summary>
    /// Asks the OS for a free port, then releases it, so nothing is listening there.
    /// </summary>
    private static int GetClosedPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
