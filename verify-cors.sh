#!/bin/bash

# Script para verificar configuración de CORS
# Prueba las conexiones entre frontend y API

echo "🔍 Verificando configuración de CORS..."

# URLs a probar
API_URL="https://api.granpasochile.cl"
FRONTEND_URL="https://inventory.granpasochile.cl"

echo "📡 API URL: $API_URL"
echo "🌐 Frontend URL: $FRONTEND_URL"

# Verificar que la API responde
echo "🔄 Verificando disponibilidad de la API..."
if curl -s --head "$API_URL/api/health" | head -n 1 | grep -q "200 OK"; then
    echo "✅ API está disponible"
else
    echo "❌ API no está disponible"
fi

# Verificar headers de CORS con una petición OPTIONS
echo "🔄 Verificando headers de CORS..."
CORS_RESPONSE=$(curl -s -I -X OPTIONS \
    -H "Origin: $FRONTEND_URL" \
    -H "Access-Control-Request-Method: GET" \
    -H "Access-Control-Request-Headers: Content-Type,Authorization" \
    "$API_URL/api/auth/validate-token")

if echo "$CORS_RESPONSE" | grep -q "Access-Control-Allow-Origin"; then
    echo "✅ CORS está configurado"
    echo "📋 Headers de CORS encontrados:"
    echo "$CORS_RESPONSE" | grep "Access-Control"
else
    echo "❌ CORS no está configurado correctamente"
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
