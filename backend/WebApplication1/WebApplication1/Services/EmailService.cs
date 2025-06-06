using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Mail;
using System.Net;
using System.Text;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;

namespace WebApplication1.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string to, string subject, string body, bool isHtml = true);
        Task SendWelcomeEmailAsync(string to, string username);
        Task SendPasswordResetEmailAsync(string to, string resetLink);
        Task SendEmailVerificationAsync(string to, string verificationLink);
        Task SendPasswordChangeNotificationAsync(string to, string username);
        Task SendFriendRequestNotificationAsync(string to, string fromUsername);
        Task SendAccountLockedNotificationAsync(string to);
        Task SendAccountUnlockedNotificationAsync(string to);
        string CreateJwtToken(string email, string secret, TimeSpan expiresIn);
        ClaimsPrincipal ValidateToken(string token, string secret);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;
        private readonly SmtpClient _smtpClient;
        private readonly string _fromEmail;
        private readonly string _fromName;
        private readonly string _backendUrl;
        private readonly string _jwtEmailSecret;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            
            try
            {
                // Email ayarlarını yapılandır
                _fromEmail = "chitchatdestek@gmail.com";
                _fromName = "ChitChat Destek";
                _backendUrl = _configuration["BackendUrl"] ?? "http://localhost:5263";
                _jwtEmailSecret = _configuration["JWT_EMAIL_SECRET"] ?? "default-email-key";

                // Gmail SMTP ayarları
                _smtpClient = new SmtpClient("smtp.gmail.com")
                {
                    Port = 587,
                    Credentials = new NetworkCredential(_fromEmail, "dwhb cmgh bryw zfno"),
                    EnableSsl = true,
                    Timeout = 10000 // 10 saniye timeout
                };

                _logger.LogInformation("EmailService başarıyla yapılandırıldı");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EmailService yapılandırması sırasında hata oluştu");
                throw;
            }
        }

        public string CreateJwtToken(string email, string secret, TimeSpan expiresIn)
        {
            var claims = new[] {
                new Claim(ClaimTypes.Name, email)
            };
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.UtcNow.Add(expiresIn),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public ClaimsPrincipal ValidateToken(string token, string secret)
        {
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                ValidateIssuer = false,
                ValidateAudience = false
            }, out _);

            return principal;
        }

        public async Task SendEmailAsync(string to, string subject, string body, bool isHtml = true)
        {
            try
            {
                _logger.LogInformation("Email gönderiliyor: {To}, {Subject}", to, subject);

                using var message = new MailMessage
                {
                    From = new MailAddress(_fromEmail, _fromName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = isHtml
                };
                message.To.Add(to);

                await _smtpClient.SendMailAsync(message);
                _logger.LogInformation("Email başarıyla gönderildi: {To}", to);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Email gönderimi sırasında hata oluştu: {To}, {Subject}", to, subject);
                throw new Exception($"Email gönderimi başarısız: {ex.Message}", ex);
            }
        }

        public async Task SendWelcomeEmailAsync(string to, string username)
        {
            try
            {
                var subject = "ChitChat'a Hoş Geldiniz!";
                var body = $@"
                    <h2>Merhaba {username},</h2>
                    <p>ChitChat'a hoş geldiniz! Hesabınız başarıyla oluşturuldu.</p>
                    <p>Hemen sohbetlere katılmaya başlayabilirsiniz.</p>
                    <p>İyi sohbetler!</p>
                    <p>ChitChat Ekibi</p>";

                await SendEmailAsync(to, subject, body);
                _logger.LogInformation("Hoş geldiniz emaili gönderildi: {To}, {Username}", to, username);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hoş geldiniz emaili gönderilirken hata oluştu: {To}, {Username}", to, username);
                throw;
            }
        }

        public async Task SendPasswordResetEmailAsync(string to, string resetLink)
        {
            try
            {
                var subject = "Şifre Sıfırlama";
                var body = $@"
                    <h2>Şifre Sıfırlama İsteği</h2>
                    <p>Şifrenizi sıfırlamak için aşağıdaki bağlantıya tıklayın:</p>
                    <p><a href='{resetLink}'>Şifremi Sıfırla</a></p>
                    <p>Bu bağlantı 1 saat süreyle geçerlidir.</p>
                    <p>Eğer bu isteği siz yapmadıysanız, bu emaili görmezden gelebilirsiniz.</p>";

                await SendEmailAsync(to, subject, body);
                _logger.LogInformation("Şifre sıfırlama emaili gönderildi: {To}", to);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Şifre sıfırlama emaili gönderilirken hata oluştu: {To}", to);
                throw;
            }
        }

        public async Task SendEmailVerificationAsync(string to, string verificationLink)
        {
            try
            {
                var subject = "Email Adresinizi Doğrulayın";
                var body = $@"
                    <h2>Email Doğrulama</h2>
                    <p>Email adresinizi doğrulamak için aşağıdaki bağlantıya tıklayın:</p>
                    <p><a href='{verificationLink}'>Email Adresimi Doğrula</a></p>
                    <p>Bu bağlantı 24 saat süreyle geçerlidir.</p>
                    <p>Doğrulama işlemi tamamlandıktan sonra <a href='{_backendUrl}/index.html'>giriş yapabilirsiniz</a>.</p>";

                await SendEmailAsync(to, subject, body);
                _logger.LogInformation("Email doğrulama emaili gönderildi: {To}", to);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Email doğrulama emaili gönderilirken hata oluştu: {To}", to);
                throw;
            }
        }

        public async Task SendPasswordChangeNotificationAsync(string to, string username)
        {
            try
            {
                var subject = "Şifre Değişikliği Bildirimi";
                var body = $@"
                    <h2>Şifre Değişikliği</h2>
                    <p>Merhaba {username},</p>
                    <p>Hesabınızın şifresi başarıyla değiştirildi.</p>
                    <p>Eğer bu değişikliği siz yapmadıysanız, lütfen hemen destek ekibimizle iletişime geçin.</p>
                    <p>İyi günler,<br>ChitChat Ekibi</p>";

                await SendEmailAsync(to, subject, body);
                _logger.LogInformation("Şifre değişikliği bildirimi gönderildi: {To}, {Username}", to, username);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Şifre değişikliği bildirimi gönderilirken hata oluştu: {To}, {Username}", to, username);
                throw;
            }
        }

        public async Task SendFriendRequestNotificationAsync(string to, string fromUsername)
        {
            try
            {
                var subject = "Yeni Arkadaşlık İsteği";
                var body = $@"
                    <h2>Yeni Arkadaşlık İsteği</h2>
                    <p>Merhaba,</p>
                    <p>{fromUsername} size bir arkadaşlık isteği gönderdi.</p>
                    <p>İsteği görüntülemek ve yanıtlamak için aşağıdaki bağlantıya tıklayın:</p>
                    <p><a href='{_backendUrl}/friends/requests'>Arkadaşlık İsteğini Görüntüle</a></p>
                    <p>İyi günler,<br>ChitChat Ekibi</p>";

                await SendEmailAsync(to, subject, body);
                _logger.LogInformation("Arkadaşlık isteği bildirimi gönderildi: {To}, {FromUsername}", to, fromUsername);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Arkadaşlık isteği bildirimi gönderilirken hata oluştu: {To}, {FromUsername}", to, fromUsername);
                throw;
            }
        }

        public async Task SendAccountLockedNotificationAsync(string to)
        {
            try
            {
                var subject = "Hesabınız Kilitlendi";
                var body = $@"
                    <h2>Hesabınız Geçici Olarak Kilitlendi</h2>
                    <p>Güvenlik nedeniyle hesabınız geçici olarak kilitlenmiştir.</p>
                    <p>Lütfen bir süre sonra tekrar giriş yapmayı deneyin veya destek ekibimizle iletişime geçin.</p>
                    <p>İyi günler,<br>ChitChat Destek Ekibi</p>";

                await SendEmailAsync(to, subject, body);
                _logger.LogInformation("Hesap kilitlenme bildirimi gönderildi: {To}", to);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hesap kilitlenme bildirimi gönderilirken hata oluştu: {To}", to);
                throw;
            }
        }

        public async Task SendAccountUnlockedNotificationAsync(string to)
        {
            try
            {
                var subject = "Hesabınızın Kilidi Açıldı";
                var body = $@"
                    <h2>Hesabınızın Kilidi Açıldı</h2>
                    <p>Hesabınızın kilidi kaldırılmıştır. Artık tekrar giriş yapabilirsiniz.</p>
                    <p>İyi günler,<br>ChitChat Destek Ekibi</p>";

                await SendEmailAsync(to, subject, body);
                _logger.LogInformation("Hesap kilidi açılma bildirimi gönderildi: {To}", to);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hesap kilidi açılma bildirimi gönderilirken hata oluştu: {To}", to);
                throw;
            }
        }
    }
} 