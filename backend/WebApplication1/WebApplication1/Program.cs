using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net.WebSockets;
using WebApplication1.Services;
using WebApplication1.Repositories;
using WebApplication1.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using WebApplication1.Hubs;
using WebApplication1.Models.Users;
using WebApplication1.Models.Chat;
using WebApplication1.Models.Messages;
using WebApplication1.Models.Notifications;
using WebApplication1.Models.Enums;
using WebApplication1.Models;
using WebApplication1.Middleware;
using Microsoft.AspNetCore.Identity;
using Microsoft.OpenApi.Models;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(
    c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo { Title = "My API", Version = "v1" });

        c.CustomSchemaIds(type => type.FullName);
    });

// Add User Secrets
builder.Configuration.AddUserSecrets<Program>();

// Configure MySQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString), mysqlOptions =>
    {
        mysqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null);
        
        // Enable sensitive data logging in development
        if (builder.Environment.IsDevelopment())
        {
            options.EnableSensitiveDataLogging();
            options.EnableDetailedErrors();
        }
    });
});

// Configure SignalR
builder.Services.AddSignalR();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

// Add logging
builder.Services.AddLogging();

// Configure JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key is not configured");
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            RoleClaimType = ClaimTypes.Role
        };

        // Configure SignalR to use JWT
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/chatHub"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

// Add Repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

// Add Services
// builder.Services.AddDbContext<ApplicationDbContext>(options =>
//     options.UseMySql(
//         builder.Configuration.GetConnectionString("DefaultConnection"),
//         ServerVersion.AutoDetect(builder.Configuration.GetConnectionString("DefaultConnection"))
//     ));

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<HashingService>();
builder.Services.AddScoped<IEncryptionService, EncryptionService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IStorageService, StorageService>();
builder.Services.AddScoped<ISecurityService, SecurityService>();
builder.Services.AddScoped<IMessageService, MessageService>();
builder.Services.AddScoped<IDigitalSignatureService, DigitalSignatureService>();
builder.Services.AddScoped<DigitalSignatureService>();
builder.Services.AddScoped<ISignatureService>(sp => sp.GetRequiredService<DigitalSignatureService>());
builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<TokenStorageService>();
builder.Services.AddScoped<IConnectionManager, ConnectionManager>();
builder.Services.AddScoped<ConnectionManager>();

// Add Password Hasher
builder.Services.AddScoped<IPasswordHasher<WebApplication1.Models.Users.User>, PasswordHasher<WebApplication1.Models.Users.User>>();

// Add UserNotificationPreferencesService
builder.Services.AddScoped<UserNotificationPreferencesService>();

// Add Memory Cache
builder.Services.AddMemoryCache();

// Add Distributed Cache
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "ChitChat_";
});

// Add HTTP Context Accessor
builder.Services.AddHttpContextAccessor();

// Add HTTP Client Factory
builder.Services.AddHttpClient();

// Add Hosted Services
// builder.Services.AddHostedService<TokenCleanupService>();

// Add Singletons
builder.Services.AddSingleton<SignalRConnectionManager>();

// Add Redis connection string
builder.Configuration["Redis"] = "localhost:6379";

var seedAdmin = builder.Configuration.GetValue<bool>("Database:SeedAdmin", false);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
        Path.Combine(builder.Environment.ContentRootPath, "../../../frontend")),
    RequestPath = ""
});

app.UseRouting();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<ChatHub>("/chatHub");

// WebSocket middleware configuration
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromMinutes(2)
});

// WebSocket endpoint
app.Map("/ws", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        try
        {
            // Get user ID from query string or token
            var userId = context.Request.Query["userId"].ToString();
            if (string.IsNullOrEmpty(userId))
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("User ID is required");
                return;
            }

            // Validate user token if provided
            var token = context.Request.Query["token"].ToString();
            if (!string.IsNullOrEmpty(token))
            {
                // Token validation logic here
                // If invalid, return 401
            }

            var ipAddress = context.Connection.RemoteIpAddress?.ToString();
            using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            var chatService = context.RequestServices.GetRequiredService<IChatService>();
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            await chatService.HandleWebSocketConnection(webSocket, userId, ipAddress);
        }
        catch (Exception ex)
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "Error handling WebSocket connection");
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync("Internal server error");
        }
    }
    else
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsync("WebSocket connection required");
    }
});

// Uygulama başlatıldığında eksik migration'ları uygula (veritabanını silmez)
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    dbContext.Database.Migrate();

    if (seedAdmin && !dbContext.Users.Any(u => u.Role == UserRole.Admin || u.Role == UserRole.SuperAdmin))
    {
        var hashingService = scope.ServiceProvider.GetRequiredService<HashingService>();
        var admin = new User
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "admin",
            Email = "adminc@chitchat.com",
            PasswordHash = hashingService.HashPassword("Admin123!"),
            DisplayName = "Admin",
            Role = UserRole.Admin,
            IsActive = true,
            IsVerified = true,
            CreatedAt = DateTime.UtcNow,
            Status = UserStatus.Online
        };
        dbContext.Users.Add(admin);
        dbContext.SaveChanges();
    }
}

app.Run();
