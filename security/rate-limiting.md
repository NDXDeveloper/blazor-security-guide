# Rate Limiting - Protection réelle contre les abus

## Pourquoi le rate limiting est essentiel

Le rate limiting est l'une des rares protections qui **fonctionne vraiment** contre :
- Les attaques par brute force
- Les bots et scrapers
- Les attaques DDoS applicatives (L7)
- Les abus d'API

Contrairement à CORS ou HTTPS, le rate limiting **refuse réellement** les requêtes excédentaires.

## Où implémenter le rate limiting ?

```
Internet
    │
    ▼
┌─────────────┐
│ CDN / WAF   │  ← 1. Première ligne (Cloudflare)
└──────┬──────┘
       │
       ▼
┌─────────────┐
│   Nginx     │  ← 2. Deuxième ligne (rate limiting local)
└──────┬──────┘
       │
       ▼
┌─────────────┐
│ Application │  ← 3. Troisième ligne (rate limiting métier)
└─────────────┘
```

**Règle importante :** Implémenter à plusieurs niveaux. Si une couche est contournée, les autres protègent.

## Niveau 1 : CDN/WAF (Cloudflare)

Le plus efficace car il bloque le trafic **avant** qu'il n'atteigne votre serveur.

Voir [cloudflare.md](../nginx/cloudflare.md) pour la configuration.

## Niveau 2 : Nginx

Configuration complète pour Blazor.

### Zones de limitation

```nginx
# Dans http { }

# Zone par IP pour l'API (10 requêtes/seconde)
limit_req_zone $binary_remote_addr zone=api:10m rate=10r/s;

# Zone par IP pour les endpoints sensibles (2 requêtes/seconde)
limit_req_zone $binary_remote_addr zone=auth:10m rate=2r/s;

# Zone par IP pour le contenu général (30 requêtes/seconde)
limit_req_zone $binary_remote_addr zone=general:10m rate=30r/s;

# Zone de connexions simultanées
limit_conn_zone $binary_remote_addr zone=conn:10m;
```

### Application par location

```nginx
# API générale
location /api/ {
    limit_req zone=api burst=20 nodelay;
    limit_conn conn 10;
    # ...
}

# Endpoints d'authentification
location /api/auth/ {
    limit_req zone=auth burst=5 nodelay;
    limit_conn conn 3;
    # ...
}

# SignalR (Blazor Server)
location /_blazor {
    limit_req zone=general burst=10 nodelay;
    limit_conn conn 5;  # Max 5 connexions simultanées par IP
    # ...
}
```

### Paramètres expliqués

| Paramètre | Description |
|-----------|-------------|
| `rate=10r/s` | 10 requêtes par seconde en moyenne |
| `burst=20` | Buffer de 20 requêtes supplémentaires |
| `nodelay` | Pas de délai, rejette immédiatement si dépassé |
| `10m` | Zone de 10 MB (environ 160 000 IPs) |

### Réponse personnalisée

```nginx
# Code 429 au lieu de 503
limit_req_status 429;
limit_conn_status 429;

# Page d'erreur personnalisée
error_page 429 /429.html;
location = /429.html {
    internal;
    return 429 '{"error": "Too many requests", "retryAfter": 60}';
}
```

## Niveau 3 : Application ASP.NET Core

Le rate limiting intégré depuis .NET 7 est puissant et flexible.

### Configuration de base

```csharp
using System.Threading.RateLimiting;

builder.Services.AddRateLimiter(options =>
{
    // Limite globale par IP
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        // Récupérer l'IP réelle (derrière proxy)
        var clientIp = context.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                    ?? context.Request.Headers["X-Real-IP"].FirstOrDefault()
                    ?? context.Connection.RemoteIpAddress?.ToString()
                    ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: clientIp,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 10
            });
    });

    // Gestion du rejet
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = 429;
        context.HttpContext.Response.Headers.RetryAfter = "60";

        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            error = "Trop de requêtes",
            retryAfter = 60
        }, token);
    };
});

// Dans le pipeline
app.UseRateLimiter();
```

### Politiques par endpoint

```csharp
// Définir les politiques
builder.Services.AddRateLimiter(options =>
{
    // Politique stricte pour l'authentification
    options.AddFixedWindowLimiter("auth", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(5);
        opt.QueueLimit = 0;  // Pas de queue, rejet direct
    });

    // Politique pour les opérations sensibles
    options.AddSlidingWindowLimiter("sensitive", opt =>
    {
        opt.PermitLimit = 10;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.SegmentsPerWindow = 4;  // Fenêtre glissante de 15 secondes
        opt.QueueLimit = 0;
    });

    // Politique par utilisateur authentifié
    options.AddTokenBucketLimiter("user", opt =>
    {
        opt.TokenLimit = 100;
        opt.TokensPerPeriod = 10;
        opt.ReplenishmentPeriod = TimeSpan.FromSeconds(1);
        opt.QueueLimit = 5;
    });
});
```

