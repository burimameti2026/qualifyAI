using Microsoft.EntityFrameworkCore;
using LeadsAI.Domain;
using LeadsAI.Persistence.SqlServer;

namespace LeadsAI.Api;

public static class FinanceEndpoints
{
    public static IEndpointRouteBuilder MapFinance(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/finance").RequireAuthorization();

        group.MapGet("/accounts", async (AppDbContext db, CancellationToken ct) =>
            Results.Ok(await db.CustomerAccounts.AsNoTracking().OrderBy(x => x.CompanyId).ToListAsync(ct)));

        group.MapPost("/accounts", async (CreateCustomerAccountRequest request, AppDbContext db, CancellationToken ct) =>
        {
            if (request.CompanyId == Guid.Empty) return Results.BadRequest(new { error = "companyId is required" });
            if (string.IsNullOrWhiteSpace(request.Currency)) return Results.BadRequest(new { error = "currency is required" });
            if (await db.CustomerAccounts.AnyAsync(x => x.CompanyId == request.CompanyId, ct))
                return Results.Conflict(new { error = "A customer account already exists for this company" });

            var account = new CustomerAccount
            {
                Id = Guid.NewGuid(), TenantId = request.TenantId,
                CompanyId = request.CompanyId,
                CustomerType = string.IsNullOrWhiteSpace(request.CustomerType) ? "customer" : request.CustomerType.Trim(),
                PaymentTerms = string.IsNullOrWhiteSpace(request.PaymentTerms) ? "due-on-receipt" : request.PaymentTerms.Trim(),
                Currency = request.Currency.Trim().ToUpperInvariant(), IsActive = true
            };
            db.CustomerAccounts.Add(account);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/finance/accounts/{account.Id}", account);
        });

        group.MapGet("/documents", async (Guid? customerAccountId, CommercialDocumentType? type, string? status, AppDbContext db, CancellationToken ct) =>
        {
            var query = db.CommercialDocuments.AsNoTracking().AsQueryable();
            if (customerAccountId.HasValue) query = query.Where(x => x.CustomerAccountId == customerAccountId.Value);
            if (type.HasValue) query = query.Where(x => x.Type == type.Value);
            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
            return Results.Ok(await query.OrderByDescending(x => x.IssuedAtUtc).ThenByDescending(x => x.CreatedAtUtc).Take(500).ToListAsync(ct));
        });

        group.MapPost("/documents", async (CreateCommercialDocumentRequest request, AppDbContext db, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Number)) return Results.BadRequest(new { error = "number is required" });
            if (request.Amount < 0) return Results.BadRequest(new { error = "amount cannot be negative" });
            if (string.IsNullOrWhiteSpace(request.Currency)) return Results.BadRequest(new { error = "currency is required" });
            if (await db.CommercialDocuments.AnyAsync(x => x.Number == request.Number.Trim(), ct))
                return Results.Conflict(new { error = "Document number already exists" });
            if (request.CustomerAccountId.HasValue && !await db.CustomerAccounts.AnyAsync(x => x.Id == request.CustomerAccountId.Value, ct))
                return Results.BadRequest(new { error = "Customer account was not found" });
            if (request.SalesOrderId.HasValue && !await db.SalesOrders.AnyAsync(x => x.Id == request.SalesOrderId.Value, ct))
                return Results.BadRequest(new { error = "Sales order was not found" });

            var document = new CommercialDocument
            {
                Id = Guid.NewGuid(), TenantId = request.TenantId,
                Number = request.Number.Trim(), Type = request.Type,
                SalesOrderId = request.SalesOrderId, CustomerAccountId = request.CustomerAccountId,
                Amount = request.Amount, Currency = request.Currency.Trim().ToUpperInvariant(),
                Status = "issued", IssuedAtUtc = request.IssuedAtUtc ?? DateTime.UtcNow,
                DueAtUtc = request.DueAtUtc, Uri = request.Uri?.Trim() ?? string.Empty
            };
            db.CommercialDocuments.Add(document);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/finance/documents/{document.Id}", document);
        });

        group.MapGet("/payments", async (Guid? documentId, PaymentStatus? status, AppDbContext db, CancellationToken ct) =>
        {
            var query = db.Payments.AsNoTracking().AsQueryable();
            if (documentId.HasValue) query = query.Where(x => x.CommercialDocumentId == documentId.Value);
            if (status.HasValue) query = query.Where(x => x.Status == status.Value);
            return Results.Ok(await query.OrderByDescending(x => x.PaidAtUtc).ThenByDescending(x => x.CreatedAtUtc).Take(500).ToListAsync(ct));
        });

        group.MapPost("/payments", async (CreatePaymentRequest request, AppDbContext db, CancellationToken ct) =>
        {
            if (request.Amount <= 0) return Results.BadRequest(new { error = "amount must be greater than zero" });
            if (string.IsNullOrWhiteSpace(request.Currency)) return Results.BadRequest(new { error = "currency is required" });
            if (request.CommercialDocumentId.HasValue)
            {
                var document = await db.CommercialDocuments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.CommercialDocumentId.Value, ct);
                if (document is null) return Results.BadRequest(new { error = "Commercial document was not found" });
                if (!string.Equals(document.Currency, request.Currency.Trim(), StringComparison.OrdinalIgnoreCase))
                    return Results.BadRequest(new { error = "Payment currency must match the commercial document currency" });
            }
            if (!string.IsNullOrWhiteSpace(request.ExternalTransactionId) && await db.Payments.AnyAsync(x => x.ExternalTransactionId == request.ExternalTransactionId.Trim(), ct))
                return Results.Conflict(new { error = "External transaction already exists" });

            var payment = new Payment
            {
                Id = Guid.NewGuid(), TenantId = request.TenantId,
                SalesOrderId = request.SalesOrderId, CommercialDocumentId = request.CommercialDocumentId,
                Amount = request.Amount, Currency = request.Currency.Trim().ToUpperInvariant(), Method = request.Method,
                Status = request.Paid ? PaymentStatus.Paid : PaymentStatus.Pending,
                Provider = request.Provider?.Trim() ?? string.Empty,
                ExternalTransactionId = request.ExternalTransactionId?.Trim() ?? string.Empty,
                Reference = request.Reference?.Trim() ?? string.Empty,
                RequestedAtUtc = DateTime.UtcNow, PaidAtUtc = request.Paid ? DateTime.UtcNow : null
            };
            db.Payments.Add(payment);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/finance/payments/{payment.Id}", payment);
        });

        group.MapPost("/payments/{paymentId:guid}/allocate", async (Guid paymentId, AllocatePaymentRequest request, AppDbContext db, CancellationToken ct) =>
        {
            if (request.Amount <= 0) return Results.BadRequest(new { error = "amount must be greater than zero" });
            var payment = await db.Payments.SingleOrDefaultAsync(x => x.Id == paymentId, ct);
            if (payment is null) return Results.NotFound(new { error = "Payment not found" });
            if (payment.Status != PaymentStatus.Paid && payment.Status != PaymentStatus.PartiallyPaid)
                return Results.BadRequest(new { error = "Only paid payments can be allocated" });
            var document = await db.CommercialDocuments.SingleOrDefaultAsync(x => x.Id == request.CommercialDocumentId, ct);
            if (document is null) return Results.NotFound(new { error = "Commercial document not found" });
            if (!string.Equals(payment.Currency, document.Currency, StringComparison.OrdinalIgnoreCase))
                return Results.BadRequest(new { error = "Payment and document currencies must match" });

            var allocated = await db.PaymentAllocations.Where(x => x.PaymentId == paymentId).SumAsync(x => x.Amount, ct);
            var documentAllocated = await db.PaymentAllocations.Where(x => x.CommercialDocumentId == document.Id).SumAsync(x => x.Amount, ct);
            if (allocated + request.Amount > payment.Amount) return Results.BadRequest(new { error = "Allocation exceeds payment amount" });
            if (documentAllocated + request.Amount > document.Amount) return Results.BadRequest(new { error = "Allocation exceeds document amount" });

            var allocation = new PaymentAllocation { Id = Guid.NewGuid(), TenantId = payment.TenantId, PaymentId = payment.Id, CommercialDocumentId = document.Id, Amount = request.Amount };
            db.PaymentAllocations.Add(allocation);
            if (allocated + request.Amount >= payment.Amount) payment.Status = PaymentStatus.Paid;
            else payment.Status = PaymentStatus.PartiallyPaid;
            document.Status = documentAllocated + request.Amount >= document.Amount ? "paid" : "partially-paid";
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { allocation, paymentStatus = payment.Status, documentStatus = document.Status });
        });

        group.MapGet("/receivables", async (AppDbContext db, CancellationToken ct) =>
        {
            var documents = await db.CommercialDocuments.AsNoTracking().Where(x => x.Type == CommercialDocumentType.Invoice && x.Status != "cancelled").ToListAsync(ct);
            var allocations = await db.PaymentAllocations.AsNoTracking().ToListAsync(ct);
            var result = documents.Select(d => new
            {
                document = d,
                paid = allocations.Where(a => a.CommercialDocumentId == d.Id).Sum(a => a.Amount),
            }).Select(x => new { x.document, x.paid, outstanding = Math.Max(0m, x.document.Amount - x.paid) });
            return Results.Ok(result);
        });

        return endpoints;
    }

    public sealed record CreateCustomerAccountRequest(Guid TenantId, Guid CompanyId, string? CustomerType, string? PaymentTerms, string Currency);
    public sealed record CreateCommercialDocumentRequest(Guid TenantId, string Number, CommercialDocumentType Type, decimal Amount, string Currency, Guid? SalesOrderId, Guid? CustomerAccountId, DateTime? IssuedAtUtc, DateTime? DueAtUtc, string? Uri);
    public sealed record CreatePaymentRequest(Guid TenantId, decimal Amount, string Currency, PaymentMethod Method, Guid? SalesOrderId, Guid? CommercialDocumentId, string? Provider, string? ExternalTransactionId, string? Reference, bool Paid);
    public sealed record AllocatePaymentRequest(Guid CommercialDocumentId, decimal Amount);
}
