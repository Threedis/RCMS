using System.Security.Cryptography;
using Inventory.Common.Configuration;
using Inventory.Common.Exceptions;
using Inventory.Common.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Inventory.Services.Files;

/// <summary>Metadata returned after a file has been written to storage.</summary>
public sealed class StoredFile
{
    public required string OriginalFileName { get; init; }

    /// <summary>Randomised name on disk; never derived from user input.</summary>
    public required string StoredFileName { get; init; }

    /// <summary>Path relative to the storage root, e.g. <c>2026/08/abc123.pdf</c>.</summary>
    public required string RelativePath { get; init; }

    public required string ContentType { get; init; }

    public required long SizeBytes { get; init; }

    /// <summary>SHA-256 of the stored bytes, used for duplicate detection.</summary>
    public required string Checksum { get; init; }
}

/// <summary>
/// Writes and reads attachment files.
/// <para>
/// The stored name is a GUID, so a hostile file name can never traverse
/// directories or overwrite an existing file, and the resolved absolute path is
/// verified to sit inside the configured root before any read or delete.
/// </para>
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Validates and stores an uploaded file.
    /// </summary>
    /// <exception cref="ValidationException">
    /// The file is empty, too large, or has a disallowed extension.
    /// </exception>
    Task<StoredFile> SaveAsync(
        Stream content,
        string fileName,
        string? contentType,
        string? subFolder = null,
        CancellationToken cancellationToken = default);

    /// <summary>Opens a stored file for reading, or null when it no longer exists.</summary>
    Task<Stream?> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default);

    /// <summary>Deletes a stored file. Returns false when it was already gone.</summary>
    bool Delete(string relativePath);

    /// <summary>True when the extension and size are acceptable.</summary>
    bool IsAllowed(string fileName, long sizeBytes, out string? reason);
}

/// <inheritdoc cref="IFileStorageService" />
public sealed class FileStorageService : IFileStorageService
{
    private readonly FileStorageSettings _settings;
    private readonly ILogger<FileStorageService> _logger;
    private readonly string _root;

    public FileStorageService(
        IOptions<FileStorageSettings> settings,
        ILogger<FileStorageService> logger)
    {
        _settings = settings?.Value ?? new FileStorageSettings();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _root = Path.IsPathRooted(_settings.RootPath)
            ? _settings.RootPath
            : Path.Combine(AppContext.BaseDirectory, _settings.RootPath);

        Directory.CreateDirectory(_root);
    }

    /// <inheritdoc />
    public bool IsAllowed(string fileName, long sizeBytes, out string? reason)
    {
        if (sizeBytes <= 0)
        {
            reason = "The file is empty.";
            return false;
        }

        var maxBytes = (long)_settings.MaxFileSizeMb * 1024 * 1024;

        if (sizeBytes > maxBytes)
        {
            reason = $"The file is larger than the {_settings.MaxFileSizeMb} MB limit.";
            return false;
        }

        var extension = Path.GetExtension(fileName)?.ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(extension)
            || !_settings.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            reason = $"Files of type '{extension}' are not accepted. " +
                     $"Allowed types: {string.Join(", ", _settings.AllowedExtensions)}.";
            return false;
        }

        reason = null;
        return true;
    }

    /// <inheritdoc />
    public async Task<StoredFile> SaveAsync(
        Stream content,
        string fileName,
        string? contentType,
        string? subFolder = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ValidationException("The upload has no file name.");
        }

        var length = content.CanSeek ? content.Length : 0;

        if (!IsAllowed(fileName, length > 0 ? length : 1, out var reason))
        {
            throw new ValidationException(reason!);
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        // Date-partitioned folders keep any single directory small enough for
        // the file system to stay responsive as the archive grows.
        var folder = subFolder.IsBlank()
            ? Path.Combine(DateTime.UtcNow.Year.ToString("0000"), DateTime.UtcNow.Month.ToString("00"))
            : Path.Combine(subFolder!.ToSafeFileName(), DateTime.UtcNow.Year.ToString("0000"));

        var absoluteFolder = Path.Combine(_root, folder);
        Directory.CreateDirectory(absoluteFolder);

        var storedName = $"{Guid.NewGuid():N}{extension}";
        var absolutePath = Path.Combine(absoluteFolder, storedName);

        long written;
        string checksum;

        await using (var target = new FileStream(
                         absolutePath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                         bufferSize: 81920, useAsync: true))
        {
            if (content.CanSeek)
            {
                content.Position = 0;
            }

            await content.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
            written = target.Length;
        }

        // Re-read to hash: this also confirms the file is genuinely on disk.
        await using (var verify = new FileStream(
                         absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                         bufferSize: 81920, useAsync: true))
        {
            var hash = await SHA256.HashDataAsync(verify, cancellationToken).ConfigureAwait(false);
            checksum = Convert.ToHexString(hash);
        }

        if (written > (long)_settings.MaxFileSizeMb * 1024 * 1024)
        {
            // A non-seekable stream hid the real size until now: undo the write.
            File.Delete(absolutePath);
            throw new ValidationException($"The file is larger than the {_settings.MaxFileSizeMb} MB limit.");
        }

        _logger.LogInformation("Stored attachment {StoredName} ({Bytes} bytes).", storedName, written);

        return new StoredFile
        {
            OriginalFileName = Path.GetFileName(fileName).ToSafeFileName(),
            StoredFileName = storedName,
            RelativePath = Path.Combine(folder, storedName).Replace('\\', '/'),
            ContentType = ResolveContentType(contentType, extension),
            SizeBytes = written,
            Checksum = checksum
        };
    }

    /// <inheritdoc />
    public Task<Stream?> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var absolute = ResolveInsideRoot(relativePath);

        if (absolute is null || !File.Exists(absolute))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(
            absolute, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 81920, useAsync: true);

        return Task.FromResult<Stream?>(stream);
    }

    /// <inheritdoc />
    public bool Delete(string relativePath)
    {
        var absolute = ResolveInsideRoot(relativePath);

        if (absolute is null || !File.Exists(absolute))
        {
            return false;
        }

        try
        {
            File.Delete(absolute);
            return true;
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Could not delete stored file {Path}.", relativePath);
            return false;
        }
    }

    /// <summary>
    /// Resolves a relative path to an absolute one and rejects anything that
    /// escapes the storage root — the guard against path traversal.
    /// </summary>
    private string? ResolveInsideRoot(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        var candidate = Path.GetFullPath(Path.Combine(_root, relativePath));
        var rootFull = Path.GetFullPath(_root);

        if (!candidate.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Rejected attempt to access {Path} outside the storage root.", relativePath);
            return null;
        }

        return candidate;
    }

    private static string ResolveContentType(string? supplied, string extension)
    {
        if (!string.IsNullOrWhiteSpace(supplied))
        {
            return supplied;
        }

        return extension switch
        {
            ".pdf" => "application/pdf",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            _ => "application/octet-stream"
        };
    }
}
