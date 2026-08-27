namespace QualifyAI.Domain;

public class KnowledgeBase : TenantEntity
{
    public string Name { get; set; } = "Default";
    public string Description { get; set; } = string.Empty;
}

public class KnowledgeSource : TenantEntity
{
    public Guid KnowledgeBaseId { get; set; }
    public string Type { get; set; } = "manual";
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Status { get; set; } = "ready";
    public DateTime? LastSyncedAtUtc { get; set; }
}

public class KnowledgeDocument : TenantEntity
{
    public Guid KnowledgeBaseId { get; set; }
    public Guid? SourceId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public bool Published { get; set; } = true;

    public static KnowledgeDocument Create(Guid tenantId, Guid knowledgeBaseId, Guid? sourceId, string title, string body, bool published)
    {
        if (knowledgeBaseId == Guid.Empty) throw new InvalidOperationException("Knowledge base is required.");
        var document = new KnowledgeDocument { TenantId = tenantId, KnowledgeBaseId = knowledgeBaseId, SourceId = sourceId };
        document.UpdateContent(title, body, published, incrementVersion: false);
        return document;
    }

    public void UpdateContent(string title, string body, bool published, bool incrementVersion = true)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new InvalidOperationException("Document title is required.");
        if (string.IsNullOrWhiteSpace(body)) throw new InvalidOperationException("Document body is required.");
        Title = title.Trim();
        Body = body.Trim();
        Published = published;
        Version = incrementVersion ? checked(Math.Max(1, Version) + 1) : 1;
        Touch();
    }

    public IReadOnlyList<KnowledgeChunk> RebuildChunks(int maxChunks = 100)
    {
        if (maxChunks <= 0) throw new ArgumentOutOfRangeException(nameof(maxChunks));
        return Body.Split(new[] { "\n\n", ". " }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Take(maxChunks)
            .Select((text, index) => KnowledgeChunk.Create(TenantId, Id, index, text))
            .ToArray();
    }
}

public class KnowledgeChunk : TenantEntity
{
    public Guid DocumentId { get; set; }
    public int ChunkIndex { get; set; }
    public string Text { get; set; } = string.Empty;
    public string VectorJson { get; set; } = "[]";

    public static KnowledgeChunk Create(Guid tenantId, Guid documentId, int chunkIndex, string text)
    {
        if (documentId == Guid.Empty) throw new InvalidOperationException("Document is required.");
        if (chunkIndex < 0) throw new InvalidOperationException("Chunk index cannot be negative.");
        if (string.IsNullOrWhiteSpace(text)) throw new InvalidOperationException("Chunk text is required.");
        return new KnowledgeChunk { TenantId = tenantId, DocumentId = documentId, ChunkIndex = chunkIndex, Text = text.Trim() };
    }
}

public class KnowledgeGap : TenantEntity
{
    public string Topic { get; set; } = string.Empty;
    public int Occurrences { get; set; }
    public string ExampleQuestion { get; set; } = string.Empty;
    public string Status { get; set; } = "open";
    public decimal ImpactScore { get; set; }

    public void ChangeStatus(string status)
    {
        Status = (status ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "open" => "open",
            "reviewing" => "reviewing",
            "resolved" => "resolved",
            "dismissed" => "dismissed",
            _ => throw new InvalidOperationException("Invalid knowledge gap status.")
        };
        Touch();
    }
}

public class AiAgent : TenantEntity
{
    public string Name { get; set; } = "AI Agent";
    public string Role { get; set; } = "Support and Sales";
    public string Instructions { get; set; } = string.Empty;
    public string Tone { get; set; } = "professional";
    public string Model { get; set; } = "local";
    public string LanguagesCsv { get; set; } = "en";
    public bool Active { get; set; } = true;
    public Guid? KnowledgeBaseId { get; set; }

    public static AiAgent Create(Guid tenantId, string name, string role, string instructions, string tone, string model, string languagesCsv, bool active, Guid? knowledgeBaseId)
    {
        var agent = new AiAgent { TenantId = tenantId };
        agent.UpdateConfiguration(name, role, instructions, tone, model, languagesCsv, active, knowledgeBaseId);
        return agent;
    }

    public void UpdateConfiguration(string name, string role, string instructions, string tone, string model, string languagesCsv, bool active, Guid? knowledgeBaseId)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Agent name is required.");
        if (string.IsNullOrWhiteSpace(instructions)) throw new InvalidOperationException("Agent instructions are required.");
        Name = name.Trim();
        Role = string.IsNullOrWhiteSpace(role) ? "Support and Sales" : role.Trim();
        Instructions = instructions.Trim();
        Tone = string.IsNullOrWhiteSpace(tone) ? "professional" : tone.Trim().ToLowerInvariant();
        Model = string.IsNullOrWhiteSpace(model) ? "local" : model.Trim();
        LanguagesCsv = NormalizeLanguages(languagesCsv);
        Active = active;
        KnowledgeBaseId = knowledgeBaseId;
        Touch();
    }

    private static string NormalizeLanguages(string? value)
    {
        var languages = (value ?? "en").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.ToLowerInvariant()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return languages.Length == 0 ? "en" : string.Join(',', languages);
    }
}

public class AiAgentVersion : TenantEntity { public Guid AgentId { get; set; } public int Version { get; set; } public string ConfigurationJson { get; set; } = "{}"; public bool Published { get; set; } }
public class AiToolDefinition : TenantEntity { public string Name { get; set; } = string.Empty; public string Description { get; set; } = string.Empty; public string InputSchemaJson { get; set; } = "{}"; public string RequiredPermission { get; set; } = string.Empty; public bool Enabled { get; set; } = true; }
public class AiToolExecution : TenantEntity { public Guid? AgentId { get; set; } public Guid? ConversationId { get; set; } public string ToolName { get; set; } = string.Empty; public string InputJson { get; set; } = "{}"; public string OutputJson { get; set; } = "{}"; public bool Success { get; set; } public long DurationMs { get; set; } }
public class PromptVersion : TenantEntity { public Guid AgentId { get; set; } public int Version { get; set; } public string Prompt { get; set; } = string.Empty; public bool Active { get; set; } }
