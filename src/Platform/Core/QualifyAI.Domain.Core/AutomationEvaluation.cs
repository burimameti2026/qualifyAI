namespace QualifyAI.Domain;
public class EvaluationDataset : TenantEntity { public string Name { get; set; }=""; public string Description { get; set; }=""; }
public class EvaluationTestCase : TenantEntity { public Guid DatasetId { get; set; } public string Input { get; set; }=""; public string ExpectedAnswer { get; set; }=""; public string ExpectedTool { get; set; }=""; }
public class EvaluationRun : TenantEntity { public Guid DatasetId { get; set; } public Guid? AgentId { get; set; } public string Status { get; set; }="pending"; public decimal OverallScore { get; set; } }
public class EvaluationResult : TenantEntity { public Guid RunId { get; set; } public Guid TestCaseId { get; set; } public decimal Accuracy { get; set; } public decimal Groundedness { get; set; } public bool ToolCorrect { get; set; } public long LatencyMs { get; set; } public decimal Cost { get; set; } public string Notes { get; set; }=""; }
