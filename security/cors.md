# CORS - Ce que c'est vraiment (et ce que ce n'est pas)

## Résumé

> **CORS n'est PAS une sécurité. C'est une politique navigateur.**

CORS (Cross-Origin Resource Sharing) empêche un site malveillant d'appeler votre API depuis le navigateur d'un utilisateur. C'est tout. Un attaquant peut contourner CORS trivialement.

## Ce que CORS fait

```
Site malveillant (evil.com)                 Votre API
        │                                        │
        │  fetch("https://votre-api.com/data")   │
        │ ────────────────────────────────────►  │
        │                                        │
        │  ◄─── Réponse avec headers CORS ───    │
        │                                        │
        │  Le navigateur vérifie :               │
        │  "Est-ce que votre-api.com autorise    │
        │   evil.com dans Access-Control-Allow-  │
        │   Origin ?"                            │
        │                                        │
        │  Si non → Le navigateur BLOQUE         │
        │           la lecture de la réponse     │
```

**Important :** La requête est quand même envoyée au serveur. CORS ne bloque que la lecture de la réponse par JavaScript.

## Ce que CORS ne fait pas

```
Attaquant avec curl/Postman/script
        │
        │  curl https://votre-api.com/data
        │ ───────────────────────────────────────►
        │                                        │
        │  ◄─── Réponse complète ─────────────   │
        │                                        │
        │  CORS n'est pas vérifié.               │
        │  Pas de navigateur = pas de CORS.      │
```

### CORS ne protège pas contre :

- **Appels directs** (curl, Postman, wget, scripts Python/Node)
- **Bots** (ils n'utilisent pas de navigateur)
- **Attaques DDoS** (les requêtes arrivent quand même au serveur)
- **Brute force** (chaque tentative atteint l'API)
- **Exploitation de vulnérabilités** (injection SQL, etc.)

## Pourquoi CORS existe-t-il alors ?

CORS protège les **utilisateurs légitimes** contre une catégorie spécifique d'attaque : le CSRF (Cross-Site Request Forgery) avec lecture de données.

### Scénario protégé par CORS

1. Utilisateur connecté à `votre-banque.com` (cookie de session actif)
2. Utilisateur visite `evil.com` (site malveillant)
3. `evil.com` exécute : `fetch("https://votre-banque.com/solde")`
4. Sans CORS : `evil.com` pourrait lire le solde de l'utilisateur
5. Avec CORS : le navigateur bloque la lecture

### Ce que CORS ne protège pas

Les requêtes "simples" (GET, POST avec form-data) sont quand même envoyées. CORS bloque seulement la lecture de la réponse.

```javascript
// Cette requête est ENVOYÉE au serveur même si CORS bloque la lecture
fetch("https://votre-api.com/virement", {
  method: "POST",
  body: new FormData(document.getElementById("form"))
})
// La réponse est bloquée, mais le virement a peut-être été effectué !
```

## Configuration CORS selon votre architecture

### Blazor WASM - API publique

CORS est **utile** mais **insuffisant**.

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("Production", policy =>
    {
        policy
            // STRICT : uniquement votre domaine
            .WithOrigins("https://votre-app.com")
            // Méthodes nécessaires uniquement
            .WithMethods("GET", "POST", "PUT", "DELETE")
            // Headers nécessaires uniquement
            .WithHeaders("Content-Type", "Authorization")
            // Si vous utilisez des cookies
            .AllowCredentials();
    });
});
```

**Complétez avec :**
- Rate limiting (Nginx + application)
- Authentification/Autorisation
- Validation des entrées
- WAF/CDN

### Blazor Server - API interne

CORS est **inutile** car l'API n'est pas appelée depuis un navigateur.

```csharp
// Dans l'API interne : PAS DE CORS
// Le code est plus simple, la surface d'attaque réduite

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
// Pas de AddCors()

var app = builder.Build();
app.MapControllers();
// Pas de UseCors()
```

## Erreurs courantes

### Erreur 1 : AllowAnyOrigin en production

```csharp
// ❌ DANGEREUX
policy.AllowAnyOrigin();
```

Cela permet à n'importe quel site d'appeler votre API depuis le navigateur d'un utilisateur.

### Erreur 2 : Croire que CORS protège l'API

```csharp
// ❌ FAUX SENTIMENT DE SÉCURITÉ
// "Mon API a CORS donc elle est protégée"
```

Un attaquant contourne CORS en 5 secondes avec curl.

### Erreur 3 : CORS avec AllowCredentials + AllowAnyOrigin

```csharp
// ❌ INTERDIT par les navigateurs (et dangereux)
policy.AllowAnyOrigin().AllowCredentials();
```

Cette combinaison est bloquée par les navigateurs modernes.

### Erreur 4 : Désactiver CORS pour "résoudre" les erreurs

```csharp
// ❌ MAUVAISE SOLUTION
policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
```

Si vous avez des erreurs CORS, configurez correctement au lieu de tout ouvrir.

## Debugging CORS

### Symptômes d'un problème CORS

- Erreur dans la console : `Access to fetch at ... has been blocked by CORS policy`
- Les requêtes échouent depuis le navigateur mais fonctionnent avec curl

### Vérifier les headers

```bash
# Tester les headers CORS
curl -I -X OPTIONS https://votre-api.com/endpoint \
  -H "Origin: https://votre-app.com" \
  -H "Access-Control-Request-Method: POST"
```

Réponse attendue :
```
Access-Control-Allow-Origin: https://votre-app.com
Access-Control-Allow-Methods: GET, POST, PUT, DELETE
Access-Control-Allow-Headers: Content-Type, Authorization
```

## Conclusion

| Situation | CORS nécessaire ? | Suffisant ? |
|-----------|-------------------|-------------|
| API publique (WASM) | Oui | Non |
| API interne (Server) | Non | N/A |
| Protection contre bots | Non applicable | Non |
| Protection DDoS | Non applicable | Non |

**CORS est une couche de protection pour les utilisateurs, pas pour votre serveur.**

Pour protéger votre serveur, utilisez :
- Rate limiting
- Authentification
- WAF/CDN
- Architecture réseau (API interne)
