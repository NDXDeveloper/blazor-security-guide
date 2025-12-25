#!/bin/bash
# =============================================================================
# Test : CORS ne protège PAS le serveur
# =============================================================================
# Ce script démontre que CORS est une politique NAVIGATEUR, pas serveur.
# Un attaquant utilisant curl/Postman/scripts contourne CORS trivialement.

set -e

# Configuration
API_URL="${API_URL:-http://localhost:80}"
ENDPOINT="/api/weather"

echo "=============================================="
echo "  TEST : CORS ne protège PAS le serveur"
echo "=============================================="
echo ""
echo "CORS = Cross-Origin Resource Sharing"
echo "C'est une politique NAVIGATEUR, pas une sécurité serveur."
echo ""

# Test 1 : Requête sans Origin
echo "▶ Test 1 : Requête sans header Origin"
echo "----------------------------------------------"
HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" "${API_URL}${ENDPOINT}")
echo "Code HTTP : $HTTP_CODE"

if [ "$HTTP_CODE" == "200" ]; then
    echo "✅ Requête acceptée (normal, pas de navigateur impliqué)"
else
    echo "❌ Code inattendu : $HTTP_CODE"
fi

echo ""

# Test 2 : Requête avec Origin légitime
echo "▶ Test 2 : Requête avec Origin légitime"
echo "----------------------------------------------"
HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" \
    -H "Origin: http://localhost:5001" \
    "${API_URL}${ENDPOINT}")
echo "Code HTTP : $HTTP_CODE"

if [ "$HTTP_CODE" == "200" ]; then
    echo "✅ Requête acceptée"
else
    echo "❌ Code inattendu : $HTTP_CODE"
fi

echo ""

# Test 3 : Requête avec Origin malveillant
echo "▶ Test 3 : Requête avec Origin MALVEILLANT"
echo "----------------------------------------------"
echo "Simulation : attaquant sur https://evil-hacker-site.com"
echo ""

HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" \
    -H "Origin: https://evil-hacker-site.com" \
    "${API_URL}${ENDPOINT}")
echo "Code HTTP : $HTTP_CODE"

if [ "$HTTP_CODE" == "200" ]; then
    echo ""
    echo "🔴 RÉSULTAT : La requête a été TRAITÉE par le serveur !"
    echo ""
    echo "   Le serveur a reçu la requête et a répondu."
    echo "   CORS n'a RIEN bloqué côté serveur."
    echo ""
    echo "   Ce que CORS fait vraiment :"
    echo "   - Le NAVIGATEUR vérifie les headers de réponse"
    echo "   - Si Origin non autorisé, le NAVIGATEUR bloque la LECTURE"
    echo "   - Mais la requête a DÉJÀ atteint le serveur"
    echo ""
    echo "   Ce que CORS ne fait PAS :"
    echo "   - Ne bloque pas les requêtes côté serveur"
    echo "   - Ne protège pas contre curl/Postman/scripts"
    echo "   - Ne protège pas contre les bots"
else
    echo "⚠️  Code inattendu : $HTTP_CODE"
fi

echo ""

# Test 4 : Contenu de la réponse
echo "▶ Test 4 : L'attaquant peut lire les données"
echo "----------------------------------------------"
echo "Depuis curl (pas de navigateur, pas de CORS) :"
echo ""

RESPONSE=$(curl -s -H "Origin: https://evil-hacker-site.com" "${API_URL}${ENDPOINT}")
echo "$RESPONSE" | head -c 300
echo ""
echo ""

if [ -n "$RESPONSE" ]; then
    echo "🔴 L'attaquant a accès aux données !"
fi

echo ""

# Test 5 : Preflight (OPTIONS)
echo "▶ Test 5 : Requête Preflight OPTIONS"
echo "----------------------------------------------"
echo "Le navigateur envoie OPTIONS avant certaines requêtes."
echo "Mais curl ne fait pas de preflight."
echo ""

HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" \
    -X OPTIONS \
    -H "Origin: https://evil-hacker-site.com" \
    -H "Access-Control-Request-Method: GET" \
    "${API_URL}${ENDPOINT}")
echo "OPTIONS Code HTTP : $HTTP_CODE"

echo ""
echo ""

# Conclusion
echo "=============================================="
echo "  CONCLUSION"
echo "=============================================="
echo ""
echo "CORS est une politique NAVIGATEUR :"
echo "  • Le serveur répond à TOUTES les requêtes"
echo "  • Le navigateur peut bloquer la LECTURE de la réponse"
echo "  • curl/Postman/scripts ignorent complètement CORS"
echo ""
echo "CORS protège contre :"
echo "  ✅ Un site malveillant appelant votre API via le navigateur"
echo "     d'un utilisateur authentifié (vol de session)"
echo ""
echo "CORS ne protège PAS contre :"
echo "  ❌ Appels directs (curl, Postman, scripts)"
echo "  ❌ Bots"
echo "  ❌ DDoS"
echo "  ❌ Brute force"
echo "  ❌ Scraping"
echo ""
echo "Pour protéger votre API, utilisez :"
echo "  • Rate limiting"
echo "  • Authentification"
echo "  • WAF/CDN"
echo "  • Validation des entrées"
