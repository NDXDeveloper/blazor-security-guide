using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using BlazorWasm;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// =============================================================================
// Configuration HttpClient pour appels API
// =============================================================================
// L'URL de base de l'API sera différente selon l'environnement
builder.Services.AddScoped(sp =>
{
    var baseAddress = builder.Configuration["ApiBaseUrl"]
        ?? builder.HostEnvironment.BaseAddress;

    return new HttpClient { BaseAddress = new Uri(baseAddress) };
});

// =============================================================================
// IMPORTANT - Sécurité côté client
// =============================================================================
// 1. Ne JAMAIS stocker de secrets dans le code WASM
// 2. Ne JAMAIS faire confiance aux données côté client
// 3. Toujours valider côté serveur (API)
// 4. Les tokens d'authentification doivent être stockés de manière sécurisée
//    (httpOnly cookies si possible, pas localStorage)

await builder.Build().RunAsync();
