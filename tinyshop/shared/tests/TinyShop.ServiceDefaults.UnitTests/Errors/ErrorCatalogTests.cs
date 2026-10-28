using TinyShop.ServiceDefaults.Errors;

namespace TinyShop.ServiceDefaults.UnitTests.Errors;

public class ErrorCatalogTests
{
    [Fact]
    public void UnknownCode_FallsBackToInternalErrorTitle()
    {
        Assert.Equal("Internal error", new ErrorCatalog().TitleFor("no_such_code"));
    }

    [Fact]
    public void AddedCode_HasItsTitle()
    {
        var catalog = new ErrorCatalog().Add("example_not_found", "Example not found");

        Assert.Equal("Example not found", catalog.TitleFor("example_not_found"));
    }

    [Fact]
    public void AddingASharedCodeAgain_Throws()
    {
        var catalog = new ErrorCatalog();

        var exception = Assert.Throws<InvalidOperationException>(
            () => catalog.Add(ErrorCodes.NotFound, "Something else"));

        Assert.Contains("not_found", exception.Message);
    }

    [Fact]
    public void MappingAStatusThatAlreadyHasACode_Throws()
    {
        var catalog = new ErrorCatalog();

        var exception = Assert.Throws<InvalidOperationException>(
            () => catalog.Add("example_not_found", "Example not found", forStatus: 404));

        Assert.Contains("404", exception.Message);
    }
}
