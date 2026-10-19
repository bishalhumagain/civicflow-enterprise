using Microsoft.EntityFrameworkCore;
using PublicServiceCaseManagement.Api.Domain.Cases;
using PublicServiceCaseManagement.Api.Infrastructure.Files;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;

namespace PublicServiceCaseManagement.Api.Features.Cases;

public sealed class CaseDocumentService(
    CivicFlowDbContext dbContext,
    IFileStorage fileStorage) : ICaseDocumentService
{
    private const long MaximumFileSizeBytes = 10 * 1024 * 1024;

    private static readonly IReadOnlyDictionary<string, string> AllowedContentTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = "application/pdf",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png",
            [".doc"] = "application/msword",
            [".docx"] =
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
        };

    public async Task<CaseDocument> UploadAsync(
        Guid organizationId,
        Guid caseId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var caseExists = await dbContext.Cases
            .AnyAsync(
                item => item.Id == caseId &&
                        item.OrganizationId == organizationId,
                cancellationToken);

        if (!caseExists)
            throw new KeyNotFoundException("The case does not exist.");

        Validate(file);

        await using var content = file.OpenReadStream();
        var storedFile = await fileStorage.SaveAsync(
            organizationId,
            caseId,
            file.FileName,
            content,
            cancellationToken);

        var document = CaseDocument.Create(
            caseId,
            organizationId,
            Path.GetFileName(file.FileName),
            storedFile.StorageKey,
            file.ContentType,
            storedFile.SizeBytes,
            storedFile.Sha256Hash,
            uploadedByUserId: null,
            DateTimeOffset.UtcNow);

        dbContext.CaseDocuments.Add(document);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            try
            {
                await fileStorage.DeleteAsync(
                    storedFile.StorageKey,
                    CancellationToken.None);
            }
            catch
            {
                // Preserve the original persistence failure. Storage cleanup is
                // best-effort and should be observable through operational logs.
            }

            throw;
        }

        return document;
    }

    public async Task<IReadOnlyList<CaseDocument>> GetAllAsync(
        Guid organizationId,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        var caseExists = await dbContext.Cases
            .AnyAsync(
                item => item.Id == caseId &&
                        item.OrganizationId == organizationId,
                cancellationToken);

        if (!caseExists)
            throw new KeyNotFoundException("The case does not exist.");

        return await dbContext.CaseDocuments
            .AsNoTracking()
            .Where(document =>
                document.CaseId == caseId &&
                document.OrganizationId == organizationId)
            .OrderBy(document => document.UploadedAt)
            .ThenBy(document => document.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<CaseDocument?> GetByIdAsync(
        Guid organizationId,
        Guid caseId,
        Guid documentId,
        CancellationToken cancellationToken) =>
        dbContext.CaseDocuments
            .AsNoTracking()
            .SingleOrDefaultAsync(
                document =>
                    document.Id == documentId &&
                    document.CaseId == caseId &&
                    document.OrganizationId == organizationId,
                cancellationToken);

    private static void Validate(IFormFile file)
    {
        if (file is null || file.Length <= 0)
            throw new InvalidDataException("The file cannot be empty.");

        if (file.Length > MaximumFileSizeBytes)
            throw new InvalidDataException(
                "The file exceeds the maximum allowed size of 10 MB.");

        var fileName = Path.GetFileName(file.FileName);

        if (!string.Equals(fileName, file.FileName, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(fileName) ||
            fileName.Length > 255)
        {
            throw new InvalidDataException("The file name is invalid.");
        }

        var extension = Path.GetExtension(fileName);

        if (!AllowedContentTypes.TryGetValue(extension, out var contentType) ||
            !string.Equals(file.ContentType, contentType, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Only PDF, JPEG, PNG, DOC, and DOCX files are supported with matching content types.");
        }
    }
}
