using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PublicServiceCaseManagement.Api.Domain.Organizations;

namespace PublicServiceCaseManagement.Tests;

public sealed class CaseDocumentsApiIntegrationTests
{
    [Fact]
    public async Task CaseWorker_CanUploadListAndDownloadDocument()
    {
        await using var factory = new CivicFlowApiFactory(
            useAuthenticatedOrganizationContext: true);
        var organizationId = await factory.SeedAuthenticatedOrganizationAsync(
            OrganizationRole.CaseWorker);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            organizationId.ToString());

        var createResponse = await client.PostAsJsonAsync(
            "/api/cases",
            new
            {
                title = "Document attachment case",
                description = "A case that includes supporting evidence."
            });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var caseJson = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());
        var caseId = caseJson.RootElement.GetProperty("id").GetGuid();

        var bytes = Encoding.UTF8.GetBytes("%PDF-test-document");
        using var content = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType =
            new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "supporting-evidence.pdf");

        var uploadResponse = await client.PostAsync(
            $"/api/cases/{caseId}/documents",
            content);

        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);

        using var documentJson = JsonDocument.Parse(
            await uploadResponse.Content.ReadAsStringAsync());
        var documentId = documentJson.RootElement.GetProperty("id").GetGuid();
        var expectedHash = Convert.ToHexString(
                SHA256.HashData(bytes))
            .ToLowerInvariant();

        Assert.Equal(
            expectedHash,
            documentJson.RootElement.GetProperty("sha256Hash").GetString());

        var listResponse = await client.GetAsync(
            $"/api/cases/{caseId}/documents");

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        using var listJson = JsonDocument.Parse(
            await listResponse.Content.ReadAsStringAsync());
        Assert.Single(listJson.RootElement.EnumerateArray());

        var downloadResponse = await client.GetAsync(
            $"/api/cases/{caseId}/documents/{documentId}");

        Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);
        Assert.Equal(bytes, await downloadResponse.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task CaseWorker_CanUploadArbitraryDocumentType()
    {
        await using var factory = new CivicFlowApiFactory(
            useAuthenticatedOrganizationContext: true);
        var organizationId = await factory.SeedAuthenticatedOrganizationAsync(
            OrganizationRole.CaseWorker);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            organizationId.ToString());

        var createResponse = await client.PostAsJsonAsync(
            "/api/cases",
            new
            {
                title = "Unsupported document case",
                description = "A case used to verify upload validation."
            });

        using var caseJson = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());
        var caseId = caseJson.RootElement.GetProperty("id").GetGuid();

        using var content = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("not an executable"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "file", "payload.exe");

        var response = await client.PostAsync(
            $"/api/cases/{caseId}/documents",
            content);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task DocumentFromAnotherOrganization_IsNotAccessible()
    {
        await using var factory = new CivicFlowApiFactory(
            useAuthenticatedOrganizationContext: true);
        var firstOrganizationId = await factory.SeedAuthenticatedOrganizationAsync(
            OrganizationRole.CaseWorker);
        var secondOrganizationId =
            await factory.SeedAdditionalOrganizationForAuthenticatedUserAsync(
                OrganizationRole.CaseWorker);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            firstOrganizationId.ToString());

        var createResponse = await client.PostAsJsonAsync(
            "/api/cases",
            new
            {
                title = "Tenant document case",
                description = "The document must remain tenant scoped."
            });

        using var caseJson = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());
        var caseId = caseJson.RootElement.GetProperty("id").GetGuid();

        using var content = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("%PDF-tenant"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "tenant.pdf");

        var uploadResponse = await client.PostAsync(
            $"/api/cases/{caseId}/documents",
            content);

        using var documentJson = JsonDocument.Parse(
            await uploadResponse.Content.ReadAsStringAsync());
        var documentId = documentJson.RootElement.GetProperty("id").GetGuid();

        client.DefaultRequestHeaders.Remove("X-Organization-Id");
        client.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            secondOrganizationId.ToString());

        var response = await client.GetAsync(
            $"/api/cases/{caseId}/documents/{documentId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
