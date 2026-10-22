namespace PublicServiceCaseManagement.Api.Features.Invitations;

public interface IInvitationService
{
    Task<InvitationResponse> CreateAsync(
        Guid organizationId,
        Guid createdByUserId,
        CreateInvitationRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InvitationResponse>> GetAllAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default);

    Task<InvitationResponse?> ResendAsync(
        Guid organizationId,
        Guid invitationId,
        CancellationToken cancellationToken = default);

    Task<bool> RevokeAsync(
        Guid organizationId,
        Guid invitationId,
        CancellationToken cancellationToken = default);

    Task<InvitationDetailsResponse?> GetDetailsAsync(
        string token,
        CancellationToken cancellationToken = default);

    Task<AcceptInvitationOutcome> AcceptAsync(
        AcceptInvitationRequest request,
        CancellationToken cancellationToken = default);
}
