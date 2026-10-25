using TinyShop.UserService.Domain.Entities;

namespace TinyShop.UserService.UnitTests.Domain;

public class UserTests
{
    [Fact]
    public void Constructor_WithValidName_SetsName()
    {
        var user = new User("Alice");

        Assert.Equal("Alice", user.Name);
    }

    [Fact]
    public void Constructor_WithNameAtMaxLength_Succeeds()
    {
        var name = new string('a', User.NameMaxLength);

        var user = new User(name);

        Assert.Equal(name, user.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithBlankName_Throws(string name)
    {
        Assert.Throws<ArgumentException>(() => new User(name));
    }

    [Fact]
    public void Constructor_WithNullName_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new User(null!));
    }

    [Fact]
    public void Constructor_WithNameLongerThanMax_Throws()
    {
        var name = new string('a', User.NameMaxLength + 1);

        Assert.Throws<ArgumentOutOfRangeException>(() => new User(name));
    }
}
