# Scripts de Test Sécurité

Scripts shell pédagogiques pour démontrer concrètement les concepts de sécurité.

## Prérequis

```bash
# Ubuntu/Debian
sudo apt install curl jq

# Pour les tests de charge
# Option 1 : hey (recommandé)
go install github.com/rakyll/hey@latest

# Option 2 : ab (Apache Benchmark)
sudo apt install apache2-utils
```

## Scripts disponibles

| Script | Description | Démontre |
|--------|-------------|----------|
| `test-public-api.sh` | Teste l'API WASM publique | L'API est accessible depuis Internet |
| `test-internal-api.sh` | Teste l'API Server interne | L'API est isolée dans Docker |
| `test-cors-myth.sh` | Contourne CORS avec curl | CORS ≠ sécurité serveur |
| `test-rate-limiting.sh` | Teste les limites de requêtes | Rate limiting fonctionne |
| `flood-test.sh` | Test de charge basique | Comportement sous stress |

## Utilisation

```bash
# Rendre exécutables
chmod +x *.sh

# Exécuter un script
./test-public-api.sh
```

## Prérequis Docker

Certains scripts nécessitent que Docker Compose soit lancé :

```bash
# Pour tester WASM
cd ../../blazor-wasm
docker-compose up -d

# Pour tester Server
cd ../../blazor-server
docker-compose up -d
```

## Ce que ces scripts prouvent

### 1. L'API WASM est publique (c'est normal)

```bash
./test-public-api.sh
# Résultat : 200 OK - L'API répond
```

### 2. L'API Server est vraiment interne

```bash
./test-internal-api.sh
# Résultat :
# - Depuis l'extérieur : Connection refused ou 404
# - Depuis Docker : 200 OK
```

### 3. CORS ne protège RIEN côté serveur

```bash
./test-cors-myth.sh
# Résultat : 200 OK même avec Origin: evil.com
# Conclusion : CORS est une politique navigateur, pas serveur
```

### 4. Le rate limiting bloque vraiment

```bash
./test-rate-limiting.sh
# Résultat :
# - 10 premières requêtes : 200 OK
# - Requêtes suivantes : 429 Too Many Requests
```

### 5. Comportement sous charge

```bash
./flood-test.sh
# Résultat : Statistiques de latence et erreurs
```
