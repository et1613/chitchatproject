using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using System.IO;
using System.Threading;
using System.Linq.Expressions;
using System.IO.Compression;
using WebApplication1.Models.Messages;

namespace WebApplication1.Services
{
    public interface IStorageService
    {
        // Temel dosya işlemleri
        Task<string> UploadFileAsync(IFormFile file);
        Task<string> UploadFileAsync(Stream fileStream, string fileName);
        Task<bool> DeleteFileAsync(string fileUrl, bool permanent = false);
        Task<bool> FileExistsAsync(string fileUrl);
        Task<long> GetFileSizeAsync(string fileUrl);
        Task<string> GetFileMimeTypeAsync(string fileUrl);
        Task<byte[]> GetFileBytesAsync(string fileUrl);
        Task<Stream> DownloadFileAsync(string fileUrl);
        Task<string> GetFileHashAsync(string fileUrl);
        Task<bool> ValidateFileAsync(string fileUrl);

        // Thumbnail işlemleri
        Task<string> GenerateThumbnailAsync(IFormFile file);
        Task<string> GenerateThumbnailAsync(string fileUrl);
        Task<string> GenerateThumbnailAsync(Stream fileStream, string fileName);

        // Dosya optimizasyonu
        Task<string> CompressFileAsync(string fileUrl, int quality = 80);
        Task<string> OptimizeImageAsync(string fileUrl, int maxWidth = 1920, int maxHeight = 1080);
        Task<string> ConvertFileFormatAsync(string fileUrl, string targetFormat);
        Task<string> ResizeImageAsync(string fileUrl, int width, int height, bool maintainAspectRatio = true);
        Task<string> CropImageAsync(string fileUrl, int x, int y, int width, int height);
        Task<string> RotateImageAsync(string fileUrl, float angle);
        Task<string> ApplyWatermarkAsync(string fileUrl, string watermarkText, float opacity = 0.5f);

        // Dosya şifreleme/şifre çözme
        Task<string> EncryptFileAsync(string fileUrl, string encryptionKey);
        Task<string> DecryptFileAsync(string fileUrl, string encryptionKey);
        Task<bool> IsFileEncryptedAsync(string fileUrl);
        Task<string> GetEncryptionKeyAsync(string fileUrl);

        // Dosya sıkıştırma
        Task<string> CompressFileAsync(string fileUrl, CompressionLevel level = CompressionLevel.Optimal);
        Task<string> DecompressFileAsync(string fileUrl);
        Task<bool> IsFileCompressedAsync(string fileUrl);
        Task<long> GetCompressedSizeAsync(string fileUrl);

        // Dosya versiyonlama
        Task<string> CreateFileVersionAsync(string fileUrl);
        Task<List<string>> GetFileVersionsAsync(string fileUrl);
        Task<string> RestoreFileVersionAsync(string fileUrl, string versionId);
        Task<bool> DeleteFileVersionAsync(string fileUrl, string versionId);
        Task<Dictionary<string, object>> GetVersionMetadataAsync(string fileUrl, string versionId);

        // Dosya önbelleğe alma
        Task<bool> CacheFileAsync(string fileUrl, TimeSpan? expiration = null);
        Task<bool> RemoveFromCacheAsync(string fileUrl);
        Task<bool> IsFileCachedAsync(string fileUrl);
        Task<DateTime?> GetCacheExpirationAsync(string fileUrl);
        Task<bool> RefreshCacheAsync(string fileUrl);

        // Dosya indirme ve ilerleme takibi
        Task<Stream> DownloadFileWithProgressAsync(string fileUrl, IProgress<long> progress, CancellationToken cancellationToken = default);
        Task<string> DownloadFileToPathAsync(string fileUrl, string targetPath, IProgress<long>? progress = null, CancellationToken cancellationToken = default);
        Task<long> GetDownloadedBytesAsync(string fileUrl);
        Task<bool> IsDownloadCompleteAsync(string fileUrl);
        Task CancelDownloadAsync(string fileUrl);

