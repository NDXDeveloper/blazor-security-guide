# Guide de Sécurité Blazor en Production

[![Security Tests](https://github.com/NDXDeveloper/blazor-security-guide/actions/workflows/security-tests.yml/badge.svg)](https://github.com/NDXDeveloper/blazor-security-guide/actions/workflows/security-tests.yml)

> Un guide pragmatique et honnête pour sécuriser vos applications Blazor face aux menaces réelles : DDoS, abus d'API, bots, scans automatisés.

**Ce guide n'est pas un discours marketing.** Il explique ce qui protège vraiment, ce qui ne protège pas, et pourquoi Blazor seul ne garantit rien en matière de sécurité.

---

## Table des matières

1. [Comprendre les surfaces d'attaque](#comprendre-les-surfaces-dattaque)
2. [Blazor WASM vs Blazor Server : comparaison sécurité](#blazor-wasm-vs-blazor-server--comparaison-s%C3%A9curit%C3%A9)
3. [Démystification des fausses protections](#d%C3%A9mystification-des-fausses-protections)
4. [Architectures sécurisées](#architectures-s%C3%A9curis%C3%A9es)
5. [Structure du repository](#structure-du-repository)
6. [Conclusion et règles essentielles](#conclusion-et-r%C3%A8gles-essentielles)
7. [Tests de sécurité](#tests-de-s%C3%A9curit%C3%A9)

---

## Comprendre les surfaces d'attaque

### La vérité fondamentale

> **La sécurité ne vient pas de Blazor. Elle vient de l'architecture.**

Blazor est un framework UI. Il ne protège pas votre API, ne bloque pas les attaques DDoS, et ne valide pas magiquement vos entrées côté serveur. Votre application est aussi vulnérable qu'une application Angular, React ou Vue si vous ne mettez pas en place les bonnes protections.

### Surfaces d'attaque communes à toute application web

| Surface | Description | Blazor concerné |
|---------|-------------|-----------------|
| API HTTP | Endpoints REST/GraphQL exposés | WASM (toujours), Server (si API publique) |
| Authentification | Tokens, sessions, credentials | Les deux |
| Injection | SQL, XSS, Command injection | Les deux (côté API) |
| Déni de service | Saturation des ressources | Les deux (différemment) |
| Données sensibles | Exposition de secrets, logs | Les deux |

---

## Blazor WASM vs Blazor Server : comparaison sécurité

### Blazor WebAssembly

```
┌─────────────────────────────────────────────────────────────┐
│                        NAVIGATEUR                           │
│  ┌─────────────────────────────────────────────────────┐    │
│  │              Application Blazor WASM                │    │
│  │         (Code C# compilé en WebAssembly)            │    │
│  └─────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────┘
                              │
                              │ HTTPS (appels API)
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                    API ASP.NET Core                         │
│                    (PUBLIQUE sur Internet)                  │
└─────────────────────────────────────────────────────────────┘
```

**Caractéristiques :**

- Code exécuté **côté client** (navigateur)
- API **forcément publique** et accessible depuis Internet
- Appels HTTP directs depuis le navigateur
- **Bonne résilience au trafic massif** (le serveur ne gère que l'API)
- **Aucune protection intrinsèque** contre l'abus d'API
- Le code client est **téléchargeable et analysable** (pas de secrets côté client)

**Implications sécurité :**

- Toute la logique sensible doit être côté API
- L'API doit implémenter rate limiting, validation, auth
- Un attaquant peut appeler l'API directement (sans passer par l'UI)

### Blazor Server

```
┌─────────────────────────────────────────────────────────────┐
│                        NAVIGATEUR                           │
│  ┌─────────────────────────────────────────────────────┐    │
│  │              Blazor Server (UI légère)              │    │
│  │         Connexion SignalR persistante               │    │
│  └─────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────┘
                              │
                              │ WebSocket (SignalR)
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                    Blazor Server Host                       │
│              (Exécution UI côté serveur)                    │
└─────────────────────────────────────────────────────────────┘
                              │
                              │ Réseau privé Docker
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                    API ASP.NET Core                         │
│                    (INTERNE, non exposée)                   │
└─────────────────────────────────────────────────────────────┘
```

**Caractéristiques :**

- UI exécutée **côté serveur** via SignalR
- **Connexions persistantes** par utilisateur (WebSocket)
- API peut être **interne et non exposée** (réseau privé)
- Surface d'attaque API **réduite** (si API interne)
- **Sensibilité accrue aux attaques par saturation** de connexions

**Implications sécurité :**

- Chaque utilisateur consomme une connexion serveur
- 10 000 connexions simultanées = charge significative
- Protection contre les bots plus facile (pas d'API publique)
- Mais vulnérable au DDoS sur SignalR

---

## Point critique : quand Blazor Server est-il vraiment plus sûr ?

> **Blazor Server n'est plus sûr que si l'API est interne et non exposée.**
>
> Si votre API Blazor Server est publique sur Internet, vous cumulez les inconvénients :
> - La fragilité de SignalR face aux connexions massives
> - L'exposition de l'API aux mêmes attaques que WASM

### Configuration recommandée vs configuration risquée

| Configuration | Sécurité API | Résilience DDoS | Recommandation |
|---------------|--------------|-----------------|----------------|
| WASM + API publique + WAF | Dépend du WAF | Bonne | ✅ Standard |
| Server + API interne | Excellente | Moyenne | ✅ Recommandée |
| Server + API publique | Faible | Faible | ❌ À éviter |

---

## Démystification des fausses protections

### CORS ≠ Sécurité

```
❌ MYTHE : "CORS protège mon API"
✅ RÉALITÉ : CORS est une politique navigateur, pas une sécurité serveur
```

**Ce que CORS fait :**
- Empêche un site malveillant d'appeler votre API depuis le navigateur d'un utilisateur

**Ce que CORS ne fait pas :**
- Ne bloque pas les appels directs (curl, Postman, scripts)
- Ne valide pas l'authentification
- Ne protège pas contre les abus

**Conséquence :**
- CORS est **inutile pour une API interne** (pas de navigateur impliqué)
- CORS est **insuffisant pour une API publique** (protection minimale)

Voir [security/cors.md](security/cors.md) pour les détails.

---

### HTTPS ≠ Protection DDoS

```
❌ MYTHE : "Mon site est en HTTPS donc il est sécurisé"
✅ RÉALITÉ : HTTPS chiffre le transport, rien d'autre
```

**Ce que HTTPS fait :**
- Chiffre les données en transit
- Authentifie le serveur (certificat)
- Empêche le man-in-the-middle

**Ce que HTTPS ne fait pas :**
- Ne limite pas le nombre de requêtes
- Ne bloque pas les bots
- Ne protège pas contre le DDoS

---

### Authentification ≠ Protection réseau

```
❌ MYTHE : "Seuls les utilisateurs authentifiés peuvent accéder à mon API"
✅ RÉALITÉ : L'endpoint de login est public, et c'est lui qui sera attaqué
```

**Vecteurs d'attaque malgré l'authentification :**
- Brute force sur `/login`
- Credential stuffing
- Enumeration d'utilisateurs
- Spam de création de comptes

**Solution :** Rate limiting + CAPTCHA + WAF, pas juste l'auth.

---

### Blazor Server ≠ Sécurité automatique

```
❌ MYTHE : "Blazor Server est plus sécurisé car le code reste sur le serveur"
✅ RÉALITÉ : La sécurité dépend de l'architecture, pas du framework
```

**Blazor Server est vulnérable à :**
- Saturation de connexions SignalR
- Attaques sur les hubs SignalR
- Tout ce qui attaque le serveur web

**Blazor Server est plus sûr uniquement si :**
- L'API est interne (réseau privé)
- SignalR est protégé par un reverse proxy
- Le rate limiting est en place

---

## Architectures sécurisées

### Architecture 1 : Blazor WASM + API publique

```
                    Internet
                        │
                        ▼
              ┌─────────────────┐
              │   CDN / WAF     │  ← Cloudflare, AWS CloudFront
              │  (Filtrage L7)  │
              └────────┬────────┘
                       │
                       ▼
              ┌─────────────────┐
              │     Nginx       │  ← Rate limiting, headers sécurité
              │ (Reverse Proxy) │
              └────────┬────────┘
                       │
          ┌────────────┴────────────┐
          │                         │
          ▼                         ▼
┌─────────────────┐       ┌─────────────────┐
│  Static Files   │       │  API ASP.NET    │
│  (Blazor WASM)  │       │     Core        │
└─────────────────┘       └─────────────────┘
```

**Fichiers de référence :** [blazor-wasm/](blazor-wasm/)

---

### Architecture 2 : Blazor Server + API interne

```
                    Internet
                        │
                        ▼
              ┌─────────────────┐
              │   CDN / WAF     │
              │  (Filtrage L7)  │
              └────────┬────────┘
                       │
                       ▼
              ┌─────────────────┐
              │     Nginx       │
              │ (Reverse Proxy) │
              └────────┬────────┘
                       │
                       ▼
              ┌─────────────────┐
              │  Blazor Server  │
              │                 │
              └────────┬────────┘
                       │
                       │ Réseau privé Docker
                       │ (non exposé à Internet)
                       ▼
              ┌─────────────────┐
              │  API ASP.NET    │
              │  Core INTERNE   │
              └─────────────────┘
```

**Fichiers de référence :** [blazor-server/](blazor-server/)

**Pourquoi `expose` au lieu de `ports` dans Docker Compose ?**

```yaml
# ❌ MAUVAIS : API accessible depuis Internet
api:
  ports:
    - "5000:5000"

# ✅ BON : API accessible uniquement dans le réseau Docker
api:
  expose:
    - "5000"
```

- `ports` : mappe le port sur l'hôte (accessible depuis Internet)
- `expose` : déclare le port pour le réseau Docker interne uniquement

---

## Structure du repository

```
blazor-security-guide/
│
├── README.md                    ← Ce fichier
├── REPO_INFO.md                 ← Métadonnées GitHub
│
├── diagrams/                    ← Diagrammes d'architecture
│   ├── wasm-architecture.md
│   └── server-architecture.md
│
├── blazor-wasm/                 ← Exemple WASM complet
│   ├── frontend/                ← Application Blazor WASM
│   ├── api/                     ← API publique
│   ├── docker-compose.yml
│   └── README.md
│
├── blazor-server/               ← Exemple Server complet
│   ├── app/                     ← Application Blazor Server
│   ├── api/                     ← API interne (non exposée)
│   ├── docker-compose.yml
│   └── README.md
│
├── nginx/                       ← Configurations reverse proxy
│   ├── nginx.conf
│   ├── nginx-wasm.conf
│   ├── nginx-server.conf
│   └── cloudflare.md
│
├── security/                    ← Documentation sécurité
│   ├── cors.md
│   ├── rate-limiting.md
│   ├── ddos.md
│   └── auth-vs-security.md
│
├── tests/                       ← Tests de sécurité
│   ├── api-integration/         ← Tests C# (xUnit)
│   ├── resilience/              ← Tests de charge
│   └── scripts/                 ← Scripts shell pédagogiques
│
├── .github/workflows/           ← CI/CD GitHub Actions
│   └── security-tests.yml
│
└── kubernetes/                  ← Déploiement K8s (si pertinent)
    ├── wasm/
    ├── server/
    └── when-to-use-k8s.md
```

---

## Ce qui protège vraiment une API

| Protection | Efficacité | Implémentation |
|------------|------------|----------------|
| **WAF (Cloudflare, AWS WAF)** | Haute | Infrastructure |
| **Rate limiting** | Haute | Nginx + Application |
| **Validation côté serveur** | Critique | Code |
| **Authentification + Autorisation** | Critique | Code |
| **Réseau privé (API interne)** | Très haute | Architecture |
| **Monitoring + Alerting** | Haute | Infrastructure |

---

## Conclusion et règles essentielles

### Ce qui protège vraiment

1. **WAF/CDN** en première ligne (Cloudflare, AWS CloudFront)
2. **Rate limiting** à plusieurs niveaux (Nginx + application)
3. **API interne** quand possible (Blazor Server)
4. **Validation systématique** côté serveur
5. **Monitoring** et alertes sur les anomalies

### Ce qui ne protège pas

1. CORS seul
2. HTTPS seul
3. Authentification sans rate limiting
4. Blazor Server avec API publique
5. "Security through obscurity"

### Quand choisir Blazor WASM

- Application grand public avec beaucoup d'utilisateurs simultanés
- Besoin de fonctionner offline (PWA)
- API déjà existante et sécurisée
- Équipe familière avec les SPA classiques
- Budget pour un WAF/CDN robuste

### Quand choisir Blazor Server

- Application métier avec utilisateurs connus
- Nombre de connexions simultanées maîtrisé (< 10 000)
- Besoin de protéger la logique métier
- API doit rester interne
- Latence réseau acceptable

### Quand Blazor Server devient risqué

- Plus de 10 000 connexions simultanées prévues
- API exposée publiquement (annule l'avantage)
- Pas de protection SignalR (rate limiting, WAF)
- Infrastructure ne supportant pas les WebSockets
- Utilisateurs sur connexions instables

---

## Tests de sécurité

> **Si un point de sécurité n'est pas testable, il est discutable.**

Ce repository inclut des tests qui **prouvent** les affirmations du guide.

### Types de tests

| Type | Objectif | Localisation |
|------|----------|--------------|
| Intégration API | Vérifier que l'API fonctionne comme prévu | `tests/api-integration/` |
| Rate limiting | Prouver que les limites sont effectives | `tests/api-integration/` |
| Scripts shell | Démontrer les concepts de manière pédagogique | `tests/scripts/` |
| Résilience | Comportement sous charge | `tests/resilience/` |

### Exécution rapide

```bash
# Tests C#
cd tests/api-integration
dotnet test

# Scripts shell (après docker-compose up)
cd tests/scripts
chmod +x *.sh
./test-cors-myth.sh      # Prouver que CORS ≠ sécurité
./test-rate-limiting.sh  # Vérifier le rate limiting
./test-internal-api.sh   # Prouver l'isolation réseau
```

### Ce que les tests démontrent

| Test | Résultat attendu |
|------|------------------|
| API WASM accessible via curl | ✅ 200 OK (normal, API publique) |
| API Server (port 5001) accessible via curl | ❌ Connection refused (isolée) |
| Endpoint `/health` Server | ✅ 200 OK (monitoring) |
| Endpoint `/api/weather` Server | ✅ 200 OK ou 429 (tests de charge) |
| Requête avec Origin malveillant | ✅ 200 OK (CORS ne bloque pas le serveur) |
| 150 requêtes rapides | 429 après ~100 (rate limiting actif) |

### CI/CD

Les tests s'exécutent automatiquement via GitHub Actions à chaque push.

```yaml
# .github/workflows/security-tests.yml
- Build et tests unitaires
- Tests Docker WASM (API publique)
- Tests Docker Server (API interne + endpoints monitoring)
- Tests de résilience
```

Voir [tests/README.md](tests/README.md) pour la documentation complète.

---

## Ressources

- [Documentation officielle Blazor](https://docs.microsoft.com/aspnet/core/blazor/)
- [OWASP Top 10](https://owasp.org/www-project-top-ten/)
- [Cloudflare - Protection DDoS](https://www.cloudflare.com/ddos/)
- [ASP.NET Core Security](https://docs.microsoft.com/aspnet/core/security/)

---

## Auteur

**Nicolas DEOUX**

- Email : NDXDev@gmail.com
- LinkedIn : [nicolas-deoux](https://www.linkedin.com/in/nicolas-deoux-ab295980/)

---

## Licence

MIT License - Voir [LICENSE](LICENSE)
