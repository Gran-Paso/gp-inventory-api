#!/bin/bash

# Script para verificar configuración de CORS
# Prueba las conexiones entre frontend y API

echo "🔍 Verificando configuración de CORS..."

# URLs a probar
API_URL="https://api.granpasochile.cl"
INVENTORY_URL="https://inventory.granpasochile.cl"
EXPENSES_URL="https://expenses.granpasochile.cl"
FACTORY_URL="https://factory.granpasochile.cl"
FACTORY_DEV_URL="http://localhost:5174"

echo "📡 API URL: $API_URL"
echo "🌐 Inventory URL: $INVENTORY_URL"
echo "💰 Expenses URL: $EXPENSES_URL"
echo "🏭 Factory URL: $FACTORY_URL"
echo "🛠️ Factory Dev URL: $FACTORY_DEV_URL"
echo "💰 Expenses URL: $EXPENSES_URL"
echo "🏭 Factory URL: $FACTORY_URL"

# Verificar que la API responde
echo "🔄 Verificando disponibilidad de la API..."
if curl -s --head "$API_URL/api/health" | head -n 1 | grep -q "200 OK"; then
    echo "✅ API está disponible"
else
    echo "❌ API no está disponible"
fi

# Verificar headers de CORS con peticiones OPTIONS desde cada frontend
echo "🔄 Verificando headers de CORS para cada frontend..."

# Función para verificar CORS
verify_cors() {
    local origin_url=$1
    local service_name=$2
    
    echo "🔍 Verificando CORS para $service_name ($origin_url)..."
    CORS_RESPONSE=$(curl -s -I -X OPTIONS \
        -H "Origin: $origin_url" \
        -H "Access-Control-Request-Method: GET" \
        -H "Access-Control-Request-Headers: Content-Type,Authorization" \
        "$API_URL/api/auth/validate-token")

    if echo "$CORS_RESPONSE" | grep -q "Access-Control-Allow-Origin"; then
        echo "✅ CORS configurado para $service_name"
        echo "📋 Headers de CORS encontrados:"
        echo "$CORS_RESPONSE" | grep "Access-Control"
    else
        echo "❌ CORS no configurado para $service_name"
    fi
    echo ""
}

# Verificar CORS para cada servicio
verify_cors "$INVENTORY_URL" "GP Inventory"
verify_cors "$EXPENSES_URL" "GP Expenses"
verify_cors "$FACTORY_URL" "GP Factory"
verify_cors "$FACTORY_DEV_URL" "GP Factory Dev"
fi

# Verificar que el frontend está disponible
echo "🔄 Verificando disponibilidad del frontend..."
if curl -s --head "$FRONTEND_URL" | head -n 1 | grep -q "200 OK"; then
    echo "✅ Frontend está disponible"
else
    echo "❌ Frontend no está disponible"
fi

echo "🎯 Verificación completada"
echo ""
echo "📋 URLs configuradas:"
echo "   Frontend: $FRONTEND_URL"
echo "   API: $API_URL"
echo "   Swagger: $API_URL/swagger"
