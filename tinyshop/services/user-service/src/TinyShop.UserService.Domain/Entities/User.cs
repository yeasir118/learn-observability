namespace TinyShop.UserService.Domain.Entities;

/// <summary>
/// A shopper. TinyShop has no authentication (out of scope for M1), so a user is just an ID and a name.
/// </summary>
public sealed class User
{
    public const int NameMaxLength = 100;

    /// <summary>
    /// Used by EF Core to rebuild users that already exist in the database.
    /// </summary>
    private User()
    {
        Name = string.Empty;
    }

    /// <summary>
    /// Creates a new user. Invalid values are rejected here, so an invalid user can't exist in memory.
    /// </summary>
    public User(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(name.Length, NameMaxLength, nameof(name));

        Name = name;
    }

    /// <summary>Assigned by the database.</summary>
    public long Id { get; private set; }

    public string Name { get; private set; }
}
