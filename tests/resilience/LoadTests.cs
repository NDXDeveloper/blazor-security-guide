using System.Diagnostics;
using System.Net;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace ResilienceTests;

/// <summary>
/// Tests de résilience et de charge.
///
/// Ces tests vérifient le comportement de l'application sous stress léger.
/// Ce ne sont PAS des tests DDoS - ils sont conçus pour être exécutés en CI.
/// </summary>
public class LoadTests
{
    private readonly ITestOutputHelper _output;
    private readonly HttpClient _client;
    private readonly string _baseUrl;

    public LoadTests(ITestOutputHelper output)
    {
        _output = output;
        _client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _baseUrl = Environment.GetEnvironmentVariable("API_URL") ?? "http://localhost:80";
    }

    #region Tests de charge basiques

    [Fact]
    public async Task ConcurrentRequests_50_ShouldAllComplete()
    {
        // Arrange
        const int concurrentRequests = 50;
        var tasks = new List<Task<HttpResponseMessage>>();
        var stopwatch = Stopwatch.StartNew();

        // Act
        for (int i = 0; i < concurrentRequests; i++)
        {
            tasks.Add(_client.GetAsync($"{_baseUrl}/api/weather"));
        }

        var responses = await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        var successCount = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        var rateLimitedCount = responses.Count(r => r.StatusCode == HttpStatusCode.TooManyRequests);
        var errorCount = responses.Count(r =>
            r.StatusCode != HttpStatusCode.OK &&
            r.StatusCode != HttpStatusCode.TooManyRequests);

        _output.WriteLine($"Concurrent Requests: {concurrentRequests}");
        _output.WriteLine($"Total Time: {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"Success (200): {successCount}");
        _output.WriteLine($"Rate Limited (429): {rateLimitedCount}");
        _output.WriteLine($"Errors: {errorCount}");
        _output.WriteLine($"Avg Time per Request: {stopwatch.ElapsedMilliseconds / concurrentRequests}ms");

        // Toutes les requêtes devraient avoir une réponse (200 ou 429)
        errorCount.Should().Be(0, "Aucune erreur serveur ne devrait se produire");
        (successCount + rateLimitedCount).Should().Be(concurrentRequests);
    }

    [Fact]
    public async Task SequentialRequests_100_ShouldMeasureLatency()
    {
        // Arrange
        const int totalRequests = 100;
        var latencies = new List<long>();

        // Act
        for (int i = 0; i < totalRequests; i++)
        {
            var sw = Stopwatch.StartNew();
            var response = await _client.GetAsync($"{_baseUrl}/api/weather");
            sw.Stop();

            if (response.StatusCode == HttpStatusCode.OK)
            {
                latencies.Add(sw.ElapsedMilliseconds);
            }
        }

        // Assert
        if (latencies.Count > 0)
        {
            var avgLatency = latencies.Average();
            var p50 = Percentile(latencies, 50);
            var p95 = Percentile(latencies, 95);
            var p99 = Percentile(latencies, 99);

            _output.WriteLine($"Total Requests: {totalRequests}");
            _output.WriteLine($"Successful: {latencies.Count}");
            _output.WriteLine($"Avg Latency: {avgLatency:F2}ms");
            _output.WriteLine($"P50: {p50}ms");
            _output.WriteLine($"P95: {p95}ms");
            _output.WriteLine($"P99: {p99}ms");

            // La latence P95 devrait être raisonnable
            p95.Should().BeLessThan(1000, "P95 latency should be under 1 second");
        }
    }

    #endregion

    #region Tests de comportement sous charge

    [Fact]
    public async Task UnderLoad_ServerShouldRemainResponsive()
    {
        // Arrange - Générer de la charge en arrière-plan
        var cts = new CancellationTokenSource();
        var backgroundTasks = new List<Task>();

        // Démarrer 10 tâches en arrière-plan
        for (int i = 0; i < 10; i++)
        {
            backgroundTasks.Add(GenerateLoadAsync(cts.Token));
        }

        // Attendre un peu que la charge se stabilise
        await Task.Delay(500);

        // Act - Mesurer la latence pendant la charge
        var latencies = new List<long>();
        for (int i = 0; i < 20; i++)
        {
            var sw = Stopwatch.StartNew();
            var response = await _client.GetAsync($"{_baseUrl}/api/weather");
            sw.Stop();

            if (response.IsSuccessStatusCode)
            {
                latencies.Add(sw.ElapsedMilliseconds);
            }

            await Task.Delay(50);
        }

        // Arrêter la charge
        cts.Cancel();
        try
        {
            await Task.WhenAll(backgroundTasks);
        }
        catch (OperationCanceledException)
        {
            // Attendu - les tâches ont été annulées
        }

        // Assert
        if (latencies.Count > 0)
        {
            var avgLatency = latencies.Average();
            _output.WriteLine($"Avg Latency Under Load: {avgLatency:F2}ms");

            avgLatency.Should().BeLessThan(2000,
                "L'API devrait rester responsive sous charge");
        }
    }

    [Fact]
    public async Task RateLimiting_ShouldProtectServerUnderHeavyLoad()
    {
        // Arrange
        const int totalRequests = 200;
        var responses = new List<HttpResponseMessage>();

        // Act - Envoyer beaucoup de requêtes rapidement
        var tasks = Enumerable.Range(0, totalRequests)
            .Select(_ => _client.GetAsync($"{_baseUrl}/api/weather"))
            .ToArray();

        responses.AddRange(await Task.WhenAll(tasks));

        // Assert
        var okCount = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        var rateLimitedCount = responses.Count(r => r.StatusCode == HttpStatusCode.TooManyRequests);
        var serverErrors = responses.Count(r => (int)r.StatusCode >= 500);

        _output.WriteLine($"200 OK: {okCount}");
        _output.WriteLine($"429 Rate Limited: {rateLimitedCount}");
        _output.WriteLine($"5xx Errors: {serverErrors}");

        // Le rate limiting devrait empêcher les erreurs serveur
        serverErrors.Should().Be(0,
            "Le rate limiting devrait protéger le serveur contre la surcharge");

        rateLimitedCount.Should().BeGreaterThan(0,
            "Le rate limiting devrait s'activer sous forte charge");
    }

    #endregion

    #region Comparaison WASM vs Server (conceptuel)

    [Fact]
    public void Documentation_WasmVsServerResilience()
    {
        // Ce test documente les différences de résilience

        _output.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        _output.WriteLine("║         COMPARAISON RÉSILIENCE : WASM vs SERVER              ║");
        _output.WriteLine("╠══════════════════════════════════════════════════════════════╣");
        _output.WriteLine("║ Métrique              │ WASM           │ Server              ║");
        _output.WriteLine("╠═══════════════════════╪════════════════╪═════════════════════╣");
        _output.WriteLine("║ Mémoire/user          │ ~0             │ ~50-100 KB          ║");
        _output.WriteLine("║ Connexions            │ Courtes        │ Persistantes (WS)   ║");
        _output.WriteLine("║ Max users (1 serveur) │ 10 000+        │ 2 000-5 000         ║");
        _output.WriteLine("║ Scaling horizontal    │ Excellent      │ Complexe (sticky)   ║");
        _output.WriteLine("║ Résistance DDoS       │ Bonne          │ Moyenne             ║");
        _output.WriteLine("║ Point faible          │ API publique   │ SignalR             ║");
        _output.WriteLine("╚══════════════════════════════════════════════════════════════╝");

        Assert.True(true);
    }

    #endregion

    #region Helpers

    private async Task GenerateLoadAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await _client.GetAsync($"{_baseUrl}/api/weather", ct);
                await Task.Delay(10, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Ignorer les erreurs pendant la génération de charge
            }
        }
    }

    private static double Percentile(List<long> values, double percentile)
    {
        if (values.Count == 0) return 0;

        var sorted = values.OrderBy(x => x).ToList();
        var index = (int)Math.Ceiling((percentile / 100) * sorted.Count) - 1;
        return sorted[Math.Max(0, index)];
    }

    #endregion
}