        // Dosya kopyalama ve taşıma
        Task<string> CopyFileAsync(string sourceUrl, string targetPath);
        Task<bool> MoveFileAsync(string sourceUrl, string targetUrl);
        Task<bool> RenameFileAsync(string fileUrl, string newName);
        Task<bool> CreateDirectoryAsync(string path);
        Task<bool> DeleteDirectoryAsync(string path);
        Task<List<string>> ListDirectoryAsync(string path);
        Task<bool> DirectoryExistsAsync(string path);

        // Dosya metadata
        Task<Dictionary<string, string>> GetFileMetadataAsync(string fileUrl);
        Task<bool> UpdateFileMetadataAsync(string fileUrl, Dictionary<string, string> metadata);
        Task<bool> RemoveFileMetadataAsync(string fileUrl, string key);
        Task<bool> ClearFileMetadataAsync(string fileUrl);
        Task<bool> HasMetadataAsync(string fileUrl, string key);

        // Dosya izinleri ve erişim kontrolü
        Task<bool> SetFilePermissionsAsync(string fileUrl, Dictionary<string, string> permissions);
        Task<Dictionary<string, string>> GetFilePermissionsAsync(string fileUrl);
        Task<bool> CheckFileAccessAsync(string fileUrl, string userId);
        Task<bool> GrantFileAccessAsync(string fileUrl, string userId, string permission);
        Task<bool> RevokeFileAccessAsync(string fileUrl, string userId);
        Task<List<string>> GetFileAccessListAsync(string fileUrl);

        // Dosya önizleme
        Task<string> GenerateDocumentPreviewAsync(string fileUrl);
        Task<string> GenerateVideoPreviewAsync(string fileUrl);
        Task<string> GenerateAudioPreviewAsync(string fileUrl);
        Task<string> GenerateImagePreviewAsync(string fileUrl, int maxWidth = 800, int maxHeight = 600);
        Task<Dictionary<string, string>> GeneratePreviewsAsync(List<string> fileUrls);

        // Dosya analizi
        Task<Dictionary<string, object>> AnalyzeFileAsync(string fileUrl);
        Task<bool> IsFileVirusFreeAsync(string fileUrl);
        Task<Dictionary<string, object>> GetFileStatisticsAsync(string fileUrl);
        Task<bool> ValidateFileIntegrityAsync(string fileUrl);
        Task<Dictionary<string, object>> GetFileContentAnalysisAsync(string fileUrl);

        // Dosya yedekleme
        Task<string> CreateBackupAsync(Stream fileStream, string fileName);
        Task<string> BackupFileAsync(string fileUrl);
        Task<bool> RestoreFileFromBackupAsync(string backupUrl, string targetUrl);
        Task<List<string>> GetFileBackupsAsync(string fileUrl);
        Task<bool> DeleteFileBackupAsync(string backupUrl);
        Task<Dictionary<string, object>> GetBackupMetadataAsync(string backupUrl);

        // Dosya paylaşımı
        Task<string> GenerateShareableLinkAsync(string fileUrl, TimeSpan? expiration = null);
        Task<bool> RevokeShareableLinkAsync(string fileUrl);
        Task<bool> IsShareableLinkValidAsync(string fileUrl);
        Task<DateTime?> GetShareableLinkExpirationAsync(string fileUrl);
        Task<bool> ExtendShareableLinkAsync(string fileUrl, TimeSpan extension);

        // Toplu işlemler
        Task<Dictionary<string, string>> UploadMultipleFilesAsync(List<IFormFile> files);
        Task<bool> DeleteMultipleFilesAsync(List<string> fileUrls);
        Task<Dictionary<string, bool>> ValidateMultipleFilesAsync(List<string> fileUrls);
        Task<Dictionary<string, string>> GenerateMultipleThumbnailsAsync(List<string> fileUrls);
        Task<Dictionary<string, Dictionary<string, object>>> AnalyzeMultipleFilesAsync(List<string> fileUrls);

