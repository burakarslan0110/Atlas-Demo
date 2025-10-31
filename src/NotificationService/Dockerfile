# Multi-stage Dockerfile for NotificationService

# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files
COPY Atlas.sln .
COPY src/NotificationService/NotificationService.csproj src/NotificationService/
COPY src/Common/Atlas.Tracing/Atlas.Tracing.csproj src/Common/Atlas.Tracing/

# Restore dependencies (including pre-release packages for OpenTelemetry)
RUN dotnet restore src/NotificationService/NotificationService.csproj --use-lock-file

# Copy source code
COPY src/NotificationService/ src/NotificationService/
COPY src/Common/Atlas.Tracing/ src/Common/Atlas.Tracing/

# Build the application
WORKDIR "/src/src/NotificationService"
RUN dotnet build "NotificationService.csproj" -c Release -o /app/build

# Stage 2: Publish
FROM build AS publish
RUN dotnet publish "NotificationService.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Copy published files
COPY --from=publish /app/publish .

# Create non-root user
RUN useradd -m -u 1000 notificationuser && chown -R notificationuser:notificationuser /app
USER notificationuser

# Expose port
EXPOSE 8080

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "NotificationService.dll"]
