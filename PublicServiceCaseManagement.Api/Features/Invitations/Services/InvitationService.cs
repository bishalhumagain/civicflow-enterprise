using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PublicServiceCaseManagement.Api.Domain.Audit;
using PublicServiceCaseManagement.Api.Domain.Organizations;
using PublicServiceCaseManagement.Api.Domain.Staff;
using PublicServiceCaseManagement.Api.Domain.Users;
using PublicServiceCaseManagement.Api.Infrastructure.Audit;
using PublicServiceCaseManagement.Api.Infrastructure.Identity;
using PublicServiceCaseManagement.Api.Infrastructure.Organizations;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;
using PublicServiceCaseManagement.Api.Features.Invitations.Contracts;

using PublicServiceCaseManagement.Api.Domain.Common;

namespace PublicServiceCaseManagement.Api.Features.Invitations.Services;

/// <summary>
/// Creates and redeems single-use staff invitations. The invitee always
/// chooses their own password through the emailed link; no credential is ever
/// generated, emailed, or stored in plaintext.
/// </summary>
public sealed class InvitationService(
    CivicFlowDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IEmailSender emailSender,
    IAuditLogger auditLogger,
    IConfiguration configuration,
    TimeProvider timeProvider,
    IOrganizationTransactionContext organizationTransactionContext) : IInvitationService
{
    private const int DefaultExpiryHours = 72;
    private const int MinimumExpiryHours = 1;
    private const int MaximumExpiryHours = 720;

    private static readonly string[] AllowedRoles =
    [
        nameof(OrganizationRole.Viewer),
        nameof(OrganizationRole.CaseWorker),
        nameof(OrganizationRole.OrganizationAdmin)
    ];

    public async Task<InvitationResponse> CreateAsync(
        Guid organizationId,
        Guid createdByUserId,
        CreateInvitationRequest request,
        CancellationToken cancellationToken = default)
    {
        var role = ParseRole(request.Role);
        var email = NormalizeEmail(request.Email);
        var fullName = TextNormalization.NormalizeName(
            request.FullName);

        var organization = await dbContext.Organizations
            .SingleOrDefaultAsync(
                candidate => candidate.Id == organizationId,
                cancellationToken);

        if (organization is null || !organization.IsActive)
        {
            throw new KeyNotFoundException("The organization does not exist.");
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new ArgumentException(
                "Full name is required.",
                nameof(request.FullName));
        }

        var existingUser = await userManager.FindByEmailAsync(email);

        if (existingUser is not null &&
            await IsActiveMemberAsync(
                existingUser.Id,
                organizationId,
                cancellationToken))
        {
            throw new InvalidOperationException(
                "This person is already a member of the organization.");
        }

        var now = timeProvider.GetUtcNow();

        var hasPendingInvitation = await dbContext.AccountInvitations
            .AnyAsync(
                invitation =>
                    invitation.OrganizationId == organizationId &&
                    invitation.NormalizedEmail == email.ToUpperInvariant() &&
                    invitation.UsedAt == null &&
                    invitation.RevokedAt == null &&
                    invitation.ExpiresAt > now,
                cancellationToken);

        if (hasPendingInvitation)
        {
            throw new InvalidOperationException(
                "An invitation is already pending for this email address.");
        }

        var token = InvitationToken.Create();
        var invitation = AccountInvitation.Create(
            organizationId,
            email,
            fullName,
            request.PhoneNumber,
            role,
            InvitationToken.Hash(token),
            createdByUserId,
            now,
            now.AddHours(GetExpiryHours()));

        dbContext.AccountInvitations.Add(invitation);
        await dbContext.SaveChangesAsync(cancellationToken);

        await TryDeliverInvitationAsync(
            invitation,
            organization.Name,
            token,
            cancellationToken);

        await auditLogger.AccountLifecycleAsync(
            email,
            null,
            AuditActions.InvitationCreated,
            organizationId,
            cancellationToken);

        return ToResponse(invitation, now);
    }

    public async Task<IReadOnlyList<InvitationResponse>> GetAllAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();

        var invitations = await dbContext.AccountInvitations
            .AsNoTracking()
            .Where(invitation =>
                invitation.OrganizationId == organizationId &&
                invitation.RevokedAt == null)
            .OrderByDescending(invitation => invitation.CreatedAt)
            .ToListAsync(cancellationToken);

        return invitations
            .Select(invitation => ToResponse(invitation, now))
            .ToList();
    }

    public async Task<InvitationResponse?> ResendAsync(
        Guid organizationId,
        Guid invitationId,
        CancellationToken cancellationToken = default)
    {
        var invitation = await dbContext.AccountInvitations
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.Id == invitationId &&
                    candidate.OrganizationId == organizationId,
                cancellationToken);

        if (invitation is null)
        {
            return null;
        }

        if (invitation.IsUsed || invitation.IsRevoked)
        {
            throw new InvalidOperationException(
                "Only a pending invitation can be resent.");
        }

        var organization = await dbContext.Organizations
            .SingleOrDefaultAsync(
                candidate => candidate.Id == organizationId,
                cancellationToken);

        if (organization is null || !organization.IsActive)
        {
            throw new KeyNotFoundException("The organization does not exist.");
        }

        var token = InvitationToken.Create();
        var now = timeProvider.GetUtcNow();

        invitation.Reissue(
            InvitationToken.Hash(token),
            now.AddHours(GetExpiryHours()),
            now);

        await dbContext.SaveChangesAsync(cancellationToken);

        await TryDeliverInvitationAsync(
            invitation,
            organization.Name,
            token,
            cancellationToken);

        await auditLogger.AccountLifecycleAsync(
            invitation.Email,
            null,
            AuditActions.InvitationResent,
            organizationId,
            cancellationToken);

        return ToResponse(invitation, now);
    }

    public async Task<bool> RevokeAsync(
        Guid organizationId,
        Guid invitationId,
        CancellationToken cancellationToken = default)
    {
        var invitation = await dbContext.AccountInvitations
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.Id == invitationId &&
                    candidate.OrganizationId == organizationId,
                cancellationToken);

        if (invitation is null)
        {
            return false;
        }

        invitation.Revoke(timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogger.AccountLifecycleAsync(
            invitation.Email,
            null,
            AuditActions.InvitationRevoked,
            organizationId,
            cancellationToken);

        return true;
    }

    public async Task<InvitationDetailsResponse?> GetDetailsAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        var tokenHash = InvitationToken.Hash(token);

        var invitation = await dbContext.AccountInvitations
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.TokenHash == tokenHash,
                cancellationToken);

        if (invitation is null || !invitation.IsAvailable(now))
        {
            return null;
        }

        var organization = await dbContext.Organizations
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.Id == invitation.OrganizationId &&
                    candidate.IsActive,
                cancellationToken);

        if (organization is null)
        {
            return null;
        }

        return new InvitationDetailsResponse(
            invitation.Email,
            invitation.FullName,
            invitation.PhoneNumber,
            organization.Name,
            invitation.OrganizationRole.ToString(),
            invitation.ExpiresAt);
    }

    public async Task<AcceptInvitationOutcome> AcceptAsync(
        AcceptInvitationRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var tokenHash = InvitationToken.Hash(request.Token);

        var invitation = await dbContext.AccountInvitations
            .SingleOrDefaultAsync(
                candidate => candidate.TokenHash == tokenHash,
                cancellationToken);

        if (invitation is null || !invitation.IsAvailable(now))
        {
            return AcceptInvitationOutcome.Unavailable();
        }

        var organization = await dbContext.Organizations
            .SingleOrDefaultAsync(
                candidate => candidate.Id == invitation.OrganizationId,
                cancellationToken);

        if (organization is null || !organization.IsActive)
        {
            return AcceptInvitationOutcome.Unavailable();
        }

        // staff_members is a row-level-security tenant table (FORCE ROW LEVEL
        // SECURITY with a WITH CHECK clause). Every write below has to run
        // inside a transaction whose `civicflow.organization_id` setting
        // matches the invitation's organization, or PostgreSQL rejects the
        // insert and the accept endpoint returns 500. The surrounding
        // OrganizationTransactionMiddleware commits the transaction once the
        // request completes successfully.
        await organizationTransactionContext.BeginAsync(
            invitation.OrganizationId,
            cancellationToken);

        var email = invitation.Email;
        var user = await userManager.FindByEmailAsync(email);
        var isNewAccount = user is null;

        if (user is not null && !user.IsActive)
        {
            return AcceptInvitationOutcome.AccountInactive();
        }

        var passwordErrors = await ValidatePasswordAsync(
            user,
            request.Password);

        if (passwordErrors.Count > 0)
        {
            return AcceptInvitationOutcome.InvalidPassword(
                string.Join(
                    " ",
                    passwordErrors
                        .Select(error => error.Description)
                        .Distinct(StringComparer.Ordinal)));
        }

        if (user is null)
        {
            user = ApplicationUser.Create(
                invitation.FullName,
                email,
                now);

            // Possession of the emailed token proves control of the address.
            user.EmailConfirmed = true;

            if (!string.IsNullOrWhiteSpace(invitation.PhoneNumber))
            {
                user.PhoneNumber = invitation.PhoneNumber;
            }

            var createResult = await userManager.CreateAsync(
                user,
                request.Password);

            if (!createResult.Succeeded)
            {
                return AcceptInvitationOutcome.InvalidPassword(
                    DescribeFailures(createResult));
            }
        }
        else
        {
            var setPasswordResult = await SetPasswordAsync(
                user,
                request.Password);

            if (!setPasswordResult.Succeeded)
            {
                return AcceptInvitationOutcome.InvalidPassword(
                    DescribeFailures(setPasswordResult));
            }

            if (!user.EmailConfirmed)
            {
                user.EmailConfirmed = true;
                await userManager.UpdateAsync(user);
            }

            // The organization collected a contact number while inviting. It
            // fills a gap on the account, but never replaces a number the
            // person set for themselves.
            if (string.IsNullOrWhiteSpace(user.PhoneNumber) &&
                !string.IsNullOrWhiteSpace(invitation.PhoneNumber))
            {
                user.PhoneNumber = invitation.PhoneNumber;
                await userManager.UpdateAsync(user);
            }

            // A credential change invalidates every existing session.
            await userManager.UpdateSecurityStampAsync(user);
        }

        if (!await userManager.IsInRoleAsync(user, AppRoles.Staff))
        {
            await userManager.AddToRoleAsync(user, AppRoles.Staff);
        }

        await EnsureOrganizationMembershipAsync(
            user,
            invitation,
            now,
            cancellationToken);

        invitation.MarkUsed(user.Id, now);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogger.AccountLifecycleAsync(
            email,
            user.Id,
            AuditActions.InvitationAccepted,
            invitation.OrganizationId,
            cancellationToken);

        await auditLogger.PrivilegeChangeAsync(
            user.Id,
            $"role=staff organizationRole={invitation.OrganizationRole} " +
            $"organizationId={invitation.OrganizationId} via=invitation",
            invitation.OrganizationId,
            cancellationToken);

        return AcceptInvitationOutcome.Success(email);
    }

    private async Task EnsureOrganizationMembershipAsync(
        ApplicationUser user,
        AccountInvitation invitation,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var membership = await dbContext.OrganizationMemberships
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.UserId == user.Id &&
                    candidate.OrganizationId == invitation.OrganizationId,
                cancellationToken);

        if (membership is null)
        {
            dbContext.OrganizationMemberships.Add(
                OrganizationMembership.Create(
                    user.Id,
                    invitation.OrganizationId,
                    invitation.OrganizationRole,
                    now));
        }
        else if (!membership.IsActive)
        {
            membership.Reactivate(now);
            membership.ChangeRole(invitation.OrganizationRole, now);
        }

        var staffMember = await dbContext.StaffMembers
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.OrganizationId == invitation.OrganizationId &&
                    candidate.Email == invitation.Email,
                cancellationToken);

        if (staffMember is null)
        {
            dbContext.StaffMembers.Add(
                StaffMember.Create(
                    invitation.OrganizationId,
                    invitation.FullName,
                    invitation.Email,
                    invitation.PhoneNumber,
                    now));
        }
        else
        {
            if (!staffMember.IsActive)
            {
                staffMember.Reactivate(now);
            }

            if (!string.IsNullOrWhiteSpace(invitation.PhoneNumber) &&
                !string.Equals(
                    staffMember.PhoneNumber,
                    invitation.PhoneNumber,
                    StringComparison.Ordinal))
            {
                staffMember.UpdatePhoneNumber(
                    invitation.PhoneNumber,
                    now);
            }
        }
    }

    private async Task<IReadOnlyList<IdentityError>> ValidatePasswordAsync(
        ApplicationUser? user,
        string password)
    {
        var candidate = user ?? new ApplicationUser();
        var errors = new List<IdentityError>();

        foreach (var validator in userManager.PasswordValidators)
        {
            var result = await validator.ValidateAsync(
                userManager,
                candidate,
                password);

            if (!result.Succeeded)
            {
                errors.AddRange(result.Errors);
            }
        }

        return errors;
    }

    private async Task<IdentityResult> SetPasswordAsync(
        ApplicationUser user,
        string password)
    {
        if (await userManager.HasPasswordAsync(user))
        {
            var removeResult = await userManager.RemovePasswordAsync(user);

            if (!removeResult.Succeeded)
            {
                return removeResult;
            }
        }

        return await userManager.AddPasswordAsync(user, password);
    }

    private async Task<bool> IsActiveMemberAsync(
        Guid userId,
        Guid organizationId,
        CancellationToken cancellationToken) =>
        await dbContext.OrganizationMemberships
            .AnyAsync(
                membership =>
                    membership.UserId == userId &&
                    membership.OrganizationId == organizationId &&
                    membership.IsActive,
                cancellationToken);

    private async Task TryDeliverInvitationAsync(
        AccountInvitation invitation,
        string organizationName,
        string token,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        try
        {
            await SendInvitationEmailAsync(
                invitation,
                organizationName,
                token,
                cancellationToken);

            invitation.MarkDeliverySucceeded(now);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            // The invitation stays valid. The administrator sees a delivery
            // failure in the grid and can retry with Resend.
            invitation.MarkDeliveryFailed(exception.Message, now);

            await auditLogger.RecordAsync(
                new AuditRecord
                {
                    Category = AuditCategory.Organization,
                    Action = AuditActions.InvitationDeliveryFailed,
                    OrganizationId = invitation.OrganizationId,
                    EntityType = "AccountInvitation",
                    EntityId = invitation.Id.ToString(),
                    Details =
                        $"Delivery failed for {invitation.Email}: " +
                        $"{exception.GetType().Name}"
                },
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SendInvitationEmailAsync(
        AccountInvitation invitation,
        string organizationName,
        string token,
        CancellationToken cancellationToken)
    {
        var frontendBaseUrl =
            configuration["Frontend:BaseUrl"]?.TrimEnd('/')
            ?? "http://localhost:5173";

        var link =
            $"{frontendBaseUrl}/setup-password" +
            $"?token={Uri.EscapeDataString(token)}";

        var body = string.Join(
            Environment.NewLine,
            $"You have been invited to join {organizationName} on CivicFlow " +
            $"as {invitation.OrganizationRole}.",
            string.Empty,
            "Choose your password using this single-use link:",
            link,
            string.Empty,
            $"The link expires {invitation.ExpiresAt:yyyy-MM-dd HH:mm} UTC.",
            string.Empty,
            "If you were not expecting this invitation, you can ignore this email.");

        if (!string.IsNullOrWhiteSpace(invitation.PhoneNumber))
        {
            body = string.Join(
                Environment.NewLine,
                body,
                string.Empty,
                $"CivicFlow will contact you on {invitation.PhoneNumber} " +
                "about the cases you work on.");
        }

        await emailSender.SendAsync(
            invitation.Email,
            $"You're invited to join {organizationName} on CivicFlow",
            body,
            cancellationToken);
    }

    private int GetExpiryHours()
    {
        var configured = configuration.GetValue<int?>(
            "Invitations:ExpiryHours");

        if (!configured.HasValue)
        {
            return DefaultExpiryHours;
        }

        return Math.Clamp(
            configured.Value,
            MinimumExpiryHours,
            MaximumExpiryHours);
    }

    private static OrganizationRole ParseRole(string role)
    {
        var normalized = role.Trim();
        var match = AllowedRoles.FirstOrDefault(candidate =>
            string.Equals(
                candidate,
                normalized,
                StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            throw new ArgumentException(
                "The role must be one of: Viewer, CaseWorker, OrganizationAdmin.",
                nameof(role));
        }

        return Enum.Parse<OrganizationRole>(match, ignoreCase: true);
    }

    private static string NormalizeEmail(string email) =>
        TextNormalization.NormalizeEmail(email);

    private static InvitationResponse ToResponse(
        AccountInvitation invitation,
        DateTimeOffset now) =>
        new(
            invitation.Id,
            invitation.OrganizationId,
            invitation.Email,
            invitation.FullName,
            invitation.PhoneNumber,
            invitation.OrganizationRole.ToString(),
            invitation.CreatedAt,
            invitation.ExpiresAt,
            invitation.IsExpired(now),
            invitation.IsUsed,
            invitation.IsRevoked,
            invitation.IsDeliveryFailed);

    private static string DescribeFailures(IdentityResult result) =>
        string.Join(
            " ",
            result.Errors
                .Select(error => error.Description)
                .Distinct(StringComparer.Ordinal));
}
