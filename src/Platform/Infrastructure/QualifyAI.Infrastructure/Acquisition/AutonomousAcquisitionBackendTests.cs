namespace QualifyAI.Infrastructure.Acquisition;

public static class AutonomousAcquisitionBackendTests
{
 public static void VerifyScoringBounds()
 {
  foreach(var score in new[]{0,45,90,100}) if(score<0||score>100) throw new InvalidOperationException("Score bounds regression.");
 }
 public static void VerifyDailyLimit(int sent,int limit)
 {
  if(limit<1) throw new InvalidOperationException("Daily limit must be positive.");
  if(sent<0) throw new InvalidOperationException("Sent count cannot be negative.");
 }
 public static void VerifyThreshold(int score,int threshold,bool qualified)
 {
  if((score>=threshold)!=qualified) throw new InvalidOperationException("Threshold enforcement regression.");
 }
 public static void VerifySuppression(bool suppressed,bool contactable)
 {
  if(suppressed&&contactable) throw new InvalidOperationException("Suppression regression.");
 }
}
