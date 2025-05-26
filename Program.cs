
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json;
using System.Net.Mail;
using System.Net;
using System.Linq;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddCors();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

app.UseCors(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
app.UseStaticFiles();
app.UseRouting();
app.MapControllers();

List<User> users = new();
List<Message> messages = new();

var config = app.Configuration;
string emailUser = config["EMAIL_USER"];
string emailPass = config["EMAIL_PASS"];
string backendUrl = config["BACKEND_URL"] ?? "http://localhost:5263";
if (string.IsNullOrWhiteSpace(emailUser) || string.IsNullOrWhiteSpace(emailPass))
    throw new Exception("EMAIL_USER veya EMAIL_PASS eksik. Lütfen appsettings'de tanımlayın.");


string jwtLoginSecret = config["JWT_LOGIN_SECRET"] ?? "default-login-key";
string jwtEmailSecret = config["JWT_EMAIL_SECRET"] ?? "default-email-key";


// 🔐 Register
app.MapPost("/api/register", async (HttpRequest req) => {
    try
    {
        var body = await JsonSerializer.DeserializeAsync<UserRegisterModel>(req.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (body == null) return Results.BadRequest(new { message = "Veri alınamadı." });

        if (users.Any(u => u.Email == body.Email))
            return Results.BadRequest(new { message = "Bu e-posta zaten kayıtlı." });

        var hashed = BCrypt.Net.BCrypt.HashPassword(body.Password);
        var token = Utils.CreateJwtToken(body.Email, jwtEmailSecret, TimeSpan.FromHours(1));

        users.Add(new User
        {
            Email = body.Email,
            Password = hashed,
            Name = body.Name,
            Gender = body.Gender,
            Avatar = body.Avatar,
            IsVerified = false
        });

        Utils.SendVerificationEmail(body.Email, token, backendUrl, emailUser, emailPass);

        return Results.Ok(new { message = "Kayıt başarılı. Lütfen e-posta adresinizi doğrulayın." });
    }
    catch (Exception ex)
    {
        Console.WriteLine("❌ Register endpoint hatası: " + ex.Message);
        return Results.Problem("Sunucu hatası: " + ex.Message); // bunu ekle
    }
});

// 🔐 Login
app.MapPost("/api/login", async (HttpRequest req) => {
    var body = await JsonSerializer.DeserializeAsync<UserLoginModel>(req.Body);
    if (body == null) return Results.BadRequest();

    var user = users.FirstOrDefault(u => u.Email == body.Email);
    if (user == null) return Results.Json(new { message = "Kullanıcı bulunamadı." }, statusCode: 401);
    if (!user.IsVerified) return Results.Json(new { message = "E-posta doğrulanmamış." }, statusCode: 403);

    bool isValid = BCrypt.Net.BCrypt.Verify(body.Password, user.Password);
    if (!isValid) return Results.Json(new { message = "Şifre yanlış." }, statusCode: 401);

    var token = Utils.CreateJwtToken(user.Email, jwtLoginSecret, TimeSpan.FromHours(2));

    return Results.Ok(new
    {
        token,
        user = new { user.Name, user.Gender, user.Email }
    });
});

// 🔐 Token Doğrulama
app.MapGet("/api/me", (HttpRequest req) => {
    var token = req.Headers["Authorization"].ToString()?.Split(" ").Last();
    if (token == null) return Results.Unauthorized();

    try
    {
        var handler = new JwtSecurityTokenHandler();
        var principal = handler.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtLoginSecret)),
            ValidateIssuer = false,
            ValidateAudience = false
        }, out _);

        var email = principal.FindFirstValue(ClaimTypes.Name);
        return Results.Ok(new { email });
    }
    catch
    {
        return Results.StatusCode(403);
    }
});

