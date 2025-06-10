using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WebApplication1.Models.Messages;
using System.IO;
using Microsoft.AspNetCore.Http;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using WebApplication1.Models.Chat;
using WebApplication1.Data;
using SixLabors.ImageSharp;
using Microsoft.EntityFrameworkCore;

namespace WebApplication1.Services
{
    public interface IEncryptionService
    {
        void EncryptFileWithAes(Stream inputStream, Stream outputStream, string key, string iv);
        void DecryptFileWithAes(Stream inputStream, Stream outputStream, string key, string iv);
    }

    public interface ISignatureService
    {
        Task<(string Signature, string Message)> SignMessageAsync(string message, string key, Dictionary<string, string>? metadata = null);
        Task<bool> VerifySignatureAsync(string message, string signature, string key);
    }

    public interface IFileService
    {
        Task<Attachment> UploadFileAsync(IFormFile file, string userId, string messageId);
        Task<bool> DeleteFileAsync(string fileId, string userId);
        Task<Attachment> GetFileAsync(string fileId);
        Task<string> GetFileUrlAsync(string fileId);
        Task<bool> UpdateFileMetadataAsync(string fileId, Dictionary<string, string> metadata);
        Task<bool> GenerateThumbnailAsync(string fileId);
        Task<bool> ValidateFileAsync(IFormFile file);
        Task<string> CalculateFileHashAsync(IFormFile file);
        Task<Attachment> CompressFileAsync(string fileId, int quality = 80);
        Task<Attachment> OptimizeImageAsync(string fileId, int maxWidth = 1920, int maxHeight = 1080);
        Task<Attachment> ConvertFileFormatAsync(string fileId, string targetFormat);
        Task<Dictionary<string, object>> AnalyzeFileAsync(string fileId);
        Task<bool> IsFileVirusFreeAsync(string fileId);
        Task<Attachment> CreateFileVersionAsync(string fileId);
        Task<List<Attachment>> GetFileVersionsAsync(string fileId);
        Task<Attachment> RestoreFileVersionAsync(string fileId, string versionId);
        Task<string> GenerateShareableLinkAsync(string fileId, TimeSpan? expiration = null);
        Task<bool> RevokeShareableLinkAsync(string fileId);
        Task<List<Attachment>> UploadMultipleFilesAsync(List<IFormFile> files, string userId, string messageId);
        Task<bool> DeleteMultipleFilesAsync(List<string> fileIds, string userId);
        Task<Dictionary<string, long>> GetStorageUsageAsync(string userId);
        Task<Dictionary<string, int>> GetFileTypeDistributionAsync(string userId);
        Task<List<Attachment>> SearchFilesAsync(string userId, string searchTerm, string? fileType = null);
        Task<List<Attachment>> GetRecentFilesAsync(string userId, int count = 10);
        Task<bool> AddFileTagAsync(string fileId, string tag);
        Task<bool> RemoveFileTagAsync(string fileId, string tag);
        Task<List<Attachment>> GetFilesByTagAsync(string userId, string tag);
        Task<bool> BackupFileAsync(string fileId);
        Task<bool> RestoreFileFromBackupAsync(string fileId);
        Task<bool> SetFilePermissionsAsync(string fileId, Dictionary<string, string> permissions);
        Task<bool> CheckFileAccessAsync(string fileId, string userId);
        Task<string?> GenerateFilePreviewAsync(string fileId);
        Task<Dictionary<string, string>> GetFilePreviewsAsync(List<string> fileIds);
        Task<Attachment> EncryptFileAsync(string fileId, string encryptionKey);
        Task<Attachment> DecryptFileAsync(string fileId, string encryptionKey);
        Task<string> UploadFileAsync(Stream fileStream, string fileName);
        Task DeleteFileAsync(string fileUrl);
        Task<Attachment> UploadAttachmentAsync(Stream fileStream, string fileName, string userId, string messageId);
        Task DeleteAttachmentAsync(Attachment attachment);
    }

    public class FileService : IFileService
    {
        private readonly ILogger<FileService> _logger;
        private readonly IStorageService _storageService;
        private readonly ApplicationDbContext _context;
        private readonly IEncryptionService _encryptionService;
        private readonly ISignatureService _signatureService;

        public FileService(
            ILogger<FileService> logger,
            IStorageService storageService,
            ApplicationDbContext context,
            IEncryptionService encryptionService,
            ISignatureService signatureService)
        {
            _logger = logger;
            _storageService = storageService;
            _context = context;
            _encryptionService = encryptionService;
            _signatureService = signatureService;
        }

