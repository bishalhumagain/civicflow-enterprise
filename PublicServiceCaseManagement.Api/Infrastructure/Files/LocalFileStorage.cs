using System.Security.Cryptography;

namespace PublicServiceCaseManagement.Api.Infrastructure.Files;

public sealed class LocalFileStorage(
    IConfiguration configuration,
    IHostEnvironment environment) : IFileStorage
{
    private const long MaximumFileSizeBytes = 10 * 1024 * 1024;

    public async Task<StoredFile> SaveAsync(
        Guid organizationId,
        Guid caseId,
        string originalFileName,
        Stream content,
        CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException("Organization ID is required.", nameof(organizationId));

        if (caseId == Guid.Empty)
            throw new ArgumentException("Case ID is required.", nameof(caseId));

        if (content is null)
            throw new ArgumentNullException(nameof(content));

        var safeFileName = Path.GetFileName(originalFileName);

        if (!string.Equals(safeFileName, originalFileName, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(safeFileName))
        {
            throw new InvalidDataException("The file name is invalid.");
        }

        if (safeFileName.Contains('\\') ||
            safeFileName.Contains('\0') ||
            safeFileName.Length > 255)
            throw new InvalidDataException("The file name is invalid.");

        var extension = Path.GetExtension(safeFileName);
        if (extension.Length > 13 ||
            extension.Length < 2 ||
            extension[1..].Any(character =>
                !char.IsAsciiLetterOrDigit(character)))
        {
            extension = ".bin";
        }

        var temporaryDirectory = GetRootDirectory();
        Directory.CreateDirectory(temporaryDirectory);

        var temporaryPath = Path.Combine(
            temporaryDirectory,
            $".{Guid.NewGuid():N}.uploading");

        try
        {
            long sizeBytes = 0;
            string hashText;
            string storageKey;
            string destinationPath;

            await using (var temporaryStream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                var buffer = new byte[64 * 1024];
                int bytesRead;

                while ((bytesRead = await content.ReadAsync(
                           buffer,
                           cancellationToken)) > 0)
                {
                    sizeBytes += bytesRead;

                    if (sizeBytes > MaximumFileSizeBytes)
                        throw new InvalidDataException(
                            "The file exceeds the maximum allowed size of 10 MB.");

                    await temporaryStream.WriteAsync(
                        buffer.AsMemory(0, bytesRead),
                        cancellationToken);
                    hash.AppendData(buffer, 0, bytesRead);
                }

                if (sizeBytes == 0)
                    throw new InvalidDataException("The file cannot be empty.");

                hashText = Convert.ToHexString(hash.GetHashAndReset())
                    .ToLowerInvariant();

                storageKey =
                    $"{organizationId:N}/{caseId:N}/{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
                destinationPath = GetSafePath(storageKey);
                await temporaryStream.FlushAsync(cancellationToken);
            }

            // Move only after the write stream has been disposed so this remains
            // portable across platforms that do not permit moving open files.
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Move(temporaryPath, destinationPath);

            return new StoredFile(storageKey, sizeBytes, hashText);
        }
        catch
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);

            throw;
        }
    }

    public Task<Stream> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken)
    {
        var path = GetSafePath(storageKey);

        if (!File.Exists(path))
            throw new FileNotFoundException("The stored document was not found.");

        Stream stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        return Task.FromResult(stream);
    }

    public Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken)
    {
        var path = GetSafePath(storageKey);

        if (File.Exists(path))
            File.Delete(path);

        return Task.CompletedTask;
    }

    private string GetRootDirectory()
    {
        var configuredPath = configuration["FileStorage:RootPath"];

        return string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(environment.ContentRootPath, "App_Data", "uploads")
            : Path.GetFullPath(Path.Combine(environment.ContentRootPath, configuredPath));
    }

    private string GetSafePath(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey) ||
            Path.IsPathRooted(storageKey) ||
            storageKey.Contains("..", StringComparison.Ordinal) ||
            storageKey.Contains('\\'))
        {
            throw new InvalidDataException("The storage key is invalid.");
        }

        var root = Path.GetFullPath(GetRootDirectory());
        var path = Path.GetFullPath(Path.Combine(root, storageKey));

        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidDataException("The storage key is invalid.");

        return path;
    }
}
