FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY backend/src/Directory.Build.props backend/src/
COPY backend/src/Fundo.Domain/Fundo.Domain.csproj backend/src/Fundo.Domain/
COPY backend/src/Fundo.Application/Fundo.Application.csproj backend/src/Fundo.Application/
COPY backend/src/Fundo.Infrastructure/Fundo.Infrastructure.csproj backend/src/Fundo.Infrastructure/
COPY backend/src/Fundo.Applications.WebApi/Fundo.Applications.WebApi.csproj backend/src/Fundo.Applications.WebApi/
RUN dotnet restore backend/src/Fundo.Applications.WebApi/Fundo.Applications.WebApi.csproj

COPY backend/src backend/src
RUN dotnet publish backend/src/Fundo.Applications.WebApi/Fundo.Applications.WebApi.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
USER root
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Fundo.Applications.WebApi.dll"]
