# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy common libraries first
COPY ["src/Common/Atlas.Common/Atlas.Common.csproj", "Common/Atlas.Common/"]

# Copy Frontend project
COPY ["src/Frontend/Frontend.csproj", "Frontend/"]

# Restore dependencies
RUN dotnet restore "Frontend/Frontend.csproj"

# Copy source code
COPY src/Common/Atlas.Common/. Common/Atlas.Common/
COPY src/Frontend/. Frontend/

# Build
WORKDIR "/src/Frontend"
RUN dotnet build "Frontend.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "Frontend.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 80
EXPOSE 443

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Frontend.dll"]
