# Blazor WebAssembly - Configuration Sécurisée

Cette configuration illustre une architecture Blazor WASM avec API publique sécurisée.

## Architecture

```
Internet
    │
    ▼
┌─────────────────┐
│  Nginx          │  Port 80/443
│  (reverse proxy)│
└────────┬────────┘
         │
    ┌────┴────┐
    │         │
    ▼         ▼
┌────────┐  ┌───────┐
│ WASM   │  │  API  │
│(static)│  │ :5000 │
└────────┘  └───────┘
```

## Points clés de sécurité

### 1. L'API est publique - elle DOIT être protégée

Contrairement à Blazor Server où l'API peut être interne, ici l'API est directement accessible depuis Internet. Toute la sécurité repose sur :

- **Rate limiting** au niveau Nginx ET application
- **Validation** de toutes les entrées côté serveur
- **Authentification/Autorisation** robuste
- **CORS** configuré strictement (mais ce n'est pas une sécurité suffisante)

### 2. Le code client est téléchargeable

N'importe qui peut :
- Télécharger les DLL Blazor
- Les décompiler avec ILSpy ou dotPeek
- Analyser la logique

**Conséquence** : Aucun secret dans le code client. Jamais.

### 3. Les appels API sont visibles

Tous les appels HTTP sont visibles dans les DevTools du navigateur.

## Lancer l'exemple

```bash
# Développement
docker-compose up --build

# Production
docker-compose -f docker-compose.yml -f docker-compose.prod.yml up -d
```

## Structure

```
blazor-wasm/
├── frontend/           # Application Blazor WASM
│   ├── BlazorWasm.csproj
│   ├── Program.cs
│   ├── wwwroot/
│   └── Pages/
│
├── api/               # API ASP.NET Core (PUBLIQUE)
│   ├── Api.csproj
│   ├── Program.cs
│   └── Controllers/
│
└── docker-compose.yml
```

## Checklist sécurité avant production

- [ ] Rate limiting configuré (Nginx + ASP.NET Core)
- [ ] CORS restrictif (uniquement votre domaine)
- [ ] HTTPS forcé
- [ ] Headers de sécurité (CSP, X-Frame-Options, etc.)
- [ ] Validation de toutes les entrées côté API
- [ ] Authentification sur tous les endpoints sensibles
- [ ] Logs des tentatives suspectes
- [ ] WAF/CDN en place (Cloudflare recommandé)
- [ ] Pas de secrets dans le code WASM
- [ ] Pas d'informations sensibles dans les messages d'erreur
