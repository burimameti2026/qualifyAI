using QualifyAI.BuildingBlocks.Domain.Abstractions;

namespace QualifyAI.Identity.Domain.Licensing;

public sealed class LicenseModule : Entity
{
    private LicenseModule() { }

    private LicenseModule(Guid licenseId, string code)
    {
        LicenseId = licenseId;
        Code = string.IsNullOrWhiteSpace(code)
            ? throw new ArgumentException("Module code is required.", nameof(code))
            : code.Trim();
        Enabled = true;
    }

    public Guid LicenseId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public bool Enabled { get; private set; }

    internal static LicenseModule Create(Guid licenseId, string code) => new(licenseId, code);

    public void Enable() => Enabled = true;
    public void Disable() => Enabled = false;
}
