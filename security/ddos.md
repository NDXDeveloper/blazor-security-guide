# Protection DDoS - Ce qui fonctionne vraiment

## Types d'attaques DDoS

### Couche 3/4 (Réseau/Transport)

| Type | Description | Cible |
|------|-------------|-------|
| SYN Flood | Saturation de connexions TCP semi-ouvertes | Serveur |
| UDP Flood | Inondation de paquets UDP | Bande passante |
| ICMP Flood | Ping flood | Routeurs/Serveur |
| Amplification | DNS/NTP reflétés et amplifiés | Bande passante |

**Protection :** Votre hébergeur ou CDN (pas vous directement).

### Couche 7 (Application)

| Type | Description | Cible |
|------|-------------|-------|
| HTTP Flood | Requêtes HTTP massives | Serveur web |
| Slowloris | Connexions lentes maintenues ouvertes | Serveur web |
| POST Flood | Gros payloads POST répétés | API |
| WebSocket Flood | Saturation de connexions WS | SignalR |

**Protection :** Rate limiting + WAF + architecture.

## Vulnérabilités spécifiques Blazor

### Blazor WASM

```
Attaque DDoS sur API
        │
        ▼
┌─────────────────┐
│   API publique  │ ← Point d'entrée unique
└─────────────────┘
```

**Risque :** Moyen-élevé. L'API est exposée directement.

**Protection :**
- CDN/WAF en première ligne
- Rate limiting strict
- Scaling horizontal possible

### Blazor Server

```
Attaque DDoS sur SignalR
        │
        ▼
┌─────────────────┐
│  Blazor Server  │ ← 1 connexion = ressources serveur
└─────────────────┘
        │
        ▼
┌─────────────────┐
│   API interne   │ ← Protégée (non exposée)
└─────────────────┘
```

**Risque :** Élevé pour SignalR. Chaque connexion consomme :
- Mémoire (~50-100 KB par circuit)
- Threads
- Descripteurs de fichiers

**Protection :**
- Limite de connexions par IP
- Timeouts agressifs
- Scaling plus difficile (état par connexion)

## Architecture de protection

### Niveau 1 : CDN/WAF (Obligatoire en production)

```
Internet
    │
    ▼
┌─────────────────┐
│   Cloudflare    │  ← Absorbe 99% du trafic malveillant
│   (ou AWS WAF)  │
└────────┬────────┘
         │
         │ Trafic filtré
         ▼
     Votre serveur
```

**Pourquoi un CDN/WAF ?**
- Capacité réseau énorme (Cloudflare : 296 Tbps)
- Détection d'anomalies automatique
- Absorption du trafic avant votre serveur
- Filtrage L3/L4 inclus

### Niveau 2 : Nginx (Defense in depth)

```nginx
# Limites de connexion
limit_conn_zone $binary_remote_addr zone=conn:10m;
limit_conn conn 20;  # Max 20 connexions par IP

# Limite de requêtes
limit_req_zone $binary_remote_addr zone=req:10m rate=10r/s;
limit_req zone=req burst=20 nodelay;

# Timeouts agressifs (anti-Slowloris)
client_body_timeout 10s;
client_header_timeout 10s;
keepalive_timeout 30s;
send_timeout 10s;

# Taille max des requêtes
client_max_body_size 10M;
client_body_buffer_size 128k;

# Limiter les headers
large_client_header_buffers 4 8k;
```

### Niveau 3 : Application

```csharp
// Configuration SignalR anti-DDoS
builder.Services.AddSignalR(options =>
{
    // Taille max des messages
    options.MaximumReceiveMessageSize = 32 * 1024; // 32 KB

    // Keepalive court
    options.KeepAliveInterval = TimeSpan.FromSeconds(10);

    // Timeout client agressif
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
});

// Limites de circuits Blazor Server
builder.Services.AddServerSideBlazor(options =>
{
    // Circuits déconnectés gardés peu de temps
    options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(1);

    // Nombre max de circuits déconnectés
    options.DisconnectedCircuitMaxRetained = 50;
});
```

## Réponse à une attaque DDoS

### Phase 1 : Détection

Signes d'une attaque :
- Latence soudainement élevée
- Taux d'erreurs 5xx en hausse
- Consommation CPU/RAM anormale
- Logs montrant beaucoup de 429/503
- Alertes monitoring

### Phase 2 : Réaction immédiate

