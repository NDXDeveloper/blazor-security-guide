var builder = WebApplication.CreateBuilder(args);

// =============================================================================
// API INTERNE - Configuration simplifiée
// =============================================================================
// Cette API n'est accessible que depuis le réseau Docker interne.
// Pas besoin de :
// - CORS (pas de navigateur impliqué)
// - Rate limiting public (le trafic vient uniquement de Blazor Server)
// - Protection DDoS (pas exposée à Internet)

builder.Services.AddControllers();

var app = builder.Build();

// =============================================================================
// Authentification Machine-to-Machine (Optionnelle mais recommandée)
// =============================================================================
// Même si l'API est interne, on peut ajouter une couche d'auth
// pour se protéger contre une compromission de Blazor Server.
app.Use(async (context, next) =>
{
    // Vérifier la clé API interne
    var expectedApiKey = builder.Configuration["ApiSettings:InternalApiKey"];

    if (!string.IsNullOrEmpty(expectedApiKey))
    {
        var providedApiKey = context.Request.Headers["X-Internal-Api-Key"].FirstOrDefault();

        if (providedApiKey != expectedApiKey)
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("Unauthorized");
            return;
        }
    }

    await next();
});

// Pas de CORS - inutile pour une API interne
// Pas de HTTPS - le trafic reste dans le réseau Docker

app.MapControllers();

// Log au démarrage pour confirmer la configuration
app.Logger.LogInformation("API interne démarrée sur le réseau Docker privé");
app.Logger.LogInformation("Cette API n'est PAS accessible depuis Internet");

app.Run();
