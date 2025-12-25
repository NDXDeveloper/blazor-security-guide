# =============================================================================
# Dockerfile Blazor Server
# =============================================================================
# Cette application gère les connexions SignalR des utilisateurs.
# Elle communique avec l'API interne sur le réseau Docker privé.

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

EXPOSE 5000

ENTRYPOINT ["dotnet", "BlazorServer.dll"]
