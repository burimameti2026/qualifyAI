using System.Text.Json;

namespace LeadsAI.Api.Services;

public sealed class AcquisitionCriteriaService
{
    public int ReadMinimumScore(string? criteriaJson)
    {
        if (string.IsNullOrWhiteSpace(criteriaJson))
            return 70;

        try
        {
            using var document = JsonDocument.Parse(criteriaJson);
            if (document.RootElement.TryGetProperty("minimumScore", out var value) &&
                value.TryGetInt32(out var score))
                return Math.Clamp(score, 0, 100);
        }
        catch (JsonException)
        {
        }

        return 70;
    }

    public string NormalizeCriteria(string? criteriaJson, int minimumScore)
    {
        var score = Math.Clamp(minimumScore, 0, 100);

        try
        {
            using var document = JsonDocument.Parse(
                string.IsNullOrWhiteSpace(criteriaJson) ? "{}" : criteriaJson);

            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                var map = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var property in document.RootElement.EnumerateObject())
                    map[property.Name] = property.Value.Clone();

                map["minimumScore"] = score;
                return JsonSerializer.Serialize(map);
            }
        }
        catch (JsonException)
        {
        }

        return JsonSerializer.Serialize(new { minimumScore = score });
    }
}
