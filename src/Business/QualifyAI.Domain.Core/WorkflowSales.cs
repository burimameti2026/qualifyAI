namespace QualifyAI.Domain;
public class QualificationFlow : TenantEntity { public string Name { get; set; }="Default Qualification"; public bool Active { get; set; }=true; }
public class WorkflowNode : TenantEntity { public Guid FlowId { get; set; } public string NodeKey { get; set; }=""; public string Type { get; set; }="question"; public string ConfigJson { get; set; }="{}"; public int X { get; set; } public int Y { get; set; } }
public class WorkflowEdge : TenantEntity { public Guid FlowId { get; set; } public string FromNodeKey { get; set; }=""; public string ToNodeKey { get; set; }=""; public string ConditionJson { get; set; }="{}"; }
public class QualificationAnswer : TenantEntity { public Guid LeadId { get; set; } public string Key { get; set; }=""; public string Value { get; set; }=""; public int ScoreDelta { get; set; } }
public class ScoringRule : TenantEntity { public string Name { get; set; }=""; public string Field { get; set; }=""; public string Operator { get; set; }="equals"; public string Value { get; set; }=""; public int Points { get; set; } }
public class Pipeline : TenantEntity
{
    public string Name { get; set; }="Sales";
    public bool IsDefault { get; set; }=true;
    public void Rename(string name) { if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Pipeline name is required."); Name=name.Trim(); Touch(); }
}
public class PipelineStage : TenantEntity
{
    public Guid PipelineId { get; set; }
    public string Name { get; set; }="New";
    public int SortOrder { get; set; }
    public decimal Probability { get; set; }
    public void Configure(string name, int sortOrder, decimal probability)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Stage name is required.");
        if (sortOrder < 0) throw new InvalidOperationException("Sort order cannot be negative.");
        if (probability is < 0 or > 100) throw new InvalidOperationException("Probability must be between 0 and 100.");
        Name=name.Trim(); SortOrder=sortOrder; Probability=probability; Touch();
    }
}
public class IcpProfile : TenantEntity { public string Name { get; set; }="Ideal Customer"; public string CriteriaJson { get; set; }="{}"; }
public class LeadScoreExplanation : TenantEntity { public Guid LeadId { get; set; } public string Factor { get; set; }=""; public int Points { get; set; } public string Reason { get; set; }=""; }
