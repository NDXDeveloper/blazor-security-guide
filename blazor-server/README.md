# Blazor Server - Configuration Sécurisée avec API Interne

Cette configuration illustre l'architecture **recommandée** pour Blazor Server : une API interne non exposée à Internet.

## Architecture

```
Internet
    │
    ▼
┌─────────────────┐
│  Nginx          │  Port 80/443 (seul point d'entrée)
│  (reverse proxy)│
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  Blazor Server  │  Port 5000 (expose, pas ports)
│  (SignalR)      │
└────────┬────────┘
         │
         │ Réseau privé Docker "backend"
         │ (NON EXPOSÉ À INTERNET)
         ▼
┌─────────────────┐
│  API ASP.NET    │  Port 5001 (expose uniquement)
│  Core INTERNE   │
└─────────────────┘
```

## Pourquoi cette architecture est plus sécurisée

### 1. L'API est invisible depuis Internet

L'API n'a pas de `ports` mapping dans docker-compose, seulement `expose`. Elle est accessible **uniquement** depuis le réseau Docker interne.

```yaml
# ❌ MAUVAIS - API accessible depuis Internet
api:
  ports:
    - "5001:5001"

# ✅ BON - API accessible uniquement dans le réseau Docker
api:
  expose:
    - "5001"
```

**Conséquence** : Un attaquant ne peut pas appeler l'API interne (port 5001) directement. Toutes les requêtes métier passent par Blazor Server.

> **Note** : Des endpoints légers (`/health`, `/api/weather`) sont exposés via Nginx pour le monitoring et les tests de charge. Ces endpoints sont protégés par rate limiting.

### 2. Pas besoin de CORS

CORS est une politique navigateur. Puisque l'API n'est jamais appelée depuis un navigateur (uniquement depuis Blazor Server côté serveur), CORS est **inutile**.

```csharp
// Dans l'API interne : pas de configuration CORS nécessaire
// Le code est plus simple et la surface d'attaque réduite
```

### 3. Authentification machine-to-machine

L'API peut utiliser une authentification simplifiée (API key interne, certificat client) puisqu'elle ne communique qu'avec Blazor Server.

## Points de vigilance

### SignalR est le nouveau point d'entrée

Blazor Server utilise SignalR (WebSocket) pour la communication. C'est maintenant le point d'attaque principal.

**Risques :**
- Saturation de connexions WebSocket
- Attaques sur le hub SignalR
- Consommation mémoire (chaque connexion = état serveur)

**Protections :**
- Rate limiting sur les connexions SignalR
- Limites de connexions par IP
- Timeouts agressifs sur les connexions inactives

### Limites de scalabilité

Chaque utilisateur = une connexion SignalR = mémoire serveur.

| Utilisateurs | Mémoire estimée | Recommandation |
|--------------|-----------------|----------------|
| 1 000        | ~500 MB         | OK             |
| 5 000        | ~2.5 GB         | Attention      |
| 10 000+      | 5+ GB           | Envisager WASM |

## Lancer l'exemple

```bash
# Développement
docker-compose up --build

# Production
docker-compose -f docker-compose.yml -f docker-compose.prod.yml up -d
```

## Structure

```
blazor-server/
├── app/                # Application Blazor Server
│   ├── BlazorServer.csproj
│   ├── Program.cs      # Endpoints /health et /api/weather
│   └── Pages/
│
├── api/                # API ASP.NET Core INTERNE
│   ├── Api.csproj
│   ├── Program.cs      # Pas de CORS !
│   └── Controllers/
│
└── docker-compose.yml  # API avec expose, pas ports
```

### Endpoints exposés (via Nginx)

| Endpoint | Usage | Rate limiting |
|----------|-------|---------------|
| `/health` | Health checks (Docker, K8s, monitoring) | 30r/s |
| `/api/weather` | Tests de charge, démonstration | 30r/s, burst 20 |

## Checklist sécurité avant production

- [ ] API interne non exposée (expose, pas ports)
- [ ] Réseau Docker isolé (backend network)
- [ ] Endpoints monitoring (`/health`, `/api/weather`) avec rate limiting
- [ ] Rate limiting SignalR configuré
- [ ] Limites de connexions par IP
- [ ] Timeouts sur connexions inactives
- [ ] HTTPS sur Nginx
- [ ] Headers de sécurité
- [ ] Monitoring des connexions SignalR
- [ ] Alertes sur pic de connexions
- [ ] WAF/CDN en place (Cloudflare recommandé)

## Quand cette architecture devient problématique

- Plus de 10 000 utilisateurs simultanés prévus
- Utilisateurs sur connexions réseau instables (mobile, pays lointains)
- Besoin de fonctionnement offline
- Infrastructure ne supportant pas bien les WebSockets
- Latence réseau élevée entre utilisateurs et serveur

Dans ces cas, considérez Blazor WASM ou une architecture hybride.
