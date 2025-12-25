using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// =============================================================================
// CORS - Configuration restrictive
// =============================================================================
// IMPORTANT : CORS n'est PAS une sécurité suffisante.
// Un attaquant peut appeler l'API directement (curl, Postman, scripts).
// CORS empêche uniquement les appels depuis d'autres sites via navigateur.
builder.Services.AddCors(options =>
{
    options.AddPolicy("ProductionPolicy", policy =>
    {
        policy
            // Remplacer par votre domaine exact
            .WithOrigins("https://votredomaine.com")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });

    // Pour le développement uniquement
    options.AddPolicy("DevelopmentPolicy", policy =>
    {
        policy
            .WithOrigins("http://localhost:5001", "https://localhost:5001")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

// =============================================================================
// Rate Limiting - Protection contre les abus
// =============================================================================
// C'est une vraie protection, contrairement à CORS.
builder.Services.AddRateLimiter(options =>
{
    // Politique globale : limite par IP
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        // Récupérer l'IP réelle (derrière Nginx/Cloudflare)
        var clientIp = context.Request.Headers["X-Forwarded-For"].FirstOrDefault()
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: clientIp,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,        // 100 requêtes
                Window = TimeSpan.FromMinutes(1), // par minute
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 10
            });
    });

    // Politique spécifique pour les endpoints sensibles (login, etc.)
    options.AddFixedWindowLimiter("AuthEndpoints", limiterOptions =>
    {
        limiterOptions.PermitLimit = 5;   // 5 tentatives
        limiterOptions.Window = TimeSpan.FromMinutes(5); // par 5 minutes
        limiterOptions.QueueLimit = 0;    // Pas de queue, rejet direct
    });

    // Réponse personnalisée quand limite atteinte
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.Headers.RetryAfter = "60";

        // Logger la tentative (pour détection d'attaques)
        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
        var ip = context.HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()
            ?? context.HttpContext.Connection.RemoteIpAddress?.ToString();
        logger.LogWarning("Rate limit exceeded for IP: {IP}, Path: {Path}",
            ip, context.HttpContext.Request.Path);

        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            error = "Too many requests",
            retryAfter = 60
        }, token);
    };
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// =============================================================================
// Middleware Pipeline - Ordre important !
// =============================================================================

// 1. Headers de sécurité (avant tout le reste)
app.Use(async (context, next) =>
{
    // Protection contre le clickjacking
    context.Response.Headers.XFrameOptions = "DENY";
    // Protection XSS (navigateurs modernes)
    context.Response.Headers.XXSSProtection = "1; mode=block";
    // Empêcher le sniffing MIME
    context.Response.Headers.XContentTypeOptions = "nosniff";
    // Referrer policy restrictive
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    // Permissions policy
    context.Response.Headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";

    await next();
});

// 2. HTTPS redirection
app.UseHttpsRedirection();

// 3. CORS (doit être avant auth et controllers)
var env = app.Environment;
app.UseCors(env.IsDevelopment() ? "DevelopmentPolicy" : "ProductionPolicy");

// 4. Rate limiting (après CORS, avant auth)
app.UseRateLimiter();

// 5. Authentification/Autorisation (à implémenter selon vos besoins)
// app.UseAuthentication();
// app.UseAuthorization();

// 6. Controllers
app.MapControllers();

app.Run();

// =============================================================================
// Classe Program partielle pour les tests d'intégration
// =============================================================================
// Nécessaire pour WebApplicationFactory<Program>
public partial class Program { }
