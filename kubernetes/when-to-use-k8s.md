# Kubernetes - Quand c'est pertinent (et quand ça ne l'est pas)

## La vérité sur Kubernetes

> **Kubernetes n'est pas une solution de sécurité. C'est une plateforme d'orchestration.**

Kubernetes aide à déployer, scaler et gérer des applications. Il n'ajoute pas magiquement de la sécurité à votre application Blazor.

## Quand Kubernetes est INUTILE

### 1. Vous avez moins de 5 000 utilisateurs simultanés

```
┌─────────────────────────────────────────────────────────────┐
│  Docker Compose sur un VPS de 50€/mois fait le travail      │
│  K8s ajoute de la complexité sans bénéfice                  │
└─────────────────────────────────────────────────────────────┘
```

**Réalité :** Un serveur correctement configuré avec Docker Compose peut gérer :
- Blazor WASM : 10 000+ utilisateurs simultanés
- Blazor Server : 2 000-5 000 utilisateurs simultanés

### 2. Vous n'avez pas d'équipe DevOps

Kubernetes requiert :
- Configuration initiale complexe
- Maintenance continue
- Monitoring spécialisé
- Compétences spécifiques

**Coût caché :** 1-2 jours par mois de maintenance minimum.

### 3. Vous cherchez de la "sécurité"

Kubernetes ne protège pas contre :
- Les attaques DDoS (utilisez un WAF)
- Les vulnérabilités applicatives
- Les erreurs de configuration
- Les abus d'API

### 4. Vous voulez juste de la haute disponibilité

Alternatives plus simples :
- Load balancer + 2 VPS
- Managed service (Azure App Service, AWS ECS)
- Docker Swarm (plus simple que K8s)

## Quand Kubernetes devient PERTINENT

### 1. Besoin de scaling automatique réel

```yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: blazor-wasm-api
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: blazor-wasm-api
  minReplicas: 2
  maxReplicas: 20
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70
```

**Cas d'usage :** Trafic très variable (pics 10x la normale).

### 2. Multi-région / Multi-cloud

```
Europe (GKE)           US (EKS)            Asie (AKS)
     │                     │                    │
     └─────────────────────┴────────────────────┘
                    Global Load Balancer
```

**Cas d'usage :** Latence critique pour utilisateurs mondiaux.

### 3. Microservices (nombreux)

```
┌─────────────────────────────────────────────────────────────┐
│  Si vous avez 10+ services à orchestrer :                   │
│  - Service mesh (Istio)                                     │
│  - Service discovery                                        │
│  - Configuration centralisée                                │
│  → Kubernetes a du sens                                     │
└─────────────────────────────────────────────────────────────┘
```

### 4. Équipe et budget appropriés

| Critère | Minimum recommandé |
|---------|-------------------|
| Équipe | 1 DevOps/SRE dédié |
| Budget | 500€+/mois (managed K8s) |
| Compétences | Certification CKA ou équivalent |
| Applications | 5+ services |

## Comparaison des options de déploiement

| Critère | VPS + Docker | Docker Swarm | Kubernetes |
|---------|--------------|--------------|------------|
| Complexité | Faible | Moyenne | Élevée |
| Coût mensuel | 50-200€ | 100-500€ | 300-2000€ |
| Temps setup | 1 jour | 2-3 jours | 1-2 semaines |
| Maintenance | 2h/mois | 4h/mois | 1-2j/mois |
| Scaling | Manuel | Auto (limité) | Auto (avancé) |
| HA | Load balancer | Intégré | Intégré |
| Courbe d'apprentissage | Facile | Moyenne | Difficile |

## Architecture Blazor WASM sur Kubernetes

Si vous décidez d'utiliser Kubernetes :

```
┌─────────────────────────────────────────────────────────────┐
│                        Internet                             │
└────────────────────────────┬────────────────────────────────┘
                             │
                             ▼
              ┌───────────────────────────┐
              │     Ingress Controller    │
              │     (nginx-ingress)       │
              │     + cert-manager (SSL)  │
              └──────────────┬────────────┘
                             │
          ┌──────────────────┼──────────────────┐
          │                  │                  │
          ▼                  ▼                  ▼
    ┌───────────┐      ┌───────────┐      ┌───────────┐
    │  Pod API  │      │  Pod API  │      │  Pod API  │
    │  replica  │      │  replica  │      │  replica  │
    └───────────┘      └───────────┘      └───────────┘
                             │
                             ▼
              ┌───────────────────────────┐
              │    Service (ClusterIP)    │
              └───────────────────────────┘
```

**Fichiers :** [kubernetes/wasm/](wasm/)

## Architecture Blazor Server sur Kubernetes

```
┌─────────────────────────────────────────────────────────────┐
│  ATTENTION : Blazor Server + K8s = Complexe                 │
│                                                             │
│  Problème : Sessions SignalR sont stateful                  │
│  Solution : Sticky sessions + Redis backplane               │
└─────────────────────────────────────────────────────────────┘
```

```
                        Internet
                             │
                             ▼
              ┌───────────────────────────┐
              │     Ingress Controller    │
              │   (sticky sessions ON)    │
              └──────────────┬────────────┘
                             │
          ┌──────────────────┼──────────────────┐
          │                  │                  │
          ▼                  ▼                  ▼
    ┌───────────┐      ┌───────────┐      ┌───────────┐
    │  Blazor   │      │  Blazor   │      │  Blazor   │
    │  Server   │      │  Server   │      │  Server   │
    └─────┬─────┘      └─────┬─────┘      └─────┬─────┘
          │                  │                  │
          └──────────────────┼──────────────────┘
                             │
                             ▼
              ┌───────────────────────────┐
              │     Redis (backplane)     │
              │     SignalR state         │
              └───────────────────────────┘
                             │
                             ▼
              ┌───────────────────────────┐
              │     API Service           │
              │     (ClusterIP)           │
              └───────────────────────────┘
```

**Fichiers :** [kubernetes/server/](server/)

## Recommandation finale

```
┌─────────────────────────────────────────────────────────────┐
│                   ARBRE DE DÉCISION                         │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  Combien d'utilisateurs simultanés ?                        │
│      │                                                      │
│      ├── < 5 000 → Docker Compose sur VPS                   │
│      │                                                      │
│      ├── 5 000 - 20 000 → Docker Swarm ou Managed Service   │
│      │                                                      │
│      └── > 20 000 → Kubernetes (si équipe/budget OK)        │
│                                                             │
│  Avez-vous une équipe DevOps ?                              │
│      │                                                      │
│      ├── Non → Managed Service (Azure, AWS, Vercel)         │
│      │                                                      │
│      └── Oui → Évaluez K8s si scaling automatique requis    │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

## Alternatives à Kubernetes

### Pour Blazor WASM

| Option | Complexité | Coût | Recommandation |
|--------|------------|------|----------------|
| Vercel/Netlify (statique) + API séparée | Très faible | Gratuit-20€ | PME |
| Azure Static Web Apps | Faible | Pay-as-you-go | Microsoft shop |
| AWS Amplify | Faible | Pay-as-you-go | AWS shop |
| VPS + Docker Compose | Faible | 20-100€ | Freelance/PME |

### Pour Blazor Server

| Option | Complexité | Coût | Recommandation |
|--------|------------|------|----------------|
| Azure App Service | Faible | 50-500€ | Microsoft shop |
| AWS ECS Fargate | Moyenne | Pay-as-you-go | AWS shop |
| VPS + Docker Compose | Faible | 50-200€ | PME/Freelance |
| Docker Swarm | Moyenne | 100-500€ | Self-hosted HA |
