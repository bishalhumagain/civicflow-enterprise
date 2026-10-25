using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PublicServiceCaseManagement.Api.Domain.Organizations;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;
using Xunit;

namespace PublicServiceCaseManagement.Tests;

public sealed class CitizenCaseIntakeIntegrationTests
{
    [Fact]
    public async Task CitizenCanSelectOrganizationAndSubmitCaseWithAnyAttachment()
    {
        await using var factory = new CivicFlowApiFactory(
            useAuthenticatedOrganizationContext: true,
            environmentName: "Production",
            enableBusinessEndpoints: false);
        var organizationId = await factory.SeedAuthenticatedOrganizationAsync();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            "X-Test-Role",
            TestAuthenticationHandler.CitizenRole);

        var organizationsResponse = await client.GetAsync(
            "/api/citizens/organizations");
        Assert.Equal(HttpStatusCode.OK, organizationsResponse.StatusCode);
        var organizations = await organizationsResponse.Content
            .ReadFromJsonAsync<IReadOnlyList<CitizenOrganizationResponse>>();
        Assert.Contains(
            organizations!,
            organization => organization.Id == organizationId);

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(organizationId.ToString()), "organizationId");
        content.Add(new StringContent("Broken streetlight"), "title");
        content.Add(
            new StringContent("The streetlight has been out for several days."),
            "description");
        using var fileContent = new ByteArrayContent(
            Encoding.UTF8.GetBytes("arbitrary non-image content"));
        fileContent.Headers.ContentType =
            new MediaTypeHeaderValue("application/x-civicflow-test");
        content.Add(fileContent, "additionalDocument", "evidence.custom");

        var response = await client.PostAsync("/api/citizens/cases", content);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var submitted = await response.Content
            .ReadFromJsonAsync<CitizenCaseResponse>();
        Assert.NotNull(submitted);
        Assert.Equal("evidence.custom", submitted!.AdditionalDocument!.OriginalFileName);
        Assert.True(submitted.AdditionalDocument.IsVisibleToCitizen);

        var listResponse = await client.GetAsync("/api/citizens/cases");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var listed = await listResponse.Content
            .ReadFromJsonAsync<CitizenCasePageResponse>();
        Assert.Contains(listed!.Items, candidate => candidate.Id == submitted.Id);
        Assert.Equal(1, listed.TotalCount);

        var detailResponse = await client.GetAsync(
            $"/api/citizens/cases/{submitted.Id}");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var detail = await detailResponse.Content
            .ReadFromJsonAsync<CitizenCaseDetailResponse>();
        Assert.NotNull(detail);
        Assert.Equal("Broken streetlight", detail!.Title);
        Assert.Equal(submitted.Status, detail.Status);
        Assert.Contains(
            detail.Documents,
            document => document.OriginalFileName == "evidence.custom");

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<CivicFlowDbContext>();
            var caseEntity = await dbContext.Cases
                .SingleAsync(candidate => candidate.Id == submitted.Id);

            Assert.Equal(organizationId, caseEntity.OrganizationId);
            Assert.Equal(
                TestAuthenticationHandler.DefaultUserId,
                caseEntity.SubmittedByUserId);

            var document = await dbContext.CaseDocuments
                .SingleAsync(candidate => candidate.CaseId == submitted.Id);
            Assert.Equal(
                TestAuthenticationHandler.DefaultUserId,
                document.UploadedByUserId);
            Assert.Equal(
                "application/x-civicflow-test",
                document.ContentType);
        }

        client.DefaultRequestHeaders.Remove("X-Test-Role");
        client.DefaultRequestHeaders.Add(
            "X-Test-Role",
            TestAuthenticationHandler.StaffRole);
        client.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            organizationId.ToString());

        var documentsResponse = await client.GetAsync(
            $"/api/cases/{submitted.Id}/documents");
        Assert.Equal(HttpStatusCode.NotFound, documentsResponse.StatusCode);
    }

    [Fact]
    public async Task CitizenCannotSubmitCaseToUnknownOrganization()
    {
        await using var factory = new CivicFlowApiFactory(
            useAuthenticatedOrganizationContext: true,
            enableBusinessEndpoints: true);
        await factory.SeedAuthenticatedOrganizationAsync();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            "X-Test-Role",
            TestAuthenticationHandler.CitizenRole);

        using var content = new MultipartFormDataContent
        {
            { new StringContent(Guid.NewGuid().ToString()), "organizationId" },
            { new StringContent("Broken streetlight"), "title" },
            {
                new StringContent("The streetlight has been out for several days."),
                "description"
            }
        };

        var response = await client.PostAsync("/api/citizens/cases", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<CivicFlowDbContext>();
        Assert.Empty(await dbContext.Cases.ToListAsync());
    }

    private sealed record CitizenOrganizationResponse(Guid Id, string Name, string Code);

    private sealed record CitizenCaseResponse(
        Guid Id,
        string ReferenceNumber,
        string Title,
        string Description,
        string Status,
        DateTimeOffset CreatedAt,
        CitizenDocumentResponse? AdditionalDocument);

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
        IReadOnlyList<CitizenDocumentDetailResponse> Documents);

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

    private sealed record CitizenDocumentDetailResponse(
        Guid Id,
        string OriginalFileName,
        string ContentType,
        long SizeBytes,
        string Sha256Hash,
        DateTimeOffset UploadedAt);

    private sealed record CitizenDocumentResponse(
        Guid Id,
        string OriginalFileName,
        bool IsVisibleToCitizen);
}
