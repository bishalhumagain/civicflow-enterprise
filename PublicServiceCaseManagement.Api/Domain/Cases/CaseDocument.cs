namespace PublicServiceCaseManagement.Api.Domain.Cases;

public sealed class CaseDocument
{
    private CaseDocument()
    {
    }

    private CaseDocument(
        Guid caseId,
        Guid organizationId,
        string originalFileName,
        string storageKey,
        string contentType,
        long sizeBytes,
        string sha256Hash,
        Guid? uploadedByUserId,
        DateTimeOffset uploadedAt)
    {
        Id = Guid.NewGuid();
        CaseId = caseId;
        OrganizationId = organizationId;
        OriginalFileName = originalFileName;
        StorageKey = storageKey;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        Sha256Hash = sha256Hash;
        UploadedByUserId = uploadedByUserId;
        UploadedAt = uploadedAt;
    }

    public Guid Id { get; private set; }

    public Guid CaseId { get; private set; }

    public Guid OrganizationId { get; private set; }

    public string OriginalFileName { get; private set; } = string.Empty;

    public string StorageKey { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long SizeBytes { get; private set; }

    public string Sha256Hash { get; private set; } = string.Empty;

    public Guid? UploadedByUserId { get; private set; }

    public DateTimeOffset UploadedAt { get; private set; }

    public static CaseDocument Create(
        Guid caseId,
        Guid organizationId,
        string originalFileName,
        string storageKey,
        string contentType,
        long sizeBytes,
        string sha256Hash,
        Guid? uploadedByUserId,
        DateTimeOffset uploadedAt)
    {
        if (caseId == Guid.Empty)
            throw new ArgumentException("Case ID is required.", nameof(caseId));

        if (organizationId == Guid.Empty)
            throw new ArgumentException(
                "Organization ID is required.",
                nameof(organizationId));

        if (string.IsNullOrWhiteSpace(originalFileName) ||
            originalFileName.Length > 255)
        {
            throw new ArgumentException(
                "The original file name is required and must not exceed 255 characters.",
                nameof(originalFileName));
        }

        if (string.IsNullOrWhiteSpace(storageKey) || storageKey.Length > 500)
            throw new ArgumentException("A storage key is required.", nameof(storageKey));

        if (string.IsNullOrWhiteSpace(contentType) || contentType.Length > 100)
            throw new ArgumentException("A content type is required.", nameof(contentType));

        if (sizeBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(sizeBytes));

        if (string.IsNullOrWhiteSpace(sha256Hash) || sha256Hash.Length != 64)
            throw new ArgumentException("A SHA-256 hash is required.", nameof(sha256Hash));

        return new CaseDocument(
            caseId,
            organizationId,
            originalFileName,
            storageKey,
            contentType,
            sizeBytes,
            sha256Hash,
            uploadedByUserId,
            uploadedAt);
    }
}
