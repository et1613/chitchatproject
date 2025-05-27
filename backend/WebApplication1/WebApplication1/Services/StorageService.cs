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

    public interface IStorageService
    {
        Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType);
        Task<string> UploadFileInChunksAsync(IFormFile file, string fileName, string contentType, CancellationToken cancellationToken = default);
        Task DeleteFileAsync(string fileUrl);
        Task<Stream> DownloadFileAsync(string fileUrl);
        Task<bool> FileExistsAsync(string fileUrl);
        Task<FileMetadata> GetFileMetadataAsync(string fileUrl);
        Task<Stream> GetThumbnailAsync(string fileUrl);
        Task<string> CompressFileAsync(string fileUrl);
        Task<string> ConvertFileFormatAsync(string fileUrl, string targetFormat);
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

        public async Task DeleteFileAsync(string fileUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(fileUrl)) return;

                var fileName = Path.GetFileName(fileUrl);
                var filePath = Path.Combine(_uploadDirectory, fileName);
                var thumbnailPath = GetThumbnailPath(filePath);
                var metadataPath = GetMetadataPath(fileName);

                await _semaphore.WaitAsync();
                try
                {
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

                    _cache.Remove(fileUrl);
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

        public async Task<string> CompressFileAsync(string fileUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(fileUrl))
                    throw new ArgumentException("Dosya URL'i boş olamaz");

                var fileName = Path.GetFileName(fileUrl);
                var filePath = Path.Combine(_uploadDirectory, fileName);
                var compressedPath = Path.Combine(_uploadDirectory, $"compressed_{fileName}");

                if (!File.Exists(filePath))
                    throw new FileNotFoundException("Dosya bulunamadı", fileName);

                using (var sourceStream = new FileStream(filePath, FileMode.Open))
                using (var destinationStream = new FileStream(compressedPath, FileMode.Create))
                using (var gzipStream = new GZipStream(destinationStream, CompressionLevel.Optimal))
                {
                    await sourceStream.CopyToAsync(gzipStream);
                }

                return $"/uploads/compressed_{fileName}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya sıkıştırılırken hata oluştu: {FileUrl}", fileUrl);
                throw new Exception($"Dosya sıkıştırılırken hata oluştu: {ex.Message}", ex);
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

        private async Task GenerateThumbnailAsync(string filePath)
        {
            var thumbnailPath = GetThumbnailPath(filePath);

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
    }
} 