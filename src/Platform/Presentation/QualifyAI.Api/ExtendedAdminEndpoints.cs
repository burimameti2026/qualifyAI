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

        api.MapGet("/tickets/sla", (AppDbContext db, ITenantContext tc) => db.SlaPolicys.Where(x=>x.TenantId==tc.TenantId()).ToListAsync());
        api.MapPost("/tickets/sla", async (SlaPolicy x, AppDbContext db, ITenantContext tc) => { x.Id=Guid.NewGuid();x.TenantId=tc.TenantId();db.SlaPolicys.Add(x);await db.SaveChangesAsync();return Results.Ok(x); });

        api.MapPost("/workflows", async (QualificationFlow x, AppDbContext db, ITenantContext tc) => { x.Id=Guid.NewGuid();x.TenantId=tc.TenantId();db.QualificationFlows.Add(x);await db.SaveChangesAsync();return Results.Ok(x); });
        api.MapDelete("/knowledge/documents/{id:guid}", async (Guid id, AppDbContext db, ITenantContext tc) => { var x=await db.KnowledgeDocuments.FirstOrDefaultAsync(v=>v.Id==id&&v.TenantId==tc.TenantId());if(x is null)return Results.NotFound();var chunks=await db.KnowledgeChunks.Where(c=>c.DocumentId==id&&c.TenantId==tc.TenantId()).ToListAsync();db.KnowledgeChunks.RemoveRange(chunks);db.KnowledgeDocuments.Remove(x);await db.SaveChangesAsync();return Results.NoContent(); });

        api.MapPut("/meetings/{id:guid}", async (Guid id, MeetingBooking input, AppDbContext db, ITenantContext tc) => { var x=await db.MeetingBookings.FirstOrDefaultAsync(v=>v.Id==id&&v.TenantId==tc.TenantId());if(x is null)return Results.NotFound();x.StartsAtUtc=input.StartsAtUtc;x.EndsAtUtc=input.EndsAtUtc;x.Status=input.Status;x.HostUserId=input.HostUserId;await db.SaveChangesAsync();return Results.Ok(x); });
        api.MapDelete("/meetings/{id:guid}", async (Guid id, AppDbContext db, ITenantContext tc) => { var x=await db.MeetingBookings.FirstOrDefaultAsync(v=>v.Id==id&&v.TenantId==tc.TenantId());if(x is null)return Results.NotFound();db.MeetingBookings.Remove(x);await db.SaveChangesAsync();return Results.NoContent(); });

        api.MapPost("/evaluations/datasets", async (EvaluationDataset x, AppDbContext db, ITenantContext tc) => { x.Id=Guid.NewGuid();x.TenantId=tc.TenantId();db.EvaluationDatasets.Add(x);await db.SaveChangesAsync();return Results.Ok(x); });
        api.MapGet("/evaluations/datasets/{id:guid}/cases", (Guid id, AppDbContext db, ITenantContext tc) => db.EvaluationTestCases.Where(x=>x.TenantId==tc.TenantId()&&x.DatasetId==id).OrderBy(x=>x.CreatedAtUtc).ToListAsync());
        api.MapPost("/evaluations/datasets/{id:guid}/cases", async (Guid id, EvaluationTestCase input, AppDbContext db, ITenantContext tc) => { if(!await db.EvaluationDatasets.AnyAsync(x=>x.Id==id&&x.TenantId==tc.TenantId()))return Results.NotFound();var test=new EvaluationTestCase{Id=Guid.NewGuid(),TenantId=tc.TenantId(),DatasetId=id,Input=input.Input?.Trim()??"",ExpectedAnswer=input.ExpectedAnswer?.Trim()??"",ExpectedTool=input.ExpectedTool?.Trim()??""};if(string.IsNullOrWhiteSpace(test.Input)||string.IsNullOrWhiteSpace(test.ExpectedAnswer))return Results.BadRequest(new{detail="Input and expected outcome are required."});db.EvaluationTestCases.Add(test);await db.SaveChangesAsync();return Results.Ok(test);});
        api.MapDelete("/evaluations/cases/{id:guid}", async (Guid id, AppDbContext db, ITenantContext tc) => {var test=await db.EvaluationTestCases.FirstOrDefaultAsync(x=>x.Id==id&&x.TenantId==tc.TenantId());if(test is null)return Results.NotFound();db.EvaluationTestCases.Remove(test);await db.SaveChangesAsync();return Results.NoContent();});
        api.MapGet("/evaluations/datasets/{id:guid}/runs", (Guid id, AppDbContext db, ITenantContext tc) => db.EvaluationRuns.Where(x=>x.TenantId==tc.TenantId()&&x.DatasetId==id).OrderByDescending(x=>x.CreatedAtUtc).Take(20).ToListAsync());
        api.MapPost("/evaluations/datasets/{id:guid}/run", async (Guid id, EvaluationRunInput input, AppDbContext db, ITenantContext tc, IAiProvider ai, CancellationToken ct) => { var tenantId=tc.TenantId();if(!await db.EvaluationDatasets.AnyAsync(x=>x.Id==id&&x.TenantId==tenantId,ct))return Results.NotFound();var tests=await db.EvaluationTestCases.Where(x=>x.TenantId==tenantId&&x.DatasetId==id).ToListAsync(ct);if(tests.Count==0)return Results.BadRequest(new{detail="Add at least one test case before running an evaluation."});var agent=input.AgentId.HasValue?await db.AiAgents.FirstOrDefaultAsync(x=>x.Id==input.AgentId&&x.TenantId==tenantId&&x.Active,ct):await db.AiAgents.FirstOrDefaultAsync(x=>x.TenantId==tenantId&&x.Active,ct);if(agent is null)return Results.BadRequest(new{detail="Select an active business assistant before running an evaluation."});var run=new EvaluationRun{Id=Guid.NewGuid(),TenantId=tenantId,DatasetId=id,AgentId=agent.Id,Status="running"};db.EvaluationRuns.Add(run);await db.SaveChangesAsync(ct);decimal total=0;foreach(var test in tests){var response=await ai.CompleteAsync(agent.Instructions,test.Input,ct);var accuracy=response.Contains(test.ExpectedAnswer,StringComparison.OrdinalIgnoreCase)?1m:0m;var toolCorrect=string.IsNullOrWhiteSpace(test.ExpectedTool)||agent.Instructions.Contains(test.ExpectedTool,StringComparison.OrdinalIgnoreCase);var score=(accuracy+accuracy+(toolCorrect?1m:0m))/3m;total+=score;db.EvaluationResults.Add(new EvaluationResult{Id=Guid.NewGuid(),TenantId=tenantId,RunId=run.Id,TestCaseId=test.Id,Accuracy=accuracy,Groundedness=accuracy,ToolCorrect=toolCorrect,LatencyMs=0,Cost=0m,Notes=response});}run.OverallScore=total/tests.Count;run.Status="completed";await db.SaveChangesAsync(ct);return Results.Ok(new{run,tests=tests.Count,agent=agent.Name}); });

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
public sealed record EvaluationRunInput(Guid? AgentId);
