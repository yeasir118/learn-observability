using System.Text.Json;
using TinyShop.ServiceDefaults.Json;

namespace TinyShop.ServiceDefaults.UnitTests.Json;

/// <summary>contracts.md §1.3: UTC, "Z", exactly three fractional digits.</summary>
public class UtcTimestampJsonConverterTests
{
    private static readonly JsonSerializerOptions Options = new() { Converters = { new UtcTimestampJsonConverter() } };

    [Theory]
    [InlineData(123, "\"2026-10-18T14:30:05.123Z\"")]
    [InlineData(120, "\"2026-10-18T14:30:05.120Z\"")]
    [InlineData(0, "\"2026-10-18T14:30:05.000Z\"")]
    public void Write_AlwaysHasThreeFractionalDigitsAndZ(int milliseconds, string expected)
    {
        var value = new DateTimeOffset(2026, 10, 18, 14, 30, 5, milliseconds, TimeSpan.Zero);

        Assert.Equal(expected, JsonSerializer.Serialize(value, Options));
    }

    [Fact]
    public void Write_ConvertsOtherOffsetsToUtc()
    {
        var value = new DateTimeOffset(2026, 10, 18, 20, 30, 5, 123, TimeSpan.FromHours(6));

        Assert.Equal("\"2026-10-18T14:30:05.123Z\"", JsonSerializer.Serialize(value, Options));
    }

    [Fact]
    public void Read_RoundTrips()
    {
        var value = JsonSerializer.Deserialize<DateTimeOffset>("\"2026-10-18T14:30:05.123Z\"", Options);

        Assert.Equal(new DateTimeOffset(2026, 10, 18, 14, 30, 5, 123, TimeSpan.Zero), value);
    }
}
