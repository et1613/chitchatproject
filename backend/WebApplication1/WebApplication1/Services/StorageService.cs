using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.IO.Compression;
using System.Threading;
using Microsoft.Extensions.Caching.Memory;
using WebApplication1.Models.Messages;
using WebApplication1.Models.Enums;
using WebApplication1.Models.Users;
using WebApplication1.Models.Chat;
using WebApplication1.Models.Notifications;
using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.Fonts;
using System.Numerics;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats;
using System.Drawing.Imaging;
using SixLabors.ImageSharp.PixelFormats;
using Newtonsoft.Json.Linq;

namespace WebApplication1.Services
{
    public class StorageOptions
    {
        public string UploadDirectory { get; set; } = "Uploads";
        public long MaxFileSize { get; set; } = 100 * 1024 * 1024; // 100MB
        public string[] AllowedFileTypes { get; set; } = new[] { ".jpg", ".jpeg", ".png", ".gif", ".pdf", ".doc", ".docx", ".txt" };
        public int ChunkSize { get; set; } = 1024 * 1024; // 1MB
        public bool EnableCompression { get; set; } = true;
        public bool GenerateThumbnails { get; set; } = true;
        public int ThumbnailWidth { get; set; } = 200;
        public int ThumbnailHeight { get; set; } = 200;
        public int CacheExpirationMinutes { get; set; } = 30;
    }

    public class FileMetadata
    {
        public required string FileName { get; set; }
        public required string ContentType { get; set; }
        public long FileSize { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastModified { get; set; }
        public required  string Hash { get; set; }
        public required Dictionary<string, string> CustomMetadata { get; set; }
    }

    public class StorageService : IStorageService
    {
        private readonly string _uploadDirectory;
        private readonly StorageOptions _options;
        private readonly ILogger<StorageService> _logger;
        private readonly IMemoryCache _cache;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private readonly Dictionary<string, long> _downloadedBytes = new Dictionary<string, long>();
        private readonly Dictionary<string, bool> _downloadComplete = new Dictionary<string, bool>();
        private readonly Dictionary<string, CancellationTokenSource> _downloadCancellations = new Dictionary<string, CancellationTokenSource>();

        public StorageService(
            IConfiguration configuration,
            ILogger<StorageService> logger,
            IMemoryCache cache)
        {
            _options = configuration.GetSection("Storage").Get<StorageOptions>() ?? new StorageOptions();
            _uploadDirectory = Path.Combine(Directory.GetCurrentDirectory(), _options.UploadDirectory);
            _logger = logger;
            _cache = cache;

            if (!Directory.Exists(_uploadDirectory))
            {
                Directory.CreateDirectory(_uploadDirectory);
            }
        }

        public async Task<Dictionary<string, long>> GetOptimizationSavingsAsync(string id)
        {
            // Gerçek bir hesaplama yerine örnek veri döndürüyoruz
            var result = new Dictionary<string, long>
        {
            { "OriginalSize", 5000 },
            { "OptimizedSize", 3200 },
            { "Saved", 1800 }
        };

            return await Task.FromResult(result);
        }

        public async Task<FileMetadata> GetFileMetadataAsync(string fileUrl)
        {
            try
            {
                var fileName = Path.GetFileName(fileUrl);
                var metadataPath = GetMetadataPath(fileName);

                if (!File.Exists(metadataPath))
                    throw new FileNotFoundException("Metadata file not found", metadataPath);

                var json = await File.ReadAllTextAsync(metadataPath);
                var metadata = JsonSerializer.Deserialize<FileMetadata>(json);

                if (metadata == null)
                    throw new InvalidOperationException("Failed to deserialize metadata");

                // Validate required properties
                if (string.IsNullOrEmpty(metadata.FileName))
                    throw new InvalidOperationException("FileName is required");
                if (string.IsNullOrEmpty(metadata.ContentType))
                    throw new InvalidOperationException("ContentType is required");
                if (string.IsNullOrEmpty(metadata.Hash))
                    throw new InvalidOperationException("Hash is required");
                if (metadata.CustomMetadata == null)
                    throw new InvalidOperationException("CustomMetadata is required");

                return metadata;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting file metadata: {FileUrl}", fileUrl);
                throw;
            }
        }

        public async Task<Dictionary<string, bool>> GetComplianceStatusAsync(string id)
        {
            try
            {
                var filePath = GetFilePathFromUrl(id);
                if (!File.Exists(filePath))
                    throw new FileNotFoundException("File not found", id);

                var complianceStatus = new Dictionary<string, bool>
                {
                    { "GDPR", await CheckGDPRComplianceAsync(id) },
                    { "HIPAA", await CheckHIPAAComplianceAsync(id) },
                    { "DataRetention", true }, // Varsayılan olarak true
                    { "DataEncryption", true } // Varsayılan olarak true
                };

                return complianceStatus;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting compliance status for file: {FileId}", id);
                throw;
            }
        }
        private IImageFormat GetImageFormat(string targetFormat)
        {
            return targetFormat.ToLowerInvariant() switch
            {
                ".jpeg" or ".jpg" => JpegFormat.Instance,
                ".png" => PngFormat.Instance,
                ".gif" => GifFormat.Instance,
                ".bmp" => BmpFormat.Instance,
                _ => throw new ArgumentException($"Unsupported image format: {targetFormat}")
            };
        }

        public async Task<List<Message>> LoadMessagesAsync(string id)
        {
            try
            {
                var backupPath = Path.Combine(_uploadDirectory, "messages", id);
                if (!File.Exists(backupPath))
                    throw new FileNotFoundException($"Message backup not found: {id}");

                var json = await File.ReadAllTextAsync(backupPath);
                var messages = System.Text.Json.JsonSerializer.Deserialize<List<Message>>(json);
                
                if (messages == null)
                    return new List<Message>();
                    
                _logger.LogInformation("Messages loaded successfully from: {BackupPath}", backupPath);
                return messages;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading messages from: {BackupPath}", id);
                throw;
            }
        }

        public async Task<List<Message>> ImportMessagesAsync(byte[] data, ImportFormat format)
        {
            try
            {
                switch (format)
                {
                    case ImportFormat.Json:
                        var json = System.Text.Encoding.UTF8.GetString(data);
                        var messages = System.Text.Json.JsonSerializer.Deserialize<List<Message>>(json);
                        return messages ?? new List<Message>();
                        
                    case ImportFormat.Csv:
                        var csv = System.Text.Encoding.UTF8.GetString(data);
                        var lines = csv.Split('\n').Skip(1); // Skip header
                        var messageList = new List<Message>();
                        
                        await Task.Run(() =>
                        {
                            foreach (var line in lines)
                            {
                                if (string.IsNullOrWhiteSpace(line)) continue;
                                
                                var parts = line.Split(',');
                                if (parts.Length >= 5)
                                {
                                    // Create temporary User and ChatRoom objects for navigation properties
                                    var sender = new User
                                    {
                                        Id = parts[3],
                                        UserName = $"ImportedUser_{parts[3]}",
                                        Email = $"imported_{parts[3]}@example.com",
                                        CreatedAt = DateTime.UtcNow
                                    };

                                    var chatRoom = new ChatRoom
                                    {
                                        Id = parts[4],
                                        Name = $"ImportedChat_{parts[4]}",
                                        CreatedAt = DateTime.UtcNow,
                                        AdminId = parts[3],
                                        Admin = sender
                                    };

                                    var message = new Message
                                    {
                                        Id = parts[0],
                                        Content = parts[1],
                                        Timestamp = DateTime.Parse(parts[2]),
                                        SenderId = parts[3],
                                        Sender = sender,
                                        ChatRoomId = parts[4],
                                        ChatRoom = chatRoom,
                                        IsRead = false,
                                        IsEdited = false,
                                        IsDeleted = false,
                                        Status = MessageStatus.Sent,
                                        Type = MessageType.Text,
                                        EditCount = 0,
                                        ReactionsJson = "{}",
                                        HiddenForUsers = new List<string>(),
                                        Attachments = new List<Attachment>(),
                                        EditHistory = new List<MessageHistory>(),
                                        Replies = new List<Message>(),
                                        Notifications = new List<Notification>()
                                    };
                                    messageList.Add(message);
                                }
                            }
                        });
                        
                        return messageList;
                        
                    default:
                        throw new ArgumentException($"Unsupported import format: {format}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing messages in format: {Format}", format);
                throw;
            }
        }
        public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType)
        {
            try
            {
                await ValidateFileAsync(fileStream, fileName, contentType);

                var sanitizedFileName = SanitizeFileName(fileName);
                var uniqueFileName = $"{Guid.NewGuid()}_{sanitizedFileName}";
                var filePath = Path.Combine(_uploadDirectory, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await fileStream.CopyToAsync(stream);
                }

                if (_options.GenerateThumbnails && IsImageFile(contentType))
                {
                    await GenerateThumbnailAsync(filePath);
                }

                var metadata = await CreateFileMetadataAsync(filePath, fileName, contentType);
                await SaveMetadataAsync(uniqueFileName, metadata);

                return $"/uploads/{uniqueFileName}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya yüklenirken hata oluştu: {FileName}", fileName);
                throw new Exception($"Dosya yüklenirken hata oluştu: {ex.Message}", ex);
            }
        }

        public async Task<string> UploadFileInChunksAsync(IFormFile file, string fileName, string contentType, CancellationToken cancellationToken = default)
        {
            try
            {
                var sanitizedFileName = SanitizeFileName(fileName);
                var uniqueFileName = $"{Guid.NewGuid()}_{sanitizedFileName}";
                var filePath = Path.Combine(_uploadDirectory, uniqueFileName);
                var tempPath = Path.Combine(_uploadDirectory, $"temp_{uniqueFileName}");

                using (var stream = new FileStream(tempPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream, cancellationToken);
                }

                using (var fileStream = new FileStream(tempPath, FileMode.Open))
                {
                    await ValidateFileAsync(fileStream, fileName, contentType);
                }

                File.Move(tempPath, filePath);

                if (_options.GenerateThumbnails && IsImageFile(contentType))
                {
                    await GenerateThumbnailAsync(filePath);
                }

                var metadata = await CreateFileMetadataAsync(filePath, fileName, contentType);
                await SaveMetadataAsync(uniqueFileName, metadata);

                return $"/uploads/{uniqueFileName}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya chunk yüklenirken hata oluştu: {FileName}", fileName);
                throw new Exception($"Dosya yüklenirken hata oluştu: {ex.Message}", ex);
            }
        }

        public async Task<bool> DeleteFileAsync(string fileUrl, bool permanent = false)
        {
            try
            {
                if (string.IsNullOrEmpty(fileUrl)) return false;

                var fileName = Path.GetFileName(fileUrl);
                var filePath = Path.Combine(_uploadDirectory, fileName);
                var thumbnailPath = GetThumbnailPath(filePath);
                var metadataPath = GetMetadataPath(fileName);

                await _semaphore.WaitAsync();
                try
                {
                    if (permanent)
                    {
                        // Kalıcı silme işlemi
                        if (File.Exists(filePath))
                        {
                            await Task.Run(() => File.Delete(filePath));
                        }

                        if (File.Exists(thumbnailPath))
                        {
                            await Task.Run(() => File.Delete(thumbnailPath));
                        }

                        if (File.Exists(metadataPath))
                        {
                            await Task.Run(() => File.Delete(metadataPath));
                        }
                    }
                    else
                    {
                        // Geçici silme işlemi - dosyayı taşıma
                        var trashDirectory = Path.Combine(_uploadDirectory, "trash");
                        if (!Directory.Exists(trashDirectory))
                        {
                            Directory.CreateDirectory(trashDirectory);
                        }

                        var trashPath = Path.Combine(trashDirectory, fileName);
                        if (File.Exists(filePath))
                        {
                            await Task.Run(() => File.Move(filePath, trashPath));
                        }
                    }

                    _cache.Remove(fileUrl);
                    return true;
                }
                finally
                {
                    _semaphore.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya silinirken hata oluştu: {FileUrl}", fileUrl);
                throw new Exception($"Dosya silinirken hata oluştu: {ex.Message}", ex);
            }
        }

        public async Task<Stream> DownloadFileAsync(string fileUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(fileUrl))
                    throw new ArgumentException("Dosya URL'i boş olamaz");

                var cacheKey = $"file_{fileUrl}";
                if (_cache.TryGetValue(cacheKey, out Stream? cachedStream) && cachedStream != null)
                {
                    return cachedStream;
                }

                var fileName = Path.GetFileName(fileUrl);
                var filePath = Path.Combine(_uploadDirectory, fileName);

                if (!File.Exists(filePath))
                    throw new FileNotFoundException("Dosya bulunamadı", fileName);

                var memoryStream = new MemoryStream();
                using (var fileStream = new FileStream(filePath, FileMode.Open))
                {
                    await fileStream.CopyToAsync(memoryStream);
                }
                memoryStream.Position = 0;

                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(_options.CacheExpirationMinutes));

                _cache.Set(cacheKey, memoryStream, cacheEntryOptions);

