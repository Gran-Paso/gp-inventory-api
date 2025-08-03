# Configuración de Dominios - GP Inventory

## 🌐 Dominios de Producción

### Frontend
- **URL Principal**: `https://inventory.granpasochile.cl`
- **Puerto Docker**: 80
- **Imagen**: `tu-usuario/gp-inventory:latest`

### API
- **URL Principal**: `https://api.granpasochile.cl`
- **Puerto Docker**: 80 (interno)
- **Imagen**: `tu-usuario/gp-inventory-api:latest`
- **Swagger**: `https://api.granpasochile.cl/swagger`

## 🔐 Configuración de CORS

### Producción (`appsettings.Production.json`)
```json
{
  "Cors": {
    "AllowedOrigins": [
      "https://granpasochile.cl",
      "https://www.granpasochile.cl",
      "https://app.granpasochile.cl",
      "https://inventory.granpasochile.cl"
    ]
  }
}
```

### Desarrollo (`appsettings.Development.json`)
```json
{
  "Cors": {
    "AllowedOrigins": [
      "http://localhost:5173",
      "http://localhost:3000",
      "http://localhost:8080",
      "https://inventory.granpasochile.cl",
      "https://granpasochile.cl",
      "https://www.granpasochile.cl",
      "https://app.granpasochile.cl"
    ]
  }
}
```

## 🛠️ Variables de Entorno

### Frontend
```bash
# Desarrollo
VITE_API_BASE_URL=http://localhost:5279

# Producción
VITE_API_BASE_URL=https://api.granpasochile.cl
```

### API
```bash
# Producción
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:80
```

## 🚀 Deploy y Verificación

### Comandos de verificación:
```bash
# Windows
verify-cors.bat

# Linux/Mac
chmod +x verify-cors.sh
./verify-cors.sh
```

### URLs de testing:
- **Frontend**: https://inventory.granpasochile.cl
- **API**: https://api.granpasochile.cl
- **API Health**: https://api.granpasochile.cl/api/health
- **Swagger**: https://api.granpasochile.cl/swagger

## 🔧 Troubleshooting

### CORS Errors:
1. Verificar que `inventory.granpasochile.cl` esté en `AllowedOrigins`
2. Verificar que la API esté ejecutándose en HTTPS
3. Comprobar certificados SSL válidos
4. Verificar que no hay redirects HTTP → HTTPS

### Network Issues:
1. Verificar DNS de ambos dominios
2. Comprobar firewall y puertos abiertos
3. Verificar proxy/load balancer configuración
4. Comprobar logs de la aplicación

### Deploy Checklist:
- [ ] DNS configurado para `inventory.granpasochile.cl`
- [ ] DNS configurado para `api.granpasochile.cl`
- [ ] Certificados SSL válidos
- [ ] CORS configurado en la API
- [ ] Variables de entorno configuradas
- [ ] Docker images deployadas
- [ ] Health checks funcionando

## 🌍 Arquitectura de Red

```
[Usuario] 
    ↓
[inventory.granpasochile.cl:443] 
    ↓ (Frontend - React App)
[Frontend Container:80]
    ↓ (API Calls)
[api.granpasochile.cl:443]
    ↓ (Backend - .NET API)
[API Container:80]
    ↓ (Database)
[MySQL Server:3306]
```

## 📝 Notas Importantes

1. **Todos los entornos** (dev/prod) incluyen `inventory.granpasochile.cl` en CORS
2. **Frontend** apunta a `api.granpasochile.cl` en producción
3. **API** acepta requests desde `inventory.granpasochile.cl`
4. **Desarrollo local** sigue funcionando con localhost
5. **GitHub Actions** construye con variables de entorno de producción
