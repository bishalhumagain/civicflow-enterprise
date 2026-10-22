namespace PublicServiceCaseManagement.Api.Domain.Errors;

public sealed class ApplicationError
{
    public Guid Id { get; set; }
    public string Message { get; set; } = string.Empty;
    public string StackTrace { get; set; } = string.Empty;
    public string? Source { get; set; }
    public string? MethodName { get; set; }
    public string? RequestPath { get; set; }
    public string? RequestMethod { get; set; }
    public string? UserEmail { get; set; }
    public Guid? UserId { get; set; }
    public Guid? OrganizationId { get; set; }
    public string? AdditionalData { get; set; }
    public bool IsHandled { get; set; }
    public DateTime OccurredAt { get; set; }
}
