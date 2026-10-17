using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PublicServiceCaseManagement.Tests;

public class CasesApiIntegrationTests
{
    [Fact]
    public async Task GetStatusHistory_WhenCaseDoesNotExist_ReturnsNotFound()
    {
        await using var factory = new CivicFlowApiFactory();
        using var client = factory.CreateClient();

        var missingCaseId = Guid.NewGuid();

        var response = await client.GetAsync(
            $"/api/cases/{missingCaseId}/history");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateCase_ChangeStatus_AndGetHistory_ReturnsRecordedTransition()
    {
        await using var factory = new CivicFlowApiFactory();
        using var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync(
            "/api/cases",
            new
            {
                title = "Integration test case",
                description = "Verify the complete case lifecycle."
            });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var createdJson = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());

        var caseId = createdJson.RootElement
            .GetProperty("id")
            .GetGuid();

        var statusResponse = await client.PatchAsJsonAsync(
            $"/api/cases/{caseId}/status",
            new { newStatus = "UnderReview" });

        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);

        var historyResponse = await client.GetAsync(
            $"/api/cases/{caseId}/history");

        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);

        using var historyJson = JsonDocument.Parse(
            await historyResponse.Content.ReadAsStringAsync());

        var historyEntries = historyJson.RootElement;

        Assert.Equal(JsonValueKind.Array, historyEntries.ValueKind);

        var historyEntry = Assert.Single(
            historyEntries.EnumerateArray());

        Assert.Equal(
            "Submitted",
            historyEntry.GetProperty("previousStatus").GetString());

        Assert.Equal(
            "UnderReview",
            historyEntry.GetProperty("newStatus").GetString());
    }

    [Theory]
    [InlineData("", "A sufficiently long description for testing.")]
    [InlineData("AB", "A sufficiently long description for testing.")]
    [InlineData("A valid title", "Too short")]
    public async Task CreateCase_WithInvalidInput_ReturnsBadRequest(
        string title,
        string description)
    {
        await using var factory = new CivicFlowApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/cases",
            new { title, description });

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task CreateCase_WithTitleLongerThan200Characters_ReturnsBadRequest()
    {
        await using var factory = new CivicFlowApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/cases",
            new
            {
                title = new string('A', 201),
                description = "A sufficiently long description for testing."
            });

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task ChangeStatus_WhenTransitionIsInvalid_ReturnsBadRequestAndDoesNotCreateHistory()
    {
        await using var factory = new CivicFlowApiFactory();
        using var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync(
            "/api/cases",
            new
            {
                title = "Invalid transition test",
                description = "Verify invalid status transitions."
            });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var createdJson = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());

        var caseId = createdJson.RootElement
            .GetProperty("id")
            .GetGuid();

        var changeResponse = await client.PatchAsJsonAsync(
            $"/api/cases/{caseId}/status",
            new { newStatus = "Closed" });

        Assert.Equal(
            HttpStatusCode.BadRequest,
            changeResponse.StatusCode);

        var getCaseResponse = await client.GetAsync(
            $"/api/cases/{caseId}");

        Assert.Equal(HttpStatusCode.OK, getCaseResponse.StatusCode);

        using var caseJson = JsonDocument.Parse(
            await getCaseResponse.Content.ReadAsStringAsync());

        Assert.Equal(
            "Submitted",
            caseJson.RootElement.GetProperty("status").GetString());

        var historyResponse = await client.GetAsync(
            $"/api/cases/{caseId}/history");

        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);

        using var historyJson = JsonDocument.Parse(
            await historyResponse.Content.ReadAsStringAsync());

        Assert.Empty(historyJson.RootElement.EnumerateArray());
    }

}