// 🔐 E-posta Doğrulama
app.MapGet("/api/verify", (HttpRequest req) => {
    var token = req.Query["token"];
    var handler = new JwtSecurityTokenHandler();
    try
    {
        var principal = handler.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtEmailSecret)),
            ValidateIssuer = false,
            ValidateAudience = false
        }, out _);

        var email = principal.FindFirstValue(ClaimTypes.Name);
        var user = users.FirstOrDefault(u => u.Email == email);
        if (user == null) return Results.BadRequest(new { message = "Kullanıcı bulunamadı." });
        user.IsVerified = true;

        return Results.Content($"<h2 style='color: green;'>E-posta doğrulandı!</h2><p><a href='{backendUrl}/index.html'>Giriş yap</a></p>", "text/html");
    }
    catch
    {
        return Results.BadRequest(new { message = "Token geçersiz" });
    }
});

// 💬 Tüm kullanıcılar
app.MapGet("/api/users", () => Results.Ok(users.Select(u => new {
    u.Name,
    u.Gender,
    u.Email,
    u.Avatar
})));

// 📩 Mesaj gönderme
app.MapPost("/api/messages", async (HttpRequest req) => {
    var body = await JsonSerializer.DeserializeAsync<Message>(req.Body);
    if (body == null || string.IsNullOrEmpty(body.Sender) || string.IsNullOrEmpty(body.Receiver))
        return Results.BadRequest(new { message = "Geçersiz mesaj isteği." });

    body.Timestamp ??= DateTime.Now.ToString();
    messages.Add(body);
    return Results.Created("", new { message = "Mesaj kaydedildi" });
});

// 📩 Mesajları al
app.MapGet("/api/messages", (string user1, string user2) => {
    if (string.IsNullOrEmpty(user1) || string.IsNullOrEmpty(user2))
        return Results.BadRequest(new { message = "Kullanıcı bilgileri eksik." });

    var result = messages.Where(m =>
        (m.Sender == user1 && m.Receiver == user2) ||
        (m.Sender == user2 && m.Receiver == user1)).ToList();

    return Results.Ok(result);
});

// ✅ Test endpoint
app.MapGet("/", () => "ChitChat API çalışıyor (.NET versiyonu)");

app.Run();

// MODELLER
record User
{
    public string Email { get; set; }
    public string Password { get; set; }
    public string Name { get; set; }
    public string Gender { get; set; }
    public string Avatar { get; set; }
    public bool IsVerified { get; set; }
}

record UserRegisterModel(string Email, string Password, string Name, string Gender, string Avatar);
record UserLoginModel(string Email, string Password);
record Message
{
    public string Sender { get; set; }
    public string Receiver { get; set; }
    public string Text { get; set; }
    public string Timestamp { get; set; }
    public string Gender { get; set; }
    public string Avatar { get; set; }
    public string FileData { get; set; }
    public string FileName { get; set; }
    public string FileType { get; set; }
}

public static class Utils
{
    public static string CreateJwtToken(string email, string secret, TimeSpan expiresIn)
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

    public static void SendVerificationEmail(string to, string token, string backendUrl, string emailUser, string emailPass)
    {
        var verifyLink = $"{backendUrl}/api/verify?token={token}";

        try
        {
            var client = new SmtpClient("smtp.gmail.com")
            {
                Port = 587,
                Credentials = new NetworkCredential(emailUser, emailPass),
                EnableSsl = true
            };

            var mail = new MailMessage(emailUser, to)
            {
                Subject = "E-posta Doğrulama",
                Body = $"""
            <p>Merhaba,</p>
            <p>
                Hesabınızı doğrulamak için <a href="{verifyLink}">tıklayın</a>.
            </p>
            <p>Link: {verifyLink}</p>
            """,
                IsBodyHtml = true
            };

            client.Send(mail);
            Console.WriteLine($"✅ Doğrulama maili gönderildi: {verifyLink}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Mail gönderilemedi: Alıcı={to}, Hata={ex.Message}");
        }
    }
}