1. **Activer le mode "Under Attack"** (Cloudflare)
   ```
   Dashboard → Overview → Under Attack Mode → On
   ```

2. **Vérifier les logs** pour identifier les patterns
   ```bash
   # Top IPs
   cat /var/log/nginx/access.log | awk '{print $1}' | sort | uniq -c | sort -rn | head -20

   # Top endpoints ciblés
   cat /var/log/nginx/access.log | awk '{print $7}' | sort | uniq -c | sort -rn | head -20
   ```

3. **Bloquer les IPs/ranges suspects** si pattern clair
   ```nginx
   # Temporairement dans nginx
   deny 1.2.3.0/24;
   ```

### Phase 3 : Mitigation durable

1. Analyser les logs pour comprendre l'attaque
2. Ajuster les règles WAF
3. Renforcer le rate limiting si nécessaire
4. Documenter l'incident

## Dimensionnement pour résister

### Blazor WASM

| Utilisateurs simultanés | Infrastructure recommandée |
|-------------------------|----------------------------|
| < 1 000 | 1 serveur + Cloudflare |
| 1 000 - 10 000 | 2+ serveurs + load balancer + Cloudflare |
| 10 000 - 100 000 | K8s + auto-scaling + Cloudflare Pro |
| > 100 000 | Architecture distribuée + Cloudflare Enterprise |

### Blazor Server

| Utilisateurs simultanés | Infrastructure recommandée |
|-------------------------|----------------------------|
| < 500 | 1 serveur (2 CPU, 4 GB RAM) + Cloudflare |
| 500 - 2 000 | 1 serveur (4 CPU, 8 GB RAM) + Cloudflare |
| 2 000 - 5 000 | 2+ serveurs + sticky sessions + Redis |
| 5 000 - 10 000 | Considérer migration vers WASM |
| > 10 000 | WASM recommandé |

**Note :** Blazor Server scale mal à cause des connexions persistantes.

## Tests de charge

### Outils recommandés

- **k6** : Simple, scriptable, bon pour HTTP
- **Artillery** : Supporte WebSocket (SignalR)
- **Locust** : Python, flexible

### Exemple k6 (API)

```javascript
// load-test.js
import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  stages: [
    { duration: '1m', target: 100 },  // Montée à 100 users
    { duration: '5m', target: 100 },  // Maintien
    { duration: '1m', target: 500 },  // Pic à 500
    { duration: '2m', target: 500 },  // Maintien du pic
    { duration: '1m', target: 0 },    // Descente
  ],
  thresholds: {
    http_req_duration: ['p(95)<500'],  // 95% < 500ms
    http_req_failed: ['rate<0.01'],    // < 1% d'erreurs
  },
};

export default function () {
  const res = http.get('https://votre-api.com/api/weather');
  check(res, {
    'status is 200': (r) => r.status === 200,
    'response time < 500ms': (r) => r.timings.duration < 500,
  });
  sleep(1);
}
```

```bash
k6 run load-test.js
```

### Exemple Artillery (SignalR)

```yaml
# signalr-test.yml
config:
  target: "wss://votre-app.com"
  phases:
    - duration: 60
      arrivalRate: 10
  engines:
    ws: {}

scenarios:
  - engine: ws
    flow:
      - connect:
          path: "/_blazor"
      - think: 5
      - send:
          payload: '{"protocol":"json","version":1}'
      - think: 60
```

## Checklist anti-DDoS

- [ ] CDN/WAF actif (Cloudflare minimum plan gratuit)
- [ ] Rate limiting Nginx configuré
- [ ] Rate limiting application configuré
- [ ] Timeouts agressifs (anti-Slowloris)
- [ ] Limites de connexion par IP
- [ ] Monitoring avec alertes
- [ ] Mode "Under Attack" testé
- [ ] Tests de charge effectués
- [ ] Plan de réponse documenté
- [ ] Logs centralisés et analysables

## Ce qui ne protège PAS contre le DDoS

| "Solution" | Pourquoi ça ne marche pas |
|------------|---------------------------|
| HTTPS | Chiffre le transport, ne limite pas le volume |
| CORS | Politique navigateur, bots l'ignorent |
| Authentification | L'endpoint de login est public |
| Firewall seul | Insuffisant contre L7, pas assez de bande passante |
| Plus de serveurs | Coûteux, l'attaquant peut scaler aussi |
| Ignorer | L'attaque ne s'arrêtera pas seule |
