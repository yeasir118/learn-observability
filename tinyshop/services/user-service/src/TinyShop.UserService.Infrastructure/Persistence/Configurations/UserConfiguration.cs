using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TinyShop.UserService.Domain.Entities;

namespace TinyShop.UserService.Infrastructure.Persistence.Configurations;

/// <summary>
/// How <see cref="User"/> maps to the database. Table and column names come from the
/// snake_case naming convention (users, id, name), configured in DependencyInjection.cs.
/// </summary>
internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(user => user.Id);

        // bigint identity column. BY DEFAULT (not ALWAYS) so that seed rows can be inserted
        // with explicit IDs.
        builder.Property(user => user.Id)
            .UseIdentityByDefaultColumn();

        builder.Property(user => user.Name)
            .IsRequired()
            .HasMaxLength(User.NameMaxLength);

        // Seed data becomes INSERTs inside the migration. User's constructor can't set Id and its
        // setters are private, so the rows are anonymous objects using the property names.
        // User 123 "Alice" is the example in contracts.md §3; the UI's default user ID.
        // Seed rows need explicit IDs; the migration then moves the identity sequence past them.
        builder.HasData(
            new { Id = 123L, Name = "Alice" },
            new { Id = 124L, Name = "Bob" },
            new { Id = 125L, Name = "Carol" },
            new { Id = 126L, Name = "Dave" },
            new { Id = 127L, Name = "Erin" });
    }
}
