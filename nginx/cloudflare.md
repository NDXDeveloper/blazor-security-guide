# Cloudflare - Configuration recommandée pour Blazor

Cloudflare est un CDN/WAF qui protège votre application avant qu'elle n'atteigne votre serveur. C'est la première ligne de défense recommandée.

## Pourquoi Cloudflare ?

| Fonctionnalité | Gratuit | Pro ($20/mois) |
|----------------|---------|----------------|
| Protection DDoS L3/L4 | Oui | Oui |
| Protection DDoS L7 | Basique | Avancée |
| WAF | Non | Oui |
| Rate limiting | 1 règle | 10 règles |
| Bot management | Basique | Avancé |
| SSL/TLS | Oui | Oui |

**Recommandation minimum :** Plan gratuit + Nginx rate limiting local.

**Recommandation production :** Plan Pro pour le WAF.

## Configuration DNS

```
Type    Name    Value               Proxy
A       @       VOTRE_IP_SERVEUR    Proxied (orange cloud)
CNAME   www     @                   Proxied
```

**Important :** Le nuage orange doit être activé pour bénéficier de la protection.

## Configuration SSL/TLS

### Mode recommandé : Full (Strict)

```
Cloudflare Dashboard → SSL/TLS → Overview
Mode : Full (strict)
```

Cela requiert un certificat valide sur votre serveur. Utilisez Let's Encrypt ou un certificat Origin de Cloudflare.

### Certificat Origin Cloudflare

```
SSL/TLS → Origin Server → Create Certificate
```

Avantages :
- Gratuit
- Valide 15 ans
- Automatiquement reconnu par Cloudflare

## Protection DDoS

### Paramètres recommandés

```
Security → DDoS → HTTP DDoS attack protection
- Sensitivity: Medium → High (si attaques fréquentes)
- Ruleset: Enable all managed rules
```

### Mode "Under Attack" (urgence)

```
Security → Settings → Security Level
- Défaut: Medium
- En cas d'attaque: I'm Under Attack!
```

Ce mode affiche un challenge JavaScript à tous les visiteurs pendant 5 secondes.

## Règles de pare-feu (WAF)

### Bloquer les pays non pertinents

Si votre audience est française uniquement :

```
Security → WAF → Custom rules

Rule name: Block non-FR countries
Expression: (not ip.geoip.country in {"FR" "BE" "CH" "CA"})
Action: Block
```

### Bloquer les User-Agents suspects

```
Rule name: Block bad bots
Expression: (http.user_agent contains "sqlmap") or
            (http.user_agent contains "nikto") or
            (http.user_agent contains "nmap")
Action: Block
```

### Protéger les endpoints sensibles

```
Rule name: Rate limit login
Expression: (http.request.uri.path contains "/api/auth/login")
Action: Rate Limiting (5 requests per minute)
```

## Rate Limiting

### Configuration recommandée

```
Security → WAF → Rate limiting rules

Rule 1: API Protection
- If: URI Path contains "/api/"
- Rate: 100 requests per minute
- Action: Block for 1 hour

Rule 2: Login Protection
- If: URI Path equals "/api/auth/login"
- Rate: 5 requests per minute
- Action: Block for 1 hour
```

## Configuration Blazor spécifique

### Blazor WASM

Pas de configuration spéciale requise. Les fichiers statiques sont bien gérés par défaut.

**Cache recommandé :**
```
Caching → Configuration
Browser Cache TTL: 1 year (pour les assets)
```

**Page Rules :**
```
Rules → Page Rules

*.dll: Cache Level: Cache Everything, Edge TTL: 1 month
*.wasm: Cache Level: Cache Everything, Edge TTL: 1 month
/api/*: Cache Level: Bypass
```

### Blazor Server (WebSocket/SignalR)

**Configuration critique pour SignalR :**

```
Network → WebSockets: Enabled
```

Sans cette option, SignalR ne fonctionnera pas.

**Timeouts :**
```
Network → HTTP/2: Enabled
Network → WebSockets: Enabled
```

**Note :** Les connexions WebSocket sur le plan gratuit sont limitées à 100 secondes d'inactivité. En production avec beaucoup de connexions longues, le plan Pro est recommandé.

## Configuration Nginx derrière Cloudflare

### Récupérer l'IP réelle du client

Cloudflare proxifie les requêtes. Pour avoir l'IP réelle dans vos logs :

```nginx
# /etc/nginx/conf.d/cloudflare.conf

# IPs Cloudflare IPv4
set_real_ip_from 173.245.48.0/20;
set_real_ip_from 103.21.244.0/22;
set_real_ip_from 103.22.200.0/22;
set_real_ip_from 103.31.4.0/22;
set_real_ip_from 141.101.64.0/18;
set_real_ip_from 108.162.192.0/18;
set_real_ip_from 190.93.240.0/20;
set_real_ip_from 188.114.96.0/20;
set_real_ip_from 197.234.240.0/22;
set_real_ip_from 198.41.128.0/17;
set_real_ip_from 162.158.0.0/15;
set_real_ip_from 104.16.0.0/13;
set_real_ip_from 104.24.0.0/14;
set_real_ip_from 172.64.0.0/13;
set_real_ip_from 131.0.72.0/22;

# IPs Cloudflare IPv6
set_real_ip_from 2400:cb00::/32;
set_real_ip_from 2606:4700::/32;
set_real_ip_from 2803:f800::/32;
set_real_ip_from 2405:b500::/32;
set_real_ip_from 2405:8100::/32;
set_real_ip_from 2a06:98c0::/29;
set_real_ip_from 2c0f:f248::/32;

real_ip_header CF-Connecting-IP;
```

### Bloquer le trafic non-Cloudflare

Pour forcer tout le trafic à passer par Cloudflare :

```nginx
# Vérifier le header Cloudflare
if ($http_cf_connecting_ip = "") {
    return 403;
}
```

Ou mieux, au niveau firewall (iptables/ufw) : autoriser uniquement les IPs Cloudflare.

## Monitoring

### Alertes recommandées

```
Notifications → Create
- DDoS attack detected
- WAF rule triggered (High severity)
- Origin error rate spike
```

### Analytics

```
Analytics → Security
- Surveiller le "Threat Score" quotidien
- Analyser les "Top Events" hebdomadaires
- Vérifier les "Bot Score" distributions
```

## Checklist avant production

- [ ] DNS proxied (nuage orange) activé
- [ ] SSL/TLS en mode Full (Strict)
- [ ] Certificat origin ou Let's Encrypt installé
- [ ] WebSockets activés (si Blazor Server)
- [ ] Rate limiting configuré pour /api/*
- [ ] Rate limiting configuré pour /login
- [ ] WAF activé (plan Pro recommandé)
- [ ] Nginx configuré pour IP réelle Cloudflare
- [ ] Alertes configurées
- [ ] Mode "Under Attack" testé

## Alternatives à Cloudflare

| Solution | DDoS | WAF | CDN | Prix |
|----------|------|-----|-----|------|
| Cloudflare | Oui | Pro+ | Oui | Gratuit-$200/mois |
| AWS CloudFront + WAF | Oui | Oui | Oui | Pay-as-you-go |
| Azure Front Door | Oui | Oui | Oui | Pay-as-you-go |
| Fastly | Oui | Oui | Oui | Enterprise |

Pour une PME ou un freelance, Cloudflare est généralement le meilleur rapport qualité/prix.
