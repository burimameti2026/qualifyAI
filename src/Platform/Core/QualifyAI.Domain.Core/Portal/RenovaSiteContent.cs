namespace QualifyAI.Domain.Core.Portal;

public sealed class RenovaSiteContentDocument
{
    public int Version { get; set; } = 1;
    public string CompanyIntro { get; set; } = string.Empty;
    public List<RenovaSiteSolution> Solutions { get; set; } = [];
    public List<RenovaSiteKpi> Kpis { get; set; } = [];
    public List<RenovaSiteStory> Stories { get; set; } = [];
    public List<RenovaSiteEvent> Events { get; set; } = [];
    public List<RenovaSiteLocation> Locations { get; set; } = [];
}

public sealed record RenovaSiteSolution(string Number, string Title, string Description);
public sealed record RenovaSiteKpi(string Value, string Title, string Description);
public sealed record RenovaSiteStory(string Title, string Description, string Location, string Url, string ImageUrl);
public sealed record RenovaSiteEvent(string Title, string Description, string Url);
public sealed record RenovaSiteLocation(string Name, string Type, string Address, string Url);
