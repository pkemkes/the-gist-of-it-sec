# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies for main project only
COPY GistBackend/*.csproj ./GistBackend/
WORKDIR /src/GistBackend
RUN dotnet restore

# Copy everything else and build
COPY . /src
RUN dotnet publish -c Release -o /app

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Create non-root user with minimal permissions
RUN adduser --disabled-password --gecos "" --no-create-home appuser

# Copy built application
COPY --from=build /app ./

# Set proper ownership
RUN chown -R appuser:appuser /app

# Environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# Add healthcheck
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD curl -f http://localhost:8080/api/v1/gists/health || exit 1

# Switch to non-root user
USER appuser

# Start the application
ENTRYPOINT ["dotnet", "GistBackend.dll"]