        public async Task<Attachment> UploadFileAsync(IFormFile file, string userId, string messageId)
        {
            try
            {
                if (file == null || file.Length == 0)
                    throw new ArgumentException("Dosya boş olamaz");

                // Dosya doğrulama
                if (!await ValidateFileAsync(file))
                    throw new InvalidOperationException("Geçersiz dosya");

                // Dosya hash'i hesaplama
                var fileHash = await CalculateFileHashAsync(file);

                // Dosyayı depolama servisine yükleme
                var fileUrl = await _storageService.UploadFileAsync(file);

                // Thumbnail oluşturma (eğer görsel/video ise)
                string? thumbnailUrl = null;
                if (file.ContentType.StartsWith("image/") || file.ContentType.StartsWith("video/"))
                {
                    thumbnailUrl = await _storageService.GenerateThumbnailAsync(file);
                }

                // Attachment oluşturma
                var attachment = new Attachment
                {
                    MessageId = messageId,
                    Message = await _context.Messages.FindAsync(messageId) ?? throw new ArgumentException("Message not found"),
                    Url = fileUrl,
                    FileName = file.FileName,
                    FileType = file.ContentType,
                    FileSize = file.Length,
                    UploadedBy = userId,
                    ThumbnailUrl = thumbnailUrl,
                    MimeType = file.ContentType,
                    Metadata = new Dictionary<string, string>
                    {
                        { "Hash", fileHash },
                        { "OriginalName", file.FileName },
                        { "ContentType", file.ContentType }
                    }
                };

                // Görsel/video boyutlarını alma
                if (file.ContentType.StartsWith("image/"))
                {
                    using var image = await Image.LoadAsync(file.OpenReadStream());
                    attachment.Width = image.Width;
                    attachment.Height = image.Height;
                }

                _context.Attachments.Add(attachment);
                await _context.SaveChangesAsync();

                return attachment;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya yükleme hatası: {FileName}", file?.FileName);
                throw;
            }
        }

        public async Task<bool> DeleteFileAsync(string fileId, string userId)
        {
            try
            {
                var attachment = await _context.Attachments.FindAsync(fileId);
                if (attachment == null)
                    throw new ArgumentException("Dosya bulunamadı");

                if (attachment.UploadedBy != userId)
                    throw new UnauthorizedAccessException("Bu dosyayı silme yetkiniz yok");

                // Dosyayı depolama servisinden silme
                if (!string.IsNullOrEmpty(attachment.Url))
                {
                    await _storageService.DeleteFileAsync(attachment.Url);
                }
                if (!string.IsNullOrEmpty(attachment.ThumbnailUrl))
                {
                    await _storageService.DeleteFileAsync(attachment.ThumbnailUrl);
                }

                // Veritabanından silme
                attachment.Delete(userId);
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya silme hatası: {FileId}", fileId);
                throw;
            }
        }

        public async Task<Attachment> GetFileAsync(string fileId)
        {
            try
            {
                var attachment = await _context.Attachments.FindAsync(fileId);
                if (attachment == null)
                    throw new ArgumentException($"Dosya bulunamadı: {fileId}");

                return attachment;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya getirme hatası: {FileId}", fileId);
                throw;
            }
        }

        public async Task<string> GetFileUrlAsync(string fileId)
        {
            try
            {
                var attachment = await _context.Attachments.FindAsync(fileId);
                if (attachment == null)
                    throw new ArgumentException("Dosya bulunamadı");

                if (string.IsNullOrEmpty(attachment.Url))
                    throw new InvalidOperationException("Dosya URL'si bulunamadı");

                return attachment.Url;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya URL getirme hatası: {FileId}", fileId);
                throw;
            }
        }

