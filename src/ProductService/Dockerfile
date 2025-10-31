# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files
COPY Atlas.sln .
COPY src/ProductService/ProductService.csproj src/ProductService/
COPY src/Common/Atlas.Common/Atlas.Common.csproj src/Common/Atlas.Common/
COPY src/Common/Atlas.EventBus/Atlas.EventBus.csproj src/Common/Atlas.EventBus/
COPY src/Common/Atlas.Logging/Atlas.Logging.csproj src/Common/Atlas.Logging/

# Restore dependencies
RUN dotnet restore src/ProductService/ProductService.csproj

# Copy all source code
COPY src/ProductService/ src/ProductService/
COPY src/Common/ src/Common/

# Build the application
WORKDIR /src/src/ProductService
RUN dotnet build -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 80

# Copy published output
COPY --from=publish /app/publish .

# Set environment variables
ENV ASPNETCORE_URLS=http://+:80
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "ProductService.dll"]
