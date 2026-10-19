namespace PublicServiceCaseManagement.Api.Features.Cases;

public sealed record CaseDocumentResponse(
    Guid Id,
    Guid CaseId,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Sha256Hash,
    DateTimeOffset UploadedAt);
