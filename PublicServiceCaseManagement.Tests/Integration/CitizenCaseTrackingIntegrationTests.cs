using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PublicServiceCaseManagement.Api.Domain.Cases;
using PublicServiceCaseManagement.Api.Domain.Notifications;
using PublicServiceCaseManagement.Api.Domain.Organizations;
using PublicServiceCaseManagement.Api.Domain.Staff;
using PublicServiceCaseManagement.Api.Domain.Users;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;
using Xunit;

using PublicServiceCaseManagement.Tests.Support;

namespace PublicServiceCaseManagement.Tests.Integration;

public sealed class CitizenCaseTrackingIntegrationTests
{
    [Fact]
    public async Task CitizenCanPageSearchFilterAndSortOnlyOwnCases()
    {
        await using var factory = CreateFactory();
        var organizationId = await factory.SeedAuthenticatedOrganizationAsync();
        using var citizen = CreateCitizenClient(factory);

        var streetlight = await CreateCaseAsync(
            citizen,
            organizationId,
            "Broken streetlight",
            "The streetlight outside the library has been dark for a week.");
        var pothole = await CreateCaseAsync(
            citizen,
            organizationId,
            "Pothole on Main Street",
            "A deep pothole is blocking the cycle lane.");
        var water = await CreateCaseAsync(
            citizen,
            organizationId,
            "Water leak",
            "Water is leaking from the pavement near the school gate.");

        await SetCreatedAtAsync(factory, streetlight.Id, new DateTimeOffset(
            2026, 10, 20, 8, 0, 0, TimeSpan.Zero));
        await SetCreatedAtAsync(factory, pothole.Id, new DateTimeOffset(
            2026, 10, 21, 8, 0, 0, TimeSpan.Zero));
        await SetCreatedAtAsync(factory, water.Id, new DateTimeOffset(
            2026, 10, 22, 8, 0, 0, TimeSpan.Zero));

        var pageResponse = await citizen.GetAsync(
            "/api/citizens/cases?page=1&pageSize=2&sort=created_asc");
        Assert.Equal(HttpStatusCode.OK, pageResponse.StatusCode);
        var firstPage = await pageResponse.Content
            .ReadFromJsonAsync<CitizenCasePageResponse>();
        Assert.NotNull(firstPage);
        Assert.Equal(3, firstPage!.TotalCount);
        Assert.Equal(2, firstPage.Items.Count);
        Assert.Equal(2, firstPage.TotalPages);
        Assert.Equal(streetlight.Id, firstPage.Items[0].Id);
        Assert.Equal(pothole.Id, firstPage.Items[1].Id);
        Assert.All(
            firstPage.Items,
            item => Assert.Equal(
                "Integration Test Organization",
                item.OrganizationName));

        var secondPage = await citizen.GetFromJsonAsync<CitizenCasePageResponse>(
            "/api/citizens/cases?page=2&pageSize=2&sort=created_asc");
        Assert.NotNull(secondPage);
        Assert.Single(secondPage!.Items);
        Assert.Equal(water.Id, secondPage.Items[0].Id);

        var search = await citizen.GetFromJsonAsync<CitizenCasePageResponse>(
            "/api/citizens/cases?search=streetlight");
        Assert.NotNull(search);
        Assert.Single(search!.Items);
        Assert.Equal(streetlight.Id, search.Items[0].Id);

        await SetCaseStatusAsync(factory, pothole.Id, CaseStatus.UnderReview);

        var filtered = await citizen.GetFromJsonAsync<CitizenCasePageResponse>(
            "/api/citizens/cases?status=UnderReview");
        Assert.NotNull(filtered);
        Assert.Single(filtered!.Items);
        Assert.Equal(pothole.Id, filtered.Items[0].Id);

        var invalidSort = await citizen.GetAsync(
            "/api/citizens/cases?sort=drop-tables");
        Assert.Equal(HttpStatusCode.BadRequest, invalidSort.StatusCode);

        var invalidPageSize = await citizen.GetAsync(
            "/api/citizens/cases?pageSize=500");
        Assert.Equal(HttpStatusCode.BadRequest, invalidPageSize.StatusCode);

        // A second citizen cannot see or open the first citizen's cases.
        var otherCitizenId = await SeedCitizenAsync(factory);
        using var otherCitizen = CreateCitizenClient(factory, otherCitizenId);

        var otherList = await otherCitizen
            .GetFromJsonAsync<CitizenCasePageResponse>(
                "/api/citizens/cases");
        Assert.NotNull(otherList);
        Assert.Empty(otherList!.Items);
        Assert.Equal(0, otherList.TotalCount);

        var otherDetail = await otherCitizen.GetAsync(
            $"/api/citizens/cases/{streetlight.Id}");
        Assert.Equal(HttpStatusCode.NotFound, otherDetail.StatusCode);
    }

