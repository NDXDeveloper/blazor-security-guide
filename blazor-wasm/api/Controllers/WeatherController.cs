using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Controllers;

/// <summary>
/// Exemple de controller avec sécurité appropriée pour API publique.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class WeatherController : ControllerBase
{
    private static readonly string[] Summaries = new[]
    {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild",
        "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };

    private readonly ILogger<WeatherController> _logger;

    public WeatherController(ILogger<WeatherController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Endpoint public - protégé par rate limiting global.
    /// </summary>
    [HttpGet]
    public IActionResult Get()
    {
        // Toujours valider les paramètres d'entrée côté serveur
        // Ne jamais faire confiance au client

        var forecast = Enumerable.Range(1, 5).Select(index => new
        {
            Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            TemperatureC = Random.Shared.Next(-20, 55),
            Summary = Summaries[Random.Shared.Next(Summaries.Length)]
        });

        return Ok(forecast);
    }

    /// <summary>
    /// Endpoint sensible - rate limiting strict.
    /// Simule un endpoint de données utilisateur.
    /// </summary>
    [HttpGet("{id}")]
    [EnableRateLimiting("AuthEndpoints")] // Rate limiting strict
    public IActionResult GetById(int id)
    {
        // Validation des entrées
        if (id <= 0)
        {
            // Ne pas révéler d'informations sur la structure des données
            return BadRequest(new { error = "Invalid request" });
        }

        // Vérification d'autorisation (à implémenter)
        // L'utilisateur a-t-il le droit d'accéder à cette ressource ?

        // Log pour audit
        _logger.LogInformation("Weather data accessed for ID: {Id} by IP: {IP}",
            id, HttpContext.Connection.RemoteIpAddress);

        return Ok(new
        {
            Id = id,
            Date = DateOnly.FromDateTime(DateTime.Now),
            TemperatureC = Random.Shared.Next(-20, 55),
            Summary = Summaries[Random.Shared.Next(Summaries.Length)]
        });
    }
}
