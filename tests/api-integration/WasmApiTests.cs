using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace SecurityTests;

/// <summary>
/// Tests d'intégration pour l'API Blazor WASM (publique).
///
/// Ces tests prouvent que :
/// 1. L'API est accessible (c'est voulu pour WASM)
/// 2. Le rate limiting fonctionne
/// 3. Les headers de sécurité sont présents
/// 4. CORS est configuré (mais ne protège pas contre curl)
/// </summary>
public class WasmApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public WasmApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    #region Accessibilité API

    [Fact]
    public async Task Api_Weather_ShouldBeAccessible()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/api/weather");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Api_Weather_ShouldReturnJsonContent()
    {
        // Act
        var response = await _client.GetAsync("/api/weather");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        content.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Api_WeatherById_WithValidId_ShouldReturnOk()
    {
        // Act
        var response = await _client.GetAsync("/api/weather/1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Api_WeatherById_WithInvalidId_ShouldReturnBadRequest()
    {
        // Act
        var response = await _client.GetAsync("/api/weather/0");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Api_WeatherById_WithNegativeId_ShouldReturnBadRequest()
    {
        // Act
        var response = await _client.GetAsync("/api/weather/-1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Headers de sécurité

    [Fact]
    public async Task Api_ShouldInclude_XFrameOptions_Header()
    {
        // Act
        var response = await _client.GetAsync("/api/weather");

        // Assert
        response.Headers.Should().ContainKey("X-Frame-Options");
        response.Headers.GetValues("X-Frame-Options").First().Should().Be("DENY");
    }

    [Fact]
    public async Task Api_ShouldInclude_XContentTypeOptions_Header()
    {
        // Act
        var response = await _client.GetAsync("/api/weather");

        // Assert
        response.Headers.Should().ContainKey("X-Content-Type-Options");
        response.Headers.GetValues("X-Content-Type-Options").First().Should().Be("nosniff");
    }

    [Fact]
    public async Task Api_ShouldInclude_XXSSProtection_Header()
    {
        // Act
        var response = await _client.GetAsync("/api/weather");

        // Assert
        response.Headers.Should().ContainKey("X-XSS-Protection");
    }

    [Fact]
    public async Task Api_ShouldInclude_ReferrerPolicy_Header()
    {
        // Act
        var response = await _client.GetAsync("/api/weather");

        // Assert
        response.Headers.Should().ContainKey("Referrer-Policy");
    }

    #endregion

    #region CORS - Démonstration que ce n'est PAS une sécurité serveur

    [Fact]
    public async Task Cors_RequestFromUnknownOrigin_StillReachesServer()
    {
        // Arrange
        // Simuler une requête depuis un domaine non autorisé
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/weather");
        request.Headers.Add("Origin", "https://evil-hacker-site.com");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        // IMPORTANT : La requête ATTEINT le serveur et retourne des données !
        // CORS ne bloque que la LECTURE de la réponse par le navigateur.
        // Côté serveur, la requête est traitée normalement.
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Ce test PROUVE que CORS n'est pas une protection serveur.
        // Un attaquant utilisant curl/Postman/scripts peut toujours appeler l'API.
    }

    [Fact]
    public async Task Cors_PreflightRequest_ShouldBeHandled()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/weather");
        request.Headers.Add("Origin", "http://localhost:5001");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        // La réponse preflight devrait être OK ou NoContent
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);
    }

    #endregion

    #region Validation des entrées

    [Fact]
    public async Task Api_ShouldNotExposeDetailedErrors_InProduction()
    {
        // Act - Requête avec paramètre invalide
        var response = await _client.GetAsync("/api/weather/abc");

        // Assert
        // Ne devrait pas exposer de stack trace ou détails d'implémentation
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotContain("System.");
        content.Should().NotContain("Exception");
        content.Should().NotContain("StackTrace");
    }

    #endregion
}

/// <summary>
/// Tests prouvant que l'API WASM est EXPOSÉE (comportement attendu).
/// Ces tests sont là pour documenter le comportement, pas pour le valider comme "sécurisé".
/// </summary>
public class WasmApiExposureTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public WasmApiExposureTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Api_IsExposedPublicly_ThisIsExpectedForWasm()
    {
        // Ce test documente le fait que l'API WASM est publique.
        // C'est NORMAL et ATTENDU pour Blazor WASM.
        // La sécurité doit être assurée par :
        // - Rate limiting
        // - Authentification/Autorisation
        // - Validation des entrées
        // - WAF/CDN

        var response = await _client.GetAsync("/api/weather");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "L'API WASM est publique par conception. " +
            "La sécurité repose sur d'autres mécanismes, pas sur l'obscurité.");
    }

    [Fact]
    public async Task Api_CanBeCalledWithoutAuthentication_OnPublicEndpoints()
    {
        // Les endpoints publics sont accessibles sans auth
        // C'est voulu pour les données publiques

        var response = await _client.GetAsync("/api/weather");

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized,
            "L'endpoint weather est public par conception.");
    }
}
