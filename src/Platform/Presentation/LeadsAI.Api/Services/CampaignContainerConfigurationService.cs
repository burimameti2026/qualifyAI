using System.Text.Json;

namespace LeadsAI.Api.Services;

public sealed class CampaignContainerConfigurationService
{
    public Guid? ReadTargetListId(string? configurationJson)
    {
        if (string.IsNullOrWhiteSpace(configurationJson))
            return null;

        try
        {
            using var document = JsonDocument.Parse(configurationJson);
            if (document.RootElement.TryGetProperty("targetListId", out var value) &&
                value.ValueKind == JsonValueKind.String &&
                Guid.TryParse(value.GetString(), out var id))
                return id;
        }
        catch (JsonException)
        {
        }

        return null;
    }

    public string Build(string? configurationJson, Guid? targetListId)
    {
        var data = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(configurationJson))
        {
            try
            {
                using var document = JsonDocument.Parse(configurationJson);
                if (document.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in document.RootElement.EnumerateObject())
                        data[property.Name] = property.Value.Clone();
                }
            }
            catch (JsonException)
            {
            }
        }

        if (targetListId.HasValue)
            data["targetListId"] = targetListId.Value;
        else
            data.Remove("targetListId");

        return JsonSerializer.Serialize(data);
    }
}
