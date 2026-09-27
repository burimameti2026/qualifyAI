namespace QualifyAI.Persistence.SqlServer.Projections;

public sealed class TenantBillingInvoiceRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Provider { get; set; } = null!;
    public string ExternalInvoiceId { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string Currency { get; set; } = "USD";
    public decimal AmountDue { get; set; }
    public decimal AmountPaid { get; set; }
    public DateTime? DueAtUtc { get; set; }
    public DateTime? PaidAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
