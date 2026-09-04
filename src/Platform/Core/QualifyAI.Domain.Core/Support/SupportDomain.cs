namespace QualifyAI.Domain;

public class Channel : TenantEntity
{
    public string Type { get; set; } = "web";
    public string Name { get; set; } = "Website";
    public bool Enabled { get; set; } = true;
    public string SettingsJson { get; set; } = "{}";
}

public class Conversation : TenantEntity
{
    public Guid? ContactId { get; set; }
    public Guid? LeadId { get; set; }
    public Guid? ChannelId { get; set; }
    public ConversationStatus Status { get; set; } = ConversationStatus.Open;
    public Guid? AssignedUserId { get; set; }
    public bool AiEnabled { get; set; } = true;
    public DateTime LastMessageAtUtc { get; set; } = DateTime.UtcNow;

    public void TakeOver(Guid? userId)
    {
        AiEnabled = false;
        if (userId.HasValue && userId.Value != Guid.Empty) AssignedUserId = userId;
        Touch();
    }

    public void RegisterMessage(DateTime? atUtc = null)
    {
        LastMessageAtUtc = atUtc ?? DateTime.UtcNow;
        if (Status == ConversationStatus.Closed) Status = ConversationStatus.Open;
        Touch();
    }

    public void UpdateState(ConversationStatus status, bool? aiEnabled)
    {
        Status = status;
        if (aiEnabled.HasValue) AiEnabled = aiEnabled.Value;
        Touch();
    }
}

public class Message : TenantEntity
{
    public Guid ConversationId { get; set; }
    public string SenderType { get; set; } = "visitor";
    public Guid? SenderUserId { get; set; }
    public string Text { get; set; } = string.Empty;
    public string MetadataJson { get; set; } = "{}";

    public static Message Create(Guid tenantId, Guid conversationId, Guid? userId, string text, string senderType)
    {
        if (conversationId == Guid.Empty) throw new InvalidOperationException("Conversation is required.");
        if (string.IsNullOrWhiteSpace(text)) throw new InvalidOperationException("Message text is required.");
        return new Message
        {
            TenantId = tenantId,
            ConversationId = conversationId,
            SenderUserId = userId,
            SenderType = string.IsNullOrWhiteSpace(senderType) ? "agent" : senderType.Trim().ToLowerInvariant(),
            Text = text.Trim()
        };
    }
}

public class ConversationNote : TenantEntity
{
    public Guid ConversationId { get; set; }
    public Guid UserId { get; set; }
    public string Text { get; set; } = string.Empty;

    public static ConversationNote Create(Guid tenantId, Guid conversationId, Guid userId, string text)
    {
        if (conversationId == Guid.Empty) throw new InvalidOperationException("Conversation is required.");
        if (userId == Guid.Empty) throw new InvalidOperationException("User is required.");
        if (string.IsNullOrWhiteSpace(text)) throw new InvalidOperationException("Note text is required.");
        return new ConversationNote { TenantId = tenantId, ConversationId = conversationId, UserId = userId, Text = text.Trim() };
    }
}

public class Attachment : TenantEntity { public Guid? MessageId { get; set; } public string FileName { get; set; } = string.Empty; public string ContentType { get; set; } = string.Empty; public string Url { get; set; } = string.Empty; public long SizeBytes { get; set; } }
public class Team : TenantEntity { public string Name { get; set; } = string.Empty; }

public class Ticket : TenantEntity
{
    public Guid? ConversationId { get; set; }
    public Guid? ContactId { get; set; }
    public string Number { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TicketStatus Status { get; set; } = TicketStatus.New;
    public TicketPriority Priority { get; set; } = TicketPriority.Normal;
    public Guid? AssignedUserId { get; set; }
    public Guid? SlaPolicyId { get; set; }
    public DateTime? FirstResponseDueUtc { get; set; }
    public DateTime? ResolutionDueUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }

    public static Ticket Create(Guid tenantId, Guid? conversationId, Guid? contactId, string subject, string description, TicketPriority priority, Guid? slaPolicyId)
    {
        if (string.IsNullOrWhiteSpace(subject)) throw new InvalidOperationException("Ticket subject is required.");
        return new Ticket
        {
            TenantId = tenantId,
            ConversationId = conversationId,
            ContactId = contactId,
            Number = $"T-{DateTime.UtcNow:yyyyMMddHHmmssfff}",
            Subject = subject.Trim(),
            Description = description?.Trim() ?? string.Empty,
            Priority = priority,
            SlaPolicyId = slaPolicyId,
            Status = TicketStatus.New
        };
    }

    public void Update(string subject, string description, TicketStatus targetStatus, TicketPriority priority, Guid? assignedUserId, Guid? slaPolicyId)
    {
        if (string.IsNullOrWhiteSpace(subject)) throw new InvalidOperationException("Ticket subject is required.");
        EnsureTransition(targetStatus);
        Subject = subject.Trim();
        Description = description?.Trim() ?? string.Empty;
        Priority = priority;
        AssignedUserId = assignedUserId;
        SlaPolicyId = slaPolicyId;
        Status = targetStatus;
        ResolvedAtUtc = targetStatus is TicketStatus.Resolved or TicketStatus.Closed ? ResolvedAtUtc ?? DateTime.UtcNow : null;
        Touch();
    }

    private void EnsureTransition(TicketStatus target)
    {
        if (target == Status) return;
        var allowed = Status switch
        {
            TicketStatus.New => target is TicketStatus.Open or TicketStatus.Pending or TicketStatus.Resolved or TicketStatus.Closed,
            TicketStatus.Open => target is TicketStatus.Pending or TicketStatus.Resolved or TicketStatus.Closed,
            TicketStatus.Pending => target is TicketStatus.Open or TicketStatus.Resolved or TicketStatus.Closed,
            TicketStatus.Resolved => target is TicketStatus.Open or TicketStatus.Closed,
            TicketStatus.Closed => target is TicketStatus.Open,
            _ => false
        };
        if (!allowed) throw new InvalidOperationException($"Invalid ticket transition from {Status} to {target}.");
    }
}

public class SlaPolicy : TenantEntity { public string Name { get; set; } = "Default"; public int FirstResponseMinutes { get; set; } = 60; public int ResolutionMinutes { get; set; } = 480; public bool BusinessHoursOnly { get; set; } = true; }

public class TicketEvent : TenantEntity
{
    public Guid TicketId { get; set; }
    public string Type { get; set; } = "created";
    public string DataJson { get; set; } = "{}";

    public static TicketEvent Updated(Ticket ticket) => new()
    {
        TenantId = ticket.TenantId,
        TicketId = ticket.Id,
        Type = "updated",
        DataJson = $"{{\"status\":\"{ticket.Status}\",\"priority\":\"{ticket.Priority}\"}}"
    };
}

public class CsatResponse : TenantEntity { public Guid ConversationId { get; set; } public int Score { get; set; } public string Comment { get; set; } = string.Empty; }
