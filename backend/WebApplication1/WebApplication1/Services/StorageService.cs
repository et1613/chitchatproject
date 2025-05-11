using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace WebApplication1.Services
{
    public interface IStorageService
    {
        Task<string> UploadFileAsync(Stream fileStream, string fileName);
        Task DeleteFileAsync(string fileUrl);
        Task<Stream> DownloadFileAsync(string fileUrl);
        Task<bool> FileExistsAsync(string fileUrl);
    }

    public class StorageService : IStorageService
    {
        private readonly string _uploadDirectory;

        public StorageService(IConfiguration configuration)
        {
            _uploadDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
            if (!Directory.Exists(_uploadDirectory))
            {
                Directory.CreateDirectory(_uploadDirectory);
            }
        }

        public async Task<string> UploadFileAsync(Stream fileStream, string fileName)
        {
            try
            {
                var uniqueFileName = $"{Guid.NewGuid()}_{fileName}";
                var filePath = Path.Combine(_uploadDirectory, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await fileStream.CopyToAsync(stream);
                }

                return $"/uploads/{uniqueFileName}";
            }
            catch (Exception ex)
            {
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

                if (File.Exists(filePath))
                {
                    await Task.Run(() => File.Delete(filePath));
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Dosya silinirken hata oluştu: {ex.Message}", ex);
            }
        }

        public async Task<Stream> DownloadFileAsync(string fileUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(fileUrl))
                    throw new ArgumentException("Dosya URL'i boş olamaz");

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
                return memoryStream;
            }
            catch (Exception ex)
            {
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
            catch
            {
                return Task.FromResult(false);
            }
        }
    }
} 