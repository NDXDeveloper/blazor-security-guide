# =============================================================================
# Dockerfile API Interne - Blazor Server
# =============================================================================
# Cette API n'est PAS exposée à Internet.
# Elle est accessible uniquement depuis le réseau Docker "backend".
# Pas besoin de CORS, rate limiting minimal, auth simplifiée.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY *.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Utilisateur non-root
RUN useradd --create-home --no-log-init appuser
USER appuser

COPY --from=build /app/publish .

# Port interne uniquement
EXPOSE 5001

ENTRYPOINT ["dotnet", "Api.dll"]
