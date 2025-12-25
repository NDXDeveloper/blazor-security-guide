#!/bin/bash
# =============================================================================
# Test : API WASM est publique
# =============================================================================
# Ce script démontre que l'API Blazor WASM est accessible publiquement.
# C'est le comportement ATTENDU pour WASM.

set -e

# Configuration
API_URL="${API_URL:-http://localhost:80}"
ENDPOINT="/api/weather"

echo "=============================================="
echo "  TEST : API WASM est publique"
echo "=============================================="
echo ""
echo "URL testée : ${API_URL}${ENDPOINT}"
echo ""

# Test 1 : Requête simple
echo "▶ Test 1 : Requête GET simple"
echo "----------------------------------------------"
HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" "${API_URL}${ENDPOINT}")
echo "Code HTTP : $HTTP_CODE"

if [ "$HTTP_CODE" == "200" ]; then
    echo "✅ SUCCÈS : L'API est accessible"
    echo ""
    echo "Contenu de la réponse :"
    curl -s "${API_URL}${ENDPOINT}" | head -c 500
    echo ""
else
    echo "❌ ÉCHEC : L'API n'est pas accessible (code $HTTP_CODE)"
    echo "   Assurez-vous que docker-compose est lancé"
    exit 1
fi

echo ""
echo ""

# Test 2 : Headers de sécurité
echo "▶ Test 2 : Headers de sécurité présents"
echo "----------------------------------------------"
HEADERS=$(curl -sI "${API_URL}${ENDPOINT}")

check_header() {
    if echo "$HEADERS" | grep -qi "$1"; then
        echo "✅ $1 : présent"
    else
        echo "⚠️  $1 : absent"
    fi
}

check_header "X-Frame-Options"
check_header "X-Content-Type-Options"
check_header "X-XSS-Protection"

echo ""
echo ""

# Test 3 : Endpoint avec paramètre
echo "▶ Test 3 : Endpoint avec paramètre"
echo "----------------------------------------------"
HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" "${API_URL}${ENDPOINT}/1")
echo "GET ${ENDPOINT}/1 : $HTTP_CODE"

HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" "${API_URL}${ENDPOINT}/0")
echo "GET ${ENDPOINT}/0 (invalide) : $HTTP_CODE"

HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" "${API_URL}${ENDPOINT}/-1")
echo "GET ${ENDPOINT}/-1 (invalide) : $HTTP_CODE"

echo ""
echo ""

# Conclusion
echo "=============================================="
echo "  CONCLUSION"
echo "=============================================="
echo ""
echo "L'API WASM est publique et accessible."
echo "C'est le comportement NORMAL pour Blazor WASM."
echo ""
echo "La sécurité repose sur :"
echo "  • Rate limiting"
echo "  • Authentification/Autorisation"
echo "  • Validation des entrées"
echo "  • WAF/CDN"
echo ""
echo "PAS sur l'obscurité ou l'isolation réseau."
