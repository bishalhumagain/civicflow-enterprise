using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Text;
using PublicServiceCaseManagement.Api.Domain.Cases;
using PublicServiceCaseManagement.Api.Infrastructure.Files;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;

namespace PublicServiceCaseManagement.Api.Features.Cases;

public sealed class CaseDocumentService(
    CivicFlowDbContext dbContext,
    IFileStorage fileStorage) : ICaseDocumentService
{
    private const long MaximumFileSizeBytes = 10 * 1024 * 1024;

    public async Task<CaseDocument> UploadAsync(
        Guid organizationId,
        Guid caseId,
        IFormFile file,
        CancellationToken cancellationToken,
        Guid? uploadedByUserId = null,
        Guid? authorizedStaffMemberId = null,
        bool isVisibleToCitizen = false)
    {
        var caseExists = await dbContext.Cases
            .AnyAsync(
                item => item.Id == caseId &&
                        item.OrganizationId == organizationId &&
                        (authorizedStaffMemberId == null ||
                         (item.AssignedToStaffMemberId == authorizedStaffMemberId &&
                          item.AssignedRole == CaseAssignmentRole.CaseWorker)),
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
            NormalizeContentType(file.ContentType),
            storedFile.SizeBytes,
            storedFile.Sha256Hash,
            uploadedByUserId,
            DateTimeOffset.UtcNow,
            isVisibleToCitizen);

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

    public async Task<IReadOnlyList<CaseDocument>> GetVisibleForCitizenAsync(
        Guid caseId,
        Guid citizenUserId,
        CancellationToken cancellationToken)
    {
        var ownsCase = await dbContext.Cases
            .AnyAsync(
                item => item.Id == caseId &&
                        item.SubmittedByUserId == citizenUserId,
                cancellationToken);

        if (!ownsCase)
        {
            throw new KeyNotFoundException("The case does not exist.");
        }

        return await dbContext.CaseDocuments
            .AsNoTracking()
            .Where(document =>
                document.CaseId == caseId &&
                document.IsVisibleToCitizen)
            .OrderBy(document => document.UploadedAt)
            .ThenBy(document => document.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<CaseDocument?> GetVisibleByIdForCitizenAsync(
        Guid caseId,
        Guid documentId,
        Guid citizenUserId,
        CancellationToken cancellationToken) =>
        await dbContext.CaseDocuments
            .AsNoTracking()
            .Where(document =>
                document.Id == documentId &&
                document.CaseId == caseId &&
                document.IsVisibleToCitizen &&
                dbContext.Cases.Any(caseEntity =>
                    caseEntity.Id == caseId &&
                    caseEntity.SubmittedByUserId == citizenUserId))
            .SingleOrDefaultAsync(cancellationToken);

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

    public static void ValidateUpload(IFormFile file)
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

        if (fileName.Contains('\\') || fileName.Contains('\0'))
        {
            throw new InvalidDataException("The file metadata is invalid.");
        }
    }

    private static void Validate(IFormFile file)
    {
        ValidateUpload(file);
        ValidateFileNameDetails(file);
        ValidateSignature(file);
    }

    private static void ValidateSignature(IFormFile file)
    {
        var mediaType = GetMediaType(file.ContentType);

        if (mediaType is null)
        {
            return;
        }

        var header = ReadHeader(file);

        switch (mediaType)
        {
            case "application/pdf":
                RequireSignature(
                    "application/pdf",
                    IndexOfAscii(header, "%PDF-") >= 0);
                break;
            case "image/png":
                RequireSignature(
                    "image/png",
                    StartsWith(
                        header,
                        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }));
                break;
            case "image/jpeg":
                RequireSignature(
                    "image/jpeg",
                    StartsWith(header, new byte[] { 0xFF, 0xD8, 0xFF }));
                break;
            case "image/gif":
                RequireSignature(
                    "image/gif",
                    IndexOfAscii(header, "GIF87a") == 0 ||
                    IndexOfAscii(header, "GIF89a") == 0);
                break;
        }
    }

    private static void RequireSignature(string declaredType, bool matches)
    {
        if (!matches)
        {
            throw new InvalidDataException(
                "The file content does not match the declared type " +
                declaredType + ".");
        }
    }

    private static string? GetMediaType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType) ||
            contentType.Length > 100 ||
            !MediaTypeHeaderValue.TryParse(contentType, out var parsed))
        {
            return null;
        }

        return parsed.MediaType?.ToLowerInvariant();
    }

    private static void ValidateFileNameDetails(IFormFile file)
    {
        var fileName = Path.GetFileName(file.FileName);

        if (fileName.Length == 0 ||
            fileName == "." ||
            fileName == ".." ||
            fileName.EndsWith((char)32) ||
            fileName.EndsWith((char)46))
        {
            throw new InvalidDataException("The file name is invalid.");
        }
    }

    private static byte[] ReadHeader(IFormFile file)
    {
        var length = (int)Math.Min(file.Length, 1024);
        var buffer = new byte[length];
        var offset = 0;

        using var stream = file.OpenReadStream();

        while (offset < length)
        {
            var read = stream.Read(buffer, offset, length - offset);

            if (read == 0)
            {
                break;
            }

            offset += read;
        }

        return offset == length ? buffer : buffer[..offset];
    }

    private static bool StartsWith(byte[] header, byte[] signature) =>
        header.Length >= signature.Length &&
        header.AsSpan(0, signature.Length).SequenceEqual(signature);

    private static int IndexOfAscii(byte[] header, string value) =>
        header.AsSpan().IndexOf(Encoding.ASCII.GetBytes(value));

    private static string NormalizeContentType(string? contentType) =>
        !string.IsNullOrWhiteSpace(contentType) &&
        contentType.Length <= 100 &&
        MediaTypeHeaderValue.TryParse(contentType, out _)
            ? contentType
            : "application/octet-stream";
}
