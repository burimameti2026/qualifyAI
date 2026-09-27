namespace QualifyAI.BuildingBlocks.Security.Tenancy;
public interface ICurrentTenant
{
    Guid Id { get; }
    string Slug { get; }
    bool IsResolved { get; }
}
