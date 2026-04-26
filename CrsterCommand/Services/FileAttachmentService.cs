using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CrsterCommand.Models;

namespace CrsterCommand.Services;

public class FileAttachmentService
{
    private readonly string _attachmentStorageDirectory;
    private readonly string[] _allowedExtensions = { ".pdf", ".mp3", ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".wav", ".m4a", ".aac" };
    private readonly long _maxFileSize = 100 * 1024 * 1024; // 100 MB

    public FileAttachmentService()
    {
        _attachmentStorageDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CrsterCommand",
            "Attachments"
        );

        if (!Directory.Exists(_attachmentStorageDirectory))
            Directory.CreateDirectory(_attachmentStorageDirectory);
    }

    public async Task<FileAttachment?> AddFileAsync(string sourceFilePath)
    {
        if (!File.Exists(sourceFilePath))
            return null;

        var fileName = Path.GetFileName(sourceFilePath);
        var extension = Path.GetExtension(sourceFilePath).ToLowerInvariant();

        // Validate file extension
        if (!IsAllowedFileType(extension))
            throw new InvalidOperationException($"File type '{extension}' is not allowed. Allowed types: {string.Join(", ", _allowedExtensions)}");

        var fileInfo = new FileInfo(sourceFilePath);

        // Validate file size
        if (fileInfo.Length > _maxFileSize)
            throw new InvalidOperationException($"File size exceeds maximum allowed size of {_maxFileSize / (1024 * 1024)} MB");

        try
        {
            var attachment = new FileAttachment
            {
                FileName = fileName,
                FileSizeBytes = fileInfo.Length,
                MimeType = GetMimeType(extension),
                AddedAt = DateTime.Now
            };

            // Store file with unique ID to avoid conflicts
            var storagePath = Path.Combine(_attachmentStorageDirectory, $"{attachment.Id}{extension}");
            attachment.FilePath = storagePath;

            // Copy file asynchronously
            using (var sourceStream = File.OpenRead(sourceFilePath))
            using (var destStream = File.Create(storagePath))
            {
                await sourceStream.CopyToAsync(destStream);
            }

            return attachment;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to add file attachment: {ex.Message}", ex);
        }
    }

    public async Task<bool> RemoveFileAsync(FileAttachment attachment)
    {
        try
        {
            if (File.Exists(attachment.FilePath))
            {
                File.Delete(attachment.FilePath);
            }
            return await Task.FromResult(true);
        }
        catch
        {
            return false;
        }
    }

    public async Task CleanupOrphanedFilesAsync(List<FileAttachment> activeAttachments)
    {
        try
        {
            var activeFilePaths = new HashSet<string>(activeAttachments.ConvertAll(a => a.FilePath));
            var directoryInfo = new DirectoryInfo(_attachmentStorageDirectory);

            foreach (var file in directoryInfo.GetFiles())
            {
                if (!activeFilePaths.Contains(file.FullName))
                {
                    try
                    {
                        file.Delete();
                    }
                    catch { }
                }
            }

            await Task.CompletedTask;
        }
        catch { }
    }

    public bool IsAllowedFileType(string extension)
    {
        return Array.Exists(_allowedExtensions, e => e.Equals(extension, StringComparison.OrdinalIgnoreCase));
    }

    public string GetMimeType(string extension)
    {
        var extLower = extension.ToLowerInvariant();
        return extLower switch
        {
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".webp" => "image/webp",
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".m4a" => "audio/mp4",
            ".aac" => "audio/aac",
            _ => "application/octet-stream"
        };
    }
}
