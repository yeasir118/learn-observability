using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TinyShop.ServiceDefaults.Json;

/// <summary>
/// Writes timestamps as contracts.md §1.3 requires: UTC, a "Z" suffix and exactly three
/// fractional digits ("2026-10-18T14:30:05.120Z"). System.Text.Json's default would write
/// "+00:00" and drop trailing zeros ("2026-10-18T14:30:05.12+00:00").
/// </summary>
internal sealed class UtcTimestampJsonConverter : JsonConverter<DateTimeOffset>
{
    private const string Format = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetDateTimeOffset();

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.UtcDateTime.ToString(Format, CultureInfo.InvariantCulture));
}
