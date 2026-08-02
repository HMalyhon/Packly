# Built from the repository root:
#   docker build -f src/Packly.Notification/Dockerfile .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Build configuration and project files first so the restore layer caches.
# .editorconfig carries the analyzer severities and the build fails without it.
COPY Directory.Build.props Directory.Packages.props stylecop.json .editorconfig ./
COPY src/Packly.Contracts/Packly.Contracts.csproj src/Packly.Contracts/
COPY src/Packly.Messaging/Packly.Messaging.csproj src/Packly.Messaging/
COPY src/Packly.Notification/Packly.Notification.csproj src/Packly.Notification/
RUN dotnet restore src/Packly.Notification/Packly.Notification.csproj

COPY src/ src/
RUN dotnet publish src/Packly.Notification/Packly.Notification.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
COPY --from=build /app .

# The base image defines APP_UID but does not switch to it.
USER $APP_UID

ENTRYPOINT ["dotnet", "Packly.Notification.dll"]
