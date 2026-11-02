namespace PublicServiceCaseManagement.Api.Features.Cases.Contracts;

/// <summary>Authorized document metadata; storage keys and filesystem paths are not exposed.</summary>
public sealed record CaseDocumentResponse(
    Guid Id,
    Guid CaseId,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    string Sha256Hash,
    DateTimeOffset UploadedAt,
    bool IsVisibleToCitizen);