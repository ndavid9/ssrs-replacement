using System.Text.Json;
using System.Text.Json.Serialization;

namespace LabReport.Core;

public static class JsonDefaults
{
    public static JsonSerializerOptions Options { get; } = Create(writeIndented: true);

    public static JsonSerializerOptions Canonical { get; } = Create(writeIndented: false);

    private static JsonSerializerOptions Create(bool writeIndented) => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = writeIndented,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
}
