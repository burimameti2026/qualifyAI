namespace QualifyAI.Domain;
public class MetricSnapshot : TenantEntity { public string Metric { get; set; }=""; public decimal Value { get; set; } public DateTime PeriodStartUtc { get; set; } public DateTime PeriodEndUtc { get; set; } public string DimensionsJson { get; set; }="{}"; }
public class RevenueAttribution : TenantEntity { public Guid? LeadId { get; set; } public Guid? OpportunityId { get; set; } public Guid? ConversationId { get; set; } public decimal InfluencedRevenue { get; set; } public string Model { get; set; }="first-touch"; }
public class Plan : Entity { public string Code { get; set; }="starter"; public string Name { get; set; }="Starter"; public decimal MonthlyPrice { get; set; } public string Currency { get; set; }="EUR"; public string EntitlementsJson { get; set; }="{}"; }
public class Subscription : TenantEntity { public Guid PlanId { get; set; } public string Status { get; set; }="active"; public string ExternalCustomerId { get; set; }=""; public string ExternalSubscriptionId { get; set; }=""; public DateTime CurrentPeriodStartUtc { get; set; } public DateTime CurrentPeriodEndUtc { get; set; } }
public class UsageRecord : TenantEntity { public string Meter { get; set; }="messages"; public decimal Quantity { get; set; } public DateTime RecordedAtUtc { get; set; }=DateTime.UtcNow; public string ReferenceId { get; set; }="";
    public string Metric
    {
        get;
        set;
    }
    public long Value
    {
        get;
        set;
    }
}
public class BillingInvoice : TenantEntity { public string Number { get; set; }=""; public decimal Amount { get; set; } public string Currency { get; set; }="EUR"; public string Status { get; set; }="draft"; public DateTime? DueAtUtc { get; set; } }
public class SsoConfiguration : TenantEntity { public string ProviderType { get; set; }="saml"; public string EntityId { get; set; }=""; public string MetadataUrl { get; set; }=""; public bool Enabled { get; set; } }
public class DataRetentionPolicy : TenantEntity { public string EntityType { get; set; }="messages"; public int RetentionDays { get; set; }=365; public bool Enabled { get; set; }=true; }
public class ConsentRecord : TenantEntity { public Guid? ContactId { get; set; } public string Type { get; set; }="marketing"; public bool Granted { get; set; } public DateTime RecordedAtUtc { get; set; }=DateTime.UtcNow; public string Source { get; set; }="web"; }
public class PiiRedactionJob : TenantEntity { public string EntityType { get; set; }="contact"; public Guid EntityId { get; set; } public string Status { get; set; }="pending"; }
