# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files
COPY Atlas.sln .
COPY src/ApiGateway/ApiGateway.csproj src/ApiGateway/
COPY src/Common/Atlas.Logging/Atlas.Logging.csproj src/Common/Atlas.Logging/

# Restore dependencies
RUN dotnet restore src/ApiGateway/ApiGateway.csproj

# Copy all source code
COPY src/ApiGateway/ src/ApiGateway/
COPY src/Common/Atlas.Logging/ src/Common/Atlas.Logging/

# Build the application
WORKDIR /src/src/ApiGateway
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

ENTRYPOINT ["dotnet", "ApiGateway.dll"]
