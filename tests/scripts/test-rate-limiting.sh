#!/bin/bash
# =============================================================================
# Test : Rate Limiting fonctionne
# =============================================================================
# Ce script démontre que le rate limiting bloque réellement les requêtes
# excessives. C'est une vraie protection, contrairement à CORS.

set -e

# Configuration
API_URL="${API_URL:-http://localhost:80}"
ENDPOINT="/api/weather"
RATE_LIMIT="${RATE_LIMIT:-100}"  # Limite configurée
TEST_REQUESTS="${TEST_REQUESTS:-150}"  # Nombre de requêtes à tester

echo "=============================================="
echo "  TEST : Rate Limiting fonctionne"
echo "=============================================="
echo ""
echo "Configuration attendue : ${RATE_LIMIT} requêtes/minute"
echo "Nombre de requêtes test : ${TEST_REQUESTS}"
echo ""

# Test 1 : Requêtes sous la limite
echo "▶ Test 1 : Requêtes sous la limite (10 requêtes)"
echo "----------------------------------------------"

SUCCESS_COUNT=0
for i in {1..10}; do
    HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" "${API_URL}${ENDPOINT}")
    if [ "$HTTP_CODE" == "200" ]; then
        ((SUCCESS_COUNT++))
    fi
done

echo "Requêtes réussies : ${SUCCESS_COUNT}/10"

if [ "$SUCCESS_COUNT" -eq 10 ]; then
    echo "✅ Toutes les requêtes sous la limite ont réussi"
else
    echo "⚠️  Certaines requêtes ont échoué (rate limit déjà atteint ?)"
fi

echo ""

# Pause pour reset du rate limit
echo "⏳ Pause de 60 secondes pour reset du rate limit..."
sleep 60

# Test 2 : Burst de requêtes
echo "▶ Test 2 : Burst de ${TEST_REQUESTS} requêtes"
echo "----------------------------------------------"

SUCCESS_COUNT=0
RATE_LIMITED=0
OTHER_ERRORS=0

for i in $(seq 1 $TEST_REQUESTS); do
    HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" "${API_URL}${ENDPOINT}")

    if [ "$HTTP_CODE" == "200" ]; then
        ((SUCCESS_COUNT++))
    elif [ "$HTTP_CODE" == "429" ]; then
        ((RATE_LIMITED++))
    else
        ((OTHER_ERRORS++))
    fi

    # Afficher progression
    if [ $((i % 20)) -eq 0 ]; then
        echo "  Progression : $i/${TEST_REQUESTS} requêtes envoyées"
    fi
done

echo ""
echo "Résultats :"
echo "  • 200 OK : ${SUCCESS_COUNT}"
echo "  • 429 Too Many Requests : ${RATE_LIMITED}"
echo "  • Autres erreurs : ${OTHER_ERRORS}"
echo ""

if [ "$RATE_LIMITED" -gt 0 ]; then
    echo "✅ Rate limiting FONCTIONNE !"
    echo "   ${RATE_LIMITED} requêtes ont été bloquées."
else
    echo "⚠️  Aucune requête bloquée."
    echo "   Vérifiez la configuration du rate limiting."
fi

echo ""

# Test 3 : Vérifier le header Retry-After
echo "▶ Test 3 : Header Retry-After sur réponse 429"
echo "----------------------------------------------"

# Envoyer beaucoup de requêtes pour déclencher 429
for i in {1..200}; do
    RESPONSE=$(curl -sI "${API_URL}${ENDPOINT}")
    if echo "$RESPONSE" | grep -q "429"; then
        echo "Réponse 429 obtenue."
        echo ""
        if echo "$RESPONSE" | grep -qi "Retry-After"; then
            RETRY_AFTER=$(echo "$RESPONSE" | grep -i "Retry-After" | cut -d: -f2 | tr -d ' \r')
            echo "✅ Header Retry-After présent : ${RETRY_AFTER} secondes"
        else
            echo "⚠️  Header Retry-After absent"
        fi
        break
    fi
done

echo ""

# Test 4 : Contenu de la réponse 429
echo "▶ Test 4 : Contenu de la réponse 429"
echo "----------------------------------------------"

for i in {1..200}; do
    HTTP_CODE=$(curl -s -o /tmp/rate_limit_response.json -w "%{http_code}" "${API_URL}${ENDPOINT}")
    if [ "$HTTP_CODE" == "429" ]; then
        echo "Contenu de la réponse 429 :"
        cat /tmp/rate_limit_response.json
        echo ""
        rm -f /tmp/rate_limit_response.json
        break
    fi
done

echo ""
echo ""

# Conclusion
echo "=============================================="
echo "  CONCLUSION"
echo "=============================================="
echo ""
echo "Le rate limiting est une VRAIE protection :"
echo "  • Bloque les requêtes excessives"
echo "  • Retourne 429 Too Many Requests"
echo "  • Indique quand réessayer (Retry-After)"
echo ""
echo "Contrairement à CORS :"
echo "  • Rate limiting fonctionne pour TOUTES les sources"
echo "  • Pas de contournement possible avec curl"
echo "  • Protection réelle contre les abus"
echo ""
echo "Configuration recommandée :"
echo "  • Endpoints publics : 60-100 req/min"
echo "  • Endpoints auth (login) : 5 req/5min"
echo "  • Endpoints sensibles : 10-20 req/min"
