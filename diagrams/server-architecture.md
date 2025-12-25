# Architecture Blazor Server - Diagrammes

## Vue d'ensemble

```
┌──────────────────────────────────────────────────────────────────────────────┐
│                              INTERNET                                        │
│                                                                              │
│   ┌─────────┐    ┌─────────┐    ┌─────────┐    ┌─────────┐                   │
│   │ Browser │    │ Browser │    │   Bot   │    │  Curl   │                   │
│   └────┬────┘    └────┬────┘    └────┬────┘    └────┬────┘                   │
│        │              │              │              │                        │
│        │ SignalR      │ SignalR      │              │                        │
│        │ (WebSocket)  │ (WebSocket)  │              │                        │
│        └──────────────┴──────────────┴──────────────┘                        │
│                              │                                               │
└──────────────────────────────┼───────────────────────────────────────────────┘
                               │
                               ▼
                 ┌─────────────────────────────┐
                 │       CDN / WAF             │
                 │      (Cloudflare)           │
                 │                             │
                 │  • Protection DDoS          │
                 │  • WebSocket support ON     │
                 │  • Connection limits        │
                 └──────────────┬──────────────┘
                                │
                                ▼
┌──────────────────────────────────────────────────────────────────────────────┐
│                           VOTRE INFRASTRUCTURE                               │
│                                                                              │
│                 ┌─────────────────────────────┐                              │
│                 │          Nginx              │                              │
│                 │     (Reverse Proxy)         │                              │
│                 │                             │                              │
│                 │  • WebSocket proxy          │                              │
│                 │  • Connection rate limit    │                              │
│                 │  • Sticky sessions (opt)    │                              │
│                 └──────────────┬──────────────┘                              │
│                                │                                             │
│                                │                                             │
│                                ▼                                             │
│                 ┌─────────────────────────────┐                              │
│                 │      Blazor Server          │                              │
│                 │                             │                              │
│                 │  • UI côté serveur          │                              │
│                 │  • Hub SignalR              │                              │
│                 │  • Circuits par user        │                              │
│                 │  • HttpClient vers API      │                              │
│                 └──────────────┬──────────────┘                              │
│                                │                                             │
│                                │ Réseau Docker INTERNE                       │
│                                │ (network: backend, internal: true)          │
│                                │                                             │
│                                ▼                                             │
│                 ┌─────────────────────────────┐                              │
│                 │    API ASP.NET Core         │                              │
│                 │       (INTERNE)             │                              │
│                 │                             │                              │
│                 │  • NON exposée à Internet   │                              │
│                 │  • Pas de CORS nécessaire   │                              │
│                 │  • Auth machine-to-machine  │                              │
│                 └─────────────────────────────┘                              │
│                                                                              │
│   ═══════════════════════════════════════════════════════════════════════    │
│        ▲                                                                     │
│        │ L'API n'est accessible que depuis Blazor Server                     │
│        │ Pas de route /api dans Nginx                                        │
│        │ Pas de ports mapping dans Docker                                    │
│                                                                              │
└──────────────────────────────────────────────────────────────────────────────┘
```

## Connexion SignalR - Détail

```
Browser                                        Blazor Server
   │                                                 │
   │  HTTP GET /_blazor/negotiate                    │
   │ ───────────────────────────────────────────────►│
   │                                                 │
   │  ◄───────────────────────────────────────────── │
   │  { connectionId: "abc", transports: [...] }     │
   │                                                 │
   │  WebSocket UPGRADE /_blazor?id=abc              │
   │ ───────────────────────────────────────────────►│
   │                                                 │
   │  ◄─────────────────────────────────────────────►│
   │         Connexion WebSocket établie             │
   │                                                 │
   │  ────── RPC Calls (UI events) ─────────────────►│
   │                                                 │
   │  ◄───── DOM Diffs (UI updates) ───────────────  │
   │                                                 │
   │       [Connexion maintenue pendant session]     │
   │                                                 │
```

## Appel API interne

```
Browser          Blazor Server                       API (interne)
   │                   │                                  │
   │  [Click button]   │                                  │
   │ ─────────────────►│                                  │
   │                   │                                  │
   │                   │  HTTP GET http://api:5001/data   │
   │                   │ ────────────────────────────────►│
   │                   │  (réseau Docker privé)           │
   │                   │                                  │
   │                   │  ◄────────────────────────────── │
   │                   │     JSON response                │
   │                   │                                  │
   │  ◄─────────────── │                                  │
   │  [UI update via   │                                  │
   │   SignalR]        │                                  │
```

## Attaque DDoS - Comparaison avec WASM

### Blazor WASM (API exposée)

```
Attacker
   │
   │  POST /api/data  ──────────────────────────────────────► API
   │  POST /api/data  ──────────────────────────────────────► API
   │  POST /api/data  ──────────────────────────────────────► API
   │  (attaque directe sur l'API)
```

### Blazor Server (API interne)

