namespace TinyShop.UserService.Application.Models;

/// <summary>
/// A user as the application exposes it. Callers never receive the Domain entity itself,
/// so the entity can change without changing what callers see.
/// </summary>
public sealed record UserDto(long Id, string Name);
