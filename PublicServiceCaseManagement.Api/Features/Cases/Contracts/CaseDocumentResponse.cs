namespace PublicServiceCaseManagement.Api.Features.Cases.Contracts;

public sealed record CaseDocumentResponse(
    Guid Id,
    Guid CaseId,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    string Sha256Hash,
    DateTimeOffset UploadedAt,
    bool IsVisibleToCitizen);
