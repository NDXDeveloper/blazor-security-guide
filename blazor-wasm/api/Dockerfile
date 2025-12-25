# =============================================================================
# Dockerfile API - Blazor WASM
# =============================================================================
# Cette API est PUBLIQUE - elle sera accessible depuis Internet.
# Toutes les protections doivent être implémentées dans le code.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copier les fichiers projet et restaurer
COPY *.csproj ./
RUN dotnet restore

# Copier le reste et publier
COPY . ./
RUN dotnet publish -c Release -o /app/publish

# Image finale
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Créer un utilisateur non-root pour la sécurité
RUN useradd --create-home --no-log-init appuser
USER appuser

COPY --from=build /app/publish .

# Port exposé (interne au réseau Docker)
EXPOSE 5000

ENTRYPOINT ["dotnet", "Api.dll"]
