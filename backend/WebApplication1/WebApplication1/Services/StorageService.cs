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
using System.Drawing;
using System.Drawing.Imaging;
using System.IO.Compression;
using System.Threading;
using Microsoft.Extensions.Caching.Memory;

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
        public string FileName { get; set; }
        public string ContentType { get; set; }
        public long FileSize { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastModified { get; set; }
        public string Hash { get; set; }
        public Dictionary<string, string> CustomMetadata { get; set; }
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
                if (_cache.TryGetValue(cacheKey, out Stream cachedStream))
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

        public async Task<FileMetadata> GetFileMetadataAsync(string fileUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(fileUrl))
                    throw new ArgumentException("Dosya URL'i boş olamaz");

                var fileName = Path.GetFileName(fileUrl);
                var metadataPath = GetMetadataPath(fileName);

                if (!File.Exists(metadataPath))
                    throw new FileNotFoundException("Dosya metadata'sı bulunamadı", fileName);

                var metadataJson = await File.ReadAllTextAsync(metadataPath);
                return System.Text.Json.JsonSerializer.Deserialize<FileMetadata>(metadataJson);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya metadata'sı alınırken hata oluştu: {FileUrl}", fileUrl);
                throw new Exception($"Dosya metadata'sı alınırken hata oluştu: {ex.Message}", ex);
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

        public async Task<string> CompressFileAsync(string fileUrl, CompressionLevel level = CompressionLevel.Optimal)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                if (!File.Exists(filePath))
                    throw new FileNotFoundException("File not found", fileUrl);

                var compressedPath = Path.Combine(_uploadDirectory, $"compressed_{Path.GetFileName(filePath)}");

                using (var sourceStream = new FileStream(filePath, FileMode.Open))
                using (var destinationStream = new FileStream(compressedPath, FileMode.Create))
                using (var gzipStream = new GZipStream(destinationStream, level))
                {
                    await sourceStream.CopyToAsync(gzipStream);
                }

                return $"/uploads/compressed_{Path.GetFileName(filePath)}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error compressing file: {FileUrl}", fileUrl);
                throw;
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

                using (var image = Image.FromFile(filePath))
                {
                    var ratio = Math.Min(
                        (float)maxWidth / image.Width,
                        (float)maxHeight / image.Height
                    );

                    if (ratio < 1)
                    {
                        var newWidth = (int)(image.Width * ratio);
                        var newHeight = (int)(image.Height * ratio);

                        using (var resized = image.GetThumbnailImage(newWidth, newHeight, null, IntPtr.Zero))
                        {
                            resized.Save(optimizedPath, ImageFormat.Jpeg);
                        }
                    }
                    else
                    {
                        image.Save(optimizedPath, ImageFormat.Jpeg);
                    }
                }

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

                var contentType = GetMimeType(filePath);
                if (!IsImageFile(contentType))
                    throw new ArgumentException("File is not an image");

                var resizedPath = Path.Combine(_uploadDirectory, $"resized_{Path.GetFileName(filePath)}");

                using (var image = Image.FromFile(filePath))
                {
                    int newWidth = width;
                    int newHeight = height;

                    if (maintainAspectRatio)
                    {
                        var ratio = Math.Min(
                            (float)width / image.Width,
                            (float)height / image.Height
                        );
                        newWidth = (int)(image.Width * ratio);
                        newHeight = (int)(image.Height * ratio);
                    }

                    using (var resized = image.GetThumbnailImage(newWidth, newHeight, null, IntPtr.Zero))
                    {
                        resized.Save(resizedPath, ImageFormat.Jpeg);
                    }
                }

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

                var contentType = GetMimeType(filePath);
                if (!IsImageFile(contentType))
                    throw new ArgumentException("File is not an image");

                var croppedPath = Path.Combine(_uploadDirectory, $"cropped_{Path.GetFileName(filePath)}");

                using (var image = Image.FromFile(filePath))
                {
                    if (x < 0 || y < 0 || width <= 0 || height <= 0 ||
                        x + width > image.Width || y + height > image.Height)
                        throw new ArgumentException("Invalid crop parameters");

                    using (var cropped = new Bitmap(width, height))
                    {
                        using (var graphics = Graphics.FromImage(cropped))
                        {
                            graphics.DrawImage(image, new Rectangle(0, 0, width, height),
                                new Rectangle(x, y, width, height), GraphicsUnit.Pixel);
                        }
                        cropped.Save(croppedPath, ImageFormat.Jpeg);
                    }
                }

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

                var contentType = GetMimeType(filePath);
                if (!IsImageFile(contentType))
                    throw new ArgumentException("File is not an image");

                var rotatedPath = Path.Combine(_uploadDirectory, $"rotated_{Path.GetFileName(filePath)}");

                using (var image = Image.FromFile(filePath))
                {
                    using (var rotated = new Bitmap(image.Width, image.Height))
                    {
                        using (var graphics = Graphics.FromImage(rotated))
                        {
                            graphics.TranslateTransform(image.Width / 2f, image.Height / 2f);
                            graphics.RotateTransform(angle);
                            graphics.TranslateTransform(-image.Width / 2f, -image.Height / 2f);
                            graphics.DrawImage(image, 0, 0);
                        }
                        rotated.Save(rotatedPath, ImageFormat.Jpeg);
                    }
                }

                return $"/uploads/rotated_{Path.GetFileName(filePath)}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rotating image: {FileUrl}", fileUrl);
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

                var contentType = GetMimeType(filePath);
                if (!IsImageFile(contentType))
                    throw new ArgumentException("File is not an image");

                var watermarkedPath = Path.Combine(_uploadDirectory, $"watermarked_{Path.GetFileName(filePath)}");

                using (var image = Image.FromFile(filePath))
                {
                    using (var watermarked = new Bitmap(image))
                    {
                        using (var graphics = Graphics.FromImage(watermarked))
                        {
                            var font = new Font("Arial", 32);
                            var brush = new SolidBrush(Color.FromArgb((int)(opacity * 255), Color.White));
                            var size = graphics.MeasureString(watermarkText, font);
                            var position = new PointF(
                                (watermarked.Width - size.Width) / 2,
                                (watermarked.Height - size.Height) / 2
                            );

                            graphics.DrawString(watermarkText, font, brush, position);
                        }
                        watermarked.Save(watermarkedPath, ImageFormat.Jpeg);
                    }
                }

                return $"/uploads/watermarked_{Path.GetFileName(filePath)}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying watermark: {FileUrl}", fileUrl);
                throw;
            }
        }

        public async Task<string> ConvertFileFormatAsync(string fileUrl, string targetFormat)
        {
            try
            {
                if (string.IsNullOrEmpty(fileUrl))
                    throw new ArgumentException("Dosya URL'i boş olamaz");

                var fileName = Path.GetFileName(fileUrl);
                var filePath = Path.Combine(_uploadDirectory, fileName);
                var convertedPath = Path.Combine(_uploadDirectory, $"converted_{Path.GetFileNameWithoutExtension(fileName)}{targetFormat}");

                if (!File.Exists(filePath))
                    throw new FileNotFoundException("Dosya bulunamadı", fileName);

                if (IsImageFile(Path.GetExtension(fileName)))
                {
                    using (var image = Image.FromFile(filePath))
                    {
                        var format = GetImageFormat(targetFormat);
                        image.Save(convertedPath, format);
                    }
                }
                else
                {
                    throw new NotSupportedException("Bu dosya türü için format dönüşümü desteklenmiyor");
                }

                return $"/uploads/converted_{Path.GetFileNameWithoutExtension(fileName)}{targetFormat}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya formatı dönüştürülürken hata oluştu: {FileUrl}", fileUrl);
                throw new Exception($"Dosya formatı dönüştürülürken hata oluştu: {ex.Message}", ex);
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
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            
            if (!_options.AllowedFileTypes.Contains(extension))
                throw new ArgumentException($"Desteklenmeyen dosya türü: {extension}");

            if (fileStream.Length > _options.MaxFileSize)
                throw new ArgumentException($"Dosya boyutu çok büyük. Maksimum boyut: {_options.MaxFileSize / (1024 * 1024)}MB");

            // Dosya içeriği kontrolü
            if (IsImageFile(extension))
            {
                try
                {
                    using (var image = Image.FromStream(fileStream))
                    {
                        // Resim doğrulandı
                    }
                }
                catch
                {
                    throw new ArgumentException("Geçersiz resim dosyası");
                }
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
                if (!IsImageFile(Path.GetExtension(filePath)))
                    throw new ArgumentException("File is not an image");

                var thumbnailPath = GetThumbnailPath(filePath);
                var thumbnailDir = Path.GetDirectoryName(thumbnailPath);
                
                if (!Directory.Exists(thumbnailDir))
                {
                    Directory.CreateDirectory(thumbnailDir);
                }

                using (var image = Image.FromFile(filePath))
                {
                    var ratio = Math.Min(
                        (float)_options.ThumbnailWidth / image.Width,
                        (float)_options.ThumbnailHeight / image.Height
                    );

                    var newWidth = (int)(image.Width * ratio);
                    var newHeight = (int)(image.Height * ratio);

                    using (var thumbnail = image.GetThumbnailImage(newWidth, newHeight, null, IntPtr.Zero))
                    {
                        thumbnail.Save(thumbnailPath, ImageFormat.Jpeg);
                    }
                }

                return $"/uploads/thumbnails/{Path.GetFileName(filePath)}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating thumbnail: {FileUrl}", fileUrl);
                throw new Exception($"Error generating thumbnail: {ex.Message}", ex);
            }
        }

        private string GetThumbnailPath(string filePath)
        {
            return Path.Combine(
                Path.GetDirectoryName(filePath),
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
        }

        private async Task SaveMetadataAsync(string fileName, FileMetadata metadata)
        {
            var metadataPath = GetMetadataPath(fileName);
            var metadataDir = Path.GetDirectoryName(metadataPath);

            if (!Directory.Exists(metadataDir))
            {
                Directory.CreateDirectory(metadataDir);
            }

            var metadataJson = System.Text.Json.JsonSerializer.Serialize(metadata);
            await File.WriteAllTextAsync(metadataPath, metadataJson);
        }

        private ImageFormat GetImageFormat(string extension)
        {
            return extension.ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => ImageFormat.Jpeg,
                ".png" => ImageFormat.Png,
                ".gif" => ImageFormat.Gif,
                ".bmp" => ImageFormat.Bmp,
                _ => throw new ArgumentException($"Desteklenmeyen resim formatı: {extension}")
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

                var fileInfo = new FileInfo(filePath);
                return fileInfo.Length;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting file size for {FileUrl}", fileUrl);
                throw;
            }
        }

        public async Task<string> GetFileMimeTypeAsync(string fileUrl)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                if (!File.Exists(filePath))
                    throw new FileNotFoundException("File not found", fileUrl);

                return GetMimeType(filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting MIME type for {FileUrl}", fileUrl);
                throw;
            }
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
                if (metadata == null || !metadata.CustomMetadata.ContainsKey("EncryptionKey"))
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

                var fileInfo = new FileInfo(filePath);
                return fileInfo.Length;
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
                    
                    if (!Directory.Exists(thumbnailDir))
                    {
                        Directory.CreateDirectory(thumbnailDir);
                    }

                    using (var image = Image.FromFile(filePath))
                    {
                        var ratio = Math.Min(
                            (float)_options.ThumbnailWidth / image.Width,
                            (float)_options.ThumbnailHeight / image.Height
                        );

                        var newWidth = (int)(image.Width * ratio);
                        var newHeight = (int)(image.Height * ratio);

                        using (var thumbnail = image.GetThumbnailImage(newWidth, newHeight, null, IntPtr.Zero))
                        {
                            thumbnail.Save(thumbnailPath, ImageFormat.Jpeg);
                        }
                    }

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

        public async Task<string> CompressFileAsync(string fileUrl, int quality = 80)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                if (!File.Exists(filePath))
                    throw new FileNotFoundException("File not found", fileUrl);

                var contentType = GetMimeType(filePath);
                var compressedPath = Path.Combine(_uploadDirectory, $"compressed_{Path.GetFileName(filePath)}");

                if (IsImageFile(contentType))
                {
                    using (var image = Image.FromFile(filePath))
                    {
                        var encoderParameters = new EncoderParameters(1);
                        encoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, quality);

                        var codec = ImageCodecInfo.GetImageEncoders()
                            .FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);

                        if (codec == null)
                            throw new InvalidOperationException("JPEG encoder not found");

                        image.Save(compressedPath, codec, encoderParameters);
                    }
                }
                else
                {
                    using (var sourceStream = new FileStream(filePath, FileMode.Open))
                    using (var destinationStream = new FileStream(compressedPath, FileMode.Create))
                    using (var gzipStream = new GZipStream(destinationStream, CompressionLevel.Optimal))
                    {
                        await sourceStream.CopyToAsync(gzipStream);
                    }
                }

                return $"/uploads/compressed_{Path.GetFileName(filePath)}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error compressing file: {FileUrl}", fileUrl);
                throw;
            }
        }

        public async Task<List<string>> GetFileVersionsAsync(string fileUrl)
        {
            try
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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting file versions: {FileUrl}", fileUrl);
                throw;
            }
        }

        public async Task<bool> DeleteFileVersionAsync(string fileUrl, string versionId)
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
        }

        public async Task<Dictionary<string, object>> GetVersionMetadataAsync(string fileUrl, string versionId)
        {
            try
            {
                var fileName = Path.GetFileNameWithoutExtension(fileUrl);
                var versionDirectory = Path.Combine(_uploadDirectory, "versions", fileName);
                var versionPath = Path.Combine(versionDirectory, versionId);

                if (!File.Exists(versionPath))
                    throw new FileNotFoundException("Version not found", versionId);

                var metadata = await GetFileMetadataAsync($"/uploads/versions/{fileName}/{versionId}");
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
        }

        public async Task<DateTime?> GetCacheExpirationAsync(string fileUrl)
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

        public async Task<long> GetDownloadedBytesAsync(string fileUrl)
        {
            try
            {
                if (_downloadedBytes.TryGetValue(fileUrl, out var bytes))
                {
                    return bytes;
                }
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting downloaded bytes: {FileUrl}", fileUrl);
                return 0;
            }
        }

        public async Task<bool> IsDownloadCompleteAsync(string fileUrl)
        {
            try
            {
                if (_downloadComplete.TryGetValue(fileUrl, out var isComplete))
                {
                    return isComplete;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking download status: {FileUrl}", fileUrl);
                return false;
            }
        }

        public async Task<string> RestoreFileVersionAsync(string fileUrl, string versionId)
        {
            try
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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring file version: {FileUrl}, {VersionId}", fileUrl, versionId);
                throw;
            }
        }

        public async Task<bool> IsFileCachedAsync(string fileUrl)
        {
            try
            {
                var cacheKey = $"file_{fileUrl}";
                return _cache.TryGetValue(cacheKey, out _);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if file is cached: {FileUrl}", fileUrl);
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
                    _downloadComplete[fileUrl] = false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error canceling download: {FileUrl}", fileUrl);
                throw;
            }
        }

        public async Task<string> CopyFileAsync(string sourceUrl, string destinationUrl)
        {
            try
            {
                var sourcePath = GetFilePathFromUrl(sourceUrl);
                var destinationPath = GetFilePathFromUrl(destinationUrl);

                if (!File.Exists(sourcePath))
                    throw new FileNotFoundException("Source file not found", sourceUrl);

                var directory = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.Copy(sourcePath, destinationPath, true);
                return destinationUrl;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error copying file: {SourceUrl} to {DestinationUrl}", sourceUrl, destinationUrl);
                throw;
            }
        }

        public async Task<bool> MoveFileAsync(string sourceUrl, string destinationUrl)
        {
            try
            {
                var sourcePath = GetFilePathFromUrl(sourceUrl);
                var destinationPath = GetFilePathFromUrl(destinationUrl);

                if (!File.Exists(sourcePath))
                    return false;

                var directory = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.Move(sourcePath, destinationPath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error moving file: {SourceUrl} to {DestinationUrl}", sourceUrl, destinationUrl);
                return false;
            }
        }

        public async Task<bool> RenameFileAsync(string fileUrl, string newName)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                if (!File.Exists(filePath))
                    return false;

                var directory = Path.GetDirectoryName(filePath);
                var newPath = Path.Combine(directory, newName);

                File.Move(filePath, newPath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error renaming file: {FileUrl} to {NewName}", fileUrl, newName);
                return false;
            }
        }

        public async Task<bool> CreateDirectoryAsync(string directoryPath)
        {
            try
            {
                var fullPath = Path.Combine(_uploadDirectory, directoryPath.TrimStart('/'));
                if (!Directory.Exists(fullPath))
                {
                    Directory.CreateDirectory(fullPath);
                }
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
                if (Directory.Exists(fullPath))
                {
                    Directory.Delete(fullPath, true);
                }
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
                if (!Directory.Exists(fullPath))
                    return new List<string>();

                var files = Directory.GetFiles(fullPath)
                    .Select(f => $"/uploads/{directoryPath.TrimStart('/')}/{Path.GetFileName(f)}")
                    .ToList();

                var directories = Directory.GetDirectories(fullPath)
                    .Select(d => $"/uploads/{directoryPath.TrimStart('/')}/{Path.GetFileName(d)}/")
                    .ToList();

                return files.Concat(directories).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing directory: {DirectoryPath}", directoryPath);
                throw;
            }
        }

        public async Task<bool> DirectoryExistsAsync(string directoryPath)
        {
            try
            {
                var fullPath = Path.Combine(_uploadDirectory, directoryPath.TrimStart('/'));
                return Directory.Exists(fullPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking directory existence: {DirectoryPath}", directoryPath);
                return false;
            }
        }

        public async Task<Dictionary<string, string>> GetFileMetadataAsync(string fileUrl)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                if (!File.Exists(filePath))
                    throw new FileNotFoundException("File not found", fileUrl);

                var fileInfo = new FileInfo(filePath);
                var metadata = new Dictionary<string, string>
                {
                    { "FileName", fileInfo.Name },
                    { "ContentType", GetMimeType(filePath) },
                    { "FileSize", fileInfo.Length.ToString() },
                    { "CreatedAt", fileInfo.CreationTimeUtc.ToString("o") },
                    { "LastModified", fileInfo.LastWriteTimeUtc.ToString("o") },
                    { "Hash", await CalculateFileHashAsync(filePath) }
                };

                return metadata;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting file metadata: {FileUrl}", fileUrl);
                throw;
            }
        }

        public async Task<bool> UpdateFileMetadataAsync(string fileUrl, Dictionary<string, string> metadata)
        {
            try
            {
                var fileName = Path.GetFileName(fileUrl);
                var metadataPath = GetMetadataPath(fileName);

                if (!File.Exists(metadataPath))
                {
                    // Create new metadata if it doesn't exist
                    var filePath = GetFilePathFromUrl(fileUrl);
                    if (!File.Exists(filePath))
                        return false;

                    var newMetadata = new FileMetadata
                    {
                        FileName = fileName,
                        ContentType = GetMimeType(filePath),
                        FileSize = new FileInfo(filePath).Length,
                        CreatedAt = DateTime.UtcNow,
                        LastModified = DateTime.UtcNow,
                        Hash = await CalculateFileHashAsync(filePath),
                        CustomMetadata = metadata
                    };

                    await SaveMetadataAsync(fileName, newMetadata);
                }
                else
                {
                    // Update existing metadata
                    var existingMetadata = await GetFileMetadataAsync(fileUrl);
                    if (existingMetadata == null)
                        return false;

                    existingMetadata.CustomMetadata = metadata;
                    existingMetadata.LastModified = DateTime.UtcNow;

                    await SaveMetadataAsync(fileName, existingMetadata);
                }

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
                var metadataPath = GetMetadataPath(fileName);

                if (!File.Exists(metadataPath))
                    return false;

                var metadata = await GetFileMetadataAsync(fileUrl);
                if (metadata == null || !metadata.CustomMetadata.ContainsKey(key))
                    return false;

                metadata.CustomMetadata.Remove(key);
                metadata.LastModified = DateTime.UtcNow;

                await SaveMetadataAsync(fileName, metadata);
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
                var metadataPath = GetMetadataPath(fileName);

                if (!File.Exists(metadataPath))
                    return false;

                var metadata = await GetFileMetadataAsync(fileUrl);
                if (metadata == null)
                    return false;

                metadata.CustomMetadata.Clear();
                metadata.LastModified = DateTime.UtcNow;

                await SaveMetadataAsync(fileName, metadata);
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
                return metadata != null && metadata.CustomMetadata.ContainsKey(key);
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
                if (!File.Exists(filePath))
                    throw new FileNotFoundException("File not found", fileUrl);

                var contentType = GetMimeType(filePath);
                if (!contentType.StartsWith("application/") && !contentType.StartsWith("text/"))
                    throw new ArgumentException("File is not a document");

                var previewPath = Path.Combine(_uploadDirectory, "previews", $"doc_{Path.GetFileNameWithoutExtension(filePath)}.jpg");
                var previewDir = Path.GetDirectoryName(previewPath);

                if (!Directory.Exists(previewDir))
                {
                    Directory.CreateDirectory(previewDir);
                }

                // TODO: Implement actual document preview generation
                // This is a placeholder implementation
                // In a real application, you would:
                // 1. Use a document processing library
                // 2. Convert the first page to an image
                // 3. Save the preview

                return $"/uploads/previews/doc_{Path.GetFileNameWithoutExtension(filePath)}.jpg";
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
                if (!File.Exists(filePath))
                    throw new FileNotFoundException("File not found", fileUrl);

                var contentType = GetMimeType(filePath);
                if (!contentType.StartsWith("video/"))
                    throw new ArgumentException("File is not a video");

                var previewPath = Path.Combine(_uploadDirectory, "previews", $"video_{Path.GetFileNameWithoutExtension(filePath)}.jpg");
                var previewDir = Path.GetDirectoryName(previewPath);

                if (!Directory.Exists(previewDir))
                {
                    Directory.CreateDirectory(previewDir);
                }

                // TODO: Implement actual video preview generation
                // This is a placeholder implementation
                // In a real application, you would:
                // 1. Use a video processing library
                // 2. Extract a frame from the video
                // 3. Save the preview

                return $"/uploads/previews/video_{Path.GetFileNameWithoutExtension(filePath)}.jpg";
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
                if (!File.Exists(filePath))
                    throw new FileNotFoundException("File not found", fileUrl);

                var contentType = GetMimeType(filePath);
                if (!contentType.StartsWith("audio/"))
                    throw new ArgumentException("File is not an audio file");

                var previewPath = Path.Combine(_uploadDirectory, "previews", $"audio_{Path.GetFileNameWithoutExtension(filePath)}.jpg");
                var previewDir = Path.GetDirectoryName(previewPath);

                if (!Directory.Exists(previewDir))
                {
                    Directory.CreateDirectory(previewDir);
                }

                // TODO: Implement actual audio preview generation
                // This is a placeholder implementation
                // In a real application, you would:
                // 1. Use an audio processing library
                // 2. Generate a waveform visualization
                // 3. Save the preview

                return $"/uploads/previews/audio_{Path.GetFileNameWithoutExtension(filePath)}.jpg";
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

                if (!Directory.Exists(previewDir))
                {
                    Directory.CreateDirectory(previewDir);
                }

                using (var image = Image.FromFile(filePath))
                {
                    var ratio = Math.Min(
                        (float)width / image.Width,
                        (float)height / image.Height
                    );

                    var newWidth = (int)(image.Width * ratio);
                    var newHeight = (int)(image.Height * ratio);

                    using (var preview = image.GetThumbnailImage(newWidth, newHeight, null, IntPtr.Zero))
                    {
                        preview.Save(previewPath, ImageFormat.Jpeg);
                    }
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
                    using (var image = Image.FromFile(filePath))
                    {
                        analysis["Width"] = image.Width;
                        analysis["Height"] = image.Height;
                        analysis["Format"] = image.RawFormat.ToString();
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

        public async Task<bool> IsFileVirusFreeAsync(string fileUrl)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                if (!File.Exists(filePath))
                    return false;

                // TODO: Implement actual virus scanning
                // This is a placeholder implementation
                // In a real application, you would:
                // 1. Use a virus scanning library or service
                // 2. Configure scanning parameters
                // 3. Handle different file types appropriately

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking file for viruses: {FileUrl}", fileUrl);
                return false;
            }
        }

        public async Task<Dictionary<string, object>> GetFileStatisticsAsync(string fileUrl)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                if (!File.Exists(filePath))
                    throw new FileNotFoundException("File not found", fileUrl);

                var fileInfo = new FileInfo(filePath);
                var statistics = new Dictionary<string, object>
                {
                    { "FileName", fileInfo.Name },
                    { "FileSize", fileInfo.Length },
                    { "CreatedAt", fileInfo.CreationTimeUtc },
                    { "LastModified", fileInfo.LastWriteTimeUtc },
                    { "LastAccessed", fileInfo.LastAccessTimeUtc },
                    { "IsReadOnly", fileInfo.IsReadOnly },
                    { "Extension", fileInfo.Extension },
                    { "Directory", fileInfo.DirectoryName }
                };

                return statistics;
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
                    using (var image = Image.FromFile(filePath))
                    {
                        analysis["Width"] = image.Width;
                        analysis["Height"] = image.Height;
                        analysis["Format"] = image.RawFormat.ToString();
                        analysis["PixelFormat"] = image.PixelFormat.ToString();
                        analysis["HorizontalResolution"] = image.HorizontalResolution;
                        analysis["VerticalResolution"] = image.VerticalResolution;
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
                var backupDirectory = Path.Combine(_uploadDirectory, "backups");
                if (!Directory.Exists(backupDirectory))
                    return new List<string>();

                var fileName = Path.GetFileNameWithoutExtension(fileUrl);
                var backups = Directory.GetFiles(backupDirectory)
                    .Where(f => Path.GetFileName(f).StartsWith(fileName + "_"))
                    .Select(f => $"/uploads/backups/{Path.GetFileName(f)}")
                    .OrderByDescending(b => b)
                    .ToList();

                return backups;
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
                if (!File.Exists(backupPath))
                    return false;

                File.Delete(backupPath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file backup: {BackupId}", backupId);
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
                    : null;

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
                        results[file.FileName] = null;
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
                if (!Directory.Exists(fullPath))
                    return new List<string>();

                var files = Directory.GetFiles(fullPath)
                    .Select(f => new
                    {
                        Path = f,
                        Info = new FileInfo(f)
                    });

                var sortedFiles = sortBy.ToLower() switch
                {
                    "name" => ascending
                        ? files.OrderBy(f => f.Info.Name)
                        : files.OrderByDescending(f => f.Info.Name),
                    "size" => ascending
                        ? files.OrderBy(f => f.Info.Length)
                        : files.OrderByDescending(f => f.Info.Length),
                    "date" => ascending
                        ? files.OrderBy(f => f.Info.LastWriteTime)
                        : files.OrderByDescending(f => f.Info.LastWriteTime),
                    _ => files.OrderBy(f => f.Info.Name)
                };

                return sortedFiles
                    .Select(f => $"/uploads/{directoryPath.TrimStart('/')}/{Path.GetFileName(f.Path)}")
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sorting files: {DirectoryPath}, {SortBy}", directoryPath, sortBy);
                throw;
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
                if (!Directory.Exists(fullPath))
                    return new List<string>();

                var files = Directory.GetFiles(fullPath)
                    .Where(f => new FileInfo(f).LastWriteTime >= startDate && new FileInfo(f).LastWriteTime <= endDate)
                    .Select(f => $"/uploads/{directoryPath.TrimStart('/')}/{Path.GetFileName(f)}")
                    .ToList();

                return files;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting files by date range: {DirectoryPath}", directoryPath);
                throw;
            }
        }

        public async Task<List<string>> GetFilesBySizeRangeAsync(string directoryPath, long minSize, long maxSize)
        {
            try
            {
                var fullPath = Path.Combine(_uploadDirectory, directoryPath.TrimStart('/'));
                if (!Directory.Exists(fullPath))
                    return new List<string>();

                var files = Directory.GetFiles(fullPath)
                    .Where(f => new FileInfo(f).Length >= minSize && new FileInfo(f).Length <= maxSize)
                    .Select(f => $"/uploads/{directoryPath.TrimStart('/')}/{Path.GetFileName(f)}")
                    .ToList();

                return files;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting files by size range: {DirectoryPath}", directoryPath);
                throw;
            }
        }

        public async Task<List<string>> GetFilesByTypeAsync(string directoryPath, string fileType)
        {
            try
            {
                var fullPath = Path.Combine(_uploadDirectory, directoryPath.TrimStart('/'));
                if (!Directory.Exists(fullPath))
                    return new List<string>();

                var extension = fileType.StartsWith(".") ? fileType : $".{fileType}";
                var files = Directory.GetFiles(fullPath, $"*{extension}")
                    .Select(f => $"/uploads/{directoryPath.TrimStart('/')}/{Path.GetFileName(f)}")
                    .ToList();

                return files;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting files by type: {DirectoryPath}, {FileType}", directoryPath, fileType);
                throw;
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
                        Metadata = metadata.CustomMetadata
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
                var indexPath = Path.Combine(_uploadDirectory, "search_index", directoryPath.TrimStart('/'));
                if (Directory.Exists(indexPath))
                {
                    Directory.Delete(indexPath, true);
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting search index: {DirectoryPath}", directoryPath);
                return false;
            }
        }

        public async Task<List<string>> SearchInIndexAsync(string directoryPath, string searchTerm = null)
        {
            try
            {
                var indexPath = Path.Combine(_uploadDirectory, "search_index", directoryPath.TrimStart('/'));
                if (!Directory.Exists(indexPath))
                    return new List<string>();

                var results = new List<string>();
                var indexFiles = Directory.GetFiles(indexPath, "*.json");

                foreach (var indexFile in indexFiles)
                {
                    var json = await File.ReadAllTextAsync(indexFile);
                    var indexData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);

                    if (string.IsNullOrEmpty(searchTerm) ||
                        indexData.Values.Any(v => v?.ToString().Contains(searchTerm, StringComparison.OrdinalIgnoreCase) == true))
                    {
                        var fileName = indexData["FileName"].ToString();
                        results.Add($"/uploads/{directoryPath.TrimStart('/')}/{fileName}");
                    }
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching in index: {DirectoryPath}", directoryPath);
                throw;
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
                        if (!await DeleteFileAsync(fileUrl))
                        {
                            success = false;
                        }
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

                    foreach (var value in indexData.Values)
                    {
                        var strValue = value?.ToString();
                        if (!string.IsNullOrEmpty(strValue) && 
                            strValue.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                        {
                            // Calculate relevance score based on string similarity
                            var similarity = CalculateStringSimilarity(searchTerm, strValue);
                            if (!suggestions.ContainsKey(strValue) || suggestions[strValue] < similarity)
                            {
                                suggestions[strValue] = similarity;
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
                var fullPath = Path.Combine(_uploadDirectory, directoryPath.TrimStart('/'));
                var indexPath = Path.Combine(_uploadDirectory, "search_index", directoryPath.TrimStart('/'));

                if (!Directory.Exists(fullPath) || !Directory.Exists(indexPath))
                    return false;

                var files = Directory.GetFiles(fullPath);
                var indexFiles = Directory.GetFiles(indexPath, "*.json");

                if (files.Length != indexFiles.Length)
                    return false;

                foreach (var file in files)
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    var indexFile = Path.Combine(indexPath, $"{fileName}.json");

                    if (!File.Exists(indexFile))
                        return false;

                    var fileInfo = new FileInfo(file);
                    var indexInfo = new FileInfo(indexFile);

                    if (fileInfo.LastWriteTime > indexInfo.LastWriteTime)
                        return false;
                }

                return true;
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
                var indexPath = Path.Combine(_uploadDirectory, "search_index", directoryPath.TrimStart('/'));
                if (!Directory.Exists(indexPath))
                    return DateTime.MinValue;

                var indexFiles = Directory.GetFiles(indexPath, "*.json");
                if (!indexFiles.Any())
                    return DateTime.MinValue;

                return indexFiles.Max(f => new FileInfo(f).LastWriteTime);
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

        public async Task<Dictionary<string, long>> GetStorageStatisticsAsync(string directoryPath = null)
        {
            try
            {
                var path = string.IsNullOrEmpty(directoryPath)
                    ? _uploadDirectory
                    : Path.Combine(_uploadDirectory, directoryPath.TrimStart('/'));

                if (!Directory.Exists(path))
                    return new Dictionary<string, long>();

                var files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);
                var stats = new Dictionary<string, long>
                {
                    { "TotalFiles", files.Length },
                    { "TotalSize", files.Sum(f => new FileInfo(f).Length) },
                    { "LastModifiedTicks", files.Max(f => new FileInfo(f).LastWriteTime.Ticks) },
                    { "DirectoryCount", Directory.GetDirectories(path, "*", SearchOption.AllDirectories).Length }
                };

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting storage statistics: {DirectoryPath}", directoryPath);
                throw;
            }
        }

        public async Task<Dictionary<string, object>> GenerateStorageReportAsync(string directoryPath = null, DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                var path = string.IsNullOrEmpty(directoryPath)
                    ? _uploadDirectory
                    : Path.Combine(_uploadDirectory, directoryPath.TrimStart('/'));

                if (!Directory.Exists(path))
                    return new Dictionary<string, object>();

                var files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);
                var start = startDate ?? DateTime.MinValue;
                var end = endDate ?? DateTime.UtcNow;

                var report = new Dictionary<string, object>
                {
                    { "TotalFiles", files.Length },
                    { "TotalSize", files.Sum(f => new FileInfo(f).Length) },
                    { "FileTypes", files.GroupBy(f => Path.GetExtension(f).ToLowerInvariant())
                        .ToDictionary(g => g.Key, g => g.Count()) },
                    { "FilesByDate", files.Where(f => new FileInfo(f).LastWriteTime >= start && new FileInfo(f).LastWriteTime <= end)
                        .GroupBy(f => new FileInfo(f).LastWriteTime.Date)
                        .ToDictionary(g => g.Key.ToString("yyyy-MM-dd"), g => g.Count()) },
                    { "LargestFiles", files.OrderByDescending(f => new FileInfo(f).Length)
                        .Take(10)
                        .Select(f => new { Name = Path.GetFileName(f), Size = new FileInfo(f).Length })
                        .ToList() }
                };

                return report;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating storage report: {DirectoryPath}", directoryPath);
                throw;
            }
        }

        public async Task<long> GetUserStorageQuotaAsync(string userId)
        {
            try
            {
                // TODO: Implement actual quota retrieval from configuration or database
                // This is a placeholder implementation
                return 1024 * 1024 * 1024; // 1GB default quota
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user storage quota: {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> IsStorageQuotaExceededAsync(string userId)
        {
            try
            {
                var quota = await GetUserStorageQuotaAsync(userId);
                var usage = await GetStorageUsageByUserAsync();
                
                return usage.TryGetValue(userId, out var userUsage) && userUsage > quota;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking storage quota: {UserId}", userId);
                throw;
            }
        }

        public async Task<Dictionary<string, long>> GetStorageTrendsAsync(string directoryPath = null, TimeSpan timeRange)
        {
            try
            {
                var path = string.IsNullOrEmpty(directoryPath)
                    ? _uploadDirectory
                    : Path.Combine(_uploadDirectory, directoryPath.TrimStart('/'));

                if (!Directory.Exists(path))
                    return new Dictionary<string, long>();

                var files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);
                var startDate = DateTime.UtcNow.Subtract(timeRange);
                var trends = new Dictionary<string, long>();

                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.LastWriteTime >= startDate)
                    {
                        var dateKey = fileInfo.LastWriteTime.ToString("yyyy-MM-dd");
                        if (!trends.ContainsKey(dateKey))
                            trends[dateKey] = 0;
                        trends[dateKey] += fileInfo.Length;
                    }
                }

                return trends;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting storage trends: {DirectoryPath}", directoryPath);
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

                var convertedPath = Path.Combine(_uploadDirectory, $"converted_{Path.GetFileNameWithoutExtension(filePath)}{targetFormat}");

                using (var image = Image.FromFile(filePath))
                {
                    var format = GetImageFormat(targetFormat);
                    if (quality.HasValue)
                    {
                        var encoderParameters = new EncoderParameters(1);
                        encoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, quality.Value);
                        var codec = ImageCodecInfo.GetImageEncoders()
                            .FirstOrDefault(c => c.FormatID == format.Guid);
                        if (codec != null)
                        {
                            image.Save(convertedPath, codec, encoderParameters);
                        }
                        else
                        {
                            image.Save(convertedPath, format);
                        }
                    }
                    else
                    {
                        image.Save(convertedPath, format);
                    }
                }

                return $"/uploads/converted_{Path.GetFileNameWithoutExtension(filePath)}{targetFormat}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting image format: {FileUrl}", fileUrl);
                throw;
            }
        }

        public async Task<string> ConvertVideoFormatAsync(string fileUrl, string targetFormat, Dictionary<string, string> options = null)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                if (!File.Exists(filePath))
                    throw new FileNotFoundException("File not found", fileUrl);

                var contentType = GetMimeType(filePath);
                if (!contentType.StartsWith("video/"))
                    throw new ArgumentException("File is not a video");

                var convertedPath = Path.Combine(_uploadDirectory, $"converted_{Path.GetFileNameWithoutExtension(filePath)}{targetFormat}");

                // TODO: Implement actual video conversion
                // This is a placeholder implementation
                // In a real application, you would:
                // 1. Use a video processing library (e.g., FFmpeg)
                // 2. Apply the provided options
                // 3. Convert the video to the target format

                return $"/uploads/converted_{Path.GetFileNameWithoutExtension(filePath)}{targetFormat}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting video format: {FileUrl}", fileUrl);
                throw;
            }
        }

        public async Task<string> ConvertAudioFormatAsync(string fileUrl, string targetFormat, Dictionary<string, string> options = null)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                if (!File.Exists(filePath))
                    throw new FileNotFoundException("File not found", fileUrl);

                var contentType = GetMimeType(filePath);
                if (!contentType.StartsWith("audio/"))
                    throw new ArgumentException("File is not an audio file");

                var convertedPath = Path.Combine(_uploadDirectory, $"converted_{Path.GetFileNameWithoutExtension(filePath)}{targetFormat}");

                // TODO: Implement actual audio conversion
                // This is a placeholder implementation
                // In a real application, you would:
                // 1. Use an audio processing library
                // 2. Apply the provided options
                // 3. Convert the audio to the target format

                return $"/uploads/converted_{Path.GetFileNameWithoutExtension(filePath)}{targetFormat}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting audio format: {FileUrl}", fileUrl);
                throw;
            }
        }

        public async Task<Dictionary<string, int>> GetFileTypeDistributionAsync(string directoryPath = null)
        {
            try
            {
                var path = string.IsNullOrEmpty(directoryPath)
                    ? _uploadDirectory
                    : Path.Combine(_uploadDirectory, directoryPath.TrimStart('/'));

                if (!Directory.Exists(path))
                    return new Dictionary<string, int>();

                var files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);
                var distribution = files
                    .GroupBy(f => Path.GetExtension(f).ToLowerInvariant())
                    .ToDictionary(g => g.Key, g => g.Count());

                return distribution;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting file type distribution: {DirectoryPath}", directoryPath);
                throw;
            }
        }

        public async Task<Dictionary<string, long>> GetStorageUsageByUserAsync(string directoryPath = null)
        {
            try
            {
                var path = string.IsNullOrEmpty(directoryPath)
                    ? _uploadDirectory
                    : Path.Combine(_uploadDirectory, directoryPath.TrimStart('/'));

                if (!Directory.Exists(path))
                    return new Dictionary<string, long>();

                var files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);
                var usage = new Dictionary<string, long>();

                foreach (var file in files)
                {
                    var metadata = await GetFileMetadataAsync($"/uploads/{Path.GetRelativePath(_uploadDirectory, file)}");
                    if (metadata?.CustomMetadata != null &&
                        metadata.CustomMetadata.TryGetValue("UserId", out var userId))
                    {
                        var fileSize = new FileInfo(file).Length;
                        if (usage.ContainsKey(userId))
                            usage[userId] += fileSize;
                        else
                            usage[userId] = fileSize;
                    }
                }

                return usage;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting storage usage by user: {DirectoryPath}", directoryPath);
                throw;
            }
        }

        public async Task<Dictionary<string, int>> GetFileActivityStatsAsync(string directoryPath = null, TimeSpan? timeRange = null)
        {
            try
            {
                var path = string.IsNullOrEmpty(directoryPath)
                    ? _uploadDirectory
                    : Path.Combine(_uploadDirectory, directoryPath.TrimStart('/'));

                if (!Directory.Exists(path))
                    return new Dictionary<string, int>();

                var files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);
                var startTime = timeRange.HasValue
                    ? DateTime.UtcNow.Subtract(timeRange.Value)
                    : DateTime.MinValue;

                var stats = new Dictionary<string, int>
                {
                    { "TotalFiles", files.Length },
                    { "NewFiles", files.Count(f => new FileInfo(f).CreationTime >= startTime) },
                    { "ModifiedFiles", files.Count(f => new FileInfo(f).LastWriteTime >= startTime) },
                    { "AccessedFiles", files.Count(f => new FileInfo(f).LastAccessTime >= startTime) }
                };

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting file activity stats: {DirectoryPath}", directoryPath);
                throw;
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

        public async Task<Dictionary<string, object>> GetStorageTrendsAsync(string directoryPath = null, TimeSpan timeRange)
        {
            try
            {
                var path = string.IsNullOrEmpty(directoryPath)
                    ? _uploadDirectory
                    : Path.Combine(_uploadDirectory, directoryPath.TrimStart('/'));

                if (!Directory.Exists(path))
                    return new Dictionary<string, object>();

                var files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);
                var startDate = DateTime.UtcNow.Subtract(timeRange);
                var trends = new Dictionary<string, object>
                {
                    { "TotalSize", 0L },
                    { "FileCount", 0 },
                    { "DailyUsage", new Dictionary<string, long>() },
                    { "FileTypeDistribution", new Dictionary<string, int>() },
                    { "UserDistribution", new Dictionary<string, long>() }
                };

                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.LastWriteTime >= startDate)
                    {
                        var dateKey = fileInfo.LastWriteTime.ToString("yyyy-MM-dd");
                        var extension = Path.GetExtension(file).ToLowerInvariant();
                        var metadata = await GetFileMetadataAsync($"/uploads/{Path.GetRelativePath(_uploadDirectory, file)}");

                        // Update daily usage
                        if (!((Dictionary<string, long>)trends["DailyUsage"]).ContainsKey(dateKey))
                            ((Dictionary<string, long>)trends["DailyUsage"])[dateKey] = 0;
                        ((Dictionary<string, long>)trends["DailyUsage"])[dateKey] += fileInfo.Length;

                        // Update file type distribution
                        if (!((Dictionary<string, int>)trends["FileTypeDistribution"]).ContainsKey(extension))
                            ((Dictionary<string, int>)trends["FileTypeDistribution"])[extension] = 0;
                        ((Dictionary<string, int>)trends["FileTypeDistribution"])[extension]++;

                        // Update user distribution
                        if (metadata?.CustomMetadata != null &&
                            metadata.CustomMetadata.TryGetValue("UserId", out var userId))
                        {
                            if (!((Dictionary<string, long>)trends["UserDistribution"]).ContainsKey(userId))
                                ((Dictionary<string, long>)trends["UserDistribution"])[userId] = 0;
                            ((Dictionary<string, long>)trends["UserDistribution"])[userId] += fileInfo.Length;
                        }

                        trends["TotalSize"] = (long)trends["TotalSize"] + fileInfo.Length;
                        trends["FileCount"] = (int)trends["FileCount"] + 1;
                    }
                }

                return trends;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting storage trends: {DirectoryPath}", directoryPath);
                throw;
            }
        }

        public async Task<string> ConvertDocumentFormatAsync(string fileUrl, string targetFormat, Dictionary<string, string> options = null)
        {
            try
            {
                var filePath = GetFilePathFromUrl(fileUrl);
                if (!File.Exists(filePath))
                    throw new FileNotFoundException("File not found", fileUrl);

                var contentType = GetMimeType(filePath);
                if (!contentType.StartsWith("application/") && !contentType.StartsWith("text/"))
                    throw new ArgumentException("File is not a document");

                var convertedPath = Path.Combine(_uploadDirectory, $"converted_{Path.GetFileNameWithoutExtension(filePath)}{targetFormat}");

                // TODO: Implement actual document conversion
                // This is a placeholder implementation
                // In a real application, you would:
                // 1. Use a document processing library
                // 2. Apply the provided options
                // 3. Convert the document to the target format

                return $"/uploads/converted_{Path.GetFileNameWithoutExtension(filePath)}{targetFormat}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting document format: {FileUrl}", fileUrl);
                throw;
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
                // Define supported format conversions
                var supportedFormats = new Dictionary<string, List<string>>
                {
                    // Image formats
                    { ".jpg", new List<string> { ".png", ".gif", ".bmp", ".webp" } },
                    { ".png", new List<string> { ".jpg", ".gif", ".bmp", ".webp" } },
                    { ".gif", new List<string> { ".jpg", ".png", ".webp" } },
                    { ".bmp", new List<string> { ".jpg", ".png", ".webp" } },
                    { ".webp", new List<string> { ".jpg", ".png", ".gif" } },

                    // Document formats
                    { ".doc", new List<string> { ".pdf", ".docx", ".txt" } },
                    { ".docx", new List<string> { ".pdf", ".doc", ".txt" } },
                    { ".pdf", new List<string> { ".doc", ".docx", ".txt" } },
                    { ".txt", new List<string> { ".pdf", ".doc", ".docx" } },

                    // Video formats
                    { ".mp4", new List<string> { ".avi", ".mov", ".wmv" } },
                    { ".avi", new List<string> { ".mp4", ".mov", ".wmv" } },
                    { ".mov", new List<string> { ".mp4", ".avi", ".wmv" } },
                    { ".wmv", new List<string> { ".mp4", ".avi", ".mov" } },

                    // Audio formats
                    { ".mp3", new List<string> { ".wav", ".ogg", ".aac" } },
                    { ".wav", new List<string> { ".mp3", ".ogg", ".aac" } },
                    { ".ogg", new List<string> { ".mp3", ".wav", ".aac" } },
                    { ".aac", new List<string> { ".mp3", ".wav", ".ogg" } }
                };

                return supportedFormats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting supported formats");
                throw;
            }
        }

        public async Task<Dictionary<string, string>> GetFormatConversionOptionsAsync(string sourceFormat, string targetFormat)
        {
            try
            {
                if (!await IsFormatConversionSupportedAsync(sourceFormat, targetFormat))
                    throw new ArgumentException($"Conversion from {sourceFormat} to {targetFormat} is not supported");

                var options = new Dictionary<string, string>();

                if (IsImageFile(sourceFormat) && IsImageFile(targetFormat))
                {
                    options["Quality"] = "0-100";
                    options["PreserveMetadata"] = "true/false";
                    options["OptimizeForWeb"] = "true/false";
                }
                else if (sourceFormat.StartsWith(".doc") || targetFormat.StartsWith(".doc"))
                {
                    options["PreserveFormatting"] = "true/false";
                    options["IncludeImages"] = "true/false";
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

                using (var image = Image.FromFile(filePath))
                {
                    var ratio = Math.Min(
                        (float)maxWidth / image.Width,
                        (float)maxHeight / image.Height
                    );

                    var newWidth = (int)(image.Width * ratio);
                    var newHeight = (int)(image.Height * ratio);

                    using (var resized = image.GetThumbnailImage(newWidth, newHeight, null, IntPtr.Zero))
                    {
                        var encoderParameters = new EncoderParameters(1);
                        encoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, quality);

                        var codec = ImageCodecInfo.GetImageEncoders()
                            .FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);

                        if (codec != null)
                        {
                            resized.Save(optimizedPath, codec, encoderParameters);
                        }
                        else
                        {
                            resized.Save(optimizedPath, ImageFormat.Jpeg);
                        }
                    }
                }

                return $"/uploads/web_optimized_{Path.GetFileNameWithoutExtension(filePath)}.jpg";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error optimizing image for web: {FileUrl}", fileUrl);
                throw;
            }
        }
    }
} 