        // Dosya sıralama ve filtreleme
        Task<List<string>> SortFilesAsync(string directory, string sortBy = "Name", bool ascending = true);
        Task<List<string>> FilterFilesAsync(string directory, Dictionary<string, string> filters);
        Task<List<string>> SearchFilesAsync(string directory, string searchTerm, bool recursive = false);
        Task<List<string>> GetFilesByDateRangeAsync(string directory, DateTime startDate, DateTime endDate);
        Task<List<string>> GetFilesBySizeRangeAsync(string directory, long minSize, long maxSize);
        Task<List<string>> GetFilesByTypeAsync(string directory, string fileType);
        Task<List<string>> GetFilesByTagsAsync(string directory, List<string> tags);
        Task<List<string>> GetFilesByMetadataAsync(string directory, Dictionary<string, string> metadata);

        // Dosya arama ve indeksleme
        Task<bool> CreateSearchIndexAsync(string directory);
        Task<bool> UpdateSearchIndexAsync(string directory);
        Task<bool> DeleteSearchIndexAsync(string directory);
        Task<List<string>> SearchInIndexAsync(string searchTerm, string? directory = null);
        Task<Dictionary<string, double>> GetSearchSuggestionsAsync(string partialTerm);
        Task<bool> IsIndexUpToDateAsync(string directory);
        Task<DateTime> GetLastIndexUpdateAsync(string directory);
        Task<Dictionary<string, int>> GetSearchStatisticsAsync(string directory);

        // Dosya istatistikleri ve raporlama
        Task<Dictionary<string, long>> GetStorageStatisticsAsync(string? directory = null);
        Task<Dictionary<string, int>> GetFileTypeDistributionAsync(string? directory = null);
        Task<Dictionary<string, long>> GetStorageUsageByUserAsync(string? directory = null);
        Task<Dictionary<string, int>> GetFileActivityStatsAsync(string? directory = null, TimeSpan? period = null);
        Task<Dictionary<string, object>> GenerateStorageReportAsync(string? directory = null, DateTime? startDate = null, DateTime? endDate = null);
        Task<Dictionary<string, object>> GetUserStorageQuotaAsync(string userId);
        Task<bool> IsStorageQuotaExceededAsync(string userId);
        Task<Dictionary<string, object>> GetStorageTrendsAsync(string? directory = null, TimeSpan period = default);

        // Dosya dönüştürme ve format işlemleri
        Task<string> ConvertImageFormatAsync(string fileUrl, string targetFormat, int? quality = null);
        Task<string> ConvertVideoFormatAsync(string fileUrl, string targetFormat, Dictionary<string, string>? options = null);
        Task<string> ConvertAudioFormatAsync(string fileUrl, string targetFormat, Dictionary<string, string>? options = null);
        Task<string> ConvertDocumentFormatAsync(string fileUrl, string targetFormat, Dictionary<string, string>? options = null);
        Task<bool> IsFormatConversionSupportedAsync(string sourceFormat, string targetFormat);
        Task<Dictionary<string, List<string>>> GetSupportedFormatsAsync();
        Task<Dictionary<string, object>> GetFormatConversionOptionsAsync(string sourceFormat, string targetFormat);

        // Gelişmiş dosya sıkıştırma ve optimizasyon
        Task<string> OptimizeImageForWebAsync(string fileUrl, int maxWidth = 1920, int maxHeight = 1080, int quality = 80);
        Task<string> OptimizeVideoForWebAsync(string fileUrl, int maxWidth = 1920, int maxHeight = 1080, int bitrate = 2000);
        Task<string> OptimizeAudioForWebAsync(string fileUrl, int bitrate = 128);
        Task<string> OptimizeDocumentForWebAsync(string fileUrl, bool compressImages = true);
        Task<Dictionary<string, object>> GetOptimizationStatisticsAsync(string fileUrl);
        Task<bool> IsOptimizationNeededAsync(string fileUrl);
        Task<Dictionary<string, long>> GetOptimizationSavingsAsync(string fileUrl);

        // Gelişmiş dosya güvenliği ve doğrulama
        Task<bool> ValidateFileSignatureAsync(string fileUrl);
        Task<bool> ValidateFileChecksumAsync(string fileUrl);
        Task<bool> ValidateFilePermissionsAsync(string fileUrl);
        Task<Dictionary<string, object>> GetFileSecurityInfoAsync(string fileUrl);
        Task<bool> IsFileSecureAsync(string fileUrl);
        Task<bool> IsFileCompliantAsync(string fileUrl, string complianceStandard);
        Task<Dictionary<string, bool>> GetComplianceStatusAsync(string fileUrl);

