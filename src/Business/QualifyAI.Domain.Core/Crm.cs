namespace QualifyAI.Domain;

public class Company : TenantEntity
{
    public string Name { get; set; } = "";
    public string Domain { get; set; } = "";
    public string Industry { get; set; } = "";
    public int? Employees { get; set; }
    public string Country { get; set; } = "";
    public decimal? AnnualRevenue { get; set; }

    public void UpdateProfile(string name, string domain, string industry, int? employees, string country, decimal? annualRevenue)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Company name is required.");
        if (employees is < 0) throw new InvalidOperationException("Employees cannot be negative.");
        if (annualRevenue is < 0) throw new InvalidOperationException("Annual revenue cannot be negative.");
        Name = name.Trim(); Domain = domain?.Trim() ?? ""; Industry = industry?.Trim() ?? "";
        Employees = employees; Country = country?.Trim() ?? ""; AnnualRevenue = annualRevenue; Touch();
    }
}

public class Contact : TenantEntity
{
    public Guid? CompanyId { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string LifecycleStage { get; set; } = "visitor";

    public void UpdateProfile(Guid? companyId, string firstName, string lastName, string email, string phone, string lifecycleStage)
    {
        if (string.IsNullOrWhiteSpace(firstName) && string.IsNullOrWhiteSpace(lastName) && string.IsNullOrWhiteSpace(email))
            throw new InvalidOperationException("A contact requires a name or email.");
        CompanyId = companyId; FirstName = firstName?.Trim() ?? ""; LastName = lastName?.Trim() ?? "";
        Email = email?.Trim().ToLowerInvariant() ?? ""; Phone = phone?.Trim() ?? "";
        LifecycleStage = NormalizeLifecycle(lifecycleStage); Touch();
    }

    private static string NormalizeLifecycle(string value) => (value ?? "visitor").Trim().ToLowerInvariant() switch
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
    public string IntentSummary { get; set; } = "";

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
        EstimatedValue = value; Touch();
    }
}

public class Opportunity : TenantEntity
{
    public Guid? LeadId { get; set; }
    public Guid? CompanyId { get; set; }
    public Guid? ContactId { get; set; }
    public Guid? PipelineStageId { get; set; }
    public string Name { get; set; } = "";
    public decimal Amount { get; set; }
    public OpportunityStatus Status { get; set; } = OpportunityStatus.Open;
    public DateTime? ExpectedCloseUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public string LossReason { get; set; } = "";

    public void UpdateDetails(string name, decimal amount, DateTime? expectedCloseUtc)
    {
        if (Status != OpportunityStatus.Open) throw new InvalidOperationException("Closed opportunities cannot be edited.");
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Opportunity name is required.");
        if (amount < 0) throw new InvalidOperationException("Opportunity amount cannot be negative.");
        Name = name.Trim(); Amount = amount; ExpectedCloseUtc = expectedCloseUtc; Touch();
    }

    public void MoveToStage(Guid stageId)
    {
        if (Status != OpportunityStatus.Open) throw new InvalidOperationException("Closed opportunities cannot change stage.");
        if (stageId == Guid.Empty) throw new InvalidOperationException("Pipeline stage is required.");
        PipelineStageId = stageId; Touch();
    }

    public void MarkWon(DateTime? closedAtUtc = null)
    {
        if (Status != OpportunityStatus.Open) throw new InvalidOperationException("Opportunity is already closed.");
        Status = OpportunityStatus.Won; ClosedAtUtc = closedAtUtc ?? DateTime.UtcNow; LossReason = ""; Touch();
    }

    public void MarkLost(string reason, DateTime? closedAtUtc = null)
    {
        if (Status != OpportunityStatus.Open) throw new InvalidOperationException("Opportunity is already closed.");
        if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("Loss reason is required.");
        Status = OpportunityStatus.Lost; ClosedAtUtc = closedAtUtc ?? DateTime.UtcNow; LossReason = reason.Trim(); Touch();
    }

    public void Reopen()
    {
        Status = OpportunityStatus.Open; ClosedAtUtc = null; LossReason = ""; Touch();
    }
}

public class CrmActivity : TenantEntity { public Guid? ContactId { get; set; } public Guid? CompanyId { get; set; } public Guid? LeadId { get; set; } public string Type { get; set; }="note"; public string Subject { get; set; }=""; public string Body { get; set; }=""; }
public class CrmTask : TenantEntity { public Guid? ContactId { get; set; } public Guid? LeadId { get; set; } public Guid? OwnerUserId { get; set; } public string Title { get; set; }=""; public DateTime? DueAtUtc { get; set; } public bool Completed { get; set; } }
public class Tag : TenantEntity { public string Name { get; set; }=""; }
public class CustomFieldDefinition : TenantEntity { public string EntityType { get; set; }="contact"; public string Key { get; set; }=""; public string Label { get; set; }=""; public string DataType { get; set; }="text"; }
public class CustomFieldValue : TenantEntity { public Guid DefinitionId { get; set; } public Guid EntityId { get; set; } public string Value { get; set; }=""; }
