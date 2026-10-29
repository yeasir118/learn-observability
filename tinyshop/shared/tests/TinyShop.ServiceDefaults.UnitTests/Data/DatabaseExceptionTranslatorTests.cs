using System.Net.Sockets;
using Npgsql;
using TinyShop.ServiceDefaults.Abstractions;
using TinyShop.ServiceDefaults.Data;

namespace TinyShop.ServiceDefaults.UnitTests.Data;

public class DatabaseExceptionTranslatorTests
{
    [Fact]
    public async Task ExecuteAsync_WhenOperationSucceeds_ReturnsResult()
    {
        var result = await DatabaseExceptionTranslator.ExecuteAsync(() => Task.FromResult(42));

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task ExecuteAsync_WhenConnectionRefused_ThrowsDataStoreUnavailable()
    {
        var refused = new NpgsqlException("Failed to connect", new SocketException((int)SocketError.ConnectionRefused));

        await Assert.ThrowsAsync<DataStoreUnavailableException>(
            () => DatabaseExceptionTranslator.ExecuteAsync<int>(() => throw refused));
    }

    /// <summary>
    /// EF Core wraps transient provider errors in an InvalidOperationException
    /// ("An exception has been raised that is likely due to a transient failure"),
    /// so the translator must look through the whole exception chain.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenTransientErrorIsWrappedByEfCore_ThrowsDataStoreUnavailable()
    {
        var wrapped = new InvalidOperationException(
            "An exception has been raised that is likely due to a transient failure.",
            new NpgsqlException("Timeout", new TimeoutException()));

        var exception = await Assert.ThrowsAsync<DataStoreUnavailableException>(
            () => DatabaseExceptionTranslator.ExecuteAsync<int>(() => throw wrapped));

        Assert.Same(wrapped, exception.InnerException);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNpgsqlErrorIsNotTransient_LetsItThrough()
    {
        var permanent = new NpgsqlException("Something that retrying won't fix");

        await Assert.ThrowsAsync<NpgsqlException>(
            () => DatabaseExceptionTranslator.ExecuteAsync<int>(() => throw permanent));
    }

    [Fact]
    public async Task ExecuteAsync_WhenErrorIsUnrelatedToTheDatabase_LetsItThrough()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => DatabaseExceptionTranslator.ExecuteAsync<int>(() => throw new ArgumentException("bug")));
    }
}
