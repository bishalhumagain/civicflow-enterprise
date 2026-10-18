namespace PublicServiceCaseManagement.Api.Infrastructure.Organizations;

public interface ICurrentOrganizationContext
{
    Guid OrganizationId { get; }
}