                return memoryStream;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya indirilirken hata oluştu: {FileUrl}", fileUrl);
                throw new Exception($"Dosya indirilirken hata oluştu: {ex.Message}", ex);
            }
        }

        public Task<bool> FileExistsAsync(string fileUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(fileUrl)) return Task.FromResult(false);

                var fileName = Path.GetFileName(fileUrl);
                var filePath = Path.Combine(_uploadDirectory, fileName);

                return Task.FromResult(File.Exists(filePath));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya kontrolü yapılırken hata oluştu: {FileUrl}", fileUrl);
                return Task.FromResult(false);
            }
        }


        public async Task<Stream> GetThumbnailAsync(string fileUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(fileUrl))
                    throw new ArgumentException("Dosya URL'i boş olamaz");

                var fileName = Path.GetFileName(fileUrl);
                var filePath = Path.Combine(_uploadDirectory, fileName);
                var thumbnailPath = GetThumbnailPath(filePath);

                if (!File.Exists(thumbnailPath))
                {
                    if (!File.Exists(filePath))
                        throw new FileNotFoundException("Dosya bulunamadı", fileName);

                    await GenerateThumbnailAsync(filePath);
                }

                var memoryStream = new MemoryStream();
                using (var fileStream = new FileStream(thumbnailPath, FileMode.Open))
                {
                    await fileStream.CopyToAsync(memoryStream);
                }
                memoryStream.Position = 0;
                return memoryStream;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Thumbnail alınırken hata oluştu: {FileUrl}", fileUrl);
                throw new Exception($"Thumbnail alınırken hata oluştu: {ex.Message}", ex);
            }
        }

        

        public async Task<string> OptimizeImageAsync(string fileUrl, int maxWidth = 1920, int maxHeight = 1080)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                if (!File.Exists(filePath))
                    throw new FileNotFoundException("File not found", fileUrl);

                var contentType = GetMimeType(filePath);
                if (!IsImageFile(contentType))
                    throw new ArgumentException("File is not an image");

                var optimizedPath = Path.Combine(_uploadDirectory, $"optimized_{Path.GetFileName(filePath)}");

                using var image = await Image.LoadAsync(filePath);
                var ratio = Math.Min(
                    (float)maxWidth / image.Width,
                    (float)maxHeight / image.Height
                );

                if (ratio < 1)
                {
                    var newWidth = (int)(image.Width * ratio);
                    var newHeight = (int)(image.Height * ratio);

                    image.Mutate(x => x.Resize(newWidth, newHeight));
                }

                await image.SaveAsJpegAsync(optimizedPath, new JpegEncoder { Quality = 85 });
                return $"/uploads/optimized_{Path.GetFileName(filePath)}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error optimizing image: {FileUrl}", fileUrl);
                throw;
            }
        }

        public async Task<string> ResizeImageAsync(string fileUrl, int width, int height, bool maintainAspectRatio = true)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                if (!File.Exists(filePath))
                    throw new FileNotFoundException("File not found", fileUrl);

                var resizedPath = Path.Combine(_uploadDirectory, $"resized_{Path.GetFileName(filePath)}");

                using var image = await Image.LoadAsync(filePath);
                
                if (maintainAspectRatio)
                {
                    var ratio = Math.Min(
                        (float)width / image.Width,
                        (float)height / image.Height
                    );
                    width = (int)(image.Width * ratio);
                    height = (int)(image.Height * ratio);
                }

                image.Mutate(x => x.Resize(width, height));
                await image.SaveAsJpegAsync(resizedPath);
                return $"/uploads/resized_{Path.GetFileName(filePath)}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resizing image: {FileUrl}", fileUrl);
                throw;
            }
        }

        public async Task<string> CropImageAsync(string fileUrl, int x, int y, int width, int height)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                if (!File.Exists(filePath))
                    throw new FileNotFoundException("File not found", fileUrl);

                var croppedPath = Path.Combine(_uploadDirectory, $"cropped_{Path.GetFileName(filePath)}");

                using var image = await Image.LoadAsync(filePath);
                image.Mutate(ctx => ctx.Crop(new Rectangle(x, y, width, height)));
                await image.SaveAsJpegAsync(croppedPath);
                return $"/uploads/cropped_{Path.GetFileName(filePath)}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cropping image: {FileUrl}", fileUrl);
                throw;
            }
        }

        public async Task<string> RotateImageAsync(string fileUrl, float angle)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                if (!File.Exists(filePath))
                    throw new FileNotFoundException("File not found", fileUrl);

                var rotatedPath = Path.Combine(_uploadDirectory, $"rotated_{Path.GetFileName(filePath)}");

                return await Task.Run(async () =>
                {
                    using var image = await Image.LoadAsync(filePath);
                    image.Mutate(x => x.Rotate(angle));
                    await image.SaveAsJpegAsync(rotatedPath);
                    return $"/uploads/rotated_{Path.GetFileName(filePath)}";
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rotating image: {FileUrl}", fileUrl);
                throw;
            }
        }

        public async Task<string> ConvertFileFormatAsync(string fileUrl, string targetFormat)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                if (!File.Exists(filePath))
                    throw new FileNotFoundException("File not found", fileUrl);

                var convertedPath = Path.Combine(_uploadDirectory, $"converted_{Path.GetFileNameWithoutExtension(filePath)}.{targetFormat.ToLower()}");

                return await Task.Run(async () =>
                {
                    using var image = await Image.LoadAsync(filePath);
                    
                    switch (targetFormat.ToLower())
                    {
                        case "jpeg":
                        case "jpg":
                            await image.SaveAsJpegAsync(convertedPath);
                            break;
                        case "png":
                            await image.SaveAsPngAsync(convertedPath);
                            break;
                        case "gif":
                            await image.SaveAsGifAsync(convertedPath);
                            break;
                        case "bmp":
                            await image.SaveAsBmpAsync(convertedPath);
                            break;
                        default:
                            throw new ArgumentException($"Unsupported target format: {targetFormat}");
                    }

                    return $"/uploads/converted_{Path.GetFileNameWithoutExtension(filePath)}.{targetFormat.ToLower()}";
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting file format: {FileUrl}", fileUrl);
                throw;
            }
        }

        public async Task<bool> ScanFileForVirusesAsync(Stream fileStream)
        {
            try
            {
                // Save the file to a temporary location for scanning
                var tempFilePath = Path.GetTempFileName();
                using (var tempFileStream = File.Create(tempFilePath))
                {
                    await fileStream.CopyToAsync(tempFileStream);
                }

                // TODO: Implement actual virus scanning logic here
                // This is a placeholder implementation
                // In a real application, you would:
                // 1. Use a virus scanning library or service
                // 2. Configure scanning parameters
                // 3. Handle different file types appropriately
                // 4. Implement proper error handling for the scanning process

                // For now, we'll just do a basic file check
                var fileInfo = new FileInfo(tempFilePath);
                if (fileInfo.Length == 0)
                {
                    return false;
                }

                // Clean up the temporary file
                try
                {
                    File.Delete(tempFilePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete temporary file after virus scan");
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scanning file for viruses");
                throw;
            }
        }

        public async Task<string> CreateBackupAsync(Stream fileStream, string fileName)
        {
            try
            {
                var backupDirectory = Path.Combine(_uploadDirectory, "backups");
                if (!Directory.Exists(backupDirectory))
                {
                    Directory.CreateDirectory(backupDirectory);
                }

                var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                var backupFileName = $"{Path.GetFileNameWithoutExtension(fileName)}_{timestamp}{Path.GetExtension(fileName)}";
                var backupPath = Path.Combine(backupDirectory, backupFileName);

                using (var backupStream = new FileStream(backupPath, FileMode.Create))
                {
                    await fileStream.CopyToAsync(backupStream);
                }

                // Create backup metadata
                var backupMetadata = new FileMetadata
                {
                    FileName = fileName,
                    ContentType = GetMimeType(fileName),
                    FileSize = new FileInfo(backupPath).Length,
                    CreatedAt = DateTime.UtcNow,
                    LastModified = DateTime.UtcNow,
                    Hash = await CalculateFileHashAsync(backupPath),
                    CustomMetadata = new Dictionary<string, string>
                    {
                        { "BackupType", "Manual" },
                        { "OriginalFileName", fileName },
                        { "BackupTimestamp", timestamp }
                    }
                };

                var metadataPath = GetMetadataPath(backupFileName);
                await SaveMetadataAsync(backupFileName, backupMetadata);

                return $"/uploads/backups/{backupFileName}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya yedekleme hatası: {FileName}", fileName);
                throw new Exception($"Dosya yedekleme hatası: {ex.Message}", ex);
            }
        }

        private string GetMimeType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".txt" => "text/plain",
                _ => "application/octet-stream"
            };
        }

        private async Task<string> CalculateFileHashAsync(string filePath)
        {
            using (var md5 = MD5.Create())
            using (var stream = File.OpenRead(filePath))
            {
                var hash = await Task.Run(() => md5.ComputeHash(stream));
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        private async Task ValidateFileAsync(Stream fileStream, string fileName, string contentType)
        {
            try
            {
                if (fileStream == null || fileStream.Length == 0)
                    throw new ArgumentException("File stream is empty");

                if (fileStream.Length > _options.MaxFileSize)
                    throw new ArgumentException($"File size exceeds maximum allowed size of {_options.MaxFileSize} bytes");

                var extension = Path.GetExtension(fileName).ToLowerInvariant();
                if (!_options.AllowedFileTypes.Contains(extension))
                    throw new ArgumentException($"File type {extension} is not allowed");

                if (IsImageFile(extension))
                {
                    try
                    {
                        using var image = await Image.LoadAsync(fileStream);
                        // Resim doğrulandı
                    }
                    catch (Exception ex)
                    {
                        throw new ArgumentException("Invalid image file", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating file: {FileName}", fileName);
                throw;
            }
        }

        private string SanitizeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
            return sanitized.TrimEnd('.');
        }

        private bool IsImageFile(string extension)
        {
            return new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp" }.Contains(extension.ToLowerInvariant());
        }

        public async Task<string> GenerateThumbnailAsync(string fileUrl)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                if (!File.Exists(filePath))
                    throw new FileNotFoundException("File not found", fileUrl);

                var contentType = GetMimeType(filePath);
                if (!IsImageFile(contentType))
                    throw new ArgumentException("File is not an image");

                var thumbnailPath = GetThumbnailPath(filePath);
                if (File.Exists(thumbnailPath))
                    return $"/uploads/thumbnails/{Path.GetFileName(thumbnailPath)}";

                using var image = await Image.LoadAsync(filePath);
                var ratio = Math.Min(
                    (float)_options.ThumbnailWidth / image.Width,
                    (float)_options.ThumbnailHeight / image.Height
                );

                var newWidth = (int)(image.Width * ratio);
                var newHeight = (int)(image.Height * ratio);

                image.Mutate(x => x.Resize(newWidth, newHeight));
                await image.SaveAsJpegAsync(thumbnailPath, new JpegEncoder { Quality = 85 });

                return $"/uploads/thumbnails/{Path.GetFileName(thumbnailPath)}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating thumbnail: {FileUrl}", fileUrl);
                throw;
            }
        }

        private string GetThumbnailPath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

            var directoryName = Path.GetDirectoryName(filePath);
            if (directoryName == null)
                throw new InvalidOperationException("Failed to determine the directory name from the file path.");

            return Path.Combine(
                directoryName,
                "thumbnails",
                Path.GetFileName(filePath)
            );
        }


        private string GetMetadataPath(string fileName)
        {
            return Path.Combine(
                _uploadDirectory,
                "metadata",
                $"{Path.GetFileNameWithoutExtension(fileName)}.json"
            );
        }

        private async Task<FileMetadata> CreateFileMetadataAsync(string filePath, string originalFileName, string contentType)
        {
            return await Task.Run(() =>
            {
                var fileInfo = new FileInfo(filePath);
                using (var md5 = MD5.Create())
                using (var stream = File.OpenRead(filePath))
                {
                    var hash = md5.ComputeHash(stream);
                    return new FileMetadata
                    {
                        FileName = originalFileName,
                        ContentType = contentType,
                        FileSize = fileInfo.Length,
                        CreatedAt = fileInfo.CreationTimeUtc,
                        LastModified = fileInfo.LastWriteTimeUtc,
                        Hash = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant(),
                        CustomMetadata = new Dictionary<string, string>()
                    };
                }
            });
        }


        private async Task SaveMetadataAsync(string fileName, FileMetadata metadata)
        {
            var metadataPath = GetMetadataPath(fileName);
            var metadataDir = Path.GetDirectoryName(metadataPath);

            if (metadataDir == null)
            {
                throw new InvalidOperationException("Failed to determine the directory name from the metadata path.");
            }

            Directory.CreateDirectory(metadataDir);

            var metadataJson = System.Text.Json.JsonSerializer.Serialize(metadata);
            await File.WriteAllTextAsync(metadataPath, metadataJson);
        }


        private IImageEncoder GetImageEncoder(string extension)
        {
            return extension.ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => new JpegEncoder { Quality = 85 },
                ".png" => new PngEncoder(),
                ".gif" => new GifEncoder(),
                ".bmp" => new BmpEncoder(),
                _ => throw new ArgumentException($"Unsupported image format: {extension}")
            };
        }

        public async Task<string> UploadFileAsync(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                    throw new ArgumentException("File is empty or null");

                var fileName = file.FileName;
                var contentType = file.ContentType;

                using (var stream = file.OpenReadStream())
                {
                    return await UploadFileAsync(stream, fileName, contentType);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file: {FileName}", file?.FileName);
                throw new Exception($"Error uploading file: {ex.Message}", ex);
            }
        }

        public async Task<string> UploadFileAsync(Stream fileStream, string fileName)
        {
            return await UploadFileAsync(fileStream, fileName, GetMimeType(fileName));
        }

        public async Task<long> GetFileSizeAsync(string fileUrl)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                if (!File.Exists(filePath))
                    throw new FileNotFoundException("File not found", fileUrl);

                return await Task.Run(() =>
                {
                    var fileInfo = new FileInfo(filePath);
                    return fileInfo.Length;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting file size for {FileUrl}", fileUrl);
                throw;
            }
        }


        public async Task<string> GetFileMimeTypeAsync(string fileUrl)
        {
            return await Task.Run(() =>
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                if (!File.Exists(filePath))
                    throw new FileNotFoundException("File not found", fileUrl);

                return GetMimeType(filePath);
            });
        }


        public async Task<byte[]> GetFileBytesAsync(string fileUrl)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                if (!File.Exists(filePath))
                    throw new FileNotFoundException("File not found", fileUrl);

                return await File.ReadAllBytesAsync(filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting file bytes for {FileUrl}", fileUrl);
                throw;
            }
        }

        public async Task<string> GetFileHashAsync(string fileUrl)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                if (!File.Exists(filePath))
                    throw new FileNotFoundException("File not found", fileUrl);

                return await CalculateFileHashAsync(filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting file hash for {FileUrl}", fileUrl);
                throw;
            }
        }

        public async Task<bool> ValidateFileAsync(string fileUrl)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var filePath = GetFilePathFromUrl(fileUrl);
                    if (!File.Exists(filePath))
                        return false;

                    var fileInfo = new FileInfo(filePath);
                    if (fileInfo.Length == 0)
                        return false;

                    var extension = Path.GetExtension(filePath).ToLowerInvariant();
                    if (!_options.AllowedFileTypes.Contains(extension))
                        return false;

                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error validating file {FileUrl}", fileUrl);
                    return false;
                }
            });
        }


        private string GetFilePathFromUrl(string fileUrl)
        {
            if (string.IsNullOrEmpty(fileUrl))
                throw new ArgumentException("File URL cannot be null or empty");

            var fileName = Path.GetFileName(fileUrl);
            return Path.Combine(_uploadDirectory, fileName);
        }

        public async Task<string> EncryptFileAsync(string fileUrl, string encryptionKey)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                if (!File.Exists(filePath))
                    throw new FileNotFoundException("File not found", fileUrl);

                var encryptedPath = Path.Combine(_uploadDirectory, $"encrypted_{Path.GetFileName(filePath)}");

                using (var aes = Aes.Create())
                {
                    aes.Key = Encoding.UTF8.GetBytes(encryptionKey);
                    aes.GenerateIV();

                    using (var sourceStream = new FileStream(filePath, FileMode.Open))
                    using (var destinationStream = new FileStream(encryptedPath, FileMode.Create))
                    {
                        // Write IV to the beginning of the file
                        await destinationStream.WriteAsync(aes.IV, 0, aes.IV.Length);

                        using (var cryptoStream = new CryptoStream(destinationStream, aes.CreateEncryptor(), CryptoStreamMode.Write))
                        {
                            await sourceStream.CopyToAsync(cryptoStream);
                        }
                    }
                }

                return $"/uploads/encrypted_{Path.GetFileName(filePath)}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error encrypting file: {FileUrl}", fileUrl);
                throw;
            }
        }

        public async Task<string> DecryptFileAsync(string fileUrl, string encryptionKey)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                if (!File.Exists(filePath))
                    throw new FileNotFoundException("File not found", fileUrl);

                var decryptedPath = Path.Combine(_uploadDirectory, $"decrypted_{Path.GetFileName(filePath)}");

                using (var aes = Aes.Create())
                {
                    aes.Key = Encoding.UTF8.GetBytes(encryptionKey);

                    using (var sourceStream = new FileStream(filePath, FileMode.Open))
                    {
                        // Read IV from the beginning of the file
                        var iv = new byte[aes.IV.Length];
                        await sourceStream.ReadAsync(iv, 0, iv.Length);
                        aes.IV = iv;

                        using (var destinationStream = new FileStream(decryptedPath, FileMode.Create))
                        using (var cryptoStream = new CryptoStream(sourceStream, aes.CreateDecryptor(), CryptoStreamMode.Read))
                        {
                            await cryptoStream.CopyToAsync(destinationStream);
                        }
                    }
                }

                return $"/uploads/decrypted_{Path.GetFileName(filePath)}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error decrypting file: {FileUrl}", fileUrl);
                throw;
            }
        }

        public async Task<bool> IsFileEncryptedAsync(string fileUrl)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                if (!File.Exists(filePath))
                    throw new FileNotFoundException("File not found", fileUrl);

                using (var stream = new FileStream(filePath, FileMode.Open))
                {
                    var iv = new byte[16]; // AES IV length
                    var bytesRead = await stream.ReadAsync(iv, 0, iv.Length);
                    return bytesRead == iv.Length;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if file is encrypted: {FileUrl}", fileUrl);
                throw;
            }
        }

        public async Task<string> GetEncryptionKeyAsync(string fileUrl)
        {
            try
            {
                var metadata = await GetFileMetadataAsync(fileUrl);
                if (metadata == null || metadata.CustomMetadata == null || !metadata.CustomMetadata.ContainsKey("EncryptionKey"))
                    throw new InvalidOperationException("Encryption key not found in metadata");

                return metadata.CustomMetadata["EncryptionKey"];
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting encryption key: {FileUrl}", fileUrl);
                throw;
            }
        }

        public async Task<string> DecompressFileAsync(string fileUrl)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                if (!File.Exists(filePath))
                    throw new FileNotFoundException("File not found", fileUrl);

                var decompressedPath = Path.Combine(_uploadDirectory, $"decompressed_{Path.GetFileName(filePath)}");

                using (var sourceStream = new FileStream(filePath, FileMode.Open))
                using (var destinationStream = new FileStream(decompressedPath, FileMode.Create))
                using (var gzipStream = new GZipStream(sourceStream, CompressionMode.Decompress))
                {
                    await gzipStream.CopyToAsync(destinationStream);
                }

                return $"/uploads/decompressed_{Path.GetFileName(filePath)}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error decompressing file: {FileUrl}", fileUrl);
                throw;
            }
        }

        public async Task<bool> IsFileCompressedAsync(string fileUrl)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                if (!File.Exists(filePath))
                    throw new FileNotFoundException("File not found", fileUrl);

                using (var stream = new FileStream(filePath, FileMode.Open))
                {
                    var header = new byte[2];
                    var bytesRead = await stream.ReadAsync(header, 0, 2);
                    return bytesRead == 2 && header[0] == 0x1F && header[1] == 0x8B; // GZip magic number
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if file is compressed: {FileUrl}", fileUrl);
                throw;
            }
        }

        public async Task<long> GetCompressedSizeAsync(string fileUrl)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                if (!File.Exists(filePath))
                    throw new FileNotFoundException("File not found", fileUrl);

                return await Task.Run(() =>
                {
                    var fileInfo = new FileInfo(filePath);
                    return fileInfo.Length;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting compressed size: {FileUrl}", fileUrl);
                throw;
            }
        }


        public async Task<string> GenerateThumbnailAsync(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                    throw new ArgumentException("File is empty or null");

                var fileName = file.FileName;
                var contentType = file.ContentType;

                if (!IsImageFile(Path.GetExtension(fileName)))
                    throw new ArgumentException("File is not an image");

                var sanitizedFileName = SanitizeFileName(fileName);
                var uniqueFileName = $"{Guid.NewGuid()}_{sanitizedFileName}";
                var filePath = Path.Combine(_uploadDirectory, uniqueFileName);

                // Save the original file temporarily
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                try
                {
                    // Generate thumbnail
                    var thumbnailPath = GetThumbnailPath(filePath);
                    var thumbnailDir = Path.GetDirectoryName(thumbnailPath);
                    if (thumbnailDir != null)
                    {
                        Directory.CreateDirectory(thumbnailDir);
                    }
                    else
                    {
                        throw new InvalidOperationException("Failed to determine the directory name for the thumbnail path.");
                    }

                    using var image = await Image.LoadAsync(filePath);
                    var ratio = Math.Min(
                        (float)_options.ThumbnailWidth / image.Width,
                        (float)_options.ThumbnailHeight / image.Height
                    );

                    var newWidth = (int)(image.Width * ratio);
                    var newHeight = (int)(image.Height * ratio);

                    image.Mutate(x => x.Resize(newWidth, newHeight));
                    await image.SaveAsJpegAsync(thumbnailPath, new JpegEncoder { Quality = 85 });

                    return $"/uploads/thumbnails/{Path.GetFileName(filePath)}";
                }
                finally
                {
                    // Clean up the temporary file
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating thumbnail: {FileName}", file?.FileName);
                throw new Exception($"Error generating thumbnail: {ex.Message}", ex);
            }
        }

        public async Task<string> GenerateThumbnailAsync(Stream fileStream, string fileName)
        {
            try
            {
                if (fileStream == null || fileStream.Length == 0)
                    throw new ArgumentException("File stream is empty or null");

                if (!IsImageFile(Path.GetExtension(fileName)))
                    throw new ArgumentException("File is not an image");

                var sanitizedFileName = SanitizeFileName(fileName);
                var uniqueFileName = $"{Guid.NewGuid()}_{sanitizedFileName}";
                var filePath = Path.Combine(_uploadDirectory, uniqueFileName);

                // Save the stream temporarily
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await fileStream.CopyToAsync(stream);
                }

                try
                {
                    return await GenerateThumbnailAsync(filePath);
                }
                finally
                {
                    // Clean up the temporary file
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating thumbnail from stream: {FileName}", fileName);
                throw new Exception($"Error generating thumbnail from stream: {ex.Message}", ex);
            }
        }

        public async Task<string> CreateFileVersionAsync(string fileUrl)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                if (!File.Exists(filePath))
                    throw new FileNotFoundException("File not found", fileUrl);

                var versionDirectory = Path.Combine(_uploadDirectory, "versions", Path.GetFileNameWithoutExtension(filePath));
                if (!Directory.Exists(versionDirectory))
                {
                    Directory.CreateDirectory(versionDirectory);
                }

                var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                var versionFileName = $"{timestamp}{Path.GetExtension(filePath)}";
                var versionPath = Path.Combine(versionDirectory, versionFileName);

                File.Copy(filePath, versionPath);

                var metadata = new FileMetadata
                {
                    FileName = versionFileName,
                    ContentType = GetMimeType(filePath),
                    FileSize = new FileInfo(versionPath).Length,
                    CreatedAt = DateTime.UtcNow,
                    LastModified = DateTime.UtcNow,
                    Hash = await CalculateFileHashAsync(versionPath),
                    CustomMetadata = new Dictionary<string, string>
                    {
                        { "VersionType", "Manual" },
                        { "OriginalFile", fileUrl },
                        { "VersionTimestamp", timestamp }
                    }
                };

                var metadataPath = GetMetadataPath(versionFileName);
                await SaveMetadataAsync(versionFileName, metadata);

                return $"/uploads/versions/{Path.GetFileNameWithoutExtension(filePath)}/{versionFileName}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating file version: {FileUrl}", fileUrl);
                throw new Exception($"Error creating file version: {ex.Message}", ex);
            }
        }

        public async Task<string> CompressFileAsync(string fileUrl, CompressionLevel level = CompressionLevel.Optimal)
        {
            try
            {
                // File path alınıyor
                var filePath = GetFilePathFromUrl(fileUrl);

                // Dosya yolu geçerli mi kontrolü
                if (string.IsNullOrWhiteSpace(filePath) || Path.GetInvalidPathChars().Any(filePath.Contains))
                    throw new ArgumentException("Invalid file path", nameof(fileUrl));

                // Dosya gerçekten var mı?
                if (!File.Exists(filePath))
                    throw new FileNotFoundException("File not found", fileUrl);

                // Dosyanın bulunduğu klasör alınıyor
                var originalDirectory = Path.GetDirectoryName(filePath);
                if (string.IsNullOrWhiteSpace(originalDirectory))
                    throw new InvalidOperationException("Could not determine the directory of the file.");

                // /compressed klasörü oluşturuluyor
                var compressedDir = Path.Combine(originalDirectory, "compressed");
                Directory.CreateDirectory(compressedDir); // null değil, güvenli

                // Sıkıştırılmış dosya yolu hazırlanıyor
                var compressedPath = Path.Combine(compressedDir, Path.GetFileName(filePath) + ".gz");

                // Dosya sıkıştırılıyor
                using (var sourceStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                using (var destinationStream = new FileStream(compressedPath, FileMode.Create, FileAccess.Write))
                using (var compressionStream = new GZipStream(destinationStream, level))
                {
                    await sourceStream.CopyToAsync(compressionStream);
                }

                // Göreli yol döndürülüyor (örneğin /uploads/compressed/foo.txt.gz)
                return $"/uploads/compressed/{Path.GetFileName(compressedPath)}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error compressing file: {FileUrl}", fileUrl);
                throw;
            }
        }


        public async Task<List<string>> GetFileVersionsAsync(string fileUrl)
        {
            return await Task.Run(() =>
            {
                var fileName = Path.GetFileNameWithoutExtension(fileUrl);
                var versionDirectory = Path.Combine(_uploadDirectory, "versions", fileName);

                if (!Directory.Exists(versionDirectory))
                    return new List<string>();

                var versions = Directory.GetFiles(versionDirectory)
                    .Select(f => $"/uploads/versions/{fileName}/{Path.GetFileName(f)}")
                    .OrderByDescending(v => v)
                    .ToList();

                return versions;
            });
        }


        public async Task<bool> DeleteFileVersionAsync(string fileUrl, string versionId)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var fileName = Path.GetFileNameWithoutExtension(fileUrl);
                    var versionDirectory = Path.Combine(_uploadDirectory, "versions", fileName);
                    var versionPath = Path.Combine(versionDirectory, versionId);

                    if (!File.Exists(versionPath))
                        return false;

                    File.Delete(versionPath);

                    // Clean up empty version directory
                    if (!Directory.EnumerateFiles(versionDirectory).Any())
                    {
                        Directory.Delete(versionDirectory);
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deleting file version: {FileUrl}, {VersionId}", fileUrl, versionId);
                    return false;
                }
            });
        }


        public async Task<Dictionary<string, object>> GetVersionMetadataAsync(string fileUrl, string versionId)
        {
            try
            {
                var fileName = Path.GetFileName(fileUrl);
                var versionPath = Path.Combine(_uploadDirectory, "versions", fileName, versionId);

                if (!File.Exists(versionPath))
                    throw new FileNotFoundException("Version not found", versionId);

                var metadata = await GetFileMetadataAsync($"/uploads/versions/{fileName}/{versionId}");
                if (metadata == null)
                    throw new InvalidOperationException("Version metadata not found");

                return new Dictionary<string, object>
                {
                    { "FileName", metadata.FileName },
                    { "ContentType", metadata.ContentType },
                    { "FileSize", metadata.FileSize },
                    { "CreatedAt", metadata.CreatedAt },
                    { "LastModified", metadata.LastModified },
                    { "Hash", metadata.Hash },
                    { "CustomMetadata", metadata.CustomMetadata }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting version metadata: {FileUrl}, {VersionId}", fileUrl, versionId);
                throw;
            }
        }

        public async Task<bool> CacheFileAsync(string fileUrl, TimeSpan? expiration = null)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                if (!File.Exists(filePath))
                    return false;

                var cacheKey = $"file_{fileUrl}";
                var fileStream = new MemoryStream();
                
                using (var sourceStream = new FileStream(filePath, FileMode.Open))
                {
                    await sourceStream.CopyToAsync(fileStream);
                }
                fileStream.Position = 0;

                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(expiration ?? TimeSpan.FromMinutes(_options.CacheExpirationMinutes));

                _cache.Set(cacheKey, fileStream, cacheEntryOptions);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error caching file: {FileUrl}", fileUrl);
                return false;
            }
        }

        public async Task<bool> RemoveFromCacheAsync(string fileUrl)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var cacheKey = $"file_{fileUrl}";
                    _cache.Remove(cacheKey);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error removing file from cache: {FileUrl}", fileUrl);
                    return false;
                }
            });
        }


        public async Task<DateTime?> GetCacheExpirationAsync(string fileUrl)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var cacheKey = $"file_{fileUrl}";
                    if (_cache.TryGetValue(cacheKey, out var entry))
                    {
                        var cacheEntry = entry as ICacheEntry;
                        return cacheEntry?.AbsoluteExpiration?.UtcDateTime;
                    }
                    return null;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error getting cache expiration: {FileUrl}", fileUrl);
                    return null;
                }
            });
        }


        public async Task<bool> RefreshCacheAsync(string fileUrl)
        {
            try
            {
                await RemoveFromCacheAsync(fileUrl);
                return await CacheFileAsync(fileUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing cache: {FileUrl}", fileUrl);
                return false;
            }
        }

        public async Task<string> DownloadFileToPathAsync(string fileUrl, string destinationPath, IProgress<long>? progress = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                if (!File.Exists(filePath))
                    throw new FileNotFoundException("File not found", fileUrl);

                var directory = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                _downloadedBytes[fileUrl] = 0;
                _downloadComplete[fileUrl] = false;

                var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _downloadCancellations[fileUrl] = cts;

                try
                {
                    using (var sourceStream = new FileStream(filePath, FileMode.Open))
                    using (var destinationStream = new FileStream(destinationPath, FileMode.Create))
                    {
                        var buffer = new byte[81920]; // 80KB buffer
                        var totalBytesRead = 0L;
                        int bytesRead;

                        while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length, cts.Token)) > 0)
                        {
                            await destinationStream.WriteAsync(buffer, 0, bytesRead, cts.Token);
                            totalBytesRead += bytesRead;
                            _downloadedBytes[fileUrl] = totalBytesRead;
                            progress?.Report(totalBytesRead);
                        }
                    }

                    _downloadComplete[fileUrl] = true;
                    return destinationPath;
                }
                finally
                {
                    _downloadCancellations.Remove(fileUrl);
                    cts.Dispose();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading file to path: {FileUrl}, {DestinationPath}", fileUrl, destinationPath);
                throw;
            }
        }

        public Task<long> GetDownloadedBytesAsync(string fileUrl)
        {
            try
            {
                if (_downloadedBytes.TryGetValue(fileUrl, out var bytes))
                {
                    return Task.FromResult(bytes);
                }
                return Task.FromResult(0L);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting downloaded bytes: {FileUrl}", fileUrl);
                return Task.FromResult(0L);
            }
        }


        public Task<bool> IsDownloadCompleteAsync(string fileUrl)
        {
            try
            {
                if (_downloadComplete.TryGetValue(fileUrl, out var isComplete))
                {
                    return Task.FromResult(isComplete);
                }
                return Task.FromResult(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking download status: {FileUrl}", fileUrl);
                return Task.FromResult(false);
            }
        }


        public async Task<string> RestoreFileVersionAsync(string fileUrl, string versionId)
        {
            return await Task.Run(() =>
            {
                var fileName = Path.GetFileNameWithoutExtension(fileUrl);
                var versionDirectory = Path.Combine(_uploadDirectory, "versions", fileName);
                var versionPath = Path.Combine(versionDirectory, versionId);

                if (!File.Exists(versionPath))
                    throw new FileNotFoundException("Version not found", versionId);

                var currentPath = GetFilePathFromUrl(fileUrl);
                var backupPath = Path.Combine(_uploadDirectory, $"backup_{Path.GetFileName(fileUrl)}");

                // Backup current file
                if (File.Exists(currentPath))
                {
                    File.Copy(currentPath, backupPath, true);
                }

                // Restore version
                File.Copy(versionPath, currentPath, true);

                return fileUrl;
            });
        }


        public async Task<bool> IsFileCachedAsync(string fileUrl)
        {
            try
            {
                var cacheKey = $"file_{fileUrl}";
                return await Task.FromResult(_cache.TryGetValue(cacheKey, out _));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking file cache: {FileUrl}", fileUrl);
                return false;
            }
        }

        public async Task<Stream> DownloadFileWithProgressAsync(string fileUrl, IProgress<long> progress, CancellationToken cancellationToken = default)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                if (!File.Exists(filePath))
                    throw new FileNotFoundException("File not found", fileUrl);

                var fileInfo = new FileInfo(filePath);
                var memoryStream = new MemoryStream();

                using (var fileStream = new FileStream(filePath, FileMode.Open))
                {
                    var buffer = new byte[81920]; // 80KB buffer
                    var totalBytesRead = 0L;
                    int bytesRead;

                    while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                    {
                        await memoryStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                        totalBytesRead += bytesRead;
                        progress?.Report(totalBytesRead);
                    }
                }

                memoryStream.Position = 0;
                return memoryStream;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading file with progress: {FileUrl}", fileUrl);
                throw;
            }
        }

        public async Task CancelDownloadAsync(string fileUrl)
        {
            try
            {
                if (_downloadCancellations.TryGetValue(fileUrl, out var cts))
                {
                    cts.Cancel();
                    _downloadCancellations.Remove(fileUrl);
                    _downloadedBytes.Remove(fileUrl);
                    _downloadComplete.Remove(fileUrl);
                }
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error canceling download: {FileUrl}", fileUrl);
            }
        }

        public async Task<string> CopyFileAsync(string sourceUrl, string destinationUrl)
        {
            try
            {
                var sourcePath = GetFilePathFromUrl(sourceUrl);
                var destPath = GetFilePathFromUrl(destinationUrl);
                
                await Task.Run(() => File.Copy(sourcePath, destPath, true));
                return destinationUrl;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error copying file from {SourceUrl} to {DestinationUrl}", sourceUrl, destinationUrl);
                throw;
            }
        }

        public async Task<bool> MoveFileAsync(string sourceUrl, string destinationUrl)
        {
            try
            {
                var sourcePath = GetFilePathFromUrl(sourceUrl);
                var destPath = GetFilePathFromUrl(destinationUrl);
                
                await Task.Run(() => File.Move(sourcePath, destPath, true));
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error moving file from {SourceUrl} to {DestinationUrl}", sourceUrl, destinationUrl);
                return false;
            }
        }

        public async Task<bool> RenameFileAsync(string fileUrl, string newName)
        {
            try
            {
                var oldPath = GetFilePathFromUrl(fileUrl);
                var directory = Path.GetDirectoryName(oldPath);
                var newPath = Path.Combine(directory ?? throw new ArgumentNullException(nameof(directory)), 
                    newName ?? throw new ArgumentNullException(nameof(newName)));
                
                await Task.Run(() => File.Move(oldPath, newPath));
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error renaming file {FileUrl} to {NewName}", fileUrl, newName);
                return false;
            }
        }

        public async Task<bool> CreateDirectoryAsync(string directoryPath)
        {
            try
            {
                var fullPath = Path.Combine(_uploadDirectory, directoryPath.TrimStart('/'));
                await Task.Run(() => Directory.CreateDirectory(fullPath));
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating directory: {DirectoryPath}", directoryPath);
                return false;
            }
        }

        public async Task<bool> DeleteDirectoryAsync(string directoryPath)
        {
            try
            {
                var fullPath = Path.Combine(_uploadDirectory, directoryPath.TrimStart('/'));
                await Task.Run(() => Directory.Delete(fullPath, true));
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting directory: {DirectoryPath}", directoryPath);
                return false;
            }
        }

        public async Task<List<string>> ListDirectoryAsync(string directoryPath)
        {
            try
            {
                var fullPath = Path.Combine(_uploadDirectory, directoryPath.TrimStart('/'));
                return await Task.Run(() => Directory.GetFiles(fullPath)
                    .Select(f => Path.GetFileName(f))
                    .ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing directory: {DirectoryPath}", directoryPath);
                return new List<string>();
            }
        }

        public Task<bool> DirectoryExistsAsync(string directoryPath)
        {
            try
            {
                var fullPath = Path.Combine(_uploadDirectory, directoryPath.TrimStart('/'));
                return Task.FromResult(Directory.Exists(fullPath));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking directory existence: {DirectoryPath}", directoryPath);
                return Task.FromResult(false);
            }
        }

        public async Task<bool> UpdateFileMetadataAsync(string fileUrl, Dictionary<string, string> metadata)
        {
            try
            {
                var fileName = Path.GetFileName(fileUrl);
                var metadataPath = GetMetadataPath(fileName);
                var filePath = GetFilePathFromUrl(fileUrl);

                if (!File.Exists(filePath))
                    return false;

                var fileMetadata = new FileMetadata
                {
                    FileName = fileName,
                    ContentType = GetMimeType(filePath),
                    FileSize = new FileInfo(filePath).Length,
                    CreatedAt = DateTime.UtcNow,
                    LastModified = DateTime.UtcNow,
                    Hash = await CalculateFileHashAsync(filePath),
                    CustomMetadata = metadata
                };

                await SaveMetadataAsync(fileName, fileMetadata);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating file metadata: {FileUrl}", fileUrl);
                return false;
            }
        }

        public async Task<bool> RemoveFileMetadataAsync(string fileUrl, string key)
        {
            try
            {
                var fileName = Path.GetFileName(fileUrl);
                var filePath = GetFilePathFromUrl(fileUrl);

                if (!File.Exists(filePath))
                    return false;

                var existingMetadata = await GetFileMetadataAsync(fileUrl);
                if (existingMetadata == null || existingMetadata.CustomMetadata == null || !existingMetadata.CustomMetadata.ContainsKey(key))
                    return false;

                var fileMetadata = new FileMetadata
                {
                    FileName = fileName,
                    ContentType = GetMimeType(filePath),
                    FileSize = new FileInfo(filePath).Length,
                    CreatedAt = DateTime.UtcNow,
                    LastModified = DateTime.UtcNow,
                    Hash = await CalculateFileHashAsync(filePath),
                    CustomMetadata = new Dictionary<string, string>(existingMetadata.CustomMetadata)
                };

                fileMetadata.CustomMetadata.Remove(key);
                await SaveMetadataAsync(fileName, fileMetadata);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing file metadata: {FileUrl}, {Key}", fileUrl, key);
                return false;
            }
        }

        public async Task<bool> ClearFileMetadataAsync(string fileUrl)
        {
            try
            {
                var fileName = Path.GetFileName(fileUrl);
                var filePath = GetFilePathFromUrl(fileUrl);

                if (!File.Exists(filePath))
                    return false;

                var existingMetadata = await GetFileMetadataAsync(fileUrl);
                if (existingMetadata == null)
                    return false;

                var fileMetadata = new FileMetadata
                {
                    FileName = fileName,
                    ContentType = GetMimeType(filePath),
                    FileSize = new FileInfo(filePath).Length,
                    CreatedAt = DateTime.UtcNow,
                    LastModified = DateTime.UtcNow,
                    Hash = await CalculateFileHashAsync(filePath),
                    CustomMetadata = new Dictionary<string, string>()
                };

                await SaveMetadataAsync(fileName, fileMetadata);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing file metadata: {FileUrl}", fileUrl);
                return false;
            }
        }

        public async Task<bool> HasMetadataAsync(string fileUrl, string key)
        {
            try
            {
                var fileName = Path.GetFileName(fileUrl);
                var metadataPath = GetMetadataPath(fileName);

                if (!File.Exists(metadataPath))
                    return false;

                var metadata = await GetFileMetadataAsync(fileUrl);
                return metadata != null && metadata.CustomMetadata != null && metadata.CustomMetadata.ContainsKey(key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking metadata existence: {FileUrl}, {Key}", fileUrl, key);
                return false;
            }
        }

        public async Task<bool> SetFilePermissionsAsync(string fileUrl, Dictionary<string, string> permissions)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                if (!File.Exists(filePath))
                    return false;

                var fileName = Path.GetFileName(fileUrl);
                var metadataPath = GetMetadataPath(fileName);

                var metadata = await GetFileMetadataAsync(fileUrl);
                if (metadata == null)
                {
                    metadata = new FileMetadata
                    {
                        FileName = fileName,
                        ContentType = GetMimeType(filePath),
                        FileSize = new FileInfo(filePath).Length,
                        CreatedAt = DateTime.UtcNow,
                        LastModified = DateTime.UtcNow,
                        Hash = await CalculateFileHashAsync(filePath),
                        CustomMetadata = new Dictionary<string, string>()
                    };
                }

                // Update or add permissions
                foreach (var permission in permissions)
                {
                    metadata.CustomMetadata[$"permission_{permission.Key}"] = permission.Value;
                }

                await SaveMetadataAsync(fileName, metadata);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting file permissions: {FileUrl}", fileUrl);
                return false;
            }
        }

        public async Task<Dictionary<string, string>> GetFilePermissionsAsync(string fileUrl)
        {
            try
            {
                var metadata = await GetFileMetadataAsync(fileUrl);
                if (metadata == null)
                    return new Dictionary<string, string>();

                return metadata.CustomMetadata
                    .Where(kvp => kvp.Key.StartsWith("permission_"))
                    .ToDictionary(
                        kvp => kvp.Key.Substring("permission_".Length),
                        kvp => kvp.Value
                    );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting file permissions: {FileUrl}", fileUrl);
                return new Dictionary<string, string>();
            }
        }

        public async Task<bool> CheckFileAccessAsync(string fileUrl, string permission)
        {
            try
            {
                var permissions = await GetFilePermissionsAsync(fileUrl);
                return permissions.ContainsKey(permission) && permissions[permission] == "granted";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking file access: {FileUrl}, {Permission}", fileUrl, permission);
                return false;
            }
        }

        public async Task<bool> GrantFileAccessAsync(string fileUrl, string permission, string userId)
        {
            try
            {
                var permissions = await GetFilePermissionsAsync(fileUrl);
                permissions[permission] = userId;

                return await SetFilePermissionsAsync(fileUrl, permissions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error granting file access: {FileUrl}, {Permission}, {UserId}", fileUrl, permission, userId);
                return false;
            }
        }

        public async Task<bool> RevokeFileAccessAsync(string fileUrl, string permission)
        {
            try
            {
                var permissions = await GetFilePermissionsAsync(fileUrl);
                if (!permissions.ContainsKey(permission))
                    return false;

                permissions.Remove(permission);
                return await SetFilePermissionsAsync(fileUrl, permissions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking file access: {FileUrl}, {Permission}", fileUrl, permission);
                return false;
            }
        }

        public async Task<List<string>> GetFileAccessListAsync(string fileUrl)
        {
            try
            {
                var permissions = await GetFilePermissionsAsync(fileUrl);
                return permissions.Values.Distinct().ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting file access list: {FileUrl}", fileUrl);
                return new List<string>();
            }
        }

        public async Task<string> GenerateDocumentPreviewAsync(string fileUrl)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                var previewPath = Path.ChangeExtension(filePath, ".preview.jpg");
                
                // Simulate document preview generation
                await Task.Run(() => 
                {
                    using var image = new Image<Rgba32>(800, 600);
                    image.SaveAsJpeg(previewPath);
                });
                
                return previewPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating document preview: {FileUrl}", fileUrl);
                throw;
            }
        }

        public async Task<string> GenerateVideoPreviewAsync(string fileUrl)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                var previewPath = Path.ChangeExtension(filePath, ".preview.jpg");
                
                // Simulate video preview generation
                await Task.Run(() => 
                {
                    using var image = new Image<Rgba32>(800, 450);
                    image.SaveAsJpeg(previewPath);
                });
                
                return previewPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating video preview: {FileUrl}", fileUrl);
                throw;
            }
        }

        public async Task<string> GenerateAudioPreviewAsync(string fileUrl)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                var previewPath = Path.ChangeExtension(filePath, ".preview.jpg");
                
                // Simulate audio preview generation
                await Task.Run(() => 
                {
                    using var image = new Image<Rgba32>(400, 200);
                    image.SaveAsJpeg(previewPath);
                });
                
                return previewPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating audio preview: {FileUrl}", fileUrl);
                throw;
            }
        }

        public async Task<string> GenerateImagePreviewAsync(string fileUrl, int width, int height)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                if (!File.Exists(filePath))
                    throw new FileNotFoundException("File not found", fileUrl);

                var contentType = GetMimeType(filePath);
                if (!IsImageFile(Path.GetExtension(filePath)))
                    throw new ArgumentException("File is not an image");

                var previewPath = Path.Combine(_uploadDirectory, "previews", $"img_{Path.GetFileNameWithoutExtension(filePath)}.jpg");
                var previewDir = Path.GetDirectoryName(previewPath);

                if (previewDir == null)
                {
                    throw new ArgumentNullException(nameof(previewDir));
                }

                if (!Directory.Exists(previewDir))
                {
                    Directory.CreateDirectory(previewDir);
                }

                using (var image = await Image.LoadAsync(filePath))
                {
                    var ratio = Math.Min(
                        (float)width / image.Width,
                        (float)height / image.Height
                    );

                    var newWidth = (int)(image.Width * ratio);
                    var newHeight = (int)(image.Height * ratio);

                    image.Mutate(x => x.Resize(newWidth, newHeight));
                    await image.SaveAsJpegAsync(previewPath);
                }

                return $"/uploads/previews/img_{Path.GetFileNameWithoutExtension(filePath)}.jpg";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating image preview: {FileUrl}", fileUrl);
                throw;
            }
        }

        public async Task<Dictionary<string, string>> GeneratePreviewsAsync(List<string> fileUrls)
        {
            try
            {
                var results = new Dictionary<string, string>();

                foreach (var fileUrl in fileUrls)
                {
                    try
                    {
                        var contentType = await GetFileMimeTypeAsync(fileUrl);
                        string previewUrl;

                        if (contentType.StartsWith("image/"))
                        {
                            previewUrl = await GenerateImagePreviewAsync(fileUrl, 800, 600);
                        }
                        else if (contentType.StartsWith("video/"))
                        {
                            previewUrl = await GenerateVideoPreviewAsync(fileUrl);
                        }
                        else if (contentType.StartsWith("audio/"))
                        {
                            previewUrl = await GenerateAudioPreviewAsync(fileUrl);
                        }
                        else if (contentType.StartsWith("application/") || contentType.StartsWith("text/"))
                        {
                            previewUrl = await GenerateDocumentPreviewAsync(fileUrl);
                        }
                        else
                        {
                            continue;
                        }

                        results[fileUrl] = previewUrl;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error generating preview for file: {FileUrl}", fileUrl);
                    }
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating previews for multiple files");
                throw;
            }
        }

        public async Task<Dictionary<string, object>> AnalyzeFileAsync(string fileUrl)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                if (!File.Exists(filePath))
                    throw new FileNotFoundException("File not found", fileUrl);

                var fileInfo = new FileInfo(filePath);
                var contentType = GetMimeType(filePath);
                var analysis = new Dictionary<string, object>
                {
                    { "FileName", fileInfo.Name },
                    { "ContentType", contentType },
                    { "FileSize", fileInfo.Length },
                    { "CreatedAt", fileInfo.CreationTimeUtc },
                    { "LastModified", fileInfo.LastWriteTimeUtc },
                    { "Hash", await CalculateFileHashAsync(filePath) }
                };

                if (IsImageFile(Path.GetExtension(filePath)))
                {
                    using var image = await Image.LoadAsync(filePath);
                    {
                        analysis["Width"] = image.Width;
                        analysis["Height"] = image.Height;
                        analysis["Format"] = image.Metadata.DecodedImageFormat?.Name ?? "Unknown";
                    }
                }

                return analysis;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing file: {FileUrl}", fileUrl);
                throw;
            }
        }



        public async Task<Dictionary<string, object>> GetFileStatisticsAsync(string fileUrl)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                var fileInfo = new FileInfo(filePath);
                
                return await Task.Run(() => new Dictionary<string, object>
                {
                    ["Size"] = fileInfo.Length,
                    ["Created"] = fileInfo.CreationTime,
                    ["Modified"] = fileInfo.LastWriteTime,
                    ["Accessed"] = fileInfo.LastAccessTime,
                    ["Extension"] = fileInfo.Extension,
                    ["IsReadOnly"] = fileInfo.IsReadOnly
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting file statistics: {FileUrl}", fileUrl);
                throw;
            }
        }

        public async Task<bool> ValidateFileIntegrityAsync(string fileUrl)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                if (!File.Exists(filePath))
                    return false;

                var metadata = await GetFileMetadataAsync(fileUrl);
                if (metadata == null)
                    return false;

                var currentHash = await CalculateFileHashAsync(filePath);
                return currentHash == metadata.Hash;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating file integrity: {FileUrl}", fileUrl);
                return false;
            }
        }

        public async Task<Dictionary<string, object>> GetFileContentAnalysisAsync(string fileUrl)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                if (!File.Exists(filePath))
                    throw new FileNotFoundException("File not found", fileUrl);

                var contentType = GetMimeType(filePath);
                var analysis = new Dictionary<string, object>();

                if (IsImageFile(Path.GetExtension(filePath)))
                {
                    using var image = await Image.LoadAsync(filePath);
                    {
                        analysis["Width"] = image.Width;
                        analysis["Height"] = image.Height;
                        analysis["Format"] = image.Metadata.DecodedImageFormat?.Name ?? "Unknown";
                        // PixelFormat değerini kullanıcıya açıklayıcı bir şekilde sunmak
                        string pixelFormatDesc = image switch
                        {
                            Image<Rgba32> => "32-bit RGBA (8 bits per channel)",
                            Image<Rgb24> => "24-bit RGB",
                            Image<L8> => "8-bit Grayscale",
                            Image<L16> => "16-bit Grayscale",
                            _ => "Unknown Pixel Format"
                        };
                        analysis["PixelFormat"] = pixelFormatDesc;

                        // HorizontalResolution ve VerticalResolution için null ve sıfır kontrolü
                        if (image.Metadata != null &&
                            image.Metadata.HorizontalResolution > 0 &&
                            image.Metadata.VerticalResolution > 0)
                        {
                            analysis["HorizontalResolution"] = $"{image.Metadata.HorizontalResolution} dpi";
                            analysis["VerticalResolution"] = $"{image.Metadata.VerticalResolution} dpi";
                        }
                        else
                        {
                            analysis["HorizontalResolution"] = "Unknown or not available";
                            analysis["VerticalResolution"] = "Unknown or not available";
                        }

                    }
                }
                else if (contentType.StartsWith("text/"))
                {
                    var text = await File.ReadAllTextAsync(filePath);
                    analysis["LineCount"] = text.Split('\n').Length;
                    analysis["WordCount"] = text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
                    analysis["CharacterCount"] = text.Length;
                }

                return analysis;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing file content: {FileUrl}", fileUrl);
                throw;
            }
        }

        public async Task<string> BackupFileAsync(string fileUrl)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                if (!File.Exists(filePath))
                    throw new FileNotFoundException("File not found", fileUrl);

                var backupDirectory = Path.Combine(_uploadDirectory, "backups");
                if (!Directory.Exists(backupDirectory))
                {
                    Directory.CreateDirectory(backupDirectory);
                }

                var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                var backupFileName = $"{Path.GetFileNameWithoutExtension(filePath)}_{timestamp}{Path.GetExtension(filePath)}";
                var backupPath = Path.Combine(backupDirectory, backupFileName);

                File.Copy(filePath, backupPath);

                var metadata = new FileMetadata
                {
                    FileName = backupFileName,
                    ContentType = GetMimeType(filePath),
                    FileSize = new FileInfo(backupPath).Length,
                    CreatedAt = DateTime.UtcNow,
                    LastModified = DateTime.UtcNow,
                    Hash = await CalculateFileHashAsync(backupPath),
                    CustomMetadata = new Dictionary<string, string>
                    {
                        { "BackupType", "Manual" },
                        { "OriginalFile", fileUrl },
                        { "BackupTimestamp", timestamp }
                    }
                };

                await SaveMetadataAsync(backupFileName, metadata);
                return $"/uploads/backups/{backupFileName}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error backing up file: {FileUrl}", fileUrl);
                throw;
            }
        }

        public async Task<bool> RestoreFileFromBackupAsync(string fileUrl, string backupId)
        {
            try
            {
                var backupPath = Path.Combine(_uploadDirectory, "backups", backupId);
                if (!File.Exists(backupPath))
                    return false;

                var filePath = GetFilePathFromUrl(fileUrl);
                var backupMetadata = await GetBackupMetadataAsync(backupId);

                // Create a backup of the current file before restoring
                if (File.Exists(filePath))
                {
                    await BackupFileAsync(fileUrl);
                }

                File.Copy(backupPath, filePath, true);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring file from backup: {FileUrl}, {BackupId}", fileUrl, backupId);
                return false;
            }
        }

        public async Task<List<string>> GetFileBackupsAsync(string fileUrl)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                var backupDir = Path.Combine(Path.GetDirectoryName(filePath) ?? throw new ArgumentNullException(nameof(filePath)), "backups");
                return await Task.Run(() => Directory.Exists(backupDir) 
                    ? Directory.GetFiles(backupDir, $"{Path.GetFileNameWithoutExtension(filePath)}_backup_*")
                        .Select(Path.GetFileName)
                        .Where(f => f != null)
                        .Select(f => f!)
                        .ToList()
                    : new List<string>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting file backups: {FileUrl}", fileUrl);
                return new List<string>();
            }
        }

        public async Task<bool> DeleteFileBackupAsync(string backupId)
        {
            try
            {
                var backupPath = Path.Combine(_uploadDirectory, "backups", backupId);
                await Task.Run(() => File.Delete(backupPath));
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file backup: {BackupId}", backupId);
                return false;
            }
        }

        public async Task<string> GenerateShareableLinkAsync(string fileUrl, TimeSpan? expiration = null)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                if (!File.Exists(filePath))
                    throw new FileNotFoundException("File not found", fileUrl);

                var shareId = Guid.NewGuid().ToString("N");
                var metadata = await GetFileMetadataAsync(fileUrl);
                if (metadata == null)
                {
                    metadata = new FileMetadata
                    {
                        FileName = Path.GetFileName(fileUrl),
                        ContentType = GetMimeType(filePath),
                        FileSize = new FileInfo(filePath).Length,
                        CreatedAt = DateTime.UtcNow,
                        LastModified = DateTime.UtcNow,
                        Hash = await CalculateFileHashAsync(filePath),
                        CustomMetadata = new Dictionary<string, string>()
                    };
                }

                metadata.CustomMetadata["ShareId"] = shareId;
                metadata.CustomMetadata["ShareExpiration"] = expiration.HasValue 
                    ? DateTime.UtcNow.Add(expiration.Value).ToString("o")
                    : string.Empty; // Use empty string instead of null

                await SaveMetadataAsync(Path.GetFileName(fileUrl), metadata);
                return $"/share/{shareId}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating shareable link: {FileUrl}", fileUrl);
                throw;
            }
        }

        public async Task<bool> RevokeShareableLinkAsync(string fileUrl)
        {
            try
            {
                var metadata = await GetFileMetadataAsync(fileUrl);
                if (metadata == null || !metadata.CustomMetadata.ContainsKey("ShareId"))
                    return false;

                metadata.CustomMetadata.Remove("ShareId");
                metadata.CustomMetadata.Remove("ShareExpiration");
                await SaveMetadataAsync(Path.GetFileName(fileUrl), metadata);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking shareable link: {FileUrl}", fileUrl);
                return false;
            }
        }

        public async Task<bool> IsShareableLinkValidAsync(string shareId)
        {
            try
            {
                var files = Directory.GetFiles(_uploadDirectory)
                    .Select(f => Path.GetFileName(f))
                    .ToList();

                foreach (var fileName in files)
                {
                    var metadata = await GetFileMetadataAsync($"/uploads/{fileName}");
                    if (metadata?.CustomMetadata != null &&
                        metadata.CustomMetadata.TryGetValue("ShareId", out var storedShareId) &&
                        storedShareId == shareId)
                    {
                        if (metadata.CustomMetadata.TryGetValue("ShareExpiration", out var expirationStr) &&
                            !string.IsNullOrEmpty(expirationStr))
                        {
                            var expiration = DateTime.Parse(expirationStr);
                            return DateTime.UtcNow < expiration;
                        }
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking shareable link validity: {ShareId}", shareId);
                return false;
            }
        }

        public async Task<DateTime?> GetShareableLinkExpirationAsync(string fileUrl)
        {
            try
            {
                var metadata = await GetFileMetadataAsync(fileUrl);
                if (metadata?.CustomMetadata == null ||
                    !metadata.CustomMetadata.TryGetValue("ShareExpiration", out var expirationStr) ||
                    string.IsNullOrEmpty(expirationStr))
                {
                    return null;
                }

                return DateTime.Parse(expirationStr);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting shareable link expiration: {FileUrl}", fileUrl);
                return null;
            }
        }

        public async Task<bool> ExtendShareableLinkAsync(string fileUrl, TimeSpan extension)
        {
            try
            {
                var metadata = await GetFileMetadataAsync(fileUrl);
                if (metadata?.CustomMetadata == null || !metadata.CustomMetadata.ContainsKey("ShareId"))
                    return false;

                var currentExpiration = await GetShareableLinkExpirationAsync(fileUrl);
                var newExpiration = currentExpiration.HasValue
                    ? currentExpiration.Value.Add(extension)
                    : DateTime.UtcNow.Add(extension);

                metadata.CustomMetadata["ShareExpiration"] = newExpiration.ToString("o");
                await SaveMetadataAsync(Path.GetFileName(fileUrl), metadata);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extending shareable link: {FileUrl}", fileUrl);
                return false;
            }
        }

        public async Task<Dictionary<string, string>> UploadMultipleFilesAsync(List<IFormFile> files)
        {
            try
            {
                var results = new Dictionary<string, string>();
                foreach (var file in files)
                {
                    try
                    {
                        var url = await UploadFileAsync(file);
                        results[file.FileName] = url;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error uploading file: {FileName}", file.FileName);
                        results[file.FileName] = string.Empty; // Use empty string instead of null
                    }
                }
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading multiple files");
                throw;
            }
        }

        public async Task<List<string>> SortFilesAsync(string directoryPath, string sortBy, bool ascending = true)
        {
            try
            {
                var fullPath = Path.Combine(_uploadDirectory, directoryPath.TrimStart('/'));
                return await Task.Run(() =>
                {
                    var files = Directory.GetFiles(fullPath);
                    return sortBy.ToLower() switch
                    {
                        "name" => ascending 
                            ? files.OrderBy(Path.GetFileName).ToList()
                            : files.OrderByDescending(Path.GetFileName).ToList(),
                        "date" => ascending
                            ? files.OrderBy(f => File.GetLastWriteTime(f)).ToList()
                            : files.OrderByDescending(f => File.GetLastWriteTime(f)).ToList(),
                        "size" => ascending
                            ? files.OrderBy(f => new FileInfo(f).Length).ToList()
                            : files.OrderByDescending(f => new FileInfo(f).Length).ToList(),
                        _ => files.ToList()
                    };
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sorting files: {DirectoryPath}", directoryPath);
                return new List<string>();
            }
        }

        public async Task<List<string>> FilterFilesAsync(string directoryPath, Dictionary<string, string> filters)
        {
            try
            {
                var fullPath = Path.Combine(_uploadDirectory, directoryPath.TrimStart('/'));
                if (!Directory.Exists(fullPath))
                    return new List<string>();

                var files = Directory.GetFiles(fullPath);
                var filteredFiles = new List<string>();

                foreach (var file in files)
                {
                    var metadata = await GetFileMetadataAsync($"/uploads/{directoryPath.TrimStart('/')}/{Path.GetFileName(file)}");
                    if (metadata == null) continue;

                    var matches = true;
                    foreach (var filter in filters)
                    {
                        if (!metadata.CustomMetadata.TryGetValue(filter.Key, out var value) ||
                            value != filter.Value)
                        {
                            matches = false;
                            break;
                        }
                    }

                    if (matches)
                    {
                        filteredFiles.Add($"/uploads/{directoryPath.TrimStart('/')}/{Path.GetFileName(file)}");
                    }
                }

                return filteredFiles;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error filtering files: {DirectoryPath}", directoryPath);
                throw;
            }
        }

        public async Task<List<string>> SearchFilesAsync(string directoryPath, string searchTerm, bool recursive = false)
        {
            try
            {
                var fullPath = Path.Combine(_uploadDirectory, directoryPath.TrimStart('/'));
                if (!Directory.Exists(fullPath))
                    return new List<string>();

                var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                var files = Directory.GetFiles(fullPath, "*.*", searchOption);
                var results = new List<string>();

                foreach (var file in files)
                {
                    var fileName = Path.GetFileName(file);
                    var metadata = await GetFileMetadataAsync($"/uploads/{directoryPath.TrimStart('/')}/{fileName}");

                    if (fileName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                        (metadata?.CustomMetadata != null &&
                         metadata.CustomMetadata.Values.Any(v => v.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))))
                    {
                        results.Add($"/uploads/{directoryPath.TrimStart('/')}/{fileName}");
                    }
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching files: {DirectoryPath}, {SearchTerm}", directoryPath, searchTerm);
                throw;
            }
        }

        public async Task<List<string>> GetFilesByDateRangeAsync(string directoryPath, DateTime startDate, DateTime endDate)
        {
            try
            {
                var fullPath = Path.Combine(_uploadDirectory, directoryPath.TrimStart('/'));
                return await Task.Run(() => Directory.GetFiles(fullPath)
                    .Where(f => File.GetLastWriteTime(f) >= startDate && File.GetLastWriteTime(f) <= endDate)
                    .Select(Path.GetFileName)
                    .Where(f => f != null)
                    .Select(f => f!)
                    .ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting files by date range: {DirectoryPath}", directoryPath);
                return new List<string>();
            }
        }

        public async Task<List<string>> GetFilesBySizeRangeAsync(string directoryPath, long minSize, long maxSize)
        {
            try
            {
                var fullPath = Path.Combine(_uploadDirectory, directoryPath.TrimStart('/'));
                return await Task.Run(() => Directory.GetFiles(fullPath)
                    .Where(f => new FileInfo(f).Length >= minSize && new FileInfo(f).Length <= maxSize)
                    .Select(Path.GetFileName)
                    .Where(f => f != null)
                    .Select(f => f!)
                    .ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting files by size range: {DirectoryPath}", directoryPath);
                return new List<string>();
            }
        }

        public async Task<List<string>> GetFilesByTypeAsync(string directoryPath, string fileType)
        {
            try
            {
                var fullPath = Path.Combine(_uploadDirectory, directoryPath.TrimStart('/'));
                return await Task.Run(() => Directory.GetFiles(fullPath, $"*{fileType}")
                    .Select(Path.GetFileName)
                    .Where(f => f != null)
                    .Select(f => f!)
                    .ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting files by type: {DirectoryPath}", directoryPath);
                return new List<string>();
            }
        }

        public async Task<List<string>> GetFilesByTagsAsync(string directoryPath, List<string> tags)
        {
            try
            {
                var fullPath = Path.Combine(_uploadDirectory, directoryPath.TrimStart('/'));
                if (!Directory.Exists(fullPath))
                    return new List<string>();

                var files = Directory.GetFiles(fullPath);
                var results = new List<string>();

                foreach (var file in files)
                {
                    var metadata = await GetFileMetadataAsync($"/uploads/{directoryPath.TrimStart('/')}/{Path.GetFileName(file)}");
                    if (metadata?.CustomMetadata == null) continue;

                    if (metadata.CustomMetadata.TryGetValue("Tags", out var fileTags) &&
                        tags.All(tag => fileTags.Contains(tag, StringComparison.OrdinalIgnoreCase)))
                    {
                        results.Add($"/uploads/{directoryPath.TrimStart('/')}/{Path.GetFileName(file)}");
                    }
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting files by tags: {DirectoryPath}", directoryPath);
                throw;
            }
        }

        public async Task<List<string>> GetFilesByMetadataAsync(string directoryPath, Dictionary<string, string> metadata)
        {
            try
            {
                var fullPath = Path.Combine(_uploadDirectory, directoryPath.TrimStart('/'));
                if (!Directory.Exists(fullPath))
                    return new List<string>();

                var files = Directory.GetFiles(fullPath);
                var results = new List<string>();

                foreach (var file in files)
                {
                    var fileMetadata = await GetFileMetadataAsync($"/uploads/{directoryPath.TrimStart('/')}/{Path.GetFileName(file)}");
                    if (fileMetadata?.CustomMetadata == null) continue;

                    var matches = true;
                    foreach (var kvp in metadata)
                    {
                        if (!fileMetadata.CustomMetadata.TryGetValue(kvp.Key, out var value) ||
                            value != kvp.Value)
                        {
                            matches = false;
                            break;
                        }
                    }

                    if (matches)
                    {
                        results.Add($"/uploads/{directoryPath.TrimStart('/')}/{Path.GetFileName(file)}");
                    }
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting files by metadata: {DirectoryPath}", directoryPath);
                throw;
            }
        }

        public async Task<bool> CreateSearchIndexAsync(string directoryPath)
        {
            try
            {
                var fullPath = Path.Combine(_uploadDirectory, directoryPath.TrimStart('/'));
                if (!Directory.Exists(fullPath))
                    return false;

                var indexPath = Path.Combine(_uploadDirectory, "search_index", directoryPath.TrimStart('/'));
                if (!Directory.Exists(indexPath))
                {
                    Directory.CreateDirectory(indexPath);
                }

                var files = Directory.GetFiles(fullPath);
                foreach (var file in files)
                {
                    var metadata = await GetFileMetadataAsync($"/uploads/{directoryPath.TrimStart('/')}/{Path.GetFileName(file)}");
                    if (metadata == null) continue;

                    var indexFile = Path.Combine(indexPath, $"{Path.GetFileNameWithoutExtension(file)}.json");
                    var indexData = new
                    {
                        FileName = Path.GetFileName(file),
                        ContentType = metadata.ContentType,
                        FileSize = metadata.FileSize,
                        CreatedAt = metadata.CreatedAt,
                        LastModified = metadata.LastModified,
                        Metadata = metadata.CustomMetadata ?? new Dictionary<string, string>()
                    };

                    var json = System.Text.Json.JsonSerializer.Serialize(indexData);
                    await File.WriteAllTextAsync(indexFile, json);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating search index: {DirectoryPath}", directoryPath);
                return false;
            }
        }

        public async Task<bool> UpdateSearchIndexAsync(string directoryPath)
        {
            try
            {
                await DeleteSearchIndexAsync(directoryPath);
                return await CreateSearchIndexAsync(directoryPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating search index: {DirectoryPath}", directoryPath);
                return false;
            }
        }

        public async Task<bool> DeleteSearchIndexAsync(string directoryPath)
        {
            try
            {
                var indexPath = Path.Combine(_uploadDirectory, directoryPath.TrimStart('/'), ".searchindex");
                await Task.Run(() => File.Delete(indexPath));
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting search index: {DirectoryPath}", directoryPath);
                return false;
            }
        }

        public async Task<List<string>> SearchInIndexAsync(string? directoryPath = null, string? searchTerm = null)
        {
            try
            {
                if (string.IsNullOrEmpty(searchTerm))
                {
                    return new List<string>();
                }

                var fullPath = directoryPath != null 
                    ? Path.Combine(_uploadDirectory, directoryPath)
                    : _uploadDirectory;

                if (!Directory.Exists(fullPath))
                {
                    return new List<string>();
                }

                return await Task.Run(() => Directory.GetFiles(fullPath, "*.*")
                    .Where(f => Path.GetFileName(f).Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                    .Select(Path.GetFileName)
                    .Where(f => f != null)
                    .Select(f => f!)
                    .ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching in index: {DirectoryPath}, {SearchTerm}", directoryPath, searchTerm);
                return new List<string>();
            }
        }

        public async Task<bool> DeleteMultipleFilesAsync(List<string> fileUrls)
        {
            try
            {
                var success = true;
                foreach (var fileUrl in fileUrls)
                {
                    try
                    {
                        await DeleteFileAsync(fileUrl);
                        success = false;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error deleting file: {FileUrl}", fileUrl);
                        success = false;
                    }
                }
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting multiple files");
                return false;
            }
        }

        public async Task<Dictionary<string, bool>> ValidateMultipleFilesAsync(List<string> fileUrls)
        {
            try
            {
                var results = new Dictionary<string, bool>();
                foreach (var fileUrl in fileUrls)
                {
                    try
                    {
                        var isValid = await ValidateFileAsync(fileUrl);
                        results[fileUrl] = isValid;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error validating file: {FileUrl}", fileUrl);
                        results[fileUrl] = false;
                    }
                }
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating multiple files");
                throw;
            }
        }

        public async Task<Dictionary<string, string>> GenerateMultipleThumbnailsAsync(List<string> fileUrls)
        {
            try
            {
                var results = new Dictionary<string, string>();
                foreach (var fileUrl in fileUrls)
                {
                    try
                    {
                        var contentType = await GetFileMimeTypeAsync(fileUrl);
                        if (IsImageFile(Path.GetExtension(fileUrl)))
                        {
                            var thumbnailUrl = await GenerateThumbnailAsync(fileUrl);
                            results[fileUrl] = thumbnailUrl;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error generating thumbnail for file: {FileUrl}", fileUrl);
                    }
                }
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating multiple thumbnails");
                throw;
            }
        }

        public async Task<Dictionary<string, Dictionary<string, object>>> AnalyzeMultipleFilesAsync(List<string> fileUrls)
        {
            try
            {
                var results = new Dictionary<string, Dictionary<string, object>>();
                foreach (var fileUrl in fileUrls)
                {
                    try
                    {
                        var analysis = await AnalyzeFileAsync(fileUrl);
                        results[fileUrl] = analysis;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error analyzing file: {FileUrl}", fileUrl);
                    }
                }
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing multiple files");
                throw;
            }
        }

        public async Task<Dictionary<string, double>> GetSearchSuggestionsAsync(string searchTerm)
        {
            try
            {
                var suggestions = new Dictionary<string, double>();
                var searchIndexPath = Path.Combine(_uploadDirectory, "search_index");

                if (!Directory.Exists(searchIndexPath))
                    return suggestions;

                var indexFiles = Directory.GetFiles(searchIndexPath, "*.json", SearchOption.AllDirectories);
                foreach (var indexFile in indexFiles)
                {
                    var json = await File.ReadAllTextAsync(indexFile);
                    var indexData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);

                    if (indexData != null)
                    {
                        foreach (var value in indexData.Values)
                        {
                            var stringValue = value?.ToString();

                            if (string.IsNullOrWhiteSpace(stringValue))
                                continue;

                            var similarity = CalculateStringSimilarity(searchTerm, stringValue);

                            if (!suggestions.ContainsKey(stringValue) || suggestions[stringValue] < similarity)
                            {
                                suggestions[stringValue] = similarity;
                            }
                        }

                    }
                }

                return suggestions.OrderByDescending(x => x.Value)
                    .ToDictionary(x => x.Key, x => x.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting search suggestions: {SearchTerm}", searchTerm);
                throw;
            }
        }

        private double CalculateStringSimilarity(string s1, string s2)
        {
            // Simple Levenshtein distance-based similarity
            var distance = LevenshteinDistance(s1, s2);
            var maxLength = Math.Max(s1.Length, s2.Length);
            return maxLength == 0 ? 1.0 : 1.0 - (double)distance / maxLength;
        }

        private int LevenshteinDistance(string s1, string s2)
        {
            var n = s1.Length;
            var m = s2.Length;
            var d = new int[n + 1, m + 1];

            if (n == 0) return m;
            if (m == 0) return n;

            for (var i = 0; i <= n; i++) d[i, 0] = i;
            for (var j = 0; j <= m; j++) d[0, j] = j;

            for (var j = 1; j <= m; j++)
            {
                for (var i = 1; i <= n; i++)
                {
                    var cost = (s2[j - 1] == s1[i - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }

            return d[n, m];
        }

        public async Task<bool> IsIndexUpToDateAsync(string directoryPath)
        {
            try
            {
                var indexPath = Path.Combine(_uploadDirectory, directoryPath.TrimStart('/'), ".searchindex");
                var dirPath = Path.Combine(_uploadDirectory, directoryPath.TrimStart('/'));
                
                return await Task.Run(() =>
                {
                    if (!File.Exists(indexPath)) return false;
                    var indexTime = File.GetLastWriteTime(indexPath);
                    var latestFileTime = Directory.GetFiles(dirPath)
                        .Select(f => File.GetLastWriteTime(f))
                        .DefaultIfEmpty()
                        .Max();
                    return indexTime >= latestFileTime;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking index status: {DirectoryPath}", directoryPath);
                return false;
            }
        }

        public async Task<DateTime> GetLastIndexUpdateAsync(string directoryPath)
        {
            try
            {
                var indexPath = Path.Combine(_uploadDirectory, directoryPath.TrimStart('/'), ".searchindex");
                return await Task.Run(() => File.Exists(indexPath) 
                    ? File.GetLastWriteTime(indexPath) 
                    : DateTime.MinValue);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting last index update: {DirectoryPath}", directoryPath);
                return DateTime.MinValue;
            }
        }

        public async Task<Dictionary<string, int>> GetSearchStatisticsAsync(string directoryPath)
        {
            try
            {
                var indexPath = Path.Combine(_uploadDirectory, "search_index", directoryPath.TrimStart('/'));
                if (!Directory.Exists(indexPath))
                    return new Dictionary<string, int>();

                var indexFiles = Directory.GetFiles(indexPath, "*.json");
                var stats = new Dictionary<string, int>
                {
                    { "TotalIndexedFiles", indexFiles.Length },
                    { "IndexSize", (int)indexFiles.Sum(f => new FileInfo(f).Length) },
                    { "LastUpdateDays", (int)(DateTime.UtcNow - await GetLastIndexUpdateAsync(directoryPath)).TotalDays },
                    { "IsUpToDate", await IsIndexUpToDateAsync(directoryPath) ? 1 : 0 }
                };

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting search statistics: {DirectoryPath}", directoryPath);
                throw;
            }
        }

        public async Task<Dictionary<string, long>> GetStorageStatisticsAsync(string? directoryPath = null)
        {
            try
            {
                var fullPath = directoryPath != null 
                    ? Path.Combine(_uploadDirectory, directoryPath)
                    : _uploadDirectory;

                if (!Directory.Exists(fullPath))
                {
                    return new Dictionary<string, long>();
                }

                var stats = new Dictionary<string, long>();
                var files = await Task.Run(() => Directory.GetFiles(fullPath, "*.*", SearchOption.AllDirectories));
                
                stats["TotalFiles"] = files.Length;
                stats["TotalSize"] = await Task.Run(() => files.Sum(f => new FileInfo(f).Length));
                stats["AverageFileSize"] = files.Length > 0 ? 
                    await Task.Run(() => (long)files.Average(f => new FileInfo(f).Length)) : 0L;
                
                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting storage statistics: {DirectoryPath}", directoryPath);
                return new Dictionary<string, long>();
            }
        }

        public async Task<Dictionary<string, object>> GenerateStorageReportAsync(string? directoryPath = null, DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                var fullPath = directoryPath != null 
                    ? Path.Combine(_uploadDirectory, directoryPath)
                    : _uploadDirectory;

                if (!Directory.Exists(fullPath))
                {
                    return new Dictionary<string, object>();
                }

                var report = new Dictionary<string, object>();
                var files = await Task.Run(() => Directory.GetFiles(fullPath, "*.*", SearchOption.AllDirectories));

                if (startDate.HasValue && endDate.HasValue)
                {
                    files = await Task.Run(() => files.Where(f => 
                        File.GetLastWriteTime(f) >= startDate.Value && 
                        File.GetLastWriteTime(f) <= endDate.Value).ToArray());
                }

                report["TotalFiles"] = files.Length;
                report["TotalSize"] = await Task.Run(() => files.Sum(f => new FileInfo(f).Length));
                report["AverageFileSize"] = files.Length > 0 ? 
                    await Task.Run(() => (long)files.Average(f => new FileInfo(f).Length)) : 0L;
                report["LastModified"] = await Task.Run(() => files.Max(f => File.GetLastWriteTime(f)));
                report["FileTypes"] = await Task.Run(() => files.GroupBy(f => Path.GetExtension(f).ToLower())
                    .ToDictionary(g => g.Key, g => g.Count()));

                return report;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating storage report: {DirectoryPath}", directoryPath);
                return new Dictionary<string, object>();
            }
        }

        public async Task<bool> IsStorageQuotaExceededAsync(string userId)
        {
            try
            {
                var quota = await GetUserStorageQuotaAsync(userId);
                var usage = await GetStorageUsageByUserAsync();
                
                if (quota.TryGetValue("TotalQuota", out var totalQuotaObj) && totalQuotaObj is long totalQuota)
                    return usage.TryGetValue(userId, out var userUsage) && userUsage > totalQuota;
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking storage quota: {UserId}", userId);
                throw;
            }
        }


        public async Task<string> ConvertImageFormatAsync(string fileUrl, string targetFormat, int? quality = null)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                if (!File.Exists(filePath))
                    throw new FileNotFoundException("File not found", fileUrl);

                var contentType = GetMimeType(filePath);
                if (!IsImageFile(Path.GetExtension(filePath)))
                    throw new ArgumentException("File is not an image");

                var convertedPath = Path.Combine(_uploadDirectory, $"converted_{Path.GetFileNameWithoutExtension(filePath)}.{targetFormat}");

                using var image = await Image.LoadAsync(filePath);

                IImageEncoder encoder = targetFormat.ToLower() switch
                {
                    "jpeg" or "jpg" => new JpegEncoder { Quality = quality ?? 75 },
                    "png" => new PngEncoder(),
                    "bmp" => new BmpEncoder(),
                    "gif" => new GifEncoder(),
                    _ => throw new NotSupportedException($"Unsupported target format: {targetFormat}")
                };

                await image.SaveAsync(convertedPath, encoder);

                return $"/uploads/converted_{Path.GetFileNameWithoutExtension(filePath)}.{targetFormat}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting image format: {FileUrl}", fileUrl);
                throw;
            }
        }

        public async Task<string> ConvertVideoFormatAsync(string fileUrl, string targetFormat, Dictionary<string, string>? options = null)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                if (!File.Exists(filePath))
                    throw new FileNotFoundException("File not found", filePath);

                var outputPath = Path.Combine(
                    Path.GetDirectoryName(filePath)!,
                    $"{Path.GetFileNameWithoutExtension(filePath)}_converted{targetFormat}"
                );

                await Task.Run(() =>
                {
                    // Video format conversion logic here
                    // This is a placeholder for actual video conversion
                    File.Copy(filePath, outputPath, true);
                });

                return Path.GetFileName(outputPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting video format: {FileUrl}", fileUrl);
                throw;
            }
        }

        public async Task<string> ConvertAudioFormatAsync(string fileUrl, string targetFormat, Dictionary<string, string>? options = null)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                if (!File.Exists(filePath))
                    throw new FileNotFoundException("File not found", filePath);

                var outputPath = Path.Combine(
                    Path.GetDirectoryName(filePath)!,
                    $"{Path.GetFileNameWithoutExtension(filePath)}_converted{targetFormat}"
                );

                await Task.Run(() =>
                {
                    // Audio format conversion logic here
                    // This is a placeholder for actual audio conversion
                    File.Copy(filePath, outputPath, true);
                });

                return Path.GetFileName(outputPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting audio format: {FileUrl}", fileUrl);
                throw;
            }
        }

        public async Task<Dictionary<string, int>> GetFileTypeDistributionAsync(string? directoryPath = null)
        {
            try
            {
                var fullPath = directoryPath != null 
                    ? Path.Combine(_uploadDirectory, directoryPath)
                    : _uploadDirectory;

                if (!Directory.Exists(fullPath))
                {
                    return new Dictionary<string, int>();
                }

                var files = await Task.Run(() => Directory.GetFiles(fullPath, "*.*", SearchOption.AllDirectories));
                
                return await Task.Run(() => files
                    .GroupBy(f => Path.GetExtension(f).ToLower())
                    .ToDictionary(g => g.Key, g => g.Count()));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting file type distribution: {DirectoryPath}", directoryPath);
                return new Dictionary<string, int>();
            }
        }

        public async Task<Dictionary<string, long>> GetStorageUsageByUserAsync(string? directory = null)
        {
            try
            {
                var fullPath = directory != null 
                    ? Path.Combine(_uploadDirectory, directory)
                    : _uploadDirectory;

                if (!Directory.Exists(fullPath))
                {
                    return new Dictionary<string, long>();
                }

                var files = await Task.Run(() => Directory.GetFiles(fullPath, "*.*", SearchOption.AllDirectories));
                
                return await Task.Run(() => files
                    .GroupBy(f => Path.GetDirectoryName(f)?.Split(Path.DirectorySeparatorChar).LastOrDefault() ?? "unknown")
                    .ToDictionary(g => g.Key, g => g.Sum(f => new FileInfo(f).Length)));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting storage usage by user: {Directory}", directory);
                return new Dictionary<string, long>();
            }
        }

        public async Task<Dictionary<string, object>> GetUserStorageQuotaAsync(string userId)
        {
            try
            {
                // TODO: Implement actual quota retrieval from configuration or database
                // This is a placeholder implementation
                var quota = new Dictionary<string, object>
                {
                    { "TotalQuota", 1024 * 1024 * 1024 }, // 1GB default quota
                    { "UsedSpace", 0L },
                    { "RemainingSpace", 1024 * 1024 * 1024 },
                    { "QuotaPercentage", 0.0 },
                    { "LastUpdated", DateTime.UtcNow }
                };

                var usage = await GetStorageUsageByUserAsync();
                if (usage.TryGetValue(userId, out var usedSpace))
                {
                    quota["UsedSpace"] = usedSpace;
                    quota["RemainingSpace"] = (long)quota["TotalQuota"] - usedSpace;
                    quota["QuotaPercentage"] = (double)usedSpace / (long)quota["TotalQuota"] * 100;
                }

                return quota;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user storage quota: {UserId}", userId);
                throw;
            }
        }

        public async Task<Dictionary<string, object>> GetStorageTrendsAsync(string? directory = null, TimeSpan period = default)
        {
            try
            {
                var fullPath = directory != null 
                    ? Path.Combine(_uploadDirectory, directory)
                    : _uploadDirectory;

                if (!Directory.Exists(fullPath))
                {
                    return new Dictionary<string, object>();
                }

                var files = await Task.Run(() => Directory.GetFiles(fullPath, "*.*", SearchOption.AllDirectories));
                var cutoff = DateTime.Now - (period == default ? TimeSpan.FromDays(30) : period);

                return await Task.Run(() =>
                {
                    var trends = new Dictionary<string, object>
                    {
                        ["TotalFiles"] = files.Length,
                        ["TotalSize"] = files.Sum(f => new FileInfo(f).Length),
                        ["FilesAdded"] = files.Count(f => File.GetCreationTime(f) >= cutoff),
                        ["FilesModified"] = files.Count(f => File.GetLastWriteTime(f) >= cutoff),
                        ["AverageFileSize"] = files.Length > 0 ? (long)files.Average(f => new FileInfo(f).Length) : 0L,
                        ["LargestFile"] = files.Length > 0 ? files.Max(f => new FileInfo(f).Length) : 0L,
                        ["MostCommonType"] = files.Length > 0 
                            ? files.GroupBy(f => Path.GetExtension(f).ToLower())
                                .OrderByDescending(g => g.Count())
                                .First().Key 
                            : "none"
                    };

                    return trends;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting storage trends: {Directory}", directory);
                return new Dictionary<string, object>();
            }
        }


        public async Task<bool> IsFormatConversionSupportedAsync(string sourceFormat, string targetFormat)
        {
            try
            {
                var supportedConversions = await GetSupportedFormatsAsync();
                return supportedConversions.TryGetValue(sourceFormat, out var targets) &&
                       targets.Contains(targetFormat);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking format conversion support: {SourceFormat} -> {TargetFormat}", sourceFormat, targetFormat);
                return false;
            }
        }

        public async Task<Dictionary<string, List<string>>> GetSupportedFormatsAsync()
        {
            try
            {
                return await Task.Run(() => new Dictionary<string, List<string>>
                {
                    ["Image"] = new List<string> { ".jpg", ".jpeg", ".png", ".gif", ".bmp" },
                    ["Document"] = new List<string> { ".pdf", ".doc", ".docx", ".txt", ".rtf" },
                    ["Video"] = new List<string> { ".mp4", ".avi", ".mov", ".wmv" },
                    ["Audio"] = new List<string> { ".mp3", ".wav", ".ogg", ".m4a" }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting supported formats");
                return new Dictionary<string, List<string>>();
            }
        }

        public async Task<Dictionary<string, object>> GetFormatConversionOptionsAsync(string sourceFormat, string targetFormat)
        {
            try
            {
                if (!await IsFormatConversionSupportedAsync(sourceFormat, targetFormat))
                    throw new ArgumentException($"Conversion from {sourceFormat} to {targetFormat} is not supported");

                var options = new Dictionary<string, object>();

                if (IsImageFile(sourceFormat) && IsImageFile(targetFormat))
                {
                    options["Quality"] = "0-100";
                    options["PreserveMetadata"] = true;
                    options["OptimizeForWeb"] = true;
                }
                else if (sourceFormat.StartsWith(".doc") || targetFormat.StartsWith(".doc"))
                {
                    options["PreserveFormatting"] = true;
                    options["IncludeImages"] = true;
                    options["PageRange"] = "e.g., 1-5,7,9-12";
                }
                else if (sourceFormat.StartsWith(".mp") || targetFormat.StartsWith(".mp"))
                {
                    options["Bitrate"] = "e.g., 128k, 256k, 320k";
                    options["SampleRate"] = "e.g., 44100, 48000";
                    options["Channels"] = "1 (mono) or 2 (stereo)";
                }

                return options;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting format conversion options: {SourceFormat} -> {TargetFormat}", sourceFormat, targetFormat);
                throw;
            }
        }

        public async Task<string> OptimizeImageForWebAsync(string fileUrl, int maxWidth, int maxHeight, int quality)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                if (!File.Exists(filePath))
                    throw new FileNotFoundException("File not found", fileUrl);

                var contentType = GetMimeType(filePath);
                if (!IsImageFile(Path.GetExtension(filePath)))
                    throw new ArgumentException("File is not an image");

                var optimizedPath = Path.Combine(_uploadDirectory, $"web_optimized_{Path.GetFileNameWithoutExtension(filePath)}.jpg");

                using var image = await Image.LoadAsync(filePath);
                var ratio = Math.Min(
                    (float)maxWidth / image.Width,
                    (float)maxHeight / image.Height
                );

                int newWidth = (int)(image.Width * ratio);
                int newHeight = (int)(image.Height * ratio);

                image.Mutate(x => x.Resize(newWidth, newHeight));
                await image.SaveAsJpegAsync(optimizedPath, new JpegEncoder { Quality = quality });

                return $"/uploads/web_optimized_{Path.GetFileNameWithoutExtension(filePath)}.jpg";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error optimizing image for web: {FileUrl}", fileUrl);
                throw;
            }
        }


        public async Task<string> OptimizeVideoForWebAsync(string fileUrl, int maxWidth, int maxHeight, int bitrate)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                if (!File.Exists(filePath))
                    throw new FileNotFoundException("File not found", filePath);

                var outputPath = Path.Combine(
                    Path.GetDirectoryName(filePath)!,
                    $"{Path.GetFileNameWithoutExtension(filePath)}_optimized{Path.GetExtension(filePath)}"
                );

                await Task.Run(() =>
                {
                    // Video optimization logic here
                    // This is a placeholder for actual video processing
                    File.Copy(filePath, outputPath, true);
                });

                return Path.GetFileName(outputPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error optimizing video for web: {FileUrl}", fileUrl);
                throw;
            }
        }

        public async Task<string> OptimizeAudioForWebAsync(string fileUrl, int bitrate)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                if (!File.Exists(filePath))
                    throw new FileNotFoundException("File not found", filePath);

                var outputPath = Path.Combine(
                    Path.GetDirectoryName(filePath)!,
                    $"{Path.GetFileNameWithoutExtension(filePath)}_optimized{Path.GetExtension(filePath)}"
                );

                await Task.Run(() =>
                {
                    // Audio optimization logic here
                    // This is a placeholder for actual audio processing
                    File.Copy(filePath, outputPath, true);
                });

                return Path.GetFileName(outputPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error optimizing audio for web: {FileUrl}", fileUrl);
                throw;
            }
        }

        public async Task<string> OptimizeDocumentForWebAsync(string fileUrl, bool compressImages)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                if (!File.Exists(filePath))
                    throw new FileNotFoundException("File not found", filePath);

                var outputPath = Path.Combine(
                    Path.GetDirectoryName(filePath)!,
                    $"{Path.GetFileNameWithoutExtension(filePath)}_optimized{Path.GetExtension(filePath)}"
                );

                await Task.Run(() =>
                {
                    // Document optimization logic here
                    // This is a placeholder for actual document processing
                    File.Copy(filePath, outputPath, true);
                });

                return Path.GetFileName(outputPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error optimizing document for web: {FileUrl}", fileUrl);
                throw;
            }
        }

        public async Task<Dictionary<string, object>> GetOptimizationStatisticsAsync(string fileUrl)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                var fileInfo = new FileInfo(filePath);
                
                return await Task.Run(() =>
                {
                    var stats = new Dictionary<string, object>
                    {
                        ["OriginalSize"] = fileInfo.Length,
                        ["CompressedSize"] = (long)(fileInfo.Length * 0.7), // Simulated compression
                        ["OptimizationRatio"] = 0.7,
                        ["LastOptimized"] = fileInfo.LastWriteTime
                    };
                    
                    return stats;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting optimization statistics: {FileUrl}", fileUrl);
                return new Dictionary<string, object>();
            }
        }

        public async Task<bool> IsOptimizationNeededAsync(string fileUrl)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                if (!File.Exists(filePath))
                    return false;

                var fileInfo = new FileInfo(filePath);
                var contentType = GetMimeType(filePath);

                // Check if file is too large
                if (fileInfo.Length > 5 * 1024 * 1024) // 5MB
                    return true;

                // Check if file is an image and needs optimization
                if (IsImageFile(Path.GetExtension(filePath)))
                {
                    using var image = await Image.LoadAsync(filePath);
                    {
                        if (image.Width > 1920 || image.Height > 1080)
                            return true;
                    }
                }

                // Check if file is a video and needs optimization
                if (contentType.StartsWith("video/"))
                {
                    // TODO: Implement video size/quality check
                    return true;
                }

                // Check if file is an audio file and needs optimization
                if (contentType.StartsWith("audio/"))
                {
                    // TODO: Implement audio quality check
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if optimization is needed: {FileUrl}", fileUrl);
                return false;
            }
        }

        public async Task<bool> ValidateFileSignatureAsync(string fileUrl)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                if (!File.Exists(filePath))
                    return false;

                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                var contentType = GetMimeType(filePath);

                // Read file signature (first few bytes)
                using (var stream = new FileStream(filePath, FileMode.Open))
                {
                    var signature = new byte[8];
                    var bytesRead = await stream.ReadAsync(signature, 0, signature.Length);

                    // Check common file signatures
                    switch (extension)
                    {
                        case ".jpg":
                        case ".jpeg":
                            return signature[0] == 0xFF && signature[1] == 0xD8;
                        case ".png":
                            return signature[0] == 0x89 && signature[1] == 0x50 && signature[2] == 0x4E && signature[3] == 0x47;
                        case ".gif":
                            return signature[0] == 0x47 && signature[1] == 0x49 && signature[2] == 0x46;
                        case ".pdf":
                            return signature[0] == 0x25 && signature[1] == 0x50 && signature[2] == 0x44 && signature[3] == 0x46;
                        default:
                            return true; // Unknown file type, assume valid
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating file signature: {FileUrl}", fileUrl);
                return false;
            }
        }

        public async Task<bool> ValidateFileChecksumAsync(string fileUrl)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                if (!File.Exists(filePath))
                    return false;

                var metadata = await GetFileMetadataAsync(fileUrl);
                if (metadata == null)
                    return false;

                var currentHash = await CalculateFileHashAsync(filePath);
                return currentHash == metadata.Hash;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating file checksum: {FileUrl}", fileUrl);
                return false;
            }
        }

        public async Task<bool> ValidateFilePermissionsAsync(string fileUrl)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                if (!File.Exists(filePath))
                    return false;

                var fileInfo = new FileInfo(filePath);
                var permissions = await GetFilePermissionsAsync(fileUrl);

                // Check if file has basic read permissions
                try
                {
                    using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                    {
                        // File is readable
                    }
                }
                catch
                {
                    return false;
                }

                // Check if file has required security permissions
                if (!permissions.ContainsKey("security") || permissions["security"] != "granted")
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating file permissions: {FileUrl}", fileUrl);
                return false;
            }
        }

        public async Task<Dictionary<string, object>> GetFileSecurityInfoAsync(string fileUrl)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                if (!File.Exists(filePath))
                    throw new FileNotFoundException("File not found", fileUrl);

                var fileInfo = new FileInfo(filePath);
                var permissions = await GetFilePermissionsAsync(fileUrl);
                var metadata = await GetFileMetadataAsync(fileUrl);

                var securityInfo = new Dictionary<string, object>
                {
                    { "FileName", fileInfo.Name },
                    { "FileSize", fileInfo.Length },
                    { "CreatedAt", fileInfo.CreationTimeUtc },
                    { "LastModified", fileInfo.LastWriteTimeUtc },
                    { "LastAccessed", fileInfo.LastAccessTimeUtc },
                    { "IsReadOnly", fileInfo.IsReadOnly },
                    { "Permissions", permissions },
                    { "Metadata", metadata?.CustomMetadata ?? new Dictionary<string, string>() },
                    { "IsEncrypted", await IsFileEncryptedAsync(fileUrl) },
                    { "IsCompressed", await IsFileCompressedAsync(fileUrl) },
                    { "HasValidSignature", await ValidateFileSignatureAsync(fileUrl) },
                    { "HasValidChecksum", await ValidateFileChecksumAsync(fileUrl) }
                };

                return securityInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting file security info: {FileUrl}", fileUrl);
                throw;
            }
        }

        public async Task<bool> IsFileSecureAsync(string fileUrl)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"File not found: {fileUrl}");
                }

                // Check file permissions
                var fileInfo = new FileInfo(filePath);
                var permissions = OperatingSystem.IsWindows() 
                    ? fileInfo.GetAccessControl() 
                    : null;
                
                // Check if file is encrypted
                var isEncrypted = await IsFileEncryptedAsync(fileUrl);
                

                return permissions != null && isEncrypted;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking file security for: {fileUrl}");
                return false;
            }
        }

        public async Task<bool> IsFileCompliantAsync(string fileUrl, string complianceStandard)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"File not found: {fileUrl}");
                }

                // Implement compliance checks based on the standard
                switch (complianceStandard.ToLower())
                {
                    case "gdpr":
                        return await CheckGDPRComplianceAsync(fileUrl);
                    case "hipaa":
                        return await CheckHIPAAComplianceAsync(fileUrl);
                    default:
                        throw new ArgumentException($"Unsupported compliance standard: {complianceStandard}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking file compliance for: {fileUrl}");
                return false;
            }
        }

        public async Task<List<Dictionary<string, object>>> GetFileOperationHistoryAsync(string fileUrl)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                return await Task.Run(() =>
                {
                    var history = new List<Dictionary<string, object>>();
                    var fileInfo = new FileInfo(filePath);
                    
                    history.Add(new Dictionary<string, object>
                    {
                        ["Operation"] = "Created",
                        ["Timestamp"] = fileInfo.CreationTime,
                        ["User"] = "System"
                    });
                    
                    history.Add(new Dictionary<string, object>
                    {
                        ["Operation"] = "Modified",
                        ["Timestamp"] = fileInfo.LastWriteTime,
                        ["User"] = "System"
                    });
                    
                    return history;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting file operation history: {FileUrl}", fileUrl);
                return new List<Dictionary<string, object>>();
            }
        }

        public async Task<Dictionary<string, object>> GetFileAuditTrailAsync(string fileUrl)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                return await Task.Run(() =>
                {
                    var fileInfo = new FileInfo(filePath);
                    return new Dictionary<string, object>
                    {
                        ["Created"] = fileInfo.CreationTime,
                        ["Modified"] = fileInfo.LastWriteTime,
                        ["Accessed"] = fileInfo.LastAccessTime,
                        ["Size"] = fileInfo.Length,
                        ["Permissions"] = fileInfo.IsReadOnly ? "ReadOnly" : "ReadWrite"
                    };
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting file audit trail: {FileUrl}", fileUrl);
                return new Dictionary<string, object>();
            }
        }

        public async Task<bool> LogFileOperationAsync(string fileUrl, string operation, string userId)
        {
            try
            {
                var logPath = Path.Combine(_uploadDirectory, "logs", "file_operations.log");
                var logDir = Path.GetDirectoryName(logPath);
                
                await Task.Run(() =>
                {
                    if (string.IsNullOrEmpty(logDir))
                        throw new ArgumentNullException(nameof(logDir), "Log directory path cannot be null or empty");

                    if (!Directory.Exists(logDir))
                        Directory.CreateDirectory(logDir);
                        
                    var logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {operation} - {fileUrl} - {userId}";
                    File.AppendAllText(logPath, logEntry + Environment.NewLine);
                });
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging file operation: {FileUrl}", fileUrl);
                return false;
            }
        }

        public async Task<List<Dictionary<string, object>>> GetRecentOperationsAsync(string fileUrl, int count)
        {
            try
            {
                var logPath = Path.Combine(_uploadDirectory, "logs", "file_operations.log");
                return await Task.Run(() =>
                {
                    if (!File.Exists(logPath))
                        return new List<Dictionary<string, object>>();
                        
                    var operations = File.ReadLines(logPath)
                        .Where(line => line.Contains(fileUrl))
                        .TakeLast(count)
                        .Select(line =>
                        {
                            var parts = line.Split(" - ");
                            return new Dictionary<string, object>
                            {
                                ["Timestamp"] = DateTime.Parse(parts[0]),
                                ["Operation"] = parts[1],
                                ["User"] = parts[3]
                            };
                        })
                        .ToList();
                        
                    return operations;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent operations: {FileUrl}", fileUrl);
                return new List<Dictionary<string, object>>();
            }
        }

        public async Task<DateTime?> GetLastSyncTimeAsync(string fileUrl)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                var syncPath = filePath + ".sync";
                
                if (!File.Exists(syncPath))
                    return null;
                    
                return await Task.Run(() => File.GetLastWriteTime(syncPath));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting last sync time: {FileUrl}", fileUrl);
                return null;
            }
        }

        public async Task<bool> CancelScheduledBackupAsync(string fileUrl)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                var schedulePath = filePath + ".schedule";
                
                await Task.Run(() =>
                {
                    if (File.Exists(schedulePath))
                        File.Delete(schedulePath);
                });
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error canceling scheduled backup: {FileUrl}", fileUrl);
                return false;
            }
        }

        public async Task<Dictionary<string, object>> GetBackupScheduleAsync(string fileUrl)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                var schedulePath = filePath + ".schedule";
                
                return await Task.Run(() =>
                {
                    if (!File.Exists(schedulePath))
                        return new Dictionary<string, object>();
                        
                    var schedule = JsonSerializer.Deserialize<Dictionary<string, string>>(
                        File.ReadAllText(schedulePath));
                        
                    if (schedule == null)
                        throw new ArgumentNullException(nameof(schedule), "Schedule dictionary cannot be null");

                    if (!schedule.TryGetValue("Interval", out var intervalValue) || string.IsNullOrEmpty(intervalValue))
                        throw new ArgumentException("Schedule must contain a valid Interval value", nameof(schedule));

                    var result = new Dictionary<string, object>
                    {
                        ["Interval"] = TimeSpan.Parse(intervalValue)
                    };

                    if (schedule.TryGetValue("LastBackup", out var lastBackupValue) && !string.IsNullOrEmpty(lastBackupValue))
                        result["LastBackup"] = DateTime.Parse(lastBackupValue);

                    if (schedule.TryGetValue("NextBackup", out var nextBackupValue) && !string.IsNullOrEmpty(nextBackupValue))
                        result["NextBackup"] = DateTime.Parse(nextBackupValue);

                    if (schedule.TryGetValue("IsEnabled", out var isEnabledValue) && !string.IsNullOrEmpty(isEnabledValue))
                        result["IsEnabled"] = bool.Parse(isEnabledValue);

                    if (schedule.TryGetValue("RetentionDays", out var retentionDaysValue) && !string.IsNullOrEmpty(retentionDaysValue))
                        result["RetentionDays"] = int.Parse(retentionDaysValue);
                    else
                        result["RetentionDays"] = 30;

                    return result;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting backup schedule: {FileUrl}", fileUrl);
                return new Dictionary<string, object>();
            }
        }

        public async Task<bool> IsBackupScheduledAsync(string fileUrl)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                var schedulePath = filePath + ".schedule";
                
                return await Task.Run(() => File.Exists(schedulePath));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking backup schedule: {FileUrl}", fileUrl);
                return false;
            }
        }

        public async Task<Dictionary<string, object>> GetFilePerformanceMetricsAsync(string fileUrl)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                return await Task.Run(() =>
                {
                    var fileInfo = new FileInfo(filePath);
                    return new Dictionary<string, object>
                    {
                        ["Size"] = fileInfo.Length,
                        ["LastAccessed"] = fileInfo.LastAccessTime,
                        ["AccessCount"] = 0, // This would need to be tracked separately
                        ["AverageAccessTime"] = 0 // This would need to be tracked separately
                    };
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting file performance metrics: {FileUrl}", fileUrl);
                return new Dictionary<string, object>();
            }
        }

        public async Task<Dictionary<string, object>> GetStoragePerformanceMetricsAsync(string? directoryPath = null)
        {
            try
            {
                var path = directoryPath ?? _uploadDirectory;
                var files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);
                
                return await Task.Run(() =>
                {
                    var metrics = new Dictionary<string, object>
                    {
                        ["TotalFiles"] = files.Length,
                        ["TotalSize"] = files.Sum(f => new FileInfo(f).Length),
                        ["AverageFileSize"] = files.Length > 0 ? (long)files.Average(f => new FileInfo(f).Length) : 0L,
                        ["LastModified"] = files.Length > 0 ? files.Max(f => File.GetLastWriteTime(f)) : DateTime.MinValue
                    };
                    
                    return metrics;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting storage performance metrics: {DirectoryPath}", directoryPath);
                return new Dictionary<string, object>();
            }
        }

        public async Task<bool> MonitorFileAccessAsync(string fileUrl)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                var monitorPath = filePath + ".monitor";
                
                await Task.Run(() =>
                {
                    var monitor = new Dictionary<string, string>
                    {
                        ["StartTime"] = DateTime.Now.ToString("o"),
                        ["Enabled"] = "true"
                    };
                    
                    File.WriteAllText(monitorPath, JsonSerializer.Serialize(monitor));
                });
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error monitoring file access: {FileUrl}", fileUrl);
                return false;
            }
        }

        public async Task<Dictionary<string, object>> GetAccessMetricsAsync(string fileUrl)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                var monitorPath = filePath + ".monitor";
                
                return await Task.Run(() =>
                {
                    if (!File.Exists(monitorPath))
                        return new Dictionary<string, object>();
                        
                    var monitor = JsonSerializer.Deserialize<Dictionary<string, string>>(
                        File.ReadAllText(monitorPath));
                        
                    if (monitor == null)
                        throw new ArgumentNullException(nameof(monitor), "Monitor dictionary cannot be null");

                    if (!monitor.TryGetValue("StartTime", out var startTimeValue) || string.IsNullOrEmpty(startTimeValue))
                        throw new ArgumentException("Monitor must contain a valid StartTime value", nameof(monitor));

                    return new Dictionary<string, object>
                    {
                        ["StartTime"] = DateTime.Parse(startTimeValue),
                        ["Enabled"] = bool.Parse(monitor["Enabled"])
                    };
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting access metrics: {FileUrl}", fileUrl);
                return new Dictionary<string, object>();
            }
        }

        public async Task<bool> IsPerformanceOptimalAsync(string fileUrl)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                return await Task.Run(() =>
                {
                    var fileInfo = new FileInfo(filePath);
                    return fileInfo.Length < 10 * 1024 * 1024; // Example: files under 10MB are considered optimal
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking performance optimal: {FileUrl}", fileUrl);
                return false;
            }
        }

        public async Task<Dictionary<string, object>> GetPerformanceRecommendationsAsync(string fileUrl)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                return await Task.Run(() =>
                {
                    var fileInfo = new FileInfo(filePath);
                    var recommendations = new Dictionary<string, object>();
                    
                    if (fileInfo.Length > 10 * 1024 * 1024)
                        recommendations["Compression"] = "Consider compressing this file";
                        
                    if (fileInfo.LastAccessTime < DateTime.Now.AddMonths(-1))
                        recommendations["Archive"] = "Consider archiving this file";
                        
                    return recommendations;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting performance recommendations: {FileUrl}", fileUrl);
                return new Dictionary<string, object>();
            }
        }

        public async Task<bool> AddFileTagAsync(string fileUrl, string tag)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                var tagsPath = filePath + ".tags";
                
                await Task.Run(() =>
                {
                    var tags = File.Exists(tagsPath)
                        ? JsonSerializer.Deserialize<List<string>>(File.ReadAllText(tagsPath))
                        : new List<string>();
                        
                    if (tags == null)
                        throw new ArgumentNullException(nameof(tags), "Tags list cannot be null");

                    if (string.IsNullOrEmpty(tag))
                        throw new ArgumentException("Tag cannot be null or empty", nameof(tag));

                    if (!tags.Contains(tag))
                    {
                        tags.Add(tag);
                        File.WriteAllText(tagsPath, JsonSerializer.Serialize(tags));
                    }
                });
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding file tag: {FileUrl}", fileUrl);
                return false;
            }
        }

        public async Task<bool> RemoveFileTagAsync(string fileUrl, string tag)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                var tagsPath = filePath + ".tags";
                
                await Task.Run(() =>
                {
                    if (File.Exists(tagsPath))
                    {
                        var tags = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(tagsPath)) ?? new List<string>();

                        if (string.IsNullOrEmpty(tag))
                            throw new ArgumentException("Tag cannot be null or empty", nameof(tag));

                        if (tags.Contains(tag))
                        {
                            tags.Remove(tag);
                            File.WriteAllText(tagsPath, JsonSerializer.Serialize(tags));
                        }
                    }
                });
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing file tag: {FileUrl}", fileUrl);
                return false;
            }
        }

        public async Task<List<string>> GetFileTagsAsync(string fileUrl)
        {
            var tagsPath = Path.Combine(_uploadDirectory, $"{fileUrl}.tags.json");
            
            return await Task.Run(() =>
            {
                if (!File.Exists(tagsPath))
                    return new List<string>();
                    
                var tags = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(tagsPath));
                return tags ?? new List<string>();
            });
        }

        public async Task<bool> CategorizeFileAsync(string fileUrl, string category)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                var categoryPath = filePath + ".category";
                
                await Task.Run(() => File.WriteAllText(categoryPath, category));
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error categorizing file: {FileUrl}", fileUrl);
                return false;
            }
        }

        public async Task<string> GetFileCategoryAsync(string fileUrl)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                var categoryPath = filePath + ".category";
                
                return await Task.Run(() => File.Exists(categoryPath)
                    ? File.ReadAllText(categoryPath)
                    : "Uncategorized");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting file category: {FileUrl}", fileUrl);
                return "Uncategorized";
            }
        }

        public async Task<bool> ShareFileWithUserAsync(string fileUrl, string userId, string permission)
        {
            if (string.IsNullOrEmpty(userId))
                throw new ArgumentException("User ID cannot be null or empty", nameof(userId));

            if (string.IsNullOrEmpty(permission))
                throw new ArgumentException("Permission cannot be null or empty", nameof(permission));

            var sharesPath = Path.Combine(_uploadDirectory, $"{fileUrl}.shares.json");
            
            return await Task.Run(async () =>
            {
                var shares = File.Exists(sharesPath)
                    ? JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync(sharesPath)) ?? new Dictionary<string, string>()
                    : new Dictionary<string, string>();

                shares[userId] = permission;
                await File.WriteAllTextAsync(sharesPath, JsonSerializer.Serialize(shares));
                return true;
            });
        }

        public async Task<bool> ShareFileWithGroupAsync(string fileUrl, string groupId, string permission)
        {
            if (string.IsNullOrEmpty(groupId))
                throw new ArgumentException("Group ID cannot be null or empty", nameof(groupId));

            if (string.IsNullOrEmpty(permission))
                throw new ArgumentException("Permission cannot be null or empty", nameof(permission));

            var sharesPath = Path.Combine(_uploadDirectory, $"{fileUrl}.shares.json");
            
            return await Task.Run(async () =>
            {
                var shares = File.Exists(sharesPath)
                    ? JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync(sharesPath)) ?? new Dictionary<string, string>()
                    : new Dictionary<string, string>();

                shares[groupId] = permission;
                await File.WriteAllTextAsync(sharesPath, JsonSerializer.Serialize(shares));
                return true;
            });
        }

        public async Task<List<string>> GetSharedWithUsersAsync(string fileUrl)
        {
            var sharesPath = Path.Combine(_uploadDirectory, $"{fileUrl}.shares.json");
            
            return await Task.Run(async () =>
            {
                if (!File.Exists(sharesPath))
                    return new List<string>();

                var shares = JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync(sharesPath)) ?? new Dictionary<string, string>();
                return shares.Keys.ToList();
            });
        }

        public async Task<List<string>> GetSharedWithGroupsAsync(string fileUrl)
        {
            try
            {
                var sharesPath = Path.Combine(_uploadDirectory, $"{fileUrl}.shares.json");
                
                return await Task.Run(async () =>
                {
                    if (!File.Exists(sharesPath))
                        return new List<string>();

                    var shares = JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync(sharesPath)) ?? new Dictionary<string, string>();
                    return shares.Keys.ToList();
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting shared groups: {FileUrl}", fileUrl);
                return new List<string>();
            }
        }

        public async Task<bool> IsFileSharedAsync(string fileUrl)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                var sharePath = filePath + ".shares";
                var groupSharePath = filePath + ".groupshares";
                
                return await Task.Run(() => File.Exists(sharePath) || File.Exists(groupSharePath));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if file is shared: {FileUrl}", fileUrl);
                return false;
            }
        }

        public async Task<byte[]> GetFileAsync(string fileUrl)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"File not found: {fileUrl}");
                }

                return await File.ReadAllBytesAsync(filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting file: {fileUrl}");
                throw;
            }
        }

        public async Task<byte[]> ExportMessagesAsync(List<Message> messages, ExportFormat format)
        {
            try
            {
                return await Task.Run(() =>
                {
                    var json = JsonSerializer.Serialize(messages);
                    return Encoding.UTF8.GetBytes(json);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting messages");
                throw;
            }
        }
        public async Task<ImportProgress> GetImportProgressAsync(string importId)
        {
            try
            {
                var progressPath = Path.Combine(_uploadDirectory, "imports", importId + ".progress");
                
                if (!File.Exists(progressPath))
                    return new ImportProgress(importId);
                    
                var json = await File.ReadAllTextAsync(progressPath);
                var progress = JsonSerializer.Deserialize<ImportProgress>(json);
                
                return progress ?? new ImportProgress(importId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting import progress: {ImportId}", importId);
                return new ImportProgress(importId);
            }
        }

        public async Task<ExportProgress> GetExportProgressAsync(string exportId)
        {
            try
            {
                var progressPath = Path.Combine(_uploadDirectory, "exports", exportId + ".progress");

                if (!File.Exists(progressPath))
                {
                    return new ExportProgress(exportId)
                    {
                        TotalMessages = 0,
                        ProcessedMessages = 0,
                        Status = ExportStatus.Pending,
                        ErrorMessage = null
                    };
                }

                var json = await File.ReadAllTextAsync(progressPath);
                var progress = JsonSerializer.Deserialize<ExportProgress>(json);

                if (progress == null)
                {
                    return new ExportProgress(exportId)
                    {
                        TotalMessages = 0,
                        ProcessedMessages = 0,
                        Status = ExportStatus.Pending,
                        ErrorMessage = null
                    };
                }

                // Eğer JSON içinde exportId eksikse, overwrite et
                if (string.IsNullOrWhiteSpace(progress.ExportId))
                {
                    progress.ExportId = exportId;
                }

                return progress;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting export progress: {ExportId}", exportId);
                return new ExportProgress(exportId)
                {
                    TotalMessages = 0,
                    ProcessedMessages = 0,
                    Status = ExportStatus.Failed,
                    ErrorMessage = ex.Message
                };
            }
        }




        private async Task<bool> CheckGDPRComplianceAsync(string fileUrl)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                return await Task.Run(() =>
                {
                    var fileInfo = new FileInfo(filePath);
                    return fileInfo.Length < 100 * 1024 * 1024; // Example: files under 100MB are GDPR compliant
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking GDPR compliance: {FileUrl}", fileUrl);
                return false;
            }
        }

        private async Task<bool> CheckHIPAAComplianceAsync(string fileUrl)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                return await Task.Run(() =>
                {
                    var fileInfo = new FileInfo(filePath);
                    return fileInfo.IsReadOnly; // Example: read-only files are HIPAA compliant
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking HIPAA compliance: {FileUrl}", fileUrl);
                return false;
            }
        }

        private double CalculateComplianceScore(string fileUrl)
        {
            // Implement compliance score calculation
            return 1.0;
        }

        public async Task<Dictionary<string, int>> GetOperationStatisticsAsync(string fileUrl)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                if (!File.Exists(filePath))
                    throw new FileNotFoundException("File not found", fileUrl);

                var stats = new Dictionary<string, int>();
                var operations = await GetFileOperationHistoryAsync(fileUrl);

                // Group operations by type and count them
                foreach (var operation in operations)
                {
                    if (operation.TryGetValue("OperationType", out var opTypeObj) && opTypeObj != null)
                    {
                        var opType = opTypeObj.ToString() ?? "Unknown";
                        if (!stats.ContainsKey(opType))
                            stats[opType] = 0;
                        stats[opType]++;
                    }
                }

                // Add file access statistics
                var fileInfo = new FileInfo(filePath);
                stats["TotalAccessCount"] = operations.Count;
                stats["LastAccessDays"] = (int)(DateTime.UtcNow - fileInfo.LastAccessTimeUtc).TotalDays;
                stats["LastModifiedDays"] = (int)(DateTime.UtcNow - fileInfo.LastWriteTimeUtc).TotalDays;

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting operation statistics for file: {FileUrl}", fileUrl);
                throw;
            }
        }

        public async Task<bool> ExportAuditLogAsync(string fileUrl, string format)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                var logPath = filePath + ".audit";
                var exportPath = filePath + ".audit." + format;
                
                await Task.Run(() =>
                {
                    if (!File.Exists(logPath))
                        return;
                        
                    var log = File.ReadAllText(logPath);
                    File.WriteAllText(exportPath, log);
                });
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting audit log: {FileUrl}", fileUrl);
                return false;
            }
        }

        public async Task<bool> SyncFileAsync(string sourceUrl, string targetUrl)
        {
            try
            {
                var sourcePath = GetFilePathFromUrl(sourceUrl);
                var targetPath = GetFilePathFromUrl(targetUrl);
                
                await Task.Run(() =>
                {
                    if (File.Exists(sourcePath))
                    {
                        var directory = Path.GetDirectoryName(targetPath);
                        if (string.IsNullOrEmpty(directory))
                        {
                            throw new InvalidOperationException($"Invalid target path: {targetPath}");
                        }
                        
                        if (!Directory.Exists(directory))
                            Directory.CreateDirectory(directory);
                            
                        File.Copy(sourcePath, targetPath, true);
                    }
                });
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing file: {SourceUrl} to {TargetUrl}", sourceUrl, targetUrl);
                return false;
            }
        }

        public async Task<bool> IsFileSyncedAsync(string fileUrl)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                var syncPath = filePath + ".sync";
                
                return await Task.Run(() => File.Exists(syncPath));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if file is synced: {FileUrl}", fileUrl);
                return false;
            }
        }

        public async Task<Dictionary<string, List<string>>> GetCategoryDistributionAsync(string? directory = null)
        {
            try
            {
                var path = directory != null 
                    ? Path.Combine(_uploadDirectory, directory.TrimStart('/'))
                    : _uploadDirectory;

                return await Task.Run(() =>
                {
                    var distribution = new Dictionary<string, List<string>>();
                    var files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);
                    
                    foreach (var file in files)
                    {
                        var category = GetFileCategory(Path.GetExtension(file));
                        if (!distribution.ContainsKey(category))
                            distribution[category] = new List<string>();
                            
                        distribution[category].Add(Path.GetFileName(file));
                    }
                    
                    return distribution;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting category distribution: {Directory}", directory);
                return new Dictionary<string, List<string>>();
            }
        }

        private string GetFileCategory(string extension)
        {
            return extension switch
            {
                // Documents
                ".doc" or ".docx" or ".pdf" or ".txt" or ".rtf" or ".odt" or ".xls" or ".xlsx" or ".ppt" or ".pptx" => "Documents",
                
                // Images
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".tiff" or ".webp" or ".svg" => "Images",
                
                // Videos
                ".mp4" or ".avi" or ".mov" or ".wmv" or ".flv" or ".mkv" or ".webm" => "Videos",
                
                // Audio
                ".mp3" or ".wav" or ".ogg" or ".flac" or ".m4a" or ".aac" => "Audio",
                
                // Archives
                ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => "Archives",
                
                // Others
                _ => "Others"
            };
        }

        public async Task<bool> AutoCategorizeFileAsync(string fileUrl)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                var category = GetFileCategory(Path.GetExtension(filePath));
                var categoryPath = filePath + ".category";
                
                await Task.Run(() => File.WriteAllText(categoryPath, category));
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error auto-categorizing file: {FileUrl}", fileUrl);
                return false;
            }
        }

        public async Task DeleteFileAsync(string filePath)
        {
            try
            {
                await Task.Run(() =>
                {
                    if (File.Exists(filePath))
                        File.Delete(filePath);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file: {FilePath}", filePath);
                throw;
            }
        }

        public async Task<string> SaveMessagesAsync(List<Message> messages)
        {
            try
            {
                if (messages == null || !messages.Any())
                    throw new ArgumentException("Messages list cannot be null or empty");

                var messagesDir = Path.Combine(_uploadDirectory, "messages");
                Directory.CreateDirectory(messagesDir);

                var fileName = $"messages_{DateTime.UtcNow:yyyyMMddHHmmss}.json";
                var filePath = Path.Combine(messagesDir, fileName);

                var options = new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                };

                var json = System.Text.Json.JsonSerializer.Serialize(messages, options);
                await File.WriteAllTextAsync(filePath, json);

                _logger.LogInformation("Messages saved successfully to: {FilePath}", filePath);
                return fileName;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving messages");
                throw;
            }
        }

        public async Task DeleteMessagesAsync(string backupPath)
        {
            try
            {
                await Task.Run(() =>
                {
                    if (File.Exists(backupPath))
                        File.Delete(backupPath);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting messages: {BackupPath}", backupPath);
                throw;
            }
        }

        public async Task<bool> ValidateImportDataAsync(byte[] data, ImportFormat format)
        {
            try
            {
                return await Task.Run(() =>
                {
                    try
                    {
                        var json = Encoding.UTF8.GetString(data);
                        var messages = JsonSerializer.Deserialize<List<Message>>(json);
                        return messages != null && messages.Any();
                    }
                    catch
                    {
                        return false;
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating import data");
                return false;
            }
        }

        public async Task<Dictionary<string, object>> GetBackupMetadataAsync(string backupId)
        {
            try
            {
                var backupPath = Path.Combine(_uploadDirectory, "backups", backupId);
                if (!File.Exists(backupPath))
                    throw new FileNotFoundException("Backup not found", backupId);

                var metadata = await GetFileMetadataAsync($"/uploads/backups/{backupId}");
                if (metadata == null)
                    throw new InvalidOperationException("Backup metadata not found");

                return new Dictionary<string, object>
                {
                    { "FileName", metadata.FileName },
                    { "ContentType", metadata.ContentType },
                    { "FileSize", metadata.FileSize },
                    { "CreatedAt", metadata.CreatedAt },
                    { "LastModified", metadata.LastModified },
                    { "Hash", metadata.Hash },
                    { "CustomMetadata", metadata.CustomMetadata }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting backup metadata: {BackupId}", backupId);
                throw;
            }
        }

        public async Task<string> ApplyWatermarkAsync(string fileUrl, string watermarkText, float opacity = 0.5f)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                if (!File.Exists(filePath))
                    throw new FileNotFoundException("File not found", fileUrl);

                var watermarkedPath = Path.Combine(_uploadDirectory, $"watermarked_{Path.GetFileName(filePath)}");

                return await Task.Run(async () =>
                {
                    using var image = await Image.LoadAsync(filePath);
                    
                    // Create a semi-transparent color for the watermark
                    var color = new Color(new Vector4(1f, 1f, 1f, opacity));
                    
                    // Calculate font size based on image dimensions
                    var fontSize = Math.Min(image.Width, image.Height) / 20f;
                    
                    // Create font
                    var font = SystemFonts.CreateFont("Arial", fontSize);
                    
                    // Add watermark text
                    image.Mutate(x => x.DrawText(
                        watermarkText,
                        font,
                        color,
                        new PointF(image.Width / 4f, image.Height / 4f)
                    ));

                    await image.SaveAsJpegAsync(watermarkedPath);
                    return $"/uploads/watermarked_{Path.GetFileName(filePath)}";
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying watermark: {FileUrl}", fileUrl);
                throw;
            }
        }

        public async Task<string> SaveFileAsync(byte[] fileData, string fileName, string contentType)
        {
            try
            {
                var filePath = Path.Combine(_uploadDirectory, SanitizeFileName(fileName));
                await Task.Run(() => File.WriteAllBytes(filePath, fileData));
                return $"/uploads/{Path.GetRelativePath(_uploadDirectory, filePath)}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving file: {FileName}", fileName);
                throw;
            }
        }

        public async Task<bool> ScheduleBackupAsync(string fileUrl, TimeSpan interval)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                var schedulePath = filePath + ".schedule";
                
                await Task.Run(() =>
                {
                    var schedule = new Dictionary<string, string>
                    {
                        ["Interval"] = interval.ToString(),
                        ["LastBackup"] = DateTime.Now.ToString("o")
                    };
                    
                    File.WriteAllText(schedulePath, JsonSerializer.Serialize(schedule));
                });
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scheduling backup: {FileUrl}", fileUrl);
                return false;
            }
        }

        public async Task<Dictionary<string, int>> GetFileActivityStatsAsync(string? directoryPath = null, TimeSpan? timeRange = null)
        {
            try
            {
                var fullPath = directoryPath != null 
                    ? Path.Combine(_uploadDirectory, directoryPath)
                    : _uploadDirectory;

                if (!Directory.Exists(fullPath))
                {
                    return new Dictionary<string, int>();
                }

                var files = await Task.Run(() => Directory.GetFiles(fullPath, "*.*", SearchOption.AllDirectories));
                
                if (timeRange.HasValue)
                {
                    var cutoff = DateTime.Now - timeRange.Value;
                    files = await Task.Run(() => files.Where(f => File.GetLastWriteTime(f) >= cutoff).ToArray());
                }

                return await Task.Run(() =>
                {
                    var stats = new Dictionary<string, int>
                    {
                        ["TotalFiles"] = files.Length,
                        ["RecentlyModified"] = files.Count(f => File.GetLastWriteTime(f) >= DateTime.Now.AddDays(-7)),
                        ["RecentlyAccessed"] = files.Count(f => File.GetLastAccessTime(f) >= DateTime.Now.AddDays(-7))
                    };
                    
                    return stats;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting file activity stats: {DirectoryPath}", directoryPath);
                return new Dictionary<string, int>();
            }
        }

        public async Task<string> ConvertDocumentFormatAsync(string fileUrl, string targetFormat, Dictionary<string, string>? options = null)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                var outputPath = Path.ChangeExtension(filePath, targetFormat);
                
                // Simulate document conversion
                await Task.Run(() =>
                {
                    using var image = new Image<Rgba32>(800, 600);
                    image.SaveAsJpeg(outputPath);
                });
                
                return outputPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting document format: {FileUrl}", fileUrl);
                throw;
            }
        }

        public async Task<bool> RevokeAllSharesAsync(string fileUrl)
        {
            try
            {
                var sharesPath = Path.Combine(_uploadDirectory, $"{fileUrl}.shares.json");
                
                return await Task.Run(() =>
                {
                    if (File.Exists(sharesPath))
                    {
                        File.Delete(sharesPath);
                    }
                    return true;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking all shares: {FileUrl}", fileUrl);
                return false;
            }
        }

        public async Task<Dictionary<string, string>> GetSharePermissionsAsync(string fileUrl)
        {
            try
            {
                var sharesPath = Path.Combine(_uploadDirectory, $"{fileUrl}.shares.json");
                
                return await Task.Run(async () =>
                {
                    if (!File.Exists(sharesPath))
                        return new Dictionary<string, string>();

                    var shares = JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync(sharesPath)) ?? new Dictionary<string, string>();
                    return shares;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting share permissions: {FileUrl}", fileUrl);
                return new Dictionary<string, string>();
            }
        }

        public async Task<string> StoreTokenAsync(string userId, string tokenType, string token, DateTime? expiration = null, TokenMetadata? metadata = null)
        {
            try
            {
                var tokenData = new
                {
                    UserId = userId,
                    TokenType = tokenType,
                    Token = token,
                    Expiration = expiration,
                    Metadata = metadata,
                    CreatedAt = DateTime.UtcNow
                };

                var tokenPath = Path.Combine(_uploadDirectory, "tokens", $"{userId}_{tokenType}.json");
                var directory = Path.GetDirectoryName(tokenPath);
                if (string.IsNullOrEmpty(directory))
                {
                    throw new InvalidOperationException($"Invalid token path: {tokenPath}");
                }

                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                await File.WriteAllTextAsync(tokenPath, JsonSerializer.Serialize(tokenData));
                return token;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing token for user {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> ValidateTokenAsync(string token, string tokenType, string? ipAddress = null, string? userAgent = null)
        {
            try
            {
                var tokenFiles = Directory.GetFiles(Path.Combine(_uploadDirectory, "tokens"), $"*_{tokenType}.json");
                foreach (var tokenFile in tokenFiles)
                {
                    var tokenData = JsonSerializer.Deserialize<dynamic>(await File.ReadAllTextAsync(tokenFile));
                    if (tokenData?.Token?.ToString() == token)
                    {
                        if (tokenData.Expiration != null && DateTime.Parse(tokenData.Expiration.ToString()) < DateTime.UtcNow)
                            return false;

                        if (ipAddress != null && tokenData.Metadata?.IpAddress?.ToString() != ipAddress)
                            return false;

                        if (userAgent != null && tokenData.Metadata?.UserAgent?.ToString() != userAgent)
                            return false;

                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating token");
                return false;
            }
        }
    }
} 