    [Fact]
    public async Task TimelineExposesPublicUpdatesButNeverInternalNotes()
    {
        await using var factory = CreateFactory();
        var organizationId = await factory.SeedAuthenticatedOrganizationAsync();
        var staff = await SeedAdditionalStaffAsync(factory, organizationId);
        using var citizen = CreateCitizenClient(factory);
        using var staffClient = CreateStaffClient(factory, organizationId, staff.UserId);

        var submitted = await CreateCaseAsync(
            citizen,
            organizationId,
            "Missed waste collection",
            "The bins on Elm Street were not collected this week.");
        await AssignCaseAsync(factory, submitted.Id, staff.StaffMemberId);

        var publicStatus = await ChangeStatusAsync(
            staffClient,
            submitted.Id,
            CaseStatus.UnderReview,
            "Your report is being reviewed by the waste team.",
            explanationVisibleToCitizen: true);
        Assert.Equal(HttpStatusCode.OK, publicStatus.StatusCode);

        var internalStatus = await ChangeStatusAsync(
            staffClient,
            submitted.Id,
            CaseStatus.Assigned,
            "Assigning to the depot crew; internal escalation reference 42.",
            explanationVisibleToCitizen: false);
        Assert.Equal(HttpStatusCode.OK, internalStatus.StatusCode);

        var internalNote = await AddUpdateAsync(
            staffClient,
            submitted.Id,
            "InternalNote",
            "Do not share: vehicle keys are held by the night supervisor.");
        Assert.Equal(HttpStatusCode.Created, internalNote.StatusCode);

        var publicNote = await AddUpdateAsync(
            staffClient,
            submitted.Id,
            "PublicUpdate",
            "A collection vehicle will visit Elm Street tomorrow morning.");
        Assert.Equal(HttpStatusCode.Created, publicNote.StatusCode);

        var informationRequest = await AddUpdateAsync(
            staffClient,
            submitted.Id,
            "InformationRequest",
            "Please confirm whether the gate was locked.");
        Assert.Equal(HttpStatusCode.Created, informationRequest.StatusCode);

        var detailResponse = await citizen.GetAsync(
            $"/api/citizens/cases/{submitted.Id}");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);

        var rawDetail = await detailResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("night supervisor", rawDetail);
        Assert.DoesNotContain("internal escalation reference", rawDetail);
        Assert.DoesNotContain(staff.UserId.ToString(), rawDetail);

        var detail = JsonSerializer.Deserialize<CitizenCaseDetailResponse>(
            rawDetail,
            JsonOptions);
        Assert.NotNull(detail);
        Assert.Equal("AwaitingInformation", detail!.Status);
        Assert.True(detail.AwaitingInformation);
        Assert.True(detail.CanRespond);

        Assert.Contains(
            detail.StatusHistory,
            entry => entry.Explanation ==
                "Your report is being reviewed by the waste team.");
        Assert.DoesNotContain(
            detail.StatusHistory,
            entry => entry.Explanation?.Contains(
                "internal escalation",
                StringComparison.OrdinalIgnoreCase) == true);
        Assert.Contains(
            detail.StatusHistory,
            entry => entry.Explanation ==
                "Additional information was requested.");

        Assert.Equal(2, detail.Updates.Count);
        Assert.Contains(detail.Updates, update => update.Kind == "PublicUpdate");
        Assert.Contains(
            detail.Updates,
            update => update.Kind == "InformationRequest");
        Assert.DoesNotContain(
            detail.Updates,
            update => update.Kind == "InternalNote");

