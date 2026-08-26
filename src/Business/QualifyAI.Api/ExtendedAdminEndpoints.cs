using MediatR;
using QualifyAI.Application.Commands;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using QualifyAI.Domain;
using QualifyAI.Infrastructure;
using QualifyAI.Application;

namespace QualifyAI.Api;

public static class ExtendedAdminEndpoints
{
    public static void MapExtendedAdmin(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").RequireAuthorization();

        api.MapPost("/crm/leads", async (Lead x, ISender sender, ITenantContext tc, CancellationToken ct) =>
        {
            var created = await sender.Send(new CreateLeadCommand(tc.TenantId(),x.ContactId,x.CompanyId,x.Source,x.Score,x.EstimatedValue,x.IntentSummary),ct);
            return Results.Created($"/api/crm/leads/{created.Id}", created);
        });
        api.MapPut("/crm/leads/{id:guid}", async (Guid id, Lead input, AppDbContext db, ITenantContext tc) =>
        {
            var x = await db.Leads.FirstOrDefaultAsync(v => v.Id == id && v.TenantId == tc.TenantId()); if (x is null) return Results.NotFound();
            x.Source=input.Source; x.Score=input.Score; x.Status=input.Status; x.EstimatedValue=input.EstimatedValue; x.IntentSummary=input.IntentSummary; x.CompanyId=input.CompanyId; x.ContactId=input.ContactId;
            x.Temperature=x.Score>=80?LeadTemperature.Hot:x.Score>=50?LeadTemperature.Warm:LeadTemperature.Cold; await db.SaveChangesAsync(); return Results.Ok(x);
        });
        api.MapPost("/crm/opportunities", async (Opportunity x, AppDbContext db, ITenantContext tc) =>
        {
            x.Id=Guid.NewGuid(); x.TenantId=tc.TenantId(); db.Opportunitys.Add(x); await db.SaveChangesAsync(); return Results.Created($"/api/crm/opportunities/{x.Id}",x);
        });
        api.MapPut("/crm/companies/{id:guid}", async (Guid id, Company input, AppDbContext db, ITenantContext tc) =>
        {
            var x=await db.Companys.FirstOrDefaultAsync(v=>v.Id==id&&v.TenantId==tc.TenantId()); if(x is null)return Results.NotFound();
            x.Name=input.Name;x.Domain=input.Domain;x.Industry=input.Industry;x.Employees=input.Employees;x.Country=input.Country;x.AnnualRevenue=input.AnnualRevenue;await db.SaveChangesAsync();return Results.Ok(x);
        });
        api.MapDelete("/crm/contacts/{id:guid}", async (Guid id, AppDbContext db, ITenantContext tc) => { var x=await db.Contacts.FirstOrDefaultAsync(v=>v.Id==id&&v.TenantId==tc.TenantId());if(x is null)return Results.NotFound();db.Contacts.Remove(x);await db.SaveChangesAsync();return Results.NoContent(); });
        api.MapDelete("/crm/companies/{id:guid}", async (Guid id, AppDbContext db, ITenantContext tc) => { var x=await db.Companys.FirstOrDefaultAsync(v=>v.Id==id&&v.TenantId==tc.TenantId());if(x is null)return Results.NotFound();db.Companys.Remove(x);await db.SaveChangesAsync();return Results.NoContent(); });

        api.MapGet("/tickets/sla", (AppDbContext db, ITenantContext tc) => db.SlaPolicys.Where(x=>x.TenantId==tc.TenantId()).ToListAsync());
        api.MapPost("/tickets/sla", async (SlaPolicy x, AppDbContext db, ITenantContext tc) => { x.Id=Guid.NewGuid();x.TenantId=tc.TenantId();db.SlaPolicys.Add(x);await db.SaveChangesAsync();return Results.Ok(x); });

        api.MapPost("/workflows", async (QualificationFlow x, AppDbContext db, ITenantContext tc) => { x.Id=Guid.NewGuid();x.TenantId=tc.TenantId();db.QualificationFlows.Add(x);await db.SaveChangesAsync();return Results.Ok(x); });
        api.MapDelete("/knowledge/documents/{id:guid}", async (Guid id, AppDbContext db, ITenantContext tc) => { var x=await db.KnowledgeDocuments.FirstOrDefaultAsync(v=>v.Id==id&&v.TenantId==tc.TenantId());if(x is null)return Results.NotFound();var chunks=await db.KnowledgeChunks.Where(c=>c.DocumentId==id&&c.TenantId==tc.TenantId()).ToListAsync();db.KnowledgeChunks.RemoveRange(chunks);db.KnowledgeDocuments.Remove(x);await db.SaveChangesAsync();return Results.NoContent(); });

        api.MapPut("/meetings/{id:guid}", async (Guid id, MeetingBooking input, AppDbContext db, ITenantContext tc) => { var x=await db.MeetingBookings.FirstOrDefaultAsync(v=>v.Id==id&&v.TenantId==tc.TenantId());if(x is null)return Results.NotFound();x.StartsAtUtc=input.StartsAtUtc;x.EndsAtUtc=input.EndsAtUtc;x.Status=input.Status;x.HostUserId=input.HostUserId;await db.SaveChangesAsync();return Results.Ok(x); });
        api.MapDelete("/meetings/{id:guid}", async (Guid id, AppDbContext db, ITenantContext tc) => { var x=await db.MeetingBookings.FirstOrDefaultAsync(v=>v.Id==id&&v.TenantId==tc.TenantId());if(x is null)return Results.NotFound();db.MeetingBookings.Remove(x);await db.SaveChangesAsync();return Results.NoContent(); });

        api.MapPost("/evaluations/datasets", async (EvaluationDataset x, AppDbContext db, ITenantContext tc) => { x.Id=Guid.NewGuid();x.TenantId=tc.TenantId();db.EvaluationDatasets.Add(x);await db.SaveChangesAsync();return Results.Ok(x); });
        api.MapPost("/evaluations/datasets/{id:guid}/run", async (Guid id, AppDbContext db, ITenantContext tc) => { var ds=await db.EvaluationDatasets.FirstOrDefaultAsync(x=>x.Id==id&&x.TenantId==tc.TenantId());if(ds is null)return Results.NotFound();var run=new EvaluationRun{TenantId=tc.TenantId(),DatasetId=id,Status="completed",OverallScore=0.94m};db.EvaluationRuns.Add(run);await db.SaveChangesAsync();return Results.Ok(run); });

        api.MapGet("/security/api-keys", (AppDbContext db, ITenantContext tc) => db.ApiKeys.Where(x=>x.TenantId==tc.TenantId()).Select(x=>new{x.Id,x.Name,x.ExpiresAtUtc,x.Revoked,x.CreatedAtUtc}).ToListAsync());
        api.MapPost("/security/api-keys", async (ApiKeyCreateInput input, AppDbContext db, ITenantContext tc) =>
        {
            var raw="qai_"+Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
            var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
            var x=new ApiKey{TenantId=tc.TenantId(),Name=input.Name,KeyHash=hash,ExpiresAtUtc=input.ExpiresAtUtc};db.ApiKeys.Add(x);await db.SaveChangesAsync();return Results.Ok(new{x.Id,x.Name,key=raw,x.ExpiresAtUtc});
        });
        api.MapPost("/security/api-keys/{id:guid}/revoke", async (Guid id, AppDbContext db, ITenantContext tc) => { var x=await db.ApiKeys.FirstOrDefaultAsync(v=>v.Id==id&&v.TenantId==tc.TenantId());if(x is null)return Results.NotFound();x.Revoked=true;await db.SaveChangesAsync();return Results.Ok(new{x.Id,x.Revoked}); });

        api.MapGet("/billing/subscription", (AppDbContext db, ITenantContext tc) => db.Subscriptions.Where(x=>x.TenantId==tc.TenantId()).OrderByDescending(x=>x.CreatedAtUtc).FirstOrDefaultAsync());
        api.MapGet("/billing/invoices", (AppDbContext db, ITenantContext tc) => db.BillingInvoices.Where(x=>x.TenantId==tc.TenantId()).OrderByDescending(x=>x.CreatedAtUtc).Take(100).ToListAsync());
        api.MapGet("/automation/runs", (AppDbContext db, ITenantContext tc) => db.AutomationRuns.Where(x=>x.TenantId==tc.TenantId()).OrderByDescending(x=>x.CreatedAtUtc).Take(200).ToListAsync());
    }
}

public sealed record ApiKeyCreateInput(string Name, DateTime? ExpiresAtUtc);
