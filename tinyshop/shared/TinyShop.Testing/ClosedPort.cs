using System.Net;
using System.Net.Sockets;

namespace TinyShop.Testing;

/// <summary>
/// A port on 127.0.0.1 that nothing listens on: connecting is refused at once. For tests that need
/// "the other side is down" (a database, an upstream service) without stopping anything shared.
/// </summary>
public static class ClosedPort
{
    /// <summary>
    /// Asks the OS for a free port, then releases it, so nothing is listening there.
    /// </summary>
    public static int Get()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
