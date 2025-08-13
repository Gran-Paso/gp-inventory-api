# Etapa de construcción
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar archivos de solución y proyectos
COPY GPInventory.sln ./
COPY src/GPInventory.Api/GPInventory.Api.csproj src/GPInventory.Api/
COPY src/GPInventory.Application/GPInventory.Application.csproj src/GPInventory.Application/
COPY src/GPInventory.Domain/GPInventory.Domain.csproj src/GPInventory.Domain/
COPY src/GPInventory.Infrastructure/GPInventory.Infrastructure.csproj src/GPInventory.Infrastructure/

# Restaurar dependencias
RUN dotnet restore GPInventory.sln

# Copiar todo el código fuente
COPY . .

# Compilar y publicar la aplicación
WORKDIR /src/src/GPInventory.Api
RUN dotnet publish GPInventory.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# Etapa de runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Instalar dependencias para la cultura es-ES (opcional pero recomendado)
RUN apt-get update && apt-get install -y locales && \
    sed -i '/es_ES.UTF-8/s/^# //g' /etc/locale.gen && \
    locale-gen && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# Configurar variables de entorno
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:80
ENV LANG=es_ES.UTF-8
ENV LANGUAGE=es_ES:es
ENV LC_ALL=es_ES.UTF-8

# Copiar archivos publicados
COPY --from=build /app/publish .

# Crear usuario no-root para seguridad
RUN adduser --disabled-password --gecos '' appuser && chown -R appuser /app
USER appuser

# Exponer puerto 80
EXPOSE 80

# Punto de entrada
ENTRYPOINT ["dotnet", "GPInventory.Api.dll"]
