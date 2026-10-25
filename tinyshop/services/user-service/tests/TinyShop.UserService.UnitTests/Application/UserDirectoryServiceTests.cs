using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TinyShop.UserService.Application.Abstractions.Persistence;
using TinyShop.UserService.Application.Common.Exceptions;
using TinyShop.UserService.Application.Models;
using TinyShop.UserService.Application.Services;
using TinyShop.UserService.Domain.Entities;

namespace TinyShop.UserService.UnitTests.Application;

public class UserDirectoryServiceTests
{
    private readonly IUserRepository _repository = Substitute.For<IUserRepository>();
    private readonly IUserDirectoryService _service;

    public UserDirectoryServiceTests()
    {
        _service = new UserDirectoryService(_repository, NullLogger<UserDirectoryService>.Instance);
    }

    [Fact]
    public async Task GetByIdAsync_WhenUserExists_ReturnsIt()
    {
        _repository.GetByIdAsync(123, Arg.Any<CancellationToken>()).Returns(CreateUser(123, "Alice"));

        var result = await _service.GetByIdAsync(123, TestContext.Current.CancellationToken);

        Assert.Equal(new UserDto(123, "Alice"), result);
    }

    [Fact]
    public async Task GetByIdAsync_WhenUserDoesNotExist_ReturnsNull()
    {
        _repository.GetByIdAsync(999, Arg.Any<CancellationToken>()).Returns((User?)null);

        var result = await _service.GetByIdAsync(999, TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_PassesCancellationTokenToRepository()
    {
        using var cts = new CancellationTokenSource();

        await _service.GetByIdAsync(123, cts.Token);

        await _repository.Received(1).GetByIdAsync(123, cts.Token);
    }

    [Fact]
    public async Task GetByIdAsync_WhenDataStoreIsUnavailable_LetsTheExceptionThrough()
    {
        // The service can't fix an outage, so it doesn't catch it: the Api's exception handler
        // turns it into a 503.
        _repository.GetByIdAsync(123, Arg.Any<CancellationToken>())
            .Returns<User?>(_ => throw new DataStoreUnavailableException("down", new TimeoutException()));

        await Assert.ThrowsAsync<DataStoreUnavailableException>(
            () => _service.GetByIdAsync(123, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Id has a private setter because the database assigns it. Tests set it through reflection,
    /// the same way EF Core does when it loads a row.
    /// </summary>
    private static User CreateUser(long id, string name)
    {
        var user = new User(name);
        typeof(User).GetProperty(nameof(User.Id))!.SetValue(user, id);
        return user;
    }
}