        // Staff retain the complete record, including internal activity and
        // the hidden status explanation.
        var staffUpdates = await staffClient.GetFromJsonAsync<
            IReadOnlyList<StaffUpdateResponse>>(
            $"/api/cases/{submitted.Id}/updates");
        Assert.NotNull(staffUpdates);
        Assert.Contains(
            staffUpdates!,
            update => update.Kind == "InternalNote" &&
                      update.IsVisibleToCitizen == false);
        Assert.Contains(
            staffUpdates!,
            update => update.Kind == "PublicUpdate" &&
                      update.IsVisibleToCitizen);

        var staffHistory = await staffClient.GetFromJsonAsync<
            IReadOnlyList<StaffStatusHistoryResponse>>(
            $"/api/cases/{submitted.Id}/history");
        Assert.NotNull(staffHistory);
        Assert.Contains(
            staffHistory!,
            entry => entry.Explanation?.Contains(
                "internal escalation",
                StringComparison.OrdinalIgnoreCase) == true);
        Assert.Contains(
            staffHistory!,
            entry => !entry.IsVisibleToCitizen &&
                     entry.Explanation is not null);

        // The owner can respond; the case returns to review and the response
        // becomes part of the shared timeline.
        var response = await citizen.PostAsJsonAsync(
            $"/api/citizens/cases/{submitted.Id}/responses",
            new { message = "The gate was locked, but the code is 1234." });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var afterResponse = await citizen
            .GetFromJsonAsync<CitizenCaseDetailResponse>(
                $"/api/citizens/cases/{submitted.Id}");
        Assert.NotNull(afterResponse);
        Assert.Equal("UnderReview", afterResponse!.Status);
        Assert.False(afterResponse.AwaitingInformation);
        Assert.Contains(
            afterResponse.Updates,
            update => update.Kind == "CitizenResponse" &&
                      update.Message.Contains("code is 1234"));
        Assert.Contains(
            afterResponse.StatusHistory,
            entry => entry.Explanation ==
                "Citizen provided the requested information.");

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<CivicFlowDbContext>();
        Assert.True(await dbContext.Notifications.AnyAsync(notification =>
            notification.RecipientUserId == TestAuthenticationHandler.DefaultUserId &&
            notification.Type == NotificationTypes.CaseUpdateAdded));
        Assert.True(await dbContext.Notifications.AnyAsync(notification =>
            notification.RecipientUserId == TestAuthenticationHandler.DefaultUserId &&
            notification.Type == NotificationTypes.CaseInformationRequested));
        Assert.True(await dbContext.Notifications.AnyAsync(notification =>
            notification.RecipientUserId == staff.UserId &&
            notification.Type == NotificationTypes.CaseCitizenResponded));
    }

    [Fact]
    public async Task DocumentVisibilityProtectsInternalAttachments()
    {
        await using var factory = CreateFactory();
        var organizationId = await factory.SeedAuthenticatedOrganizationAsync();
        var staff = await SeedAdditionalStaffAsync(factory, organizationId);
        using var citizen = CreateCitizenClient(factory);
        using var staffClient = CreateStaffClient(factory, organizationId, staff.UserId);
        var otherCitizenId = await SeedCitizenAsync(factory);
        using var otherCitizen = CreateCitizenClient(factory, otherCitizenId);

        var submitted = await CreateCaseAsync(
            citizen,
            organizationId,
            "Noise complaint",
            "Construction noise continues past the permitted hours.",
            ("citizen-evidence.pdf", "%PDF-citizen-evidence"));
        await AssignCaseAsync(factory, submitted.Id, staff.StaffMemberId);

        using var internalContent = new MultipartFormDataContent();
        using var internalFile = new ByteArrayContent(
            Encoding.UTF8.GetBytes("internal investigation notes"));
        internalFile.Headers.ContentType =
            new MediaTypeHeaderValue("text/plain");
        internalContent.Add(internalFile, "file", "internal-notes.txt");

        var internalUpload = await staffClient.PostAsync(
            $"/api/cases/{submitted.Id}/documents",
            internalContent);
        Assert.Equal(HttpStatusCode.Created, internalUpload.StatusCode);
        var internalDocument = await internalUpload.Content
            .ReadFromJsonAsync<StaffDocumentResponse>();
        Assert.NotNull(internalDocument);
        Assert.False(internalDocument!.IsVisibleToCitizen);

        using var sharedContent = new MultipartFormDataContent();
        using var sharedFile = new ByteArrayContent(
            Encoding.UTF8.GetBytes("published summary"));
        sharedFile.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        sharedContent.Add(sharedFile, "file", "summary.txt");
        sharedContent.Add(
            new StringContent("true"),
            "visibleToCitizen");

        var sharedUpload = await staffClient.PostAsync(
            $"/api/cases/{submitted.Id}/documents",
            sharedContent);
        Assert.Equal(HttpStatusCode.Created, sharedUpload.StatusCode);
        var sharedDocument = await sharedUpload.Content
            .ReadFromJsonAsync<StaffDocumentResponse>();
        Assert.NotNull(sharedDocument);
        Assert.True(sharedDocument!.IsVisibleToCitizen);

        var detail = await citizen.GetFromJsonAsync<CitizenCaseDetailResponse>(
            $"/api/citizens/cases/{submitted.Id}");
        Assert.NotNull(detail);
        Assert.Equal(2, detail!.Documents.Count);
        Assert.DoesNotContain(
            detail.Documents,
            document => document.OriginalFileName == "internal-notes.txt");
        Assert.Contains(
            detail.Documents,
            document => document.OriginalFileName == "summary.txt");

        var internalDownload = await citizen.GetAsync(
            $"/api/citizens/cases/{submitted.Id}/documents/{internalDocument.Id}");
        Assert.Equal(HttpStatusCode.NotFound, internalDownload.StatusCode);

        var otherCitizenDownload = await otherCitizen.GetAsync(
            $"/api/citizens/cases/{submitted.Id}/documents/{sharedDocument.Id}");
        Assert.Equal(HttpStatusCode.NotFound, otherCitizenDownload.StatusCode);

        var sharedDownload = await citizen.GetAsync(
            $"/api/citizens/cases/{submitted.Id}/documents/{sharedDocument.Id}");
        Assert.Equal(HttpStatusCode.OK, sharedDownload.StatusCode);
        Assert.Equal(
            "published summary",
            await sharedDownload.Content.ReadAsStringAsync());

        var citizenDocument = detail.Documents.Single(document =>
            document.OriginalFileName == "citizen-evidence.pdf");
        var citizenDownload = await citizen.GetAsync(
            $"/api/citizens/cases/{submitted.Id}/documents/{citizenDocument.Id}");
        Assert.Equal(HttpStatusCode.OK, citizenDownload.StatusCode);

        var staffDocuments = await staffClient.GetFromJsonAsync<
            IReadOnlyList<StaffDocumentResponse>>(
            $"/api/cases/{submitted.Id}/documents");
        Assert.NotNull(staffDocuments);
        Assert.Equal(3, staffDocuments!.Count);
        Assert.Contains(
            staffDocuments,
            document => document.OriginalFileName == "internal-notes.txt" &&
                        !document.IsVisibleToCitizen);
    }

    [Fact]
    public async Task InvalidTransitionsAndAuthorizationAreRejected()
    {
        await using var factory = CreateFactory();
        var organizationId = await factory.SeedAuthenticatedOrganizationAsync();
        var staff = await SeedAdditionalStaffAsync(factory, organizationId);
        using var citizen = CreateCitizenClient(factory);
        using var staffClient = CreateStaffClient(factory, organizationId, staff.UserId);
        using var anonymous = factory.CreateClient();
        anonymous.DefaultRequestHeaders.Add("X-Test-Anonymous", "true");
        using var staffAsCitizen = factory.CreateClient();
        staffAsCitizen.DefaultRequestHeaders.Add(
            "X-Test-Role",
            TestAuthenticationHandler.StaffRole);

        var submitted = await CreateCaseAsync(
            citizen,
            organizationId,
            "Illegal transition case",
            "A case used to verify the workflow state machine.");
        await AssignCaseAsync(factory, submitted.Id, staff.StaffMemberId);

        var invalid = await ChangeStatusAsync(
            staffClient,
            submitted.Id,
            CaseStatus.Closed,
            null,
            explanationVisibleToCitizen: false);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        // Submitted cases cannot jump directly to AwaitingInformation.
        var invalidInformationRequest = await AddUpdateAsync(
            staffClient,
            submitted.Id,
            "InformationRequest",
            "Please provide more detail.");
        Assert.Equal(HttpStatusCode.BadRequest, invalidInformationRequest.StatusCode);

        var noContent = await AddUpdateAsync(
            staffClient,
            submitted.Id,
            "PublicUpdate",
            "   ");
        Assert.Equal(HttpStatusCode.BadRequest, noContent.StatusCode);

        var anonymousList = await anonymous.GetAsync("/api/citizens/cases");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousList.StatusCode);

        var staffList = await staffAsCitizen.GetAsync("/api/citizens/cases");
        Assert.Equal(HttpStatusCode.Forbidden, staffList.StatusCode);

        var unknownCaseResponse = await citizen.PostAsJsonAsync(
            $"/api/citizens/cases/{Guid.NewGuid()}/responses",
            new { message = "This case does not exist." });
        Assert.Equal(HttpStatusCode.NotFound, unknownCaseResponse.StatusCode);
    }

    private static CivicFlowApiFactory CreateFactory() =>
        new(
            useAuthenticatedOrganizationContext: true,
            enableBusinessEndpoints: true);

    private static HttpClient CreateCitizenClient(
        CivicFlowApiFactory factory,
        Guid? userId = null)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            "X-Test-Role",
            TestAuthenticationHandler.CitizenRole);
        if (userId.HasValue)
        {
            client.DefaultRequestHeaders.Add(
                "X-Test-User-Id",
                userId.Value.ToString());
        }

        return client;
    }

    private static HttpClient CreateStaffClient(
        CivicFlowApiFactory factory,
        Guid organizationId,
        Guid userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            "X-Test-Role",
            TestAuthenticationHandler.StaffRole);
        client.DefaultRequestHeaders.Add("X-Organization-Id", organizationId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-User-Id", userId.ToString());
        return client;
    }

    private static async Task<CitizenSubmissionResponse> CreateCaseAsync(
        HttpClient client,
        Guid organizationId,
        string title,
        string description,
        (string FileName, string Content)? file = null)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(organizationId.ToString()), "organizationId");
        content.Add(new StringContent(title), "title");
        content.Add(new StringContent(description), "description");

        if (file.HasValue)
        {
            var bytes = Encoding.UTF8.GetBytes(file.Value.Content);
            var fileContent = new ByteArrayContent(bytes);
            fileContent.Headers.ContentType =
                new MediaTypeHeaderValue("application/pdf");
            content.Add(fileContent, "additionalDocument", file.Value.FileName);
        }

        var response = await client.PostAsync("/api/citizens/cases", content);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var submitted = await response.Content
            .ReadFromJsonAsync<CitizenSubmissionResponse>();
        Assert.NotNull(submitted);
        return submitted!;
    }

    private static Task<Guid> SeedCitizenAsync(CivicFlowApiFactory factory) =>
        factory.SeedCitizenAsync("Other Citizen");

    private static async Task<(Guid UserId, Guid StaffMemberId)> SeedAdditionalStaffAsync(
        CivicFlowApiFactory factory,
        Guid organizationId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<CivicFlowDbContext>();
        var now = DateTimeOffset.UtcNow;
        var email = $"tracking-staff-{Guid.NewGuid():N}@example.com";
        var user = ApplicationUser.Create("Tracking Officer", email, now);
        var membership = OrganizationMembership.Create(
            user.Id,
            organizationId,
            OrganizationRole.CaseWorker,
            now);
        var staffMember = StaffMember.Create(
            organizationId,
            user.FullName,
            email,
            null,
            now);

        dbContext.ApplicationUsers.Add(user);
        dbContext.OrganizationMemberships.Add(membership);
        dbContext.StaffMembers.Add(staffMember);
        await dbContext.SaveChangesAsync();

        return (user.Id, staffMember.Id);
    }

    private static async Task AssignCaseAsync(
        CivicFlowApiFactory factory,
        Guid caseId,
        Guid staffMemberId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<CivicFlowDbContext>();
        var caseEntity = await dbContext.Cases.SingleAsync(item => item.Id == caseId);
        caseEntity.AssignTo(
            staffMemberId,
            CaseAssignmentRole.CaseWorker,
            DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync();
    }

    private static async Task SetCaseStatusAsync(
        CivicFlowApiFactory factory,
        Guid caseId,
        CaseStatus status)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<CivicFlowDbContext>();
        var caseEntity = await dbContext.Cases.SingleAsync(item => item.Id == caseId);
        caseEntity.ChangeStatus(status, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync();
    }

    private static async Task SetCreatedAtAsync(
        CivicFlowApiFactory factory,
        Guid caseId,
        DateTimeOffset createdAt)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<CivicFlowDbContext>();
        var caseEntity = await dbContext.Cases.SingleAsync(item => item.Id == caseId);
        typeof(Case)
            .GetProperty(nameof(Case.CreatedAt))!
            .SetValue(caseEntity, createdAt);
        await dbContext.SaveChangesAsync();
    }

    private static Task<HttpResponseMessage> ChangeStatusAsync(
        HttpClient client,
        Guid caseId,
        CaseStatus status,
        string? explanation,
        bool explanationVisibleToCitizen) =>
        client.PatchAsJsonAsync(
            $"/api/cases/{caseId}/status",
            new
            {
                newStatus = status,
                explanation,
                explanationVisibleToCitizen
            });

    private static Task<HttpResponseMessage> AddUpdateAsync(
        HttpClient client,
        Guid caseId,
        string kind,
        string message) =>
        client.PostAsJsonAsync(
            $"/api/cases/{caseId}/updates",
            new { kind, message });

    private static readonly JsonSerializerOptions JsonOptions = new(
        JsonSerializerDefaults.Web);

    private sealed record CitizenCasePageResponse(
        IReadOnlyList<CitizenCaseSummaryResponse> Items,
        int Page,
        int PageSize,
        int TotalCount,
        int TotalPages);

    private sealed record CitizenCaseSummaryResponse(
        Guid Id,
        string ReferenceNumber,
        string Title,
        string Status,
        Guid OrganizationId,
        string OrganizationName,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);

    private sealed record CitizenCaseDetailResponse(
        Guid Id,
        string ReferenceNumber,
        string Title,
        string Description,
        string Status,
        Guid OrganizationId,
        string OrganizationName,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt,
        bool CanRespond,
        bool AwaitingInformation,
        IReadOnlyList<CitizenStatusHistoryResponse> StatusHistory,
        IReadOnlyList<CitizenUpdateResponse> Updates,
        IReadOnlyList<CitizenDocumentResponse> Documents);

    private sealed record CitizenStatusHistoryResponse(
        string? PreviousStatus,
        string NewStatus,
        DateTimeOffset ChangedAt,
        string? Explanation);

    private sealed record CitizenUpdateResponse(
        Guid Id,
        string Kind,
        string Message,
        DateTimeOffset CreatedAt);

    private sealed record CitizenDocumentResponse(
        Guid Id,
        string OriginalFileName,
        string ContentType,
        long SizeBytes,
        string Sha256Hash,
        DateTimeOffset UploadedAt);

    private sealed record CitizenSubmissionResponse(
        Guid Id,
        string ReferenceNumber,
        string Title,
        string Description,
        string Status,
        DateTimeOffset CreatedAt,
        CitizenSubmissionDocumentResponse? AdditionalDocument);

    private sealed record CitizenSubmissionDocumentResponse(
        Guid Id,
        string OriginalFileName,
        bool IsVisibleToCitizen);

    private sealed record StaffUpdateResponse(
        Guid Id,
        string Kind,
        string Message,
        bool IsVisibleToCitizen,
        Guid AuthorUserId,
        DateTimeOffset CreatedAt);

    private sealed record StaffStatusHistoryResponse(
        Guid Id,
        string PreviousStatus,
        string NewStatus,
        DateTimeOffset ChangedAt,
        Guid? ChangedByUserId,
        string? Explanation,
        bool IsVisibleToCitizen);

    private sealed record StaffDocumentResponse(
        Guid Id,
        Guid CaseId,
        string OriginalFileName,
        string ContentType,
        long SizeBytes,
        string Sha256Hash,
        DateTimeOffset UploadedAt,
        bool IsVisibleToCitizen);
}