```
Attacker
   │
   │  POST /api/data  ────────► Nginx ────────► ???
   │  (pas de route /api)                        │
   │                                             ▼
   │                                     404 Not Found
   │
   │  L'attaquant doit attaquer SignalR
   │
   │  WebSocket FLOOD /_blazor ───────────────────────────► Blazor Server
   │  WebSocket FLOOD /_blazor ───────────────────────────► Blazor Server
   │                                                              │
   │                                                              │
   │  [Chaque connexion consomme des ressources serveur]          │
   │  [Plus facile à saturer, mais API reste protégée]            │
```

## Réseaux Docker

```
┌──────────────────────────────────────────────────────────────────────────────┐
│                            docker-compose.yml                                │
├──────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│   ┌────────────────────────────────────────────────────────────────────┐     │
│   │                    Network: frontend                               │     │
│   │                                                                    │     │
│   │   ┌─────────────────┐        ┌─────────────────┐                   │     │
│   │   │     nginx       │        │  blazor-server  │                   │     │
│   │   │                 │        │                 │                   │     │
│   │   │  ports:         │        │  expose:        │                   │     │
│   │   │   - 80:80       │───────►│   - 5000        │                   │     │
│   │   │   - 443:443     │        │                 │                   │     │
│   │   │                 │        │  networks:      │                   │     │
│   │   │                 │        │   - frontend    │                   │     │
│   │   │                 │        │   - backend     │───────┐           │     │
│   │   └─────────────────┘        └─────────────────┘       │           │     │
│   │                                                        │           │     │
│   └────────────────────────────────────────────────────────┼───────────┘     │
│                                                            │                 │
│   ┌────────────────────────────────────────────────────────┼───────────┐     │
│   │                    Network: backend (internal: true)   │           │     │
│   │                                                        │           │     │
│   │   ┌─────────────────┐                                  │           │     │
│   │   │       api       │◄─────────────────────────────────┘           │     │
│   │   │                 │                                              │     │
│   │   │  expose:        │   ← PAS de "ports" !                         │     │
│   │   │   - 5001        │   ← Uniquement accessible depuis backend     │     │
│   │   │                 │                                              │     │
│   │   │  networks:      │                                              │     │
│   │   │   - backend     │   ← UNIQUEMENT backend, pas frontend         │     │
│   │   │                 │                                              │     │
│   │   └─────────────────┘                                              │     │
│   │                                                                    │     │
│   │   ⚠️  internal: true → pas de gateway vers Internet                │     │
│   │                                                                    │     │
│   └────────────────────────────────────────────────────────────────────┘     │
│                                                                              │
└──────────────────────────────────────────────────────────────────────────────┘
```

## Consommation de ressources

```
                    Mémoire par connexion SignalR

     │
1 GB │                                          ┌────────────────
     │                                     ┌────┘
     │                                ┌────┘
     │                           ┌────┘
512MB│                      ┌────┘
     │                 ┌────┘
     │            ┌────┘
256MB│       ┌────┘
     │  ┌────┘
     │──┘
     └──────────────────────────────────────────────────────────►
        1000  2000  3000  4000  5000  6000  7000  8000  9000  10000
                          Connexions simultanées

     ┌────────────────────────────────────────────────────────────┐
     │ Règle empirique : ~50-100 KB par circuit Blazor            │
     │ 5000 users ≈ 250-500 MB de mémoire juste pour les circuits │
     │ + overhead SignalR, GC, etc.                               │
     └────────────────────────────────────────────────────────────┘
```

## Comparaison des surfaces d'attaque

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         BLAZOR WASM                                         │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│   Surfaces d'attaque :                                                      │
│   ┌─────────────────┐   ┌─────────────────┐                                 │
│   │  Static Files   │   │   API publique  │                                 │
│   │  (faible risque)│   │  (HAUT risque)  │                                 │
│   └─────────────────┘   └─────────────────┘                                 │
│                                                                             │
│   • API accessible directement depuis Internet                              │
│   • Nécessite protection robuste (WAF, rate limiting, auth)                 │
│   • Mais : bonne résilience, pas d'état serveur                             │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│                         BLAZOR SERVER                                       │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│   Surfaces d'attaque :                                                      │
│   ┌─────────────────┐   ┌─────────────────┐   ┌─────────────────┐           │
│   │    SignalR      │   │  Static Files   │   │  API interne    │           │
│   │  (MOYEN risque) │   │  (faible risque)│   │  (PROTÉGÉE)     │           │
│   └─────────────────┘   └─────────────────┘   └─────────────────┘           │
│                                                                             │
│   • API non accessible depuis Internet                                      │
│   • Mais : SignalR peut être saturé (connexions)                            │
│   • Ressources serveur par utilisateur                                      │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Résumé

| Aspect | Blazor WASM | Blazor Server |
|--------|-------------|---------------|
| API | Publique, exposée | Interne, protégée |
| Point d'attaque principal | API REST | SignalR |
| Résilience DDoS | Bonne (stateless) | Moyenne (stateful) |
| Ressources serveur | Faibles | ~50-100 KB/user |
| Scalabilité | Excellente | Limitée |
| Protection API | Complexe | Simple (isolation) |
