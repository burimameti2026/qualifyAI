using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QualifyAI.Domain;
using QualifyAI.Domain.Core.Portal;
using QualifyAI.Persistence.SqlServer;

namespace QualifyAI.Infrastructure.It;

public sealed class RenovaSiteContentSeeder(AppDbContext db)
{
    private const string SettingKey = "renova.portal.content.v1";

    public async Task SeedAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var existing = await db.TenantSettings.SingleOrDefaultAsync(
            x => x.TenantId == tenantId && x.Key == SettingKey,
            cancellationToken);

        if (existing is not null && !string.IsNullOrWhiteSpace(existing.Value))
            return;

        var content = BuildDefaultContent();
        var json = JsonSerializer.Serialize(content);

        if (existing is null)
        {
            db.TenantSettings.Add(new TenantSetting
            {
                TenantId = tenantId,
                Key = SettingKey,
                Value = json
            });
        }
        else
        {
            existing.Value = json;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public static RenovaSiteContentDocument BuildDefaultContent() => new()
    {
        Version = 1,
        CompanyIntro = "RENOVA began in 1992 and developed from a small trading and service company into a regional producer serving professional construction markets.",
        Solutions =
        [
            new("01", "Facade & insulation", "Durable exterior surfaces, insulation and professional facade work."),
            new("02", "Plasters & mortars", "Dry construction materials for interior and exterior preparation and finishing."),
            new("03", "Adhesives & primers", "Preparation, bonding and reinforcement solutions for demanding projects."),
            new("04", "Liquid solutions", "Decorative coatings, primers and finishing products for complete systems.")
        ],
        Kpis =
        [
            new("30+", "Years of development", "Renova began its journey in 1992."),
            new("5", "Factories", "Renova presents five factories in its company facilities overview."),
            new("6", "Production plants", "The Renova product catalog describes six production plants."),
            new("100+", "Distribution centers", "The Renova catalog describes a regional distribution network.")
        ],
        Stories =
        [
            new("REMALL", "Shopping & Apartments", "Tetovo", "https://renova.com.mk/en/", "https://renova.com.mk/wp-content/uploads/2024/07/367005464_687768376727744_8432136002171504273_n-2.jpg"),
            new("HOTEL MERCURE", "Hospitality project", "Tetovo", "https://renova.com.mk/en/", "https://renova.com.mk/wp-content/uploads/2024/06/a356_ho_00_p_2048x1536.jpg"),
            new("RENOVA FACILITIES", "Production & industrial facilities", "North Macedonia / Balkans", "https://renova.com.mk/en/facilities-en/", "https://renova.com.mk/wp-content/uploads/2024/07/OBJEKTET-ENArtboard-1.jpg")
        ],
        Events =
        [
            new("Presentation at DENA KOMPANI", "Renova product presentation at a new partner.", "https://renova.com.mk/"),
            new("Presentation at EUROFIX – Kicevo", "A Renova presentation for a regional partner.", "https://renova.com.mk/"),
            new("Presentation at HAMI-STAM", "A further Renova product and partner presentation.", "https://renova.com.mk/")
        ],
        Locations =
        [
            new("RENOVA – Đepčište", "Factory / headquarters", "Tetovo, North Macedonia", "https://www.google.com/maps/search/?api=1&query=Renova+Dzepciste+Tetovo"),
            new("RENOVA – Uroševac", "Facility", "Ferizaj, Kosovo", "https://www.google.com/maps/search/?api=1&query=Renova+Ferizaj+Kosovo"),
            new("RENOVA – Tirana", "Facility / project", "Tirana, Albania", "https://www.google.com/maps/search/?api=1&query=Renova+Tirana+Albania"),
            new("RENOSIL – Bitola", "Facility", "Bitola, North Macedonia", "https://www.google.com/maps/search/?api=1&query=Renosil+Bitola"),
            new("RENOVELUR – Tetovo", "Company facility", "Tetovo, North Macedonia", "https://www.google.com/maps/search/?api=1&query=Renovelur+Tetovo"),
            new("REMALL – Tetovo", "Shopping & apartments", "Tetovo, North Macedonia", "https://www.google.com/maps/search/?api=1&query=REMALL+Tetovo")
        ]
    };
}
