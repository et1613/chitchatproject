namespace WebApplication1.Services
{
    public class EncryptionService
    {
        public string EncryptMessage(string content, string publicKey) { return "EncryptedData"; }
        public string DecryptMessage(string encryptedContent, string privateKey) { return "DecryptedData"; }
    }
} 