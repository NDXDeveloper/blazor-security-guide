# Authentification vs Sécurité - Deux choses différentes

## Le mythe

> "Mon API est sécurisée car seuls les utilisateurs authentifiés peuvent y accéder."

C'est **faux**. L'authentification répond à "Qui êtes-vous ?", pas à "Devez-vous être autorisé ?", et surtout pas à "Comment protéger le serveur ?".

## Ce que l'authentification fait

```
┌─────────────────────────────────────────────────────────────┐
│                    AUTHENTIFICATION                         │
├─────────────────────────────────────────────────────────────┤
│  • Vérifie l'identité de l'utilisateur                      │
│  • Délivre un token/session                                 │
│  • Permet de savoir QUI fait la requête                     │
└─────────────────────────────────────────────────────────────┘
```

## Ce que l'authentification ne fait pas

```
┌─────────────────────────────────────────────────────────────┐
│              CE QUI N'EST PAS DE L'AUTH                     │
├─────────────────────────────────────────────────────────────┤
│  ✗ Ne limite pas le nombre de requêtes                      │
│  ✗ Ne protège pas l'endpoint de login                       │
│  ✗ Ne valide pas les entrées utilisateur                    │
│  ✗ Ne protège pas contre le DDoS                            │
│  ✗ Ne vérifie pas les droits d'accès aux ressources         │
│  ✗ Ne protège pas contre les injections                     │
└─────────────────────────────────────────────────────────────┘
```

## Les trois piliers de la sécurité d'accès

### 1. Authentification (AuthN)

**Question :** Qui êtes-vous ?

```csharp
// L'utilisateur prouve son identité
[HttpPost("login")]
public async Task<IActionResult> Login(LoginRequest request)
{
    var user = await _userService.ValidateCredentials(
        request.Email,
        request.Password
    );

    if (user == null)
        return Unauthorized();

    var token = GenerateJwtToken(user);
    return Ok(new { token });
}
```

### 2. Autorisation (AuthZ)

**Question :** Avez-vous le droit de faire cette action ?

```csharp
// L'utilisateur est authentifié, mais a-t-il le droit ?
[HttpDelete("users/{id}")]
[Authorize(Roles = "Admin")]  // Seuls les admins peuvent supprimer
public async Task<IActionResult> DeleteUser(int id)
{
    // Vérification supplémentaire : pas de self-delete
    if (id == GetCurrentUserId())
        return Forbid();

    await _userService.Delete(id);
    return NoContent();
}
```

### 3. Protection de l'infrastructure

**Question :** Comment empêcher les abus même sur les endpoints publics ?

```csharp
// Le endpoint de login est PUBLIC par définition
// Il DOIT être protégé par rate limiting
[HttpPost("login")]
[EnableRateLimiting("auth")]  // 5 tentatives par 5 minutes
public async Task<IActionResult> Login(LoginRequest request)
{
    // ...
}
```

## Vecteurs d'attaque malgré l'authentification

### 1. L'endpoint de login est public

```
Attaquant
    │
    │  POST /api/auth/login  (pas besoin d'être authentifié)
    │  { "email": "admin@...", "password": "test1" }
    │  { "email": "admin@...", "password": "test2" }
    │  { "email": "admin@...", "password": "test3" }
    │  ... (brute force)
    │
    ▼
┌─────────────────┐
│   Votre API     │
└─────────────────┘
```

**Solution :** Rate limiting strict sur `/login` (5 tentatives / 5 minutes).

### 2. Credential stuffing

L'attaquant utilise des listes de credentials volées sur d'autres sites.

```
Attaquant
    │
    │  Base de données volée : 1 million d'emails/passwords
    │  Test chaque combo sur votre site
    │
    ▼
┌─────────────────┐
│   Votre API     │
└─────────────────┘
```

**Solutions :**
- Rate limiting par IP
- Rate limiting par email ciblé
- Détection d'anomalies (géolocalisation, device)
- CAPTCHA après X échecs

### 3. Enumération d'utilisateurs

```
"Invalid password" → L'email existe
"User not found"   → L'email n'existe pas
```

L'attaquant peut construire une liste d'emails valides.

**Solution :** Message uniforme "Invalid credentials".

### 4. Token volé/forgé

