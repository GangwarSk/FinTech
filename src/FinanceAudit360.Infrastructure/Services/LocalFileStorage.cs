using System.Security.Cryptography;
using FinanceAudit360.Application.Common.Interfaces;
using FinanceAudit360.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace FinanceAudit360.Infrastructure.Services;

/// <summary>
/// Stores uploads under a configured root, partitioned by yyyy/MM. File names are generated,
/// never taken from the client, and every resolved path is verified to stay inside the root.
/// </summary>
public sealed class LocalFileStorage : IFileStorage
{
    private readonly FileStorageOptions _options;
    private readonly string _root;

    public LocalFileStorage(IOptions<FileStorageOptions> options)
    {
        _options = options.Value;
        _root = Path.GetFullPath(Path.IsPathRooted(_options.RootPath)
            ? _options.RootPath
            : Path.Combine(AppContext.BaseDirectory, _options.RootPath));

        Directory.CreateDirectory(_root);
    }

    public async Task<StoredFile> SaveAsync(
        Stream content,
        string originalFileName,
        string subFolder,
        CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        if (!_options.AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException($"File type '{extension}' is not allowed.");
        }

        var now = DateTime.UtcNow;
        var relativeFolder = Path.Combine(SanitizeSegment(subFolder), now.ToString("yyyy"), now.ToString("MM"));
        var absoluteFolder = ResolveWithinRoot(relativeFolder);
        Directory.CreateDirectory(absoluteFolder);

        var storedFileName = $"{Guid.CreateVersion7():N}{extension}";
        var relativePath = Path.Combine(relativeFolder, storedFileName).Replace('\\', '/');
        var absolutePath = Path.Combine(absoluteFolder, storedFileName);

        if (content.CanSeek)
        {
            content.Position = 0;
        }

        await using (var target = File.Create(absolutePath))
        {
            await content.CopyToAsync(target, cancellationToken);
        }

        var fileInfo = new FileInfo(absolutePath);
        if (fileInfo.Length > _options.MaxFileSizeBytes)
        {
            File.Delete(absolutePath);
            throw new InvalidOperationException("The uploaded file exceeds the configured size limit.");
        }

        string hash;
        await using (var readStream = File.OpenRead(absolutePath))
        {
            hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(readStream, cancellationToken));
        }

        return new StoredFile(storedFileName, relativePath, fileInfo.Length, hash);
    }

    public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var absolutePath = ResolveWithinRoot(relativePath);
        if (!File.Exists(absolutePath))
        {
            throw new FileNotFoundException("The stored file no longer exists.", relativePath);
        }

        return Task.FromResult<Stream>(File.OpenRead(absolutePath));
    }

    public Task<bool> ExistsAsync(string relativePath, CancellationToken cancellationToken = default) =>
        Task.FromResult(File.Exists(ResolveWithinRoot(relativePath)));

    public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var absolutePath = ResolveWithinRoot(relativePath);
        if (File.Exists(absolutePath))
        {
            File.Delete(absolutePath);
        }

        return Task.CompletedTask;
    }

    public async Task WriteTextAsync(string relativePath, string content, CancellationToken cancellationToken = default)
    {
        var absolutePath = ResolveWithinRoot(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
        await File.WriteAllTextAsync(absolutePath, content, cancellationToken);
    }

    public string GetAbsolutePath(string relativePath) => ResolveWithinRoot(relativePath);

    /// <summary>Blocks path traversal: the combined path must remain under the storage root.</summary>
    private string ResolveWithinRoot(string relativePath)
    {
        var combined = Path.GetFullPath(Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

        if (!combined.StartsWith(_root, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("The resolved path is outside the storage root.");
        }

        return combined;
    }

    private static string SanitizeSegment(string segment)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(segment.Where(c => !invalid.Contains(c)).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "misc" : cleaned;
    }
}
