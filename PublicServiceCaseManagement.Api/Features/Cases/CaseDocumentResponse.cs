namespace PublicServiceCaseManagement.Api.Features.Cases;

public sealed record CaseDocumentResponse(
    Guid Id,
    Guid CaseId,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    string Sha256Hash,
    DateTimeOffset UploadedAt,
    bool IsVisibleToCitizen);
