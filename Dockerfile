# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore src/Api/gesFactu.Api/gesFactu.Api.csproj
RUN dotnet publish src/Api/gesFactu.Api/gesFactu.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN addgroup --system gesfactu \
    && adduser --system --ingroup gesfactu --home /app gesfactu

COPY --from=build --chown=gesfactu:gesfactu /app/publish .

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_EnableDiagnostics=0

EXPOSE 8080

USER gesfactu

ENTRYPOINT ["dotnet", "gesFactu.Api.dll"]
