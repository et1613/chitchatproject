namespace WebApplication1.Services
{
    public class DigitalSignatureService
    {
        public string SignMessage(string content, string privateKey) { return "Signature"; }
        public bool VerifySignature(string content, string signature, string publicKey) { return true; }
    }
} 