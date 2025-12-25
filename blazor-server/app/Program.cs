var builder = WebApplication.CreateBuilder(args);

// =============================================================================
// Configuration Blazor Server
// =============================================================================
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor(options =>
{
    // ==========================================================================
    // Configuration SignalR pour la sécurité
    // ==========================================================================

    // Taille maximale des messages (protection contre les gros payloads)
    options.MaxBufferedUnacknowledgedRenderBatches = 10;

    // Timeout de déconnexion
    options.DisconnectedCircuitMaxRetained = 100;
    options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(3);

    // Gestion des erreurs (ne pas exposer les détails en production)
    options.DetailedErrors = builder.Environment.IsDevelopment();
});

// Configuration du hub SignalR
builder.Services.AddSignalR(options =>
{
    // Taille max des messages entrants
    options.MaximumReceiveMessageSize = 64 * 1024; // 64 KB

    // Keepalive pour détecter les connexions mortes
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);

    // Timeout client
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);

    // Activer les logs détaillés en dev uniquement
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
});

// =============================================================================
// HttpClient pour appeler l'API interne
// =============================================================================
builder.Services.AddHttpClient("InternalApi", client =>
{
    // URL de l'API interne (réseau Docker)
    var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"]
        ?? "http://api:5001";
    client.BaseAddress = new Uri(apiBaseUrl);

    // Header d'authentification machine-to-machine
    var apiKey = builder.Configuration["ApiSettings:InternalApiKey"];
    if (!string.IsNullOrEmpty(apiKey))
    {
        client.DefaultRequestHeaders.Add("X-Internal-Api-Key", apiKey);
    }

    // Timeout raisonnable
    client.Timeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

// =============================================================================
// Middleware Pipeline
// =============================================================================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Headers de sécurité
app.Use(async (context, next) =>
{
    context.Response.Headers.XFrameOptions = "DENY";
    context.Response.Headers.XXSSProtection = "1; mode=block";
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
    await next();
});

app.UseStaticFiles();
app.UseRouting();

// =============================================================================
// Endpoints de monitoring et test
// =============================================================================

// Health check endpoint (pour Docker, Kubernetes, load balancers)
app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    timestamp = DateTime.UtcNow,
    service = "blazor-server"
}));

// Endpoint météo pour tests de charge (simule un appel API léger)
app.MapGet("/api/weather", () =>
{
    var summaries = new[] { "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching" };
    var forecast = Enumerable.Range(1, 5).Select(index => new
    {
        Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
        TemperatureC = Random.Shared.Next(-20, 55),
        Summary = summaries[Random.Shared.Next(summaries.Length)]
    }).ToArray();
    return Results.Ok(forecast);
});

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
