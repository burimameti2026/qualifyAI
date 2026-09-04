namespace QualifyAI.Domain;

public class Company : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string Industry { get; set; } = string.Empty;
    public int? Employees { get; set; }
    public string Country { get; set; } = string.Empty;
    public decimal? AnnualRevenue { get; set; }

    public static Company Create(Guid tenantId, string name, string domain, string industry, int? employees, string country, decimal? annualRevenue)
    {
        var company = new Company { TenantId = tenantId };
        company.UpdateProfile(name, domain, industry, employees, country, annualRevenue);
        return company;
    }

    public void UpdateProfile(string name, string domain, string industry, int? employees, string country, decimal? annualRevenue)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Company name is required.");
        if (employees is < 0) throw new InvalidOperationException("Employees cannot be negative.");
        if (annualRevenue is < 0) throw new InvalidOperationException("Annual revenue cannot be negative.");
        Name = name.Trim();
        Domain = domain?.Trim().ToLowerInvariant() ?? string.Empty;
        Industry = industry?.Trim() ?? string.Empty;
        Employees = employees;
        Country = country?.Trim() ?? string.Empty;
        AnnualRevenue = annualRevenue;
        Touch();
    }
}

public class Contact : TenantEntity
{
    public Guid? CompanyId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string LifecycleStage { get; set; } = "visitor";

    public static Contact Create(Guid tenantId, Guid? companyId, string firstName, string lastName, string email, string phone, string lifecycleStage)
    {
        var contact = new Contact { TenantId = tenantId };
        contact.UpdateProfile(companyId, firstName, lastName, email, phone, lifecycleStage);
        return contact;
    }

    public void UpdateProfile(Guid? companyId, string firstName, string lastName, string email, string phone, string lifecycleStage)
    {
        if (string.IsNullOrWhiteSpace(firstName) && string.IsNullOrWhiteSpace(lastName) && string.IsNullOrWhiteSpace(email))
            throw new InvalidOperationException("A contact requires a name or email.");
        CompanyId = companyId;
        FirstName = firstName?.Trim() ?? string.Empty;
        LastName = lastName?.Trim() ?? string.Empty;
        Email = email?.Trim().ToLowerInvariant() ?? string.Empty;
        Phone = phone?.Trim() ?? string.Empty;
        LifecycleStage = NormalizeLifecycle(lifecycleStage);
        Touch();
    }

    private static string NormalizeLifecycle(string? value) => (value ?? "visitor").Trim().ToLowerInvariant() switch
    {
        "visitor" or "subscriber" or "lead" or "mql" or "sql" or "opportunity" or "customer" => (value ?? "visitor").Trim().ToLowerInvariant(),
        _ => throw new InvalidOperationException("Invalid contact lifecycle stage.")
    };
}

public class Lead : TenantEntity
{
    public Guid ContactId { get; set; }
    public Guid? CompanyId { get; set; }
    public string Source { get; set; } = "web";
    public int Score { get; set; }
    public LeadTemperature Temperature { get; set; }
    public string Status { get; set; } = "new";
    public decimal? EstimatedValue { get; set; }
    public string IntentSummary { get; set; } = string.Empty;

    public static Lead Create(Guid tenantId, Guid contactId, Guid? companyId, string source, int score, decimal? estimatedValue, string intentSummary)
    {
        if (contactId == Guid.Empty) throw new InvalidOperationException("Lead contact is required.");
        var lead = new Lead
        {
            TenantId = tenantId,
            ContactId = contactId,
            CompanyId = companyId,
            Source = string.IsNullOrWhiteSpace(source) ? "web" : source.Trim().ToLowerInvariant()
        };
        lead.SetEstimatedValue(estimatedValue);
        lead.SetScore(score, intentSummary);
        return lead;
    }

