# Built from the repository root, not from this directory, because the build
# depends on Directory.Build.props and friends:
#   docker build -f src/Packly.Api/Dockerfile .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Build configuration and project files first, so the restore layer is cached and
# only re-runs when dependencies actually change - not on every source edit.
# .editorconfig is not optional here: it carries the analyzer severities, and
# without it the analyzers fail this build under TreatWarningsAsErrors.
COPY Directory.Build.props Directory.Packages.props stylecop.json .editorconfig ./
COPY src/Packly.Contracts/Packly.Contracts.csproj src/Packly.Contracts/
COPY src/Packly.Api/Packly.Api.csproj src/Packly.Api/
RUN dotnet restore src/Packly.Api/Packly.Api.csproj

COPY src/ src/
RUN dotnet publish src/Packly.Api/Packly.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .

# The aspnet image defines APP_UID but does not switch to it, so without this the
# container runs as root. The image already listens on 8080.
USER $APP_UID

EXPOSE 8080
ENTRYPOINT ["dotnet", "Packly.Api.dll"]
