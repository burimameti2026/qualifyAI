using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QualifyAI.Domain;
using QualifyAI.Persistence.SqlServer;

namespace QualifyAI.Infrastructure.Demo;

public sealed record ScenarioInstallResult(string Scenario, int Prospects, int Campaigns, int Opportunities, int Meetings, int Tickets, int Automations);

public sealed class RealisticScenarioService(AppDbContext db)
{
    public async Task<ScenarioInstallResult> InstallAsync(Guid tenantId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var icp = await db.IcpProfiles.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Name == "DACH & Southern Europe logistics growth", ct);
        if (icp is null)
        {
            icp = new IcpProfile
            {
                TenantId = tenantId, Name = "DACH & Southern Europe logistics growth",
                Industry = "Logistics, Distribution, Manufacturing, E-commerce",
                CountriesCsv = "Germany, France, Italy", MinimumEmployees = 20, MaximumEmployees = 1000,
                IntentKeywordsCsv = "freight tender, warehouse expansion, fleet growth, delivery delays",
                CriteriaJson = "{\"requiredSignals\":1,\"minimumFitScore\":70}", Active = true, LastDiscoveryAtUtc = now
            };
            db.IcpProfiles.Add(icp);
        }

        var prospectSeeds = new[]
        {
            new ProspectSeed("NordCargo Solutions", "nordcargo.example", "Lukas Meyer", "Head of Operations", "lukas.meyer@nordcargo.example", "Logistics", "Germany", 92, 81, "Published a freight technology tender after expanding its cross-border fleet."),
            new ProspectSeed("RheinFulfil GmbH", "rheinfulfil.example", "Anna Fischer", "VP Supply Chain", "anna.fischer@rheinfulfil.example", "E-commerce", "Germany", 89, 74, "Hiring warehouse systems specialists for a new fulfilment location."),
            new ProspectSeed("Atlas Components", "atlascomponents.example", "Sofia Romano", "Logistics Director", "sofia.romano@atlascomponents.example", "Manufacturing", "Italy", 91, 69, "Announced distribution expansion into two additional regions."),
            new ProspectSeed("BlueLine 3PL", "blueline3pl.example", "Marc Dubois", "Managing Director", "marc.dubois@blueline3pl.example", "Logistics", "France", 88, 84, "Customer reviews indicate recurring shipment visibility and delivery-delay issues."),
            new ProspectSeed("Milano Distribution", "milanodistribution.example", "Giulia Conti", "Warehouse Director", "giulia.conti@milanodistribution.example", "Distribution", "Italy", 86, 77, "Opened a second warehouse and increased transport coordinator hiring.")
        };

