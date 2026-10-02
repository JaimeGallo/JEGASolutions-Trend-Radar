FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props .editorconfig ./
COPY src/TrendRadar.Domain/TrendRadar.Domain.csproj src/TrendRadar.Domain/
COPY src/TrendRadar.Application/TrendRadar.Application.csproj src/TrendRadar.Application/
COPY src/TrendRadar.Infrastructure/TrendRadar.Infrastructure.csproj src/TrendRadar.Infrastructure/
COPY src/TrendRadar.API/TrendRadar.API.csproj src/TrendRadar.API/
RUN dotnet restore src/TrendRadar.API/TrendRadar.API.csproj
COPY src/ src/
RUN dotnet publish src/TrendRadar.API/TrendRadar.API.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
# Archivos de evidencia: el volumen hereda este propietario la primera vez.
RUN mkdir -p /data/evidence && chown app /data/evidence
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "TrendRadar.API.dll"]