        public async Task<bool> UpdateFileMetadataAsync(string fileId, Dictionary<string, string> metadata)
        {
            try
            {
                var attachment = await _context.Attachments.FindAsync(fileId);
                if (attachment == null)
                    throw new ArgumentException("Dosya bulunamadı");

                foreach (var item in metadata)
                {
                    attachment.Metadata[item.Key] = item.Value;
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya metadata güncelleme hatası: {FileId}", fileId);
                throw;
            }
        }

        public async Task<bool> GenerateThumbnailAsync(string fileId)
        {
            try
            {
                var attachment = await _context.Attachments.FindAsync(fileId);
                if (attachment == null)
                    throw new ArgumentException("Dosya bulunamadı");

                if (string.IsNullOrEmpty(attachment.Url))
                    throw new InvalidOperationException("Dosya URL'si bulunamadı");

                if (!attachment.IsImage() && !attachment.IsVideo())
                    throw new InvalidOperationException("Sadece görsel ve video dosyaları için thumbnail oluşturulabilir");

                var thumbnailUrl = await _storageService.GenerateThumbnailAsync(attachment.Url);
                attachment.ThumbnailUrl = thumbnailUrl;

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Thumbnail oluşturma hatası: {FileId}", fileId);
                throw;
            }
        }

        public async Task<bool> ValidateFileAsync(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return false;

                // Dosya boyutu kontrolü (örn: 100MB)
                if (file.Length > 100 * 1024 * 1024)
                    return false;

                // Dosya türü kontrolü
                var allowedTypes = new[]
                {
                    "image/jpeg", "image/png", "image/gif",
                    "video/mp4", "video/quicktime",
                    "audio/mpeg", "audio/wav",
                    "application/pdf", "application/msword",
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
                };

                if (!allowedTypes.Contains(file.ContentType.ToLower()))
                    return false;

                // Dosya içeriği kontrolü
                using var stream = file.OpenReadStream();
                var buffer = new byte[4];
                await stream.ReadAsync(buffer, 0, 4);
                stream.Position = 0;

                // Basit dosya imza kontrolü
                var signatures = new Dictionary<string, byte[]>
                {
                    { "image/jpeg", new byte[] { 0xFF, 0xD8, 0xFF } },
                    { "image/png", new byte[] { 0x89, 0x50, 0x4E, 0x47 } },
                    { "application/pdf", new byte[] { 0x25, 0x50, 0x44, 0x46 } }
                };

                if (signatures.TryGetValue(file.ContentType.ToLower(), out var signature))
                {
                    return buffer.Take(signature.Length).SequenceEqual(signature);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya doğrulama hatası: {FileName}", file?.FileName);
                return false;
            }
        }

        public async Task<string> CalculateFileHashAsync(IFormFile file)
        {
            try
            {
                using var stream = file.OpenReadStream();
                using var sha256 = SHA256.Create();
                var hash = await sha256.ComputeHashAsync(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya hash hesaplama hatası: {FileName}", file?.FileName);
                throw;
            }
        }

        public async Task<Attachment> CompressFileAsync(string fileId, int quality = 80)
        {
            try
            {
                var attachment = await _context.Attachments.FindAsync(fileId);
                if (attachment == null)
                    throw new ArgumentException("Dosya bulunamadı");

                if (string.IsNullOrEmpty(attachment.Url))
                    throw new InvalidOperationException("Dosya URL'si bulunamadı");

                if (!attachment.IsImage() && !attachment.IsVideo())
                    throw new InvalidOperationException("Sadece görsel ve video dosyaları sıkıştırılabilir");

                // Convert quality (0-100) to CompressionLevel
                var compressionLevel = quality switch
                {
                    >= 90 => System.IO.Compression.CompressionLevel.NoCompression,
                    >= 70 => System.IO.Compression.CompressionLevel.Fastest,
                    >= 40 => System.IO.Compression.CompressionLevel.Optimal,
                    _ => System.IO.Compression.CompressionLevel.SmallestSize
                };

                var compressedUrl = await _storageService.CompressFileAsync(attachment.Url, compressionLevel);
                
                var compressedAttachment = new Attachment
                {
                    MessageId = attachment.MessageId,
                    Message = attachment.Message,
                    Url = compressedUrl,
                    FileName = $"compressed_{attachment.FileName}",
                    FileType = attachment.FileType,
                    FileSize = await _storageService.GetFileSizeAsync(compressedUrl),
                    UploadedBy = attachment.UploadedBy,
                    MimeType = attachment.MimeType,
                    Metadata = new Dictionary<string, string>
                    {
                        { "OriginalFileId", fileId },
                        { "CompressionQuality", quality.ToString() },
                        { "CompressedAt", DateTime.UtcNow.ToString() }
                    }
                };

                _context.Attachments.Add(compressedAttachment);
                await _context.SaveChangesAsync();

                return compressedAttachment;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya sıkıştırma hatası: {FileId}", fileId);
                throw;
            }
        }

        public async Task<Attachment> OptimizeImageAsync(string fileId, int maxWidth = 1920, int maxHeight = 1080)
        {
            try
            {
                var attachment = await _context.Attachments.FindAsync(fileId);
                if (attachment == null)
                    throw new ArgumentException("Dosya bulunamadı");

                if (string.IsNullOrEmpty(attachment.Url))
                    throw new InvalidOperationException("Dosya URL'si bulunamadı");

                if (!attachment.IsImage())
                    throw new InvalidOperationException("Sadece görsel dosyaları optimize edilebilir");

                var optimizedUrl = await _storageService.OptimizeImageAsync(attachment.Url, maxWidth, maxHeight);
                
                var optimizedAttachment = new Attachment
                {
                    MessageId = attachment.MessageId,
                    Message = attachment.Message,
                    Url = optimizedUrl,
                    FileName = $"optimized_{attachment.FileName}",
                    FileType = attachment.FileType,
                    FileSize = await _storageService.GetFileSizeAsync(optimizedUrl),
                    UploadedBy = attachment.UploadedBy,
                    MimeType = attachment.MimeType,
                    Metadata = new Dictionary<string, string>
                    {
                        { "OriginalFileId", fileId },
                        { "MaxWidth", maxWidth.ToString() },
                        { "MaxHeight", maxHeight.ToString() },
                        { "OptimizedAt", DateTime.UtcNow.ToString() }
                    }
                };

                _context.Attachments.Add(optimizedAttachment);
                await _context.SaveChangesAsync();

                return optimizedAttachment;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Görsel optimizasyon hatası: {FileId}", fileId);
                throw;
            }
        }

        public async Task<List<Attachment>> UploadMultipleFilesAsync(List<IFormFile> files, string userId, string messageId)
        {
            try
            {
                var attachments = new List<Attachment>();
                foreach (var file in files)
                {
                    var attachment = await UploadFileAsync(file, userId, messageId);
                    attachments.Add(attachment);
                }
                return attachments;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Çoklu dosya yükleme hatası");
                throw;
            }
        }

        public async Task<bool> DeleteMultipleFilesAsync(List<string> fileIds, string userId)
        {
            try
            {
                foreach (var fileId in fileIds)
                {
                    await DeleteFileAsync(fileId, userId);
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Çoklu dosya silme hatası");
                throw;
            }
        }

        public async Task<Dictionary<string, long>> GetStorageUsageAsync(string userId)
        {
            try
            {
                var attachments = await _context.Attachments
                    .Where(a => a.UploadedBy == userId && !a.IsDeleted)
                    .ToListAsync();

                var usage = new Dictionary<string, long>
                {
                    { "Total", attachments.Sum(a => a.FileSize) },
                    { "Images", attachments.Where(a => a.IsImage()).Sum(a => a.FileSize) },
                    { "Videos", attachments.Where(a => a.IsVideo()).Sum(a => a.FileSize) },
                    { "Documents", attachments.Where(a => a.IsDocument()).Sum(a => a.FileSize) },
                    { "Audio", attachments.Where(a => a.IsAudio()).Sum(a => a.FileSize) }
                };

                return usage;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Depolama kullanımı hesaplama hatası: {UserId}", userId);
                throw;
            }
        }

        public async Task<List<Attachment>> GetRecentFilesAsync(string userId, int count = 10)
        {
            try
            {
                return await _context.Attachments
                    .Where(a => a.UploadedBy == userId && !a.IsDeleted)
                    .OrderByDescending(a => a.UploadedAt)
                    .Take(count)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Son dosyaları getirme hatası: {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> AddFileTagAsync(string fileId, string tag)
        {
            try
            {
                var attachment = await _context.Attachments.FindAsync(fileId);
                if (attachment == null)
                    throw new ArgumentException("Dosya bulunamadı");

                if (!attachment.Metadata.ContainsKey("Tags"))
                {
                    attachment.Metadata["Tags"] = tag;
                }
                else
                {
                    var tags = attachment.Metadata["Tags"].Split(',').ToList();
                    if (!tags.Contains(tag))
                    {
                        tags.Add(tag);
                        attachment.Metadata["Tags"] = string.Join(",", tags);
                    }
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya etiketleme hatası: {FileId}", fileId);
                throw;
            }
        }

        public async Task<List<Attachment>> GetFilesByTagAsync(string userId, string tag)
        {
            try
            {
                return await _context.Attachments
                    .Where(a => a.UploadedBy == userId && 
                           !a.IsDeleted && 
                           a.Metadata.ContainsKey("Tags") && 
                           a.Metadata["Tags"].Contains(tag))
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Etiketli dosyaları getirme hatası: {UserId}, {Tag}", userId, tag);
                throw;
            }
        }

        public async Task<string?> GenerateFilePreviewAsync(string fileId)
        {
            try
            {
                var attachment = await _context.Attachments.FindAsync(fileId);
                if (attachment == null)
                    throw new ArgumentException("Dosya bulunamadı");

                if (string.IsNullOrEmpty(attachment.Url))
                    throw new InvalidOperationException("Dosya URL'si bulunamadı");

                if (attachment.IsImage())
                {
                    return attachment.Url; // Görseller için orijinal URL
                }
                else if (attachment.IsVideo())
                {
                    return attachment.ThumbnailUrl ?? attachment.Url; // Videolar için thumbnail
                }
                else if (attachment.IsDocument())
                {
                    // PDF önizleme URL'si oluştur
                    return await _storageService.GenerateDocumentPreviewAsync(attachment.Url);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya önizleme oluşturma hatası: {FileId}", fileId);
                throw;
            }
        }

        public async Task<Dictionary<string, string>> GetFilePreviewsAsync(List<string> fileIds)
        {
            try
            {
                var previews = new Dictionary<string, string>();
                foreach (var fileId in fileIds)
                {
                    previews[fileId] = await GenerateFilePreviewAsync(fileId) ?? string.Empty;
                }
                return previews;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya önizleme URL'leri getirme hatası");
                throw;
            }
        }

        public async Task<Attachment> EncryptFileAsync(string fileId, string encryptionKey)
        {
            try
            {
                var attachment = await _context.Attachments.FindAsync(fileId);
                if (attachment == null)
                    throw new ArgumentException("Dosya bulunamadı");

                if (string.IsNullOrEmpty(attachment.Url))
                    throw new InvalidOperationException("Dosya URL'si bulunamadı");

                var fileStream = await _storageService.DownloadFileAsync(attachment.Url);
                var encryptedStream = new MemoryStream();
                
                _encryptionService.EncryptFileWithAes(
                    fileStream,
                    encryptedStream,
                    encryptionKey,
                    Convert.ToBase64String(new byte[16]) // IV
                );

                var encryptedUrl = await _storageService.UploadFileAsync(encryptedStream, $"{attachment.FileName}.encrypted");
                
                attachment.Url = encryptedUrl;
                attachment.Metadata["Encrypted"] = "true";
                attachment.Metadata["EncryptionKeyId"] = Guid.NewGuid().ToString();
                
                await _context.SaveChangesAsync();
                return attachment;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya şifreleme hatası: {FileId}", fileId);
                throw;
            }
        }

        public async Task<Attachment> DecryptFileAsync(string fileId, string encryptionKey)
        {
            try
            {
                var attachment = await _context.Attachments.FindAsync(fileId);
                if (attachment == null)
                    throw new ArgumentException("Dosya bulunamadı");

                if (string.IsNullOrEmpty(attachment.Url))
                    throw new InvalidOperationException("Dosya URL'si bulunamadı");

                if (!attachment.Metadata.ContainsKey("Encrypted"))
                    throw new InvalidOperationException("Dosya şifrelenmemiş");

                var fileStream = await _storageService.DownloadFileAsync(attachment.Url);
                var decryptedStream = new MemoryStream();
                
                _encryptionService.DecryptFileWithAes(
                    fileStream,
                    decryptedStream,
                    encryptionKey,
                    Convert.ToBase64String(new byte[16]) // IV
                );

                var decryptedUrl = await _storageService.UploadFileAsync(decryptedStream, attachment.FileName);
                
                attachment.Url = decryptedUrl;
                attachment.Metadata.Remove("Encrypted");
                attachment.Metadata.Remove("EncryptionKeyId");
                
                await _context.SaveChangesAsync();
                return attachment;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya şifre çözme hatası: {FileId}", fileId);
                throw;
            }
        }

        public async Task<Attachment> CreateFileVersionAsync(string fileId)
        {
            try
            {
                var originalAttachment = await _context.Attachments.FindAsync(fileId);
                if (originalAttachment == null)
                    throw new ArgumentException("Dosya bulunamadı");

                if (string.IsNullOrEmpty(originalAttachment.Url))
                    throw new InvalidOperationException("Dosya URL'si bulunamadı");

                var version = new Attachment
                {
                    MessageId = originalAttachment.MessageId,
                    Message = originalAttachment.Message,
                    FileType = originalAttachment.FileType,
                    FileSize = originalAttachment.FileSize,
                    FileName = $"{originalAttachment.FileName}.v{DateTime.UtcNow.Ticks}",
                    UploadedBy = originalAttachment.UploadedBy,
                    MimeType = originalAttachment.MimeType,
                    Metadata = new Dictionary<string, string>
                    {
                        { "VersionOf", fileId },
                        { "VersionCreatedAt", DateTime.UtcNow.ToString("o") }
                    }
                };

                var fileStream = await _storageService.DownloadFileAsync(originalAttachment.Url);
                version.Url = await _storageService.UploadFileAsync(fileStream, version.FileName);

                _context.Attachments.Add(version);
                await _context.SaveChangesAsync();

                return version;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya versiyonu oluşturma hatası: {FileId}", fileId);
                throw;
            }
        }

        public async Task<string> GenerateShareableLinkAsync(string fileId, TimeSpan? expiration = null)
        {
            try
            {
                var attachment = await _context.Attachments.FindAsync(fileId);
                if (attachment == null)
                    throw new ArgumentException("Dosya bulunamadı");

                var token = await _signatureService.SignMessageAsync(
                    fileId,
                    Convert.ToBase64String(new byte[32]), // Key
                    metadata: new Dictionary<string, string>
                    {
                        { "ExpiresAt", DateTime.UtcNow.Add(expiration ?? TimeSpan.FromDays(7)).ToString("o") }
                    }
                );

                return $"/api/files/shared/{fileId}?token={token.Signature}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Paylaşım linki oluşturma hatası: {FileId}", fileId);
                throw;
            }
        }

        public async Task<Dictionary<string, object>> AnalyzeFileAsync(string fileId)
        {
            try
            {
                var attachment = await _context.Attachments.FindAsync(fileId);
                if (attachment == null)
                    throw new ArgumentException("Dosya bulunamadı");

                if (string.IsNullOrEmpty(attachment.Url))
                    throw new InvalidOperationException("Dosya URL'si bulunamadı");

                var analysis = new Dictionary<string, object>
                {
                    { "FileSize", attachment.FileSize },
                    { "FileType", attachment.FileType },
                    { "UploadedAt", attachment.UploadedAt },
                    { "UploadedBy", attachment.UploadedBy },
                    { "IsEncrypted", attachment.Metadata.ContainsKey("Encrypted") },
                    { "IsCompressed", attachment.Metadata.ContainsKey("Compressed") }
                };

                if (attachment.IsImage())
                {
                    var fileStream = await _storageService.DownloadFileAsync(attachment.Url);
                    using var image = await Image.LoadAsync(fileStream);
                    analysis["Width"] = image.Width;
                    analysis["Height"] = image.Height;
                    analysis["Format"] = image.Metadata.DecodedImageFormat?.Name ?? "Unknown";
                }

                return analysis;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya analizi hatası: {FileId}", fileId);
                throw;
            }
        }

        public async Task<bool> SetFilePermissionsAsync(string fileId, Dictionary<string, string> permissions)
        {
            try
            {
                var attachment = await _context.Attachments.FindAsync(fileId);
                if (attachment == null)
                    throw new ArgumentException("Dosya bulunamadı");

                foreach (var permission in permissions)
                {
                    attachment.Metadata[$"Permission_{permission.Key}"] = permission.Value;
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya izinleri güncelleme hatası: {FileId}", fileId);
                throw;
            }
        }

        public async Task<bool> CheckFileAccessAsync(string fileId, string userId)
        {
            try
            {
                var attachment = await _context.Attachments.FindAsync(fileId);
                if (attachment == null)
                    throw new ArgumentException("Dosya bulunamadı");

                // Dosya sahibi her zaman erişebilir
                if (attachment.UploadedBy == userId)
                    return true;

                // Özel izinler kontrol edilir
                var permissionKey = $"Permission_{userId}";
                if (attachment.Metadata.ContainsKey(permissionKey))
                {
                    var permission = attachment.Metadata[permissionKey];
                    return permission == "Read" || permission == "Write";
                }

                // Varsayılan olarak erişim reddedilir
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya erişim kontrolü hatası: {FileId}", fileId);
                throw;
            }
        }

        public async Task<Attachment> ConvertFileFormatAsync(string fileId, string targetFormat)
        {
            try
            {
                var attachment = await _context.Attachments.FindAsync(fileId);
                if (attachment == null)
                    throw new ArgumentException("Dosya bulunamadı");

                if (string.IsNullOrEmpty(attachment.Url))
                    throw new InvalidOperationException("Dosya URL'si bulunamadı");

                // Download the file to a temporary location
                var tempFilePath = Path.GetTempFileName();
                using (var fileStream = await _storageService.DownloadFileAsync(attachment.Url))
                using (var tempFileStream = File.Create(tempFilePath))
                {
                    await fileStream.CopyToAsync(tempFileStream);
                }

                // Convert the file format
                var convertedFilePath = await _storageService.ConvertFileFormatAsync(tempFilePath, targetFormat);
                
                // Create new attachment for the converted file
                var convertedAttachment = new Attachment
                {
                    MessageId = attachment.MessageId,
                    Message = attachment.Message,
                    FileName = $"{Path.GetFileNameWithoutExtension(attachment.FileName)}.{targetFormat}",
                    FileType = targetFormat,
                    FileSize = new FileInfo(convertedFilePath).Length,
                    UploadedBy = attachment.UploadedBy,
                    MimeType = GetMimeType($".{targetFormat}"),
                    Metadata = new Dictionary<string, string>
                    {
                        { "OriginalFileId", fileId },
                        { "ConvertedFrom", attachment.FileType },
                        { "ConvertedAt", DateTime.UtcNow.ToString() }
                    }
                };

                // Upload the converted file
                using (var convertedFileStream = File.OpenRead(convertedFilePath))
                {
                    convertedAttachment.Url = await _storageService.UploadFileAsync(convertedFileStream, convertedAttachment.FileName);
                }

                // Clean up temporary files
                try
                {
                    File.Delete(tempFilePath);
                    File.Delete(convertedFilePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Temporary file cleanup failed");
                }

                _context.Attachments.Add(convertedAttachment);
                await _context.SaveChangesAsync();

                return convertedAttachment;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya formatı dönüştürme hatası: {FileId}", fileId);
                throw;
            }
        }

        public async Task<bool> IsFileVirusFreeAsync(string fileId)
        {
            try
            {
                var attachment = await _context.Attachments.FindAsync(fileId);
                if (attachment == null)
                    throw new ArgumentException("Dosya bulunamadı");

                if (string.IsNullOrEmpty(attachment.Url))
                    throw new InvalidOperationException("Dosya URL'si bulunamadı");

                var fileStream = await _storageService.DownloadFileAsync(attachment.Url);
                return await _storageService.ScanFileForVirusesAsync(fileStream);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Virüs tarama hatası: {FileId}", fileId);
                throw;
            }
        }

        public async Task<List<Attachment>> GetFileVersionsAsync(string fileId)
        {
            try
            {
                return await _context.Attachments
                    .Where(a => a.Metadata.ContainsKey("VersionOf") && 
                           a.Metadata["VersionOf"] == fileId)
                    .OrderByDescending(a => DateTime.Parse(a.Metadata["VersionCreatedAt"]))
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya versiyonlarını getirme hatası: {FileId}", fileId);
                throw;
            }
        }

        public async Task<Attachment> RestoreFileVersionAsync(string fileId, string versionId)
        {
            try
            {
                var version = await _context.Attachments.FindAsync(versionId);
                if (version == null || !version.Metadata.ContainsKey("VersionOf") || 
                    version.Metadata["VersionOf"] != fileId)
                    throw new ArgumentException("Geçersiz versiyon");

                if (string.IsNullOrEmpty(version.Url))
                    throw new InvalidOperationException("Versiyon dosyası URL'si bulunamadı");

                var original = await _context.Attachments.FindAsync(fileId);
                if (original == null)
                    throw new ArgumentException("Orijinal dosya bulunamadı");

                // Versiyonu yeni bir dosya olarak kopyala
                var restoredAttachment = new Attachment
                {
                    MessageId = original.MessageId,
                    Message = original.Message,
                    FileName = $"restored_{original.FileName}",
                    FileType = original.FileType,
                    FileSize = version.FileSize,
                    UploadedBy = original.UploadedBy,
                    MimeType = original.MimeType,
                    Metadata = new Dictionary<string, string>
                    {
                        { "RestoredFromVersion", versionId },
                        { "RestoredAt", DateTime.UtcNow.ToString() }
                    }
                };

                var fileStream = await _storageService.DownloadFileAsync(version.Url);
                restoredAttachment.Url = await _storageService.UploadFileAsync(fileStream, restoredAttachment.FileName);

                _context.Attachments.Add(restoredAttachment);
                await _context.SaveChangesAsync();

                return restoredAttachment;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya versiyonu geri yükleme hatası: {FileId}, {VersionId}", fileId, versionId);
                throw;
            }
        }

        public async Task<bool> RevokeShareableLinkAsync(string fileId)
        {
            try
            {
                var attachment = await _context.Attachments.FindAsync(fileId);
                if (attachment == null)
                    throw new ArgumentException("Dosya bulunamadı");

                if (attachment.Metadata.ContainsKey("ShareToken"))
                {
                    attachment.Metadata.Remove("ShareToken");
                    attachment.Metadata.Remove("ShareExpiresAt");
                    await _context.SaveChangesAsync();
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Paylaşım linki iptal hatası: {FileId}", fileId);
                throw;
            }
        }

        public async Task<Dictionary<string, int>> GetFileTypeDistributionAsync(string userId)
        {
            try
            {
                var attachments = await _context.Attachments
                    .Where(a => a.UploadedBy == userId && !a.IsDeleted)
                    .ToListAsync();

                return attachments
                    .GroupBy(a => a.FileType)
                    .ToDictionary(g => g.Key, g => g.Count());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya tipi dağılımı hesaplama hatası: {UserId}", userId);
                throw;
            }
        }

        public async Task<List<Attachment>> SearchFilesAsync(string userId, string searchTerm, string? fileType = null)
        {
            try
            {
                var query = _context.Attachments
                    .Where(a => a.UploadedBy == userId && !a.IsDeleted);

                if (!string.IsNullOrEmpty(fileType))
                {
                    query = query.Where(a => a.FileType == fileType);
                }

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    query = query.Where(a => 
                        a.FileName.Contains(searchTerm) || 
                        (a.Metadata.ContainsKey("Tags") && a.Metadata["Tags"].Contains(searchTerm)));
                }

                return await query.ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya arama hatası: {UserId}, {SearchTerm}", userId, searchTerm);
                throw;
            }
        }

        public async Task<bool> RemoveFileTagAsync(string fileId, string tag)
        {
            try
            {
                var attachment = await _context.Attachments.FindAsync(fileId);
                if (attachment == null)
                    throw new ArgumentException("Dosya bulunamadı");

                if (attachment.Metadata.ContainsKey("Tags"))
                {
                    var tags = attachment.Metadata["Tags"].Split(',').ToList();
                    if (tags.Remove(tag))
                    {
                        attachment.Metadata["Tags"] = string.Join(",", tags);
                        await _context.SaveChangesAsync();
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya etiketi kaldırma hatası: {FileId}", fileId);
                throw;
            }
        }

        public async Task<bool> BackupFileAsync(string fileId)
        {
            try
            {
                var attachment = await _context.Attachments.FindAsync(fileId);
                if (attachment == null)
                    throw new ArgumentException("Dosya bulunamadı");

                if (string.IsNullOrEmpty(attachment.Url))
                    throw new InvalidOperationException("Dosya URL'si bulunamadı");

                var fileStream = await _storageService.DownloadFileAsync(attachment.Url);
                var backupUrl = await _storageService.CreateBackupAsync(fileStream, attachment.FileName);

                attachment.Metadata["BackupUrl"] = backupUrl;
                attachment.Metadata["BackupCreatedAt"] = DateTime.UtcNow.ToString();
                
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya yedekleme hatası: {FileId}", fileId);
                throw;
            }
        }

        public async Task<bool> RestoreFileFromBackupAsync(string fileId)
        {
            try
            {
                var attachment = await _context.Attachments.FindAsync(fileId);
                if (attachment == null)
                    throw new ArgumentException("Dosya bulunamadı");

                if (!attachment.Metadata.ContainsKey("BackupUrl"))
                    throw new InvalidOperationException("Dosya için yedek bulunamadı");

                var backupStream = await _storageService.DownloadFileAsync(attachment.Metadata["BackupUrl"]);
                attachment.Url = await _storageService.UploadFileAsync(backupStream, attachment.FileName);
                
                attachment.Metadata["RestoredFromBackupAt"] = DateTime.UtcNow.ToString();
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya yedekten geri yükleme hatası: {FileId}", fileId);
                throw;
            }
        }

        public async Task<string> UploadFileAsync(Stream fileStream, string fileName)
        {
            if (fileStream == null || string.IsNullOrEmpty(fileName))
                throw new ArgumentException("File stream and file name are required");

            var fileType = Path.GetExtension(fileName).ToLower();
            var fileSize = fileStream.Length;

            // Validate file type
            var allowedTypes = new[] { ".jpg", ".jpeg", ".png", ".gif", ".pdf", ".doc", ".docx", ".txt" };
            if (!allowedTypes.Contains(fileType))
                throw new ArgumentException("Invalid file type");

            // Validate file size (10MB)
            const long maxSize = 10 * 1024 * 1024;
            if (fileSize > maxSize)
                throw new ArgumentException("File size exceeds limit");

            var uniqueFileName = $"{Guid.NewGuid()}_{fileName}";
            var fileUrl = await _storageService.UploadFileAsync(fileStream, uniqueFileName);

            return fileUrl;
        }

        public async Task DeleteFileAsync(string fileUrl)
        {
            if (string.IsNullOrEmpty(fileUrl))
                throw new ArgumentException("File URL is required");

            await _storageService.DeleteFileAsync(fileUrl);
        }

        public async Task<Attachment> UploadAttachmentAsync(Stream fileStream, string fileName, string userId, string messageId)
        {
            if (fileStream == null || string.IsNullOrEmpty(fileName))
                throw new ArgumentException("File stream and file name are required");

            var fileType = Path.GetExtension(fileName).ToLower();
            var fileSize = fileStream.Length;

            // Validate file type
            var allowedTypes = new[] { ".jpg", ".jpeg", ".png", ".gif", ".pdf", ".doc", ".docx", ".txt" };
            if (!allowedTypes.Contains(fileType))
                throw new ArgumentException("Invalid file type");

            // Validate file size (10MB)
            const long maxSize = 10 * 1024 * 1024;
            if (fileSize > maxSize)
                throw new ArgumentException("File size exceeds limit");

            var attachment = new Attachment
            {
                MessageId = messageId,
                Message = await _context.Messages.FindAsync(messageId) ?? throw new ArgumentException("Message not found"),
                FileName = fileName,
                FileType = fileType,
                FileSize = fileSize,
                UploadedBy = userId,
                MimeType = GetMimeType(fileName)
            };

            var uniqueFileName = $"{Guid.NewGuid()}_{fileName}";
            attachment.Url = await _storageService.UploadFileAsync(fileStream, uniqueFileName);

            _context.Attachments.Add(attachment);
            await _context.SaveChangesAsync();

            return attachment;
        }

        public async Task DeleteAttachmentAsync(Attachment attachment)
        {
            if (attachment.IsDeleted) return;

            if (!string.IsNullOrEmpty(attachment.Url))
            {
                await _storageService.DeleteFileAsync(attachment.Url);
            }

            if (!string.IsNullOrEmpty(attachment.ThumbnailUrl))
            {
                await _storageService.DeleteFileAsync(attachment.ThumbnailUrl);
            }

            attachment.Delete(attachment.UploadedBy ?? throw new InvalidOperationException("UploadedBy cannot be null"));
            await _context.SaveChangesAsync();
        }

        private string GetMimeType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLower();
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
    }
} 