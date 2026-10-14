# Immagine del server centrale (PtlOrchestrator.Api). Contesto di build: la radice del repository.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# prima i soli file di progetto: il restore resta in cache finché non cambiano le dipendenze
COPY Directory.Build.props ./
COPY src/PtlOrchestrator.Contracts/PtlOrchestrator.Contracts.csproj src/PtlOrchestrator.Contracts/
COPY src/PtlOrchestrator.Api/PtlOrchestrator.Api.csproj src/PtlOrchestrator.Api/
RUN dotnet restore src/PtlOrchestrator.Api/PtlOrchestrator.Api.csproj

COPY src/PtlOrchestrator.Contracts/ src/PtlOrchestrator.Contracts/
COPY src/PtlOrchestrator.Api/ src/PtlOrchestrator.Api/
RUN dotnet publish src/PtlOrchestrator.Api/PtlOrchestrator.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# non serve essere root: l'immagine aspnet include l'utente "app"
USER app
ENTRYPOINT ["dotnet", "PtlOrchestrator.Api.dll"]
