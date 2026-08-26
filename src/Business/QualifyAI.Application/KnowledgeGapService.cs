namespace QualifyAI.Application;
public sealed class KnowledgeGapService { public bool ShouldCreateGap(int retrievedCount,double topScore,bool humanCorrected,bool negativeCsat)=>retrievedCount==0||topScore<0.25||humanCorrected||negativeCsat; }
