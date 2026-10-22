namespace PublicServiceCaseManagement.Api.Domain.Dashboard;

public sealed class CaseStatistics
{
    public int TotalCases { get; set; }
    public int SubmittedCases { get; set; }
    public int UnderReviewCases { get; set; }
    public int AssignedCases { get; set; }
    public int InProgressCases { get; set; }
    public int AwaitingInformationCases { get; set; }
    public int ResolvedCases { get; set; }
    public int ClosedCases { get; set; }
    public int RejectedCases { get; set; }
    public int HighPriorityCases { get; set; }
    public int NormalPriorityCases { get; set; }
    public int LowPriorityCases { get; set; }
    public int CasesCreatedThisWeek { get; set; }
    public int CasesCreatedThisMonth { get; set; }
    public int CasesResolvedThisWeek { get; set; }
    public int CasesResolvedThisMonth { get; set; }
}
