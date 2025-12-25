#!/bin/bash
# =============================================================================
# Test : Résilience basique (flood test léger)
# =============================================================================
# Ce script envoie un nombre modéré de requêtes concurrentes pour tester
# la résilience de l'application. Ce n'est PAS un vrai test DDoS.

set -e

# Configuration
API_URL="${API_URL:-http://localhost:80}"
ENDPOINT="/api/weather"
CONCURRENT="${CONCURRENT:-50}"
TOTAL_REQUESTS="${TOTAL_REQUESTS:-500}"

echo "=============================================="
echo "  TEST : Résilience basique"
echo "=============================================="
echo ""
echo "⚠️  Ce test envoie ${TOTAL_REQUESTS} requêtes avec ${CONCURRENT} connexions simultanées."
echo "    C'est un test LÉGER, pas un vrai stress test."
echo ""
echo "URL : ${API_URL}${ENDPOINT}"
echo ""

# Vérifier si 'hey' est installé
if command -v hey &> /dev/null; then
    TOOL="hey"
elif command -v ab &> /dev/null; then
    TOOL="ab"
else
    echo "❌ Aucun outil de test de charge trouvé."
    echo ""
    echo "Installez l'un des outils suivants :"
    echo ""
    echo "  # hey (recommandé)"
    echo "  go install github.com/rakyll/hey@latest"
    echo ""
    echo "  # ab (Apache Benchmark)"
    echo "  sudo apt install apache2-utils"
    echo ""
    exit 1
fi

echo "Outil utilisé : $TOOL"
echo ""

# Test avec hey
if [ "$TOOL" == "hey" ]; then
    echo "▶ Lancement du test avec hey"
    echo "----------------------------------------------"
    echo ""

    hey -n "$TOTAL_REQUESTS" -c "$CONCURRENT" "${API_URL}${ENDPOINT}"

    echo ""
fi

# Test avec ab
if [ "$TOOL" == "ab" ]; then
    echo "▶ Lancement du test avec ab"
    echo "----------------------------------------------"
    echo ""

    ab -n "$TOTAL_REQUESTS" -c "$CONCURRENT" "${API_URL}${ENDPOINT}"

    echo ""
fi

echo ""

# Comparaison WASM vs Server
echo "=============================================="
echo "  COMPARAISON : WASM vs Server"
echo "=============================================="
echo ""
echo "Comportement attendu sous charge :"
echo ""
echo "┌────────────────────┬────────────────────┬────────────────────┐"
echo "│ Métrique           │ Blazor WASM        │ Blazor Server      │"
echo "├────────────────────┼────────────────────┼────────────────────┤"
echo "│ Latence moyenne    │ Stable             │ Augmente           │"
echo "│ Mémoire serveur    │ ~50 MB             │ ~500+ MB           │"
echo "│ Connexions actives │ Courtes (HTTP)     │ Longues (WS)       │"
echo "│ Erreurs attendues  │ 429 (rate limit)   │ 429 + timeouts     │"
echo "│ Scalabilité        │ Excellente         │ Limitée            │"
echo "└────────────────────┴────────────────────┴────────────────────┘"
echo ""

# Recommandations
echo "=============================================="
echo "  RECOMMANDATIONS"
echo "=============================================="
echo ""
echo "Si les résultats montrent :"
echo ""
echo "  • Latence > 500ms : Ajouter du cache ou optimiser les requêtes DB"
echo "  • Beaucoup de 429 : Normal, rate limiting fonctionne"
echo "  • Beaucoup de 5xx : Problème serveur, vérifier les logs"
echo "  • Timeouts : Serveur surchargé, besoin de scaling"
echo ""
echo "Pour Blazor Server spécifiquement :"
echo ""
echo "  • Si mémoire explose : Limite de circuits trop haute"
echo "  • Si connexions refusées : Limite worker_connections Nginx"
echo "  • Si SignalR timeout : Client trop lent ou réseau saturé"
echo ""

# Test spécifique SignalR (si Blazor Server)
echo "=============================================="
echo "  TEST SIGNALR (Blazor Server uniquement)"
echo "=============================================="
echo ""
echo "Pour tester SignalR, utilisez un outil spécialisé :"
echo ""
echo "  # Avec Artillery"
echo "  npm install -g artillery"
echo "  artillery run signalr-test.yml"
echo ""
echo "  # Configuration exemple :"
echo "  # config:"
echo "  #   target: \"wss://localhost\""
echo "  #   phases:"
echo "  #     - duration: 60"
echo "  #       arrivalRate: 10"
echo "  # scenarios:"
echo "  #   - engine: ws"
echo "  #     flow:"
echo "  #       - connect: \"/_blazor\""
echo ""