    public void SetScore(int score, string? intentSummary = null)
    {
        Score = Math.Clamp(score, 0, 100);
        Temperature = Score >= 80 ? LeadTemperature.Hot : Score >= 50 ? LeadTemperature.Warm : LeadTemperature.Cold;
        if (intentSummary is not null) IntentSummary = intentSummary.Trim();
        Touch();
    }

    public void Qualify()
    {
        SetScore(Score);
        Status = Score >= 80 ? "qualified" : Score >= 50 ? "nurture" : "new";
        Touch();
    }

    public void SetEstimatedValue(decimal? value)
    {
        if (value is < 0) throw new InvalidOperationException("Estimated value cannot be negative.");
        EstimatedValue = value;
        Touch();
    }
}

public class Opportunity : TenantEntity
{
    public Guid? LeadId { get; set; }
    public Guid? CompanyId { get; set; }
    public Guid? ContactId { get; set; }
    public Guid? PipelineStageId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public OpportunityStatus Status { get; set; } = OpportunityStatus.Open;
    public DateTime? ExpectedCloseUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public string LossReason { get; set; } = string.Empty;

    public void UpdateDetails(string name, decimal amount, DateTime? expectedCloseUtc)
    {
        EnsureOpen();
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Opportunity name is required.");
        if (amount < 0) throw new InvalidOperationException("Opportunity amount cannot be negative.");
        Name = name.Trim();
        Amount = amount;
        ExpectedCloseUtc = expectedCloseUtc;
        Touch();
    }

    public void MoveToStage(Guid stageId)
    {
        EnsureOpen();
        if (stageId == Guid.Empty) throw new InvalidOperationException("Pipeline stage is required.");
        PipelineStageId = stageId;
        Touch();
    }

    public void MarkWon(DateTime? closedAtUtc = null)
    {
        EnsureOpen();
        Status = OpportunityStatus.Won;
        ClosedAtUtc = closedAtUtc ?? DateTime.UtcNow;
        LossReason = string.Empty;
        Touch();
    }

    public void MarkLost(string reason, DateTime? closedAtUtc = null)
    {
        EnsureOpen();
        if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("Loss reason is required.");
        Status = OpportunityStatus.Lost;
        ClosedAtUtc = closedAtUtc ?? DateTime.UtcNow;
        LossReason = reason.Trim();
        Touch();
    }

    public void Reopen()
    {
        if (Status == OpportunityStatus.Open) return;
        Status = OpportunityStatus.Open;
        ClosedAtUtc = null;
        LossReason = string.Empty;
        Touch();
    }

    private void EnsureOpen()
    {
        if (Status != OpportunityStatus.Open) throw new InvalidOperationException("Closed opportunities cannot be modified.");
    }
}

public class CrmActivity : TenantEntity
{
    public Guid? ContactId { get; set; }
    public Guid? CompanyId { get; set; }
    public Guid? LeadId { get; set; }
    public string Type { get; set; } = "note";
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    public static CrmActivity ForOpportunity(Opportunity opportunity, string subject, string body) => new()
    {
        TenantId = opportunity.TenantId,
        LeadId = opportunity.LeadId,
        CompanyId = opportunity.CompanyId,
        ContactId = opportunity.ContactId,
        Type = "pipeline",
        Subject = subject.Trim(),
        Body = body.Trim()
    };
}

public class CrmTask : TenantEntity { public Guid? ContactId { get; set; } public Guid? LeadId { get; set; } public Guid? OwnerUserId { get; set; } public string Title { get; set; } = string.Empty; public DateTime? DueAtUtc { get; set; } public bool Completed { get; set; } }
public class Tag : TenantEntity { public string Name { get; set; } = string.Empty; }
public class CustomFieldDefinition : TenantEntity { public string EntityType { get; set; } = "contact"; public string Key { get; set; } = string.Empty; public string Label { get; set; } = string.Empty; public string DataType { get; set; } = "text"; }
public class CustomFieldValue : TenantEntity { public Guid DefinitionId { get; set; } public Guid EntityId { get; set; } public string Value { get; set; } = string.Empty; }
