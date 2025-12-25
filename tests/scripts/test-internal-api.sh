#!/bin/bash
# =============================================================================
# Test : API Blazor Server est INTERNE
# =============================================================================
# Ce script démontre que l'API Blazor Server n'est PAS accessible depuis
# l'extérieur, mais uniquement depuis le réseau Docker interne.

set -e

# Configuration
EXTERNAL_URL="${EXTERNAL_URL:-http://localhost:80}"
INTERNAL_API_PORT="${INTERNAL_API_PORT:-5001}"
BLAZOR_CONTAINER="${BLAZOR_CONTAINER:-blazor-server-blazor-1}"
API_CONTAINER="${API_CONTAINER:-blazor-server-api-1}"

echo "=============================================="
echo "  TEST : API Server est INTERNE"
echo "=============================================="
echo ""

# Test 1 : Accès direct au port API depuis l'extérieur
echo "▶ Test 1 : Accès direct au port API (${INTERNAL_API_PORT}) depuis l'extérieur"
echo "----------------------------------------------"

HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" --connect-timeout 5 "http://localhost:${INTERNAL_API_PORT}/api/weather" 2>/dev/null || echo "000")

if [ "$HTTP_CODE" == "000" ]; then
    echo "✅ SUCCÈS : Connection refused / timeout"
    echo "   L'API n'est pas exposée sur le port ${INTERNAL_API_PORT}"
elif [ "$HTTP_CODE" == "200" ]; then
    echo "❌ ÉCHEC : L'API est accessible directement !"
    echo "   Vérifiez votre docker-compose : utilisez 'expose' au lieu de 'ports'"
else
    echo "⚠️  Code HTTP inattendu : $HTTP_CODE"
fi

echo ""

# Test 2 : Accès via Nginx à /api (ne devrait pas exister)
echo "▶ Test 2 : Accès via Nginx à /api (route inexistante)"
echo "----------------------------------------------"

HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" --connect-timeout 5 "${EXTERNAL_URL}/api/weather")

if [ "$HTTP_CODE" == "404" ]; then
    echo "✅ SUCCÈS : 404 Not Found"
    echo "   Nginx n'a pas de route /api pour Blazor Server"
elif [ "$HTTP_CODE" == "200" ]; then
    echo "❌ ÉCHEC : L'API est accessible via /api !"
    echo "   Vérifiez votre nginx.conf : supprimez la route /api"
else
    echo "⚠️  Code HTTP : $HTTP_CODE"
fi

echo ""

# Test 3 : Accès depuis le conteneur Blazor Server vers l'API
echo "▶ Test 3 : Accès depuis le réseau Docker interne"
echo "----------------------------------------------"

# Vérifier si Docker est disponible
if ! command -v docker &> /dev/null; then
    echo "⚠️  Docker non disponible, test skip"
else
    # Vérifier si le conteneur existe
    if docker ps --format '{{.Names}}' | grep -q "$BLAZOR_CONTAINER"; then
        INTERNAL_RESULT=$(docker exec "$BLAZOR_CONTAINER" curl -s -o /dev/null -w "%{http_code}" "http://api:5001/api/weather" 2>/dev/null || echo "error")

        if [ "$INTERNAL_RESULT" == "200" ]; then
            echo "✅ SUCCÈS : 200 OK depuis le réseau Docker"
            echo "   L'API répond aux requêtes internes"
        elif [ "$INTERNAL_RESULT" == "error" ]; then
            echo "⚠️  Erreur lors de l'exécution dans le conteneur"
        else
            echo "⚠️  Code HTTP : $INTERNAL_RESULT"
        fi
    else
        echo "⚠️  Conteneur $BLAZOR_CONTAINER non trouvé"
        echo "   Lancez d'abord : docker-compose up -d"
    fi
fi

echo ""

# Test 4 : Accès à Blazor Server (devrait fonctionner)
echo "▶ Test 4 : Accès à Blazor Server (page principale)"
echo "----------------------------------------------"

HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" --connect-timeout 5 "${EXTERNAL_URL}/")

if [ "$HTTP_CODE" == "200" ]; then
    echo "✅ SUCCÈS : 200 OK"
    echo "   Blazor Server est accessible via Nginx"
else
    echo "⚠️  Code HTTP : $HTTP_CODE"
fi

echo ""
echo ""

# Conclusion
echo "=============================================="
echo "  CONCLUSION"
echo "=============================================="
echo ""
echo "API Blazor Server :"
echo "  • Inaccessible depuis l'extérieur (port ${INTERNAL_API_PORT})"
echo "  • Pas de route /api dans Nginx"
echo "  • Accessible uniquement depuis le réseau Docker interne"
echo ""
echo "C'est la différence FONDAMENTALE avec Blazor WASM."
echo ""
echo "Avantages :"
echo "  • Pas de CORS nécessaire"
echo "  • Pas de rate limiting public sur l'API"
echo "  • Surface d'attaque réduite"
echo ""
echo "Inconvénient :"
echo "  • SignalR devient le point d'entrée (et de vulnérabilité)"
