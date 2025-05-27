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

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add User Secrets
builder.Configuration.AddUserSecrets<Program>();

// Add DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IChatRepository, ChatRepository>();

// Register our custom services
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddSingleton<ConnectionManager>();
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<EncryptionService>();
builder.Services.AddSingleton<HashingService>();
builder.Services.AddSingleton<DigitalSignatureService>();
builder.Services.AddSingleton<TokenStorage>();
builder.Services.AddScoped<IStorageService, StorageService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<TokenStorageService>();
builder.Services.AddScoped<IUserService, UserService>();

// Configure Storage Options
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection("Storage"));
builder.Services.AddMemoryCache();

// Configure Security Options
builder.Services.Configure<SecurityOptions>(options =>
{
    options.JwtSecret = builder.Configuration["Jwt:Key"];
    options.JwtIssuer = builder.Configuration["Jwt:Issuer"];
    options.JwtAudience = builder.Configuration["Jwt:Audience"];
    options.JwtExpirationMinutes = 60;
    options.RefreshTokenExpirationDays = 7;
    options.MaxFailedLoginAttempts = 5;
    options.AccountLockoutMinutes = 30;
    options.RequireEmailVerification = true;
    options.RequireTwoFactor = false;
    options.PasswordMinLength = 8;
    options.RequireSpecialCharacters = true;
    options.RequireNumbers = true;
    options.RequireUppercase = true;
    options.RequireLowercase = true;
});

builder.Services.AddScoped<SecurityService>();

// Add JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
        };
    });

// Add WebSocket support
builder.Services.AddSignalR();

// Add CORS
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

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<ChatHub>("/chatHub");

// WebSocket middleware configuration
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromMinutes(2),
    ReceiveBufferSize = 4 * 1024
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

            using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            var chatService = context.RequestServices.GetRequiredService<IChatService>();
            await chatService.HandleWebSocketConnection(webSocket, userId);
        }
        catch (Exception ex)
        {
            // Log the error
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

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        context.Database.EnsureCreated();
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while creating the database.");
    }
}

app.Run();
