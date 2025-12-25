# Tests de Sécurité - Blazor Security Guide

> **Si un point de sécurité n'est pas testable, il est discutable.**

Ce dossier contient des tests qui **prouvent** les affirmations du guide, pas des tests unitaires gadget.

## Philosophie des tests

### Ce qu'on teste

| Type de test | Objectif | Outil |
|--------------|----------|-------|
| Intégration API | Vérifier que l'API fonctionne comme prévu | xUnit + WebApplicationFactory |
| Rate limiting | Prouver que les limites sont effectives | xUnit + scripts shell |
| Sécurité réseau | Démontrer l'isolation API interne | Scripts shell + Docker |
| Résilience | Comportement sous charge | hey/ab + scripts |

### Ce qu'on ne teste PAS

- Tests unitaires sur des contrôleurs triviaux
- Mocks partout
- Couverture artificielle

## Structure

```
tests/
├── README.md                    ← Ce fichier
│
├── api-integration/             ← Tests d'intégration C#
│   ├── SecurityTests.csproj
│   ├── WasmApiTests.cs          ← API WASM (publique)
│   ├── ServerInternalApiTests.cs← API Server (interne)
│   └── RateLimitingTests.cs     ← Tests rate limiting
│
├── resilience/                  ← Tests de charge
│   ├── ResilienceTests.csproj
│   └── LoadTests.cs
│
└── scripts/                     ← Scripts shell pédagogiques
    ├── README.md
    ├── test-public-api.sh       ← Tester l'API WASM
    ├── test-internal-api.sh     ← Prouver que l'API Server est interne
    ├── test-rate-limiting.sh    ← Tester les limites
    ├── test-cors-myth.sh        ← Démontrer que CORS ≠ sécurité
    └── flood-test.sh            ← Test de résilience basique
```

## Exécution rapide

### Tests automatisés (C#)

```bash
# Depuis la racine du repo
cd tests/api-integration
dotnet test

cd ../resilience
dotnet test
```

### Scripts shell (pédagogiques)

```bash
# Depuis le dossier scripts
cd tests/scripts

# Rendre exécutables
chmod +x *.sh

# Tester l'API publique WASM
./test-public-api.sh

# Prouver que l'API interne n'est pas accessible
./test-internal-api.sh

# Démontrer que CORS ne protège rien
./test-cors-myth.sh

# Tester le rate limiting
./test-rate-limiting.sh

# Test de charge basique
./flood-test.sh
```

## Prérequis

### Pour les tests C#

- .NET 10 SDK
- Docker et Docker Compose

### Pour les scripts shell

- curl
- jq (optionnel, pour le formatage JSON)
- hey ou ab (pour les tests de charge)

Installation sur Ubuntu :
```bash
sudo apt install curl jq
# hey (Go HTTP load generator)
go install github.com/rakyll/hey@latest
# ou ab (Apache Benchmark)
sudo apt install apache2-utils
```

## Ce que les tests démontrent

### 1. API WASM est publique

```bash
# N'importe qui peut appeler l'API
curl https://votre-api.com/api/weather
# → Réponse 200 OK
```

### 2. API Server : interne vs endpoints monitoring

```bash
# API métier interne (port 5001) : inaccessible depuis l'extérieur
curl http://localhost:5001/api/weather
# → Connection refused (port non exposé)

# Endpoints monitoring Blazor Server (via Nginx) : accessibles
curl https://votre-app.com/health
# → 200 OK {"status":"healthy",...}

curl https://votre-app.com/api/weather
# → 200 OK ou 429 (rate limited)

# Depuis le réseau Docker : API interne fonctionne
docker exec blazor-server curl http://api:5001/api/weather
# → Réponse 200 OK
```

### 3. CORS ne protège rien

```bash
# curl ignore CORS (pas de navigateur)
curl -H "Origin: https://evil.com" https://votre-api.com/api/weather
# → Réponse 200 OK (CORS n'a rien bloqué côté serveur)
```

### 4. Rate limiting fonctionne

```bash
# 10 requêtes : OK
for i in {1..10}; do curl -s -o /dev/null -w "%{http_code}\n" http://localhost/api/weather; done
# → 200 200 200 200 200 200 200 200 200 200

# 11ème requête : bloquée
curl -s -o /dev/null -w "%{http_code}\n" http://localhost/api/weather
# → 429
```

### 5. Blazor Server consomme plus de ressources

```bash
# 100 connexions simultanées
# WASM : ~50 MB RAM serveur
# Server : ~500 MB RAM serveur (circuits SignalR)
```

## Résultats attendus

| Test | WASM | Server |
|------|------|--------|
| API métier accessible curl externe | ✅ 200 | ❌ Connection refused (port 5001) |
| Endpoint `/health` accessible | ✅ 200 | ✅ 200 |
| Endpoint `/api/weather` accessible | ✅ 200 | ✅ 200 ou 429 (monitoring) |
| API accessible réseau Docker | ✅ 200 | ✅ 200 |
| Rate limit après X requêtes | ✅ 429 | ✅ 429 |
| Mémoire sous 100 connexions | ~50 MB | ~500 MB |
| Connexions persistantes | ❌ Non | ✅ Oui |

## CI/CD

Les tests sont exécutés automatiquement via GitHub Actions à chaque push.

Voir `.github/workflows/security-tests.yml`