### Application aux controllers

```csharp
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        // ...
    }
}

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("user")]  // Appliqué à tout le controller
public class DataController : ControllerBase
{
    [HttpGet]
    public IActionResult GetData()
    {
        // Rate limiting "user" appliqué
    }

    [HttpDelete("{id}")]
    [EnableRateLimiting("sensitive")]  // Override avec politique plus stricte
    public IActionResult Delete(int id)
    {
        // Rate limiting "sensitive" appliqué
    }
}
```

## Stratégies de rate limiting

### 1. Fixed Window (Fenêtre fixe)

```
Fenêtre 1 minute : 100 requêtes max
├─────────────────────────────────────┤
  └── 100 req à 00:59 ✓
├─────────────────────────────────────┤
  └── 100 req à 01:00 ✓ (nouvelle fenêtre)
```

**Problème :** Burst de 200 requêtes autour du changement de fenêtre.

### 2. Sliding Window (Fenêtre glissante)

```
Lisse les bursts sur la période
├────┼────┼────┼────┤
 25   25   25   25   (25 par segment de 15s)
```

**Meilleur** pour éviter les bursts.

### 3. Token Bucket

```
Bucket de 100 tokens, 10 tokens/seconde ajoutés
├── Request arrive → token consommé
├── Pas de token → request rejetée ou en queue
├── Tokens se rechargent progressivement
```

**Idéal** pour les API où on veut permettre des bursts occasionnels.

## Valeurs recommandées par type d'endpoint

| Endpoint | Rate | Burst | Stratégie |
|----------|------|-------|-----------|
| Login | 5/min | 0 | Fixed Window |
| Register | 3/min | 0 | Fixed Window |
| API lecture | 60/min | 20 | Sliding Window |
| API écriture | 30/min | 10 | Sliding Window |
| Upload | 10/min | 2 | Fixed Window |
| WebSocket connect | 5/min | 2 | Fixed Window |

## Logging et monitoring

### Logger les dépassements

```csharp
options.OnRejected = async (context, token) =>
{
    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
    var ip = context.HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()
          ?? context.HttpContext.Connection.RemoteIpAddress?.ToString();

    logger.LogWarning(
        "Rate limit exceeded. IP: {IP}, Path: {Path}, User: {User}",
        ip,
        context.HttpContext.Request.Path,
        context.HttpContext.User?.Identity?.Name ?? "anonymous"
    );

    // Alerte si beaucoup de dépassements (possible attaque)
    // Intégrer avec votre système de monitoring

    context.HttpContext.Response.StatusCode = 429;
    await context.HttpContext.Response.WriteAsJsonAsync(new
    {
        error = "Rate limit exceeded"
    }, token);
};
```

### Métriques recommandées

- Nombre de 429 par minute/heure
- Top 10 des IPs bloquées
- Endpoints les plus ciblés
- Ratio requêtes acceptées/rejetées

## Contournements et mitigations

### Contournement : Rotation d'IP

**Attaque :** L'attaquant utilise des centaines d'IPs (proxies, VPN, botnets).

**Mitigation :**
- Rate limiting par session/utilisateur (pas seulement IP)
- CAPTCHA après X échecs
- Fingerprinting navigateur

### Contournement : Slowloris

**Attaque :** Connexions lentes qui consomment des ressources.

**Mitigation :**
```nginx
# Timeouts agressifs
client_body_timeout 10s;
client_header_timeout 10s;
keepalive_timeout 30s;
send_timeout 10s;
```

### Contournement : Application layer DDoS

**Attaque :** Requêtes légitimes mais massives (botnets).

**Mitigation :**
- WAF/CDN en première ligne (Cloudflare)
- Challenge CAPTCHA pour trafic suspect
- Geo-blocking si pertinent

## Checklist rate limiting

- [ ] Rate limiting WAF (Cloudflare ou équivalent)
- [ ] Rate limiting Nginx configuré
- [ ] Rate limiting ASP.NET Core configuré
- [ ] Endpoints auth avec limite stricte (5/min)
- [ ] IP réelle récupérée (X-Forwarded-For)
- [ ] Réponse 429 avec header Retry-After
- [ ] Logging des dépassements
- [ ] Monitoring/alertes en place
- [ ] Tests de charge effectués
