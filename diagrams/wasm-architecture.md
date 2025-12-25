# Architecture Blazor WebAssembly - Diagrammes

## Vue d'ensemble

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              INTERNET                                       │
│                                                                             │
│   ┌─────────┐    ┌─────────┐    ┌─────────┐    ┌─────────┐                  │
│   │ Browser │    │ Browser │    │   Bot   │    │  Curl   │                  │
│   └────┬────┘    └────┬────┘    └────┬────┘    └────┬────┘                  │
│        │              │              │              │                       │
│        └──────────────┴──────────────┴──────────────┘                       │
│                              │                                              │
└──────────────────────────────┼──────────────────────────────────────────────┘
                               │
                               ▼
                 ┌─────────────────────────────┐
                 │       CDN / WAF             │
                 │      (Cloudflare)           │
                 │                             │
                 │  • Protection DDoS L3/L4/L7 │
                 │  • Rate limiting            │
                 │  • Bot detection            │
                 │  • SSL termination          │
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
│                 │  • Rate limiting local      │                              │
│                 │  • Headers sécurité         │                              │
│                 │  • Routing                  │                              │
│                 └──────────────┬──────────────┘                              │
│                                │                                             │
│               ┌────────────────┴────────────────┐                            │
│               │                                 │                            │
│               ▼                                 ▼                            │
│   ┌─────────────────────────┐     ┌─────────────────────────┐                │
│   │     Fichiers WASM       │     │    API ASP.NET Core     │                │
│   │      (Statiques)        │     │       (PUBLIQUE)        │                │
│   │                         │     │                         │                │
│   │  • blazor.webassembly.js│     │  • Rate limiting app    │                │
│   │  • *.dll                │     │  • Validation           │                │
│   │  • *.wasm               │     │  • Auth/Authz           │                │
│   │  • index.html           │     │  • Logging              │                │
│   └─────────────────────────┘     └─────────────────────────┘                │
│                                                                              │
└──────────────────────────────────────────────────────────────────────────────┘
```

## Flux de requête - Chargement initial

```
Browser                    CDN           Nginx         Static Files
   │                        │              │                │
   │  GET /                 │              │                │
   │ ──────────────────────►│              │                │
   │                        │  (cache HIT) │                │
   │  ◄─────────────────────│              │                │
   │    index.html          │              │                │
   │                        │              │                │
   │  GET /blazor.wasm      │              │                │
   │ ──────────────────────►│              │                │
   │                        │  (cache HIT) │                │
   │  ◄─────────────────────│              │                │
   │    blazor.wasm (6MB)   │              │                │
   │                        │              │                │
   │  [App Blazor démarre]  │              │                │
   │                        │              │                │
```

## Flux de requête - Appel API

```
Browser                    CDN           Nginx              API
   │                        │              │                  │
   │  GET /api/data         │              │                  │
   │ ──────────────────────►│              │                  │
   │                        │              │                  │
   │                        │  Forward     │                  │
   │                        │ ────────────►│                  │
   │                        │              │                  │
   │                        │              │  Rate limit OK?  │
   │                        │              │ ────────────────►│
   │                        │              │                  │
   │                        │              │  ◄───────────────│
   │                        │              │     Response     │
   │                        │  ◄───────────│                  │
   │  ◄─────────────────────│              │                  │
   │    JSON response       │              │                  │
   │                        │              │                  │
```

## Flux d'attaque - DDoS sur API

```
Attacker                   CDN           Nginx              API
   │                        │              │                  │
   │  1000 req/s /api/data  │              │                  │
   │ ──────────────────────►│              │                  │
   │                        │              │                  │
   │  [CDN détecte pattern] │              │                  │
   │  [Rate limit CDN]      │              │                  │
   │                        │              │                  │
   │  ◄─────────────────────│              │                  │
   │    429 Too Many Req    │              │                  │
   │                        │              │                  │
   │  [Si CDN contourné]    │              │                  │
   │                        │  Forward     │                  │
   │                        │ ────────────►│                  │
   │                        │              │                  │
   │                        │              │  [Rate limit]    │
   │                        │  ◄───────────│                  │
   │                        │    429       │                  │
   │  ◄─────────────────────│              │                  │
   │    429 Too Many Req    │              │                  │
   │                        │              │                  │
   │  [Si Nginx contourné]  │              │                  │
   │                        │              │ ────────────────►│
   │                        │              │                  │
   │                        │              │  [App rate limit]│
   │                        │              │  ◄───────────────│
   │                        │              │     429          │
   │                        │  ◄───────────│                  │
   │  ◄─────────────────────│              │                  │
   │    429 Too Many Req    │              │                  │
```

## Points d'attaque et protections

```
                                    POINTS D'ATTAQUE
                                          │
    ┌─────────────────────────────────────┼───────────────────────────────────┐
    │                                     │                                   │
    ▼                                     ▼                                   ▼
┌───────────────────┐         ┌───────────────────┐         ┌───────────────────┐
│   DDoS Réseau     │         │   DDoS Applicatif │         │   Abus d'API      │
│   (L3/L4)         │         │   (L7)            │         │                   │
├───────────────────┤         ├───────────────────┤         ├───────────────────┤
│ SYN Flood         │         │ HTTP Flood        │         │ Brute force login │
│ UDP Flood         │         │ Slowloris         │         │ Scraping          │
│ Amplification     │         │ POST Flood        │         │ Enumeration       │
└─────────┬─────────┘         └─────────┬─────────┘         └─────────┬─────────┘
          │                             │                             │
          ▼                             ▼                             ▼
┌───────────────────┐         ┌───────────────────┐         ┌───────────────────┐
│   PROTECTION      │         │   PROTECTION      │         │   PROTECTION      │
├───────────────────┤         ├───────────────────┤         ├───────────────────┤
│ CDN/WAF           │         │ Rate limiting     │         │ Rate limit strict │
│ (Cloudflare)      │         │ WAF rules         │         │ CAPTCHA           │
│                   │         │ Timeouts          │         │ Auth + Authz      │
└───────────────────┘         └───────────────────┘         └───────────────────┘
```

## Configuration Docker

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                            docker-compose.yml                               │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│   ┌─────────────────────────────────────────────────────────────────────┐   │
│   │                         Network: frontend                           │   │
│   │                                                                     │   │
│   │   ┌─────────────────┐              ┌─────────────────┐              │   │
│   │   │     nginx       │              │       api       │              │   │
│   │   │                 │              │                 │              │   │
│   │   │  ports:         │   expose     │  expose:        │              │   │
│   │   │   - 80:80       │ ────────────►│   - 5000        │              │   │
│   │   │   - 443:443     │              │                 │              │   │
│   │   │                 │              │  (pas de ports) │              │   │
│   │   └─────────────────┘              └─────────────────┘              │   │
│   │         │                                                           │   │
│   │         │ Volumes                                                   │   │
│   │         ▼                                                           │   │
│   │   ┌─────────────────┐                                               │   │
│   │   │  Static files   │                                               │   │
│   │   │  (WASM)         │                                               │   │
│   │   └─────────────────┘                                               │   │
│   │                                                                     │   │
│   └─────────────────────────────────────────────────────────────────────┘   │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Résumé des responsabilités

| Composant | Responsabilité | Configuré dans |
|-----------|----------------|----------------|
| CDN/WAF | DDoS, Bot detection, SSL | Cloudflare dashboard |
| Nginx | Rate limiting, Headers, Routing | nginx-wasm.conf |
| API | Auth, Validation, Rate limit métier | Program.cs |
| WASM | UI uniquement | Aucune sécurité ici |