        // Dosya işlem geçmişi ve denetim
        Task<List<Dictionary<string, object>>> GetFileOperationHistoryAsync(string fileUrl);
        Task<Dictionary<string, object>> GetFileAuditTrailAsync(string fileUrl);
        Task<bool> LogFileOperationAsync(string fileUrl, string operation, string userId);
        Task<Dictionary<string, int>> GetOperationStatisticsAsync(string fileUrl);
        Task<List<Dictionary<string, object>>> GetRecentOperationsAsync(string fileUrl, int count = 10);
        Task<bool> ExportAuditLogAsync(string fileUrl, string format = "json");

        // Dosya senkronizasyon ve yedekleme
        Task<bool> SyncFileAsync(string sourceUrl, string targetUrl);
        Task<bool> IsFileSyncedAsync(string fileUrl);
        Task<DateTime?> GetLastSyncTimeAsync(string fileUrl);
        Task<bool> ScheduleBackupAsync(string fileUrl, TimeSpan interval);
        Task<bool> CancelScheduledBackupAsync(string fileUrl);
        Task<Dictionary<string, object>> GetBackupScheduleAsync(string fileUrl);
        Task<bool> IsBackupScheduledAsync(string fileUrl);

        // Dosya performans ve izleme
        Task<Dictionary<string, object>> GetFilePerformanceMetricsAsync(string fileUrl);
        Task<Dictionary<string, object>> GetStoragePerformanceMetricsAsync(string? directory = null);
        Task<bool> MonitorFileAccessAsync(string fileUrl);
        Task<Dictionary<string, object>> GetAccessMetricsAsync(string fileUrl);
        Task<bool> IsPerformanceOptimalAsync(string fileUrl);
        Task<Dictionary<string, object>> GetPerformanceRecommendationsAsync(string fileUrl);

        // Dosya etiketleme ve kategorizasyon
        Task<bool> AddFileTagAsync(string fileUrl, string tag);
        Task<bool> RemoveFileTagAsync(string fileUrl, string tag);
        Task<List<string>> GetFileTagsAsync(string fileUrl);
        Task<bool> CategorizeFileAsync(string fileUrl, string category);
        Task<string> GetFileCategoryAsync(string fileUrl);
        Task<Dictionary<string, List<string>>> GetCategoryDistributionAsync(string? directory = null);
        Task<bool> AutoCategorizeFileAsync(string fileUrl);

        // Dosya işbirliği ve paylaşım
        Task<bool> ShareFileWithUserAsync(string fileUrl, string userId, string permission);
        Task<bool> ShareFileWithGroupAsync(string fileUrl, string groupId, string permission);
        Task<List<string>> GetSharedWithUsersAsync(string fileUrl);
        Task<List<string>> GetSharedWithGroupsAsync(string fileUrl);
        Task<bool> RevokeAllSharesAsync(string fileUrl);
        Task<Dictionary<string, string>> GetSharePermissionsAsync(string fileUrl);
        Task<bool> IsFileSharedAsync(string fileUrl);

        // Yeni eklenen metod
        Task<bool> ScanFileForVirusesAsync(Stream fileStream);

        Task<string> SaveFileAsync(byte[] fileData, string fileName, string contentType);
        Task<byte[]> GetFileAsync(string filePath);
        Task DeleteFileAsync(string filePath);
        Task<string> SaveMessagesAsync(List<Message> messages);
        Task<List<Message>> LoadMessagesAsync(string backupPath);
        Task DeleteMessagesAsync(string backupPath);
        Task<byte[]> ExportMessagesAsync(List<Message> messages, ExportFormat format);
        Task<List<Message>> ImportMessagesAsync(byte[] data, ImportFormat format);
        Task<bool> ValidateImportDataAsync(byte[] data, ImportFormat format);
        Task<ImportProgress> GetImportProgressAsync(string importId);
        Task<ExportProgress> GetExportProgressAsync(string exportId);
    }
} 