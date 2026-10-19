namespace PublicServiceCaseManagement.Api.Infrastructure.Files;

public sealed record StoredFile(
    string StorageKey,
    long SizeBytes,
    string Sha256Hash);

public interface IFileStorage
{
    Task<StoredFile> SaveAsync(
        Guid organizationId,
        Guid caseId,
        string originalFileName,
        Stream content,
        CancellationToken cancellationToken);

    Task<Stream> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken);
}
