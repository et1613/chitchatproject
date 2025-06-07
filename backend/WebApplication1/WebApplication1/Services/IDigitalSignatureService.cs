using System;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace WebApplication1.Services
{
    public interface IDigitalSignatureService
    {
        Task<DigitalSignature> SignDataAsync(string data, string certificateId, string? signatureAlgorithm = null, string? hashAlgorithm = null);
        Task<bool> VerifySignatureAsync(string data, string signature, string certificateId);
        Task<Certificate> CreateCertificateAsync(string subjectName, DateTime validFrom, DateTime validTo, int keySize = 2048, string? password = null);
        Task<IEnumerable<Certificate>> GetUserCertificatesAsync(string userId);
        Task<Certificate?> GetCertificateAsync(string certificateId);
        Task<bool> RevokeCertificateAsync(string certificateId, string userId);
        Task<byte[]> ExportCertificateAsync(string certificateId, string format, bool includePrivateKey, string? password = null);
        Task<Certificate> ImportCertificateAsync(string certificateData, string? password, string format);
        Task<Certificate> RenewCertificateAsync(string certificateId, DateTime validTo, string? password = null);
        Task<CertificateStatus> GetCertificateStatusAsync(string certificateId);
    }

    public class DigitalSignature
    {
        public required string Signature { get; set; }
        public required string CertificateId { get; set; }
        public required DateTime SignedAt { get; set; }
        public required string Algorithm { get; set; }
    }

    public class Certificate
    {
        public required string Id { get; set; }
        public required string SubjectName { get; set; }
        public required string IssuerName { get; set; }
        public required DateTime ValidFrom { get; set; }
        public required DateTime ValidTo { get; set; }
        public required string SerialNumber { get; set; }
        public required string Thumbprint { get; set; }
        public required bool HasPrivateKey { get; set; }
        public required CertificateStatus Status { get; set; }
        public string? UserId { get; set; }
    }

    public enum CertificateStatus
    {
        Valid,
        Expired,
        Revoked,
        Invalid
    }
} 