/// <summary>
/// Tests spécifiques pour mesurer la consommation mémoire.
/// Ces tests sont plus utiles en environnement de développement.
/// </summary>
public class MemoryTests
{
    private readonly ITestOutputHelper _output;

    public MemoryTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Documentation_MemoryConsumption()
    {
        // Documentation de la consommation mémoire attendue

        _output.WriteLine("Consommation mémoire typique :");
        _output.WriteLine("");
        _output.WriteLine("Blazor WASM :");
        _output.WriteLine("  - Serveur : API stateless, ~50-100 MB total");
        _output.WriteLine("  - Client : ~6 MB téléchargés (DLLs + runtime)");
        _output.WriteLine("  - Par user sur serveur : ~0 (pas d'état)");
        _output.WriteLine("");
        _output.WriteLine("Blazor Server :");
        _output.WriteLine("  - Base : ~200-300 MB");
        _output.WriteLine("  - Par circuit : ~50-100 KB");
        _output.WriteLine("  - 1 000 users : ~300-400 MB");
        _output.WriteLine("  - 5 000 users : ~700-800 MB");
        _output.WriteLine("  - 10 000 users : ~1.2-1.5 GB");
        _output.WriteLine("");
        _output.WriteLine("Facteurs aggravants Blazor Server :");
        _output.WriteLine("  - Composants lourds (grids, charts)");
        _output.WriteLine("  - États non nettoyés");
        _output.WriteLine("  - Circuits zombies");

        Assert.True(true);
    }
}
