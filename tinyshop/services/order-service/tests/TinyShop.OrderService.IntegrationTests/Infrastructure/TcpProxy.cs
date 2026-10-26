using System.Net;
using System.Net.Sockets;

namespace TinyShop.OrderService.IntegrationTests.Infrastructure;

/// <summary>
/// A minimal TCP forwarder on 127.0.0.1, like the socat proxies in Compose. Stopping it closes
/// every open connection and refuses new ones: to the app behind it, the database has just gone
/// away mid-request, while the real database (shared with other tests) keeps running.
/// </summary>
public sealed class TcpProxy : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stopping = new();
    private readonly List<TcpClient> _connections = [];
    private readonly string _targetHost;
    private readonly int _targetPort;

    public TcpProxy(string targetHost, int targetPort)
    {
        _targetHost = targetHost;
        _targetPort = targetPort;
        _listener.Start();
        _ = AcceptLoopAsync();
    }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    public async Task StopAsync()
    {
        await _stopping.CancelAsync();
        _listener.Stop();
        lock (_connections)
        {
            foreach (var connection in _connections)
            {
                connection.Dispose();
            }

            _connections.Clear();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _stopping.Dispose();
    }

    private async Task AcceptLoopAsync()
    {
        try
        {
            while (!_stopping.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(_stopping.Token);
                var server = new TcpClient();
                await server.ConnectAsync(_targetHost, _targetPort, _stopping.Token);

                lock (_connections)
                {
                    _connections.Add(client);
                    _connections.Add(server);
                }

                _ = PumpAsync(client.GetStream(), server.GetStream());
                _ = PumpAsync(server.GetStream(), client.GetStream());
            }
        }
        catch (Exception exception) when (exception is OperationCanceledException or SocketException or ObjectDisposedException)
        {
            // Stopped.
        }
    }

    private async Task PumpAsync(NetworkStream from, NetworkStream to)
    {
        try
        {
            await from.CopyToAsync(to, _stopping.Token);
        }
        catch (Exception exception) when (exception is OperationCanceledException or IOException or ObjectDisposedException)
        {
            // One side closed, or the proxy stopped.
        }
        finally
        {
            from.Close();
            to.Close();
        }
    }
}