        var prospects = new List<Prospect>();
        foreach (var seed in prospectSeeds)
        {
            var prospect = await db.Prospects.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Domain == seed.Domain, ct);
            if (prospect is null)
            {
                prospect = new Prospect
                {
                    TenantId = tenantId, CompanyName = seed.Company, Domain = seed.Domain,
                    ContactName = seed.Contact, JobTitle = seed.JobTitle, Email = seed.Email,
                    Industry = seed.Industry, Country = seed.Country, Source = "realistic-demo-scenario"
                };
                prospect.Evaluate(seed.Fit, seed.Intent);
                db.Prospects.Add(prospect);
            }
            prospects.Add(prospect);
            if (!await db.ProspectSignals.AnyAsync(x => x.TenantId == tenantId && x.ProspectId == prospect.Id && x.Evidence == seed.Evidence, ct))
                db.ProspectSignals.Add(new ProspectSignal { TenantId = tenantId, ProspectId = prospect.Id, Type = "public-buying-intent", Source = "scenario-market-feed", Evidence = seed.Evidence, Score = seed.Intent, SourceUrl = $"https://signals.example/{seed.Domain}" });
        }

        var targetList = await db.TargetLists.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Name == "European logistics accounts – high intent", ct);
        if (targetList is null)
        {
            targetList = new TargetList { TenantId = tenantId, IcpProfileId = icp.Id, Name = "European logistics accounts – high intent", Description = "Companies matching the logistics ICP with current buying signals." };
            db.TargetLists.Add(targetList);
        }
        foreach (var prospect in prospects)
            if (!await db.TargetListMembers.AnyAsync(x => x.TenantId == tenantId && x.TargetListId == targetList.Id && x.ProspectId == prospect.Id, ct))
                db.TargetListMembers.Add(new TargetListMember { TenantId = tenantId, TargetListId = targetList.Id, ProspectId = prospect.Id });

        var campaign = await db.Campaigns.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Name == "Logistics growth – book operational demo", ct);
        if (campaign is null)
        {
            campaign = new Campaign { TenantId = tenantId, TargetListId = targetList.Id, Name = "Logistics growth – book operational demo", Goal = "book-demo", Status = CampaignStatus.Running, SenderName = "Burim – Solutions", SenderEmail = "sales@qualifyai.demo", StartsAtUtc = now.AddDays(-5) };
            db.Campaigns.Add(campaign);
            db.CampaignSteps.AddRange(
                new CampaignStep { TenantId = tenantId, CampaignId = campaign.Id, StepNumber = 1, DelayHours = 0, Channel = "email", SubjectTemplate = "{{company}}: reduce dispatch and delivery exceptions", BodyTemplate = "Hi {{contact}}, I noticed current growth signals at {{company}}. We help {{industry}} teams automate dispatch, warehouse and customer operations. Would a 25-minute operational demo be useful?" },
                new CampaignStep { TenantId = tenantId, CampaignId = campaign.Id, StepNumber = 2, DelayHours = 72, Channel = "email", SubjectTemplate = "Operational benchmark for {{company}}", BodyTemplate = "Hi {{contact}}, I prepared a short benchmark for teams operating across {{country}}. I can tailor the demo to your fleet, warehouse and delivery workflow." });
        }
        foreach (var prospect in prospects)
            if (!await db.CampaignRecipients.AnyAsync(x => x.TenantId == tenantId && x.CampaignId == campaign.Id && x.ProspectId == prospect.Id, ct))
                db.CampaignRecipients.Add(new CampaignRecipient { TenantId = tenantId, CampaignId = campaign.Id, ProspectId = prospect.Id, Status = "active", NextRunAtUtc = now });

        var firstStep = db.CampaignSteps.Local.FirstOrDefault(x => x.CampaignId == campaign.Id && x.StepNumber == 1)
            ?? await db.CampaignSteps.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.CampaignId == campaign.Id && x.StepNumber == 1, ct);
        if (firstStep is not null)
            foreach (var prospect in prospects)
                if (!await db.OutreachMessages.AnyAsync(x => x.TenantId == tenantId && x.CampaignId == campaign.Id && x.ProspectId == prospect.Id && x.CampaignStepId == firstStep.Id, ct))
                    db.OutreachMessages.Add(new OutreachMessage
                    {
                        TenantId = tenantId, CampaignId = campaign.Id, ProspectId = prospect.Id, CampaignStepId = firstStep.Id,
                        Channel = "email", Subject = $"{prospect.CompanyName}: reduce dispatch and delivery exceptions",
                        Body = $"Hi {prospect.ContactName}, I noticed current growth signals at {prospect.CompanyName}. Would a focused operational demo be useful?",
                        Status = prospect == prospects[0] ? OutreachStatus.Delivered : OutreachStatus.Queued,
                        ProviderMessageId = prospect == prospects[0] ? "demo-msg-nordcargo" : "",
                        SentAtUtc = prospect == prospects[0] ? now.AddDays(-2) : null
                    });

        await EnsureConvertedJourneyAsync(tenantId, prospects[0], campaign, now, ct);
        await EnsureSupportScenarioAsync(tenantId, now, ct);
        await EnsureAutomationsAsync(tenantId, now, ct);
        await db.SaveChangesAsync(ct);

        return new ScenarioInstallResult(
            "Find Customers & Book Demos + Contract-aware Customer Support",
            await db.Prospects.CountAsync(x => x.TenantId == tenantId, ct),
            await db.Campaigns.CountAsync(x => x.TenantId == tenantId, ct),
            await db.Opportunitys.CountAsync(x => x.TenantId == tenantId, ct),
            await db.MeetingBookings.CountAsync(x => x.TenantId == tenantId, ct),
            await db.Tickets.CountAsync(x => x.TenantId == tenantId, ct),
            await db.AutomationRules.CountAsync(x => x.TenantId == tenantId, ct));
    }

    private async Task EnsureConvertedJourneyAsync(Guid tenantId, Prospect prospect, Campaign campaign, DateTime now, CancellationToken ct)
    {
        var company = await db.Companys.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Domain == prospect.Domain, ct);
        if (company is null) { company = Company.Create(tenantId, prospect.CompanyName, prospect.Domain, prospect.Industry, 86, prospect.Country, 18_400_000m); db.Companys.Add(company); }
        var contact = await db.Contacts.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Email == prospect.Email, ct);
        if (contact is null) { var names = prospect.ContactName.Split(' ', 2); contact = Contact.Create(tenantId, company.Id, names[0], names.Length > 1 ? names[1] : "", prospect.Email, "+49 30 555 0142", "qualified"); db.Contacts.Add(contact); }
        var lead = await db.Leads.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.ContactId == contact.Id, ct);
        if (lead is null) { lead = Lead.Create(tenantId, contact.Id, company.Id, "outbound-campaign", 94, 48_000m, "Fleet expansion and freight technology tender; interested reply received."); lead.Qualify(); db.Leads.Add(lead); }
        var opportunity = await db.Opportunitys.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.LeadId == lead.Id, ct);
        if (opportunity is null) { opportunity = new Opportunity { TenantId = tenantId, LeadId = lead.Id, CompanyId = company.Id, ContactId = contact.Id, Name = "NordCargo operational platform rollout", Amount = 48_000m, Status = OpportunityStatus.Open, ExpectedCloseUtc = now.AddDays(28) }; db.Opportunitys.Add(opportunity); }
        if (!await db.RevenueAttributions.AnyAsync(x => x.TenantId == tenantId && x.OpportunityId == opportunity.Id, ct))
            db.RevenueAttributions.Add(new RevenueAttribution { TenantId = tenantId, LeadId = lead.Id, OpportunityId = opportunity.Id, InfluencedRevenue = opportunity.Amount, Model = "automation-assisted" });
        var meetingType = await db.MeetingTypes.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Name == "Operational discovery demo", ct);
        if (meetingType is null) { meetingType = new MeetingType { TenantId = tenantId, Name = "Operational discovery demo", DurationMinutes = 30, LocationType = "video" }; db.MeetingTypes.Add(meetingType); }
        if (!await db.MeetingBookings.AnyAsync(x => x.TenantId == tenantId && x.LeadId == lead.Id, ct))
            db.MeetingBookings.Add(new MeetingBooking { TenantId = tenantId, MeetingTypeId = meetingType.Id, ContactId = contact.Id, LeadId = lead.Id, StartsAtUtc = now.AddDays(3).Date.AddHours(10), EndsAtUtc = now.AddDays(3).Date.AddHours(10.5), Status = "booked", ExternalEventId = "demo-calendar-event" });
        if (!await db.ProspectReplies.AnyAsync(x => x.TenantId == tenantId && x.CampaignId == campaign.Id && x.ProspectId == prospect.Id, ct))
        {
            var recipient = db.CampaignRecipients.Local.FirstOrDefault(x => x.TenantId == tenantId && x.CampaignId == campaign.Id && x.ProspectId == prospect.Id)
                ?? await db.CampaignRecipients.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.CampaignId == campaign.Id && x.ProspectId == prospect.Id, ct);
            if (recipient is not null) { recipient.Status = "replied"; recipient.RepliedAtUtc = now.AddDays(-1); recipient.NextRunAtUtc = null; }
            db.ProspectReplies.Add(new ProspectReply { TenantId = tenantId, CampaignId = campaign.Id, ProspectId = prospect.Id, Body = "Yes, visibility across dispatch and delivery exceptions is a priority. Tuesday morning works for a demo.", Classification = "interested", SentimentScore = 91, RequiresHuman = true, ReceivedAtUtc = now.AddDays(-1) });
            prospect.Status = ProspectStatus.DemoReady;
        }
    }

    private async Task EnsureSupportScenarioAsync(Guid tenantId, DateTime now, CancellationToken ct)
    {
        if (!await db.Tickets.AnyAsync(x => x.TenantId == tenantId && x.Number == "T-DEMO-DELIVERY", ct))
            db.Tickets.Add(new Ticket { TenantId = tenantId, Number = "T-DEMO-DELIVERY", Subject = "Cross-border delivery delayed", Description = "Customer contract includes priority delivery support. Shipment has not updated for 18 hours.", Status = TicketStatus.Open, Priority = TicketPriority.High, FirstResponseDueUtc = now.AddMinutes(25), ResolutionDueUtc = now.AddHours(4) });
        if (!await db.Tickets.AnyAsync(x => x.TenantId == tenantId && x.Number == "T-DEMO-PAYMENT", ct))
            db.Tickets.Add(new Ticket { TenantId = tenantId, Number = "T-DEMO-PAYMENT", Subject = "Duplicate payment transaction", Description = "Customer reports two captures for the same invoice. Refund requires finance approval.", Status = TicketStatus.Pending, Priority = TicketPriority.Urgent, FirstResponseDueUtc = now.AddMinutes(10), ResolutionDueUtc = now.AddHours(2) });
        if (!await db.KnowledgeGaps.AnyAsync(x => x.TenantId == tenantId && x.Topic == "Cross-border delivery exception policy", ct))
            db.KnowledgeGaps.Add(new KnowledgeGap { TenantId = tenantId, Topic = "Cross-border delivery exception policy", Occurrences = 18, ExampleQuestion = "When should a delayed international shipment be escalated?", ImpactScore = 86, Status = "open" });
    }

    private async Task EnsureAutomationsAsync(Guid tenantId, DateTime now, CancellationToken ct)
    {
        var flow = await db.QualificationFlows.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Name == "Find Customers & Book Demos", ct);
        if (flow is null)
        {
            flow = new QualificationFlow { TenantId = tenantId, Name = "Find Customers & Book Demos", Active = true };
            db.QualificationFlows.Add(flow);
            db.WorkflowNodes.AddRange(
                WorkflowNode.Create(tenantId, flow.Id, "schedule", "start", "{\"trigger\":\"weekday 08:00\"}", 60, 160),
                WorkflowNode.Create(tenantId, flow.Id, "discover", "discoverProspects", "{\"source\":\"configured-provider\"}", 300, 160),
                WorkflowNode.Create(tenantId, flow.Id, "score", "score", "{\"points\":75}", 540, 160),
                WorkflowNode.Create(tenantId, flow.Id, "campaign", "email", "{\"template\":\"logistics-growth\"}", 780, 160),
                WorkflowNode.Create(tenantId, flow.Id, "reply", "bookMeeting", "{\"classification\":\"interested\"}", 1020, 160));
            db.WorkflowEdges.AddRange(
                WorkflowEdge.Create(tenantId, flow.Id, "schedule", "discover", "{}"),
                WorkflowEdge.Create(tenantId, flow.Id, "discover", "score", "{}"),
                WorkflowEdge.Create(tenantId, flow.Id, "score", "campaign", "{\"minimumScore\":75}"),
                WorkflowEdge.Create(tenantId, flow.Id, "campaign", "reply", "{\"reply\":\"interested\"}"));
        }
        var definitions = new[]
        {
            new { Name = "ICP discovery → qualified target list", Trigger = "schedule.weekday", Conditions = "[{\"field\":\"icp.active\",\"operator\":\"equals\",\"value\":true}]", Actions = "[{\"type\":\"discoverProspects\"},{\"type\":\"enrichProspects\"},{\"type\":\"scoreProspects\"},{\"type\":\"createTargetList\"}]" },
            new { Name = "Interested reply → demo and opportunity", Trigger = "campaign.reply.interested", Conditions = "[{\"field\":\"sentimentScore\",\"operator\":\">=\",\"value\":70}]", Actions = "[{\"type\":\"createOpportunity\"},{\"type\":\"bookMeeting\"},{\"type\":\"notifySales\"}]" },
            new { Name = "Payment dispute → finance approval", Trigger = "ticket.payment.dispute", Conditions = "[{\"field\":\"priority\",\"operator\":\"equals\",\"value\":\"Urgent\"}]", Actions = "[{\"type\":\"requestApproval\",\"title\":\"Approve duplicate-payment refund\",\"dueInHours\":2},{\"type\":\"notify\"}]" }
        };
        foreach (var definition in definitions)
        {
            var rule = await db.AutomationRules.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Name == definition.Name, ct);
            if (rule is null)
            {
                rule = AutomationRule.Create(tenantId, definition.Name, definition.Trigger, definition.Conditions, definition.Actions, true);
                db.AutomationRules.Add(rule);
                var run = AutomationRun.Create(tenantId, rule.Id, "{\"source\":\"scenario-installer\"}"); run.Start();
                run.Complete(JsonSerializer.Serialize(new[] { new { status = "completed", message = "Scenario workflow persisted and validated.", atUtc = now } }));
                db.AutomationRuns.Add(run);
            }
        }
        if (!await db.UsageRecords.AnyAsync(x => x.TenantId == tenantId && x.Meter == "automation_actions" && x.ReferenceId == "realistic-scenario", ct))
            db.UsageRecords.Add(new UsageRecord { TenantId = tenantId, Meter = "automation_actions", Quantity = 12, ReferenceId = "realistic-scenario", RecordedAtUtc = now });
    }

    private sealed record ProspectSeed(string Company, string Domain, string Contact, string JobTitle, string Email, string Industry, string Country, int Fit, int Intent, string Evidence);
}
