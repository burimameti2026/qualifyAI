using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QualifyAI.Domain;
using QualifyAI.Domain.Core.Catalog;
using QualifyAI.Domain.Core.Portal;
using QualifyAI.Domain.Core.Promotions;
using QualifyAI.Infrastructure.Acquisition;
using QualifyAI.Persistence.SqlServer;

namespace QualifyAI.Infrastructure.It;

public sealed class RenovaDemoSeeder(
    AppDbContext db,
    IAutonomousAcquisitionTemplateRegistry templates)
{
    public async Task<bool> SeedAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (await db.CatalogProducts.AnyAsync(x => x.TenantId == tenantId, cancellationToken))
            return false;

        var categories = await EnsureCategoriesAsync(tenantId, cancellationToken);
        var putz = categories["PUTZ"];
        var exterior = categories["EXT"];
        var primer = categories["PRIMER"];

        var products = new[]
        {
            new CatalogProduct
            {
                TenantId = tenantId,
                ProductCategoryId = putz.Id,
                Name = "Renova Interior Putz",
                Code = "REN-INT-001",
                Brand = "Renova",
                ShortDescription = "Demo mineral interior plaster for smooth, durable interior wall finishes.",
                Description = "Sample product for the Renova QualifyAI demonstration. Designed for interior finishing projects and professional applicators.",
                KeyBenefits = "Consistent finish; easy application; professional surface preparation; suitable for renovation and new-build work.",
                Applications = "Interior walls; renovation projects; residential and commercial buildings.",
                TechnicalSpecifications = "Demo specification only. Replace with approved Renova technical data before production use."
            },
            new CatalogProduct
            {
                TenantId = tenantId,
                ProductCategoryId = exterior.Id,
                Name = "Renova Exterior Finish",
                Code = "REN-EXT-001",
                Brand = "Renova",
                ShortDescription = "Demo exterior finishing plaster for durable facade applications.",
                Description = "Sample product for demonstrating market expansion, distributor acquisition and localized campaigns in Balkan markets.",
                KeyBenefits = "Weather-resistant concept; consistent texture; contractor-friendly application; suited to facade finishing.",
                Applications = "Exterior facades; residential construction; commercial and renovation projects.",
                TechnicalSpecifications = "Demo specification only. Replace with approved Renova technical data before production use."
            },
            new CatalogProduct
            {
                TenantId = tenantId,
                ProductCategoryId = primer.Id,
                Name = "Renova Universal Primer",
                Code = "REN-PRI-001",
                Brand = "Renova",
                ShortDescription = "Demo universal primer for improved substrate preparation before finishing systems.",
                Description = "Sample product for the Renova promotion automation demonstration.",
                KeyBenefits = "Improved substrate preparation; compatible workflow with finishing systems; easy professional handling.",
                Applications = "Interior and exterior substrate preparation; plaster and coating systems.",
                TechnicalSpecifications = "Demo specification only. Replace with approved Renova technical data before production use."
            }
        };

        db.CatalogProducts.AddRange(products);

        var variants = products.SelectMany(product => new[]
        {
            new ProductVariant
            {
                TenantId = tenantId,
                CatalogProductId = product.Id,
                Name = "25 kg bag",
                Sku = product.Code + "-25KG",
                Packaging = "Bag",
                NetWeight = 25,
                WeightUnit = "kg",
                Specifications = "Demo variant"
            },
            new ProductVariant
            {
                TenantId = tenantId,
                CatalogProductId = product.Id,
                Name = "5 kg bag",
                Sku = product.Code + "-5KG",
                Packaging = "Bag",
                NetWeight = 5,
                WeightUnit = "kg",
                Specifications = "Demo variant"
            }
        }).ToArray();
        db.ProductVariants.AddRange(variants);

        var languages = new[] { "en", "mk", "sq", "de" };
        foreach (var product in products)
            db.ProductLocalizations.AddRange(languages.Select(language => BuildLocalization(product, language)));

        foreach (var product in products)
        {
            db.PortalPublications.Add(new PortalPublication
            {
                TenantId = tenantId,
                CatalogProductId = product.Id,
                Slug = Slugify(product.Name) + "-demo",
                Status = "Published",
                IsVisible = true,
                Version = 1,
                PublishedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        var primaryProduct = products[1];
        var markets = new[]
        {
            new TargetMarket
            {
                TenantId = tenantId,
                CatalogProductId = primaryProduct.Id,
                CountryCode = "AL",
                CountryName = "Albania",
                DefaultLanguage = "sq",
                TargetIndustries = "Construction materials; building supply; facade systems",
                TargetCustomerTypes = "Distributors; building-material wholesalers; construction companies; professional contractors"
            },
            new TargetMarket
            {
                TenantId = tenantId,
                CatalogProductId = primaryProduct.Id,
                CountryCode = "MK",
                CountryName = "North Macedonia",
                DefaultLanguage = "mk",
                TargetIndustries = "Construction materials; building supply; facade systems",
                TargetCustomerTypes = "Distributors; retailers; construction companies; professional contractors"
            },
            new TargetMarket
            {
                TenantId = tenantId,
                CatalogProductId = primaryProduct.Id,
                CountryCode = "XK",
                CountryName = "Kosovo",
                DefaultLanguage = "sq",
                TargetIndustries = "Construction materials; building supply; facade systems",
                TargetCustomerTypes = "Distributors; wholesalers; construction companies; professional contractors"
            }
        };
        db.TargetMarkets.AddRange(markets);

        await db.SaveChangesAsync(cancellationToken);

        db.ProductPromotionPlans.Add(new ProductPromotionPlan
        {
            TenantId = tenantId,
            CatalogProductId = primaryProduct.Id,
            TargetMarketId = markets[0].Id,
            Name = "Renova Exterior Finish — Albania Distributor Acquisition",
            CampaignLanguage = "sq",
            Status = "Active",
            TargetCustomerProfile = "Established Albanian building-material distributors and wholesalers that already sell facade plasters, renders, primers or related construction systems.",
            QualificationRules = "Business website required; Albania market presence; construction-material distribution capability; active commercial contact; evidence of serving contractors or retailers.",
            MessagingStrategy = "Lead with product quality, application support, technical documentation and distributor opportunity. Offer a technical pack and a short commercial conversation. Do not fabricate price or certification claims.",
            EnableAutonomousProspecting = true,
            EnableAutomaticCampaignEnrollment = true,
            ActivatedAt = DateTimeOffset.UtcNow
        });

        var targetList = new TargetList
        {
            TenantId = tenantId,
            Name = "Renova Balkan Distributor Acquisition Agent — Qualified Prospects",
            Description = "Qualified prospects for the Renova Exterior Finish Balkan distributor acquisition use case.",
            Dynamic = true
        };
        db.TargetLists.Add(targetList);

        var demoProspects = new[]
        {
            new Prospect
            {
                TenantId = tenantId,
                CompanyName = "Balkan Build Supply (Demo)",
                Domain = "balkan-build.test",
                ContactName = "Demo Contact",
                Email = "procurement@balkan-build.test",
                JobTitle = "Procurement Manager",
                Industry = "Building Materials Distribution",
                Country = "Albania",
                Source = "demo",
                Priority = "high",
                ContactReadiness = "demo-only",
                SuggestedBuyer = "Procurement / Category Manager",
                SizeBand = "mid-market",
                PainHypothesis = "Needs differentiated facade products and reliable supplier support.",
                Offer = "Renova Exterior Finish distributor discussion and technical pack",
                SourceUrl = "https://balkan-build.test",
                VerificationStatus = "demo",
                OutreachStatus = "not-ready",
                DatasetOrigin = "renova-demo",
                FitScore = 88,
                IntentScore = 82,
                Status = ProspectStatus.Qualified,
                LastEvaluatedAtUtc = DateTime.UtcNow
            },
            new Prospect
            {
                TenantId = tenantId,
                CompanyName = "Adriatic Trade Materials (Demo)",
                Domain = "adriatic-trade.test",
                ContactName = "Demo Buyer",
                Email = "buying@adriatic-trade.test",
                JobTitle = "Category Buyer",
                Industry = "Construction Supply",
                Country = "Albania",
                Source = "demo",
                Priority = "high",
                ContactReadiness = "demo-only",
                SuggestedBuyer = "Category Buyer",
                SizeBand = "mid-market",
                PainHypothesis = "Expanding facade-system assortment for contractor customers.",
                Offer = "Technical documentation and distributor qualification conversation",
                SourceUrl = "https://adriatic-trade.test",
                VerificationStatus = "demo",
                OutreachStatus = "not-ready",
                DatasetOrigin = "renova-demo",
                FitScore = 84,
                IntentScore = 78,
                Status = ProspectStatus.Qualified,
                LastEvaluatedAtUtc = DateTime.UtcNow
            }
        };
        db.Prospects.AddRange(demoProspects);

        var template = templates.Resolve("construction-materials");
        var agent = new AutonomousAcquisitionAgent
        {
            TenantId = tenantId,
            Name = "Renova Balkan Distributor Acquisition Agent",
            TemplateCode = template.Code,
            Industry = template.Industry,
            Region = "Balkans",
            CountriesJson = JsonSerializer.Serialize(new[] { "AL", "MK", "XK" }),
            IcpJson = JsonSerializer.Serialize(new
            {
                customerTypes = new[] { "building material distributors", "wholesalers", "construction companies", "professional contractors" },
                buyingSignals = new[] { "facade products", "plasters", "renders", "primers", "new warehouse", "new construction projects" },
                requiredEvidence = new[] { "business website", "commercial contact", "market presence" }
            }),
            MinimumScore = 75,
            DailyDiscoveryLimit = 25,
            DailyEmailLimit = 10,
            RunTimeUtc = new TimeOnly(8, 0),
            Status = AutonomousAgentStatus.Active
        };
        db.AutonomousAcquisitionAgents.Add(agent);

        var campaign = new Campaign
        {
            TenantId = tenantId,
            TargetListId = targetList.Id,
            Name = "Renova Exterior Finish — Albania Distributor Outreach",
            Goal = "book-distributor-conversation",
            Status = CampaignStatus.Running,
            SenderName = "Renova Export Team (Demo)",
            SenderEmail = "sales@renova.test",
            StartsAtUtc = DateTime.UtcNow
        };
        db.Campaigns.Add(campaign);

        db.CampaignSteps.AddRange(
            new CampaignStep
            {
                TenantId = tenantId,
                CampaignId = campaign.Id,
                StepNumber = 1,
                DelayHours = 48,
                Channel = "email",
                SubjectTemplate = "Renova Exterior Finish — distributor opportunity for {{company}}",
                BodyTemplate = "Hello {{contact}},\n\nWe are preparing a distributor expansion program for Renova exterior finishing materials in Albania. Based on {{company}}'s position in construction supply, I thought a short conversation could be relevant.\n\nWe can share a technical product pack and discuss distributor requirements, territory coverage and next steps.\n\nWould you be open to a 15-minute introduction?\n\nRenova Export Team (Demo)"
            },
            new CampaignStep
            {
                TenantId = tenantId,
                CampaignId = campaign.Id,
                StepNumber = 2,
                DelayHours = 0,
                Channel = "email",
                SubjectTemplate = "Following up — Renova Exterior Finish",
                BodyTemplate = "Hello {{contact}},\n\nFollowing up on the Renova Exterior Finish distributor conversation for {{company}}. We can send the technical pack first and then align on commercial fit.\n\nPlease reply if this is relevant, or let us know who handles construction-material sourcing.\n\nRenova Export Team (Demo)"
            });

        db.TargetListMembers.AddRange(demoProspects.Select(prospect => new TargetListMember
        {
            TenantId = tenantId,
            TargetListId = targetList.Id,
            ProspectId = prospect.Id
        }));
        db.CampaignRecipients.AddRange(demoProspects.Select(prospect => new CampaignRecipient
        {
            TenantId = tenantId,
            CampaignId = campaign.Id,
            ProspectId = prospect.Id,
            Status = "active",
            CurrentStep = 0,
            NextRunAtUtc = DateTime.UtcNow
        }));

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<Dictionary<string, ProductCategory>> EnsureCategoriesAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var existing = await db.ProductCategories
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .ToDictionaryAsync(x => x.Code ?? string.Empty, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var required = new (string Code, string Name, int SortOrder)[]
        {
            ("PUTZ", "Plasters / Putz", 1),
            ("ADH", "Adhesives", 2),
            ("PRIMER", "Primers", 3),
            ("EXT", "Exterior Solutions", 4)
        };

        foreach (var item in required)
        {
            if (existing.ContainsKey(item.Code)) continue;
            var category = new ProductCategory
            {
                TenantId = tenantId,
                Code = item.Code,
                Name = item.Name,
                SortOrder = item.SortOrder,
                IsActive = true
            };
            db.ProductCategories.Add(category);
            existing[item.Code] = category;
        }

        await db.SaveChangesAsync(cancellationToken);
        return existing;
    }

    private static ProductLocalization BuildLocalization(CatalogProduct product, string language) =>
        language switch
        {
            "mk" => new ProductLocalization
            {
                TenantId = product.TenantId,
                CatalogProductId = product.Id,
                Language = "mk",
                Name = product.Name.Replace("Renova", "Ренова"),
                ShortDescription = "Демо производ за професионални градежни и фасадни примени.",
                Description = "Демо локализација. Заменете ја со одобрен маркетинг текст на Ренова пред продукциско објавување.",
                KeyBenefits = "Професионална примена; конзистентна завршница; поддршка за градежни изведувачи.",
                Applications = product.Applications
            },
            "sq" => new ProductLocalization
            {
                TenantId = product.TenantId,
                CatalogProductId = product.Id,
                Language = "sq",
                Name = product.Name.Replace("Renova", "Renova"),
                ShortDescription = "Produkt demonstrues për aplikime profesionale në ndërtim dhe fasada.",
                Description = "Lokalizim demonstrues. Zëvendësojeni me tekst marketingu të miratuar nga Renova para përdorimit në prodhim.",
                KeyBenefits = "Aplikim profesional; përfundim i njëtrajtshëm; i përshtatshëm për kontraktorë dhe distributorë.",
                Applications = product.Applications
            },
            "de" => new ProductLocalization
            {
                TenantId = product.TenantId,
                CatalogProductId = product.Id,
                Language = "de",
                Name = product.Name,
                ShortDescription = "Demoprodukt für professionelle Bau- und Fassadenanwendungen.",
                Description = "Demo-Lokalisierung. Vor dem produktiven Einsatz durch freigegebene Renova-Marketinginhalte ersetzen.",
                KeyBenefits = "Professionelle Anwendung; gleichmäßige Oberfläche; geeignet für Bauunternehmen und Händler.",
                Applications = product.Applications
            },
            _ => new ProductLocalization
            {
                TenantId = product.TenantId,
                CatalogProductId = product.Id,
                Language = "en",
                Name = product.Name,
                ShortDescription = product.ShortDescription,
                Description = product.Description,
                KeyBenefits = product.KeyBenefits,
                Applications = product.Applications
            }
        };

    private static string Slugify(string value) =>
        new string(value.Trim().ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray())
            .Trim('-')
            .Replace("--", "-");
}