Un token JWT valide peut être :
- Volé (XSS, interception)
- Forgé (si secret faible)
- Réutilisé indéfiniment (pas d'expiration)

**Solutions :**
- HTTPS obligatoire
- Tokens courte durée (15 min) + refresh tokens
- Stockage sécurisé (httpOnly cookies, pas localStorage)
- Secret fort (256 bits minimum)
- Révocation possible (blacklist ou tokens stateful)

### 5. Autorisation insuffisante

```csharp
// ❌ DANGEREUX - Pas de vérification de propriété
[HttpGet("documents/{id}")]
[Authorize]  // Authentifié = suffisant ?
public async Task<IActionResult> GetDocument(int id)
{
    var document = await _repo.GetById(id);
    return Ok(document);  // N'importe quel user peut lire n'importe quel doc
}

// ✅ CORRECT - Vérification de propriété
[HttpGet("documents/{id}")]
[Authorize]
public async Task<IActionResult> GetDocument(int id)
{
    var document = await _repo.GetById(id);

    if (document.OwnerId != GetCurrentUserId())
        return Forbid();

    return Ok(document);
}
```

## Configuration sécurisée complète

### JWT - Configuration recommandée

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            // Valider l'émetteur
            ValidateIssuer = true,
            ValidIssuer = "https://votre-app.com",

            // Valider l'audience
            ValidateAudience = true,
            ValidAudience = "https://votre-app.com",

            // Valider la signature
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(configuration["Jwt:Secret"]!)
            ),

            // Valider l'expiration
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,  // Pas de tolérance

            // Requis
            RequireExpirationTime = true,
            RequireSignedTokens = true,
        };
    });
```

### Génération de tokens

```csharp
public string GenerateToken(User user)
{
    var claims = new[]
    {
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new Claim(ClaimTypes.Email, user.Email),
        new Claim(ClaimTypes.Role, user.Role),
        // Claim personnalisé pour tracking
        new Claim("session_id", Guid.NewGuid().ToString()),
    };

    var key = new SymmetricSecurityKey(
        Encoding.UTF8.GetBytes(_configuration["Jwt:Secret"]!)
    );

    var token = new JwtSecurityToken(
        issuer: "https://votre-app.com",
        audience: "https://votre-app.com",
        claims: claims,
        expires: DateTime.UtcNow.AddMinutes(15),  // Court !
        signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
    );

    return new JwtSecurityTokenHandler().WriteToken(token);
}
```

### Stockage côté client (Blazor WASM)

```csharp
// ❌ DANGEREUX - localStorage accessible via XSS
localStorage.setItem("token", token);

// ✅ MIEUX - Cookie httpOnly (nécessite modification côté serveur)
// Le token est envoyé automatiquement et n'est pas accessible en JS
```

Configuration serveur pour cookies :

```csharp
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.HttpOnly = true;      // Pas accessible en JS
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;  // HTTPS only
        options.Cookie.SameSite = SameSiteMode.Strict;  // Anti-CSRF
        options.ExpireTimeSpan = TimeSpan.FromHours(1);
        options.SlidingExpiration = true;
    });
```

## Checklist sécurité authentification

### Endpoint login
- [ ] Rate limiting strict (5/min)
- [ ] CAPTCHA après 3 échecs
- [ ] Messages d'erreur uniformes
- [ ] Logging des échecs
- [ ] Alerte sur patterns suspects

### Tokens
- [ ] Expiration courte (15-30 min)
- [ ] Refresh token rotation
- [ ] Secret fort (256+ bits)
- [ ] Stockage sécurisé (httpOnly si possible)
- [ ] HTTPS obligatoire

### Autorisation
- [ ] Vérification de propriété des ressources
- [ ] Principe du moindre privilège
- [ ] Roles/Permissions granulaires
- [ ] Audit des accès sensibles

### Protection générale
- [ ] Rate limiting global
- [ ] WAF/CDN
- [ ] Monitoring des anomalies
- [ ] Plan de réponse aux incidents

## Résumé

| Couche | Protège contre | Exemple |
|--------|----------------|---------|
| Authentification | Usurpation d'identité | JWT, OAuth, Cookies |
| Autorisation | Accès non autorisé | Roles, Policies, Claims |
| Rate limiting | Abus, brute force | 5 req/min sur login |
| WAF | Attaques applicatives | SQL injection, XSS |
| Réseau | DDoS, scan | Cloudflare, pare-feu |

**L'authentification est nécessaire mais insuffisante. La sécurité est un ensemble de couches.**
