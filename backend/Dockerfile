# =============================================================================
# BUILD STAGE
# =============================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

# Set up build workspace
WORKDIR /src

# Copy project file and restore dependencies (for better caching)
COPY GistBackend/*.csproj ./GistBackend/
WORKDIR /src/GistBackend
RUN dotnet restore

# Copy source code and build application
COPY . /src
RUN dotnet build

# Publish application for production
RUN dotnet publish -c Release -o /app

# =============================================================================
# RUNTIME STAGE
# =============================================================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

# Set up runtime workspace
WORKDIR /app

# Install system dependencies 
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Create non-root user for security
RUN groupadd --system appuser \
    && useradd --system --no-create-home --gid appuser --shell /usr/sbin/nologin appuser

# Copy application from build stage
COPY --from=build /app ./

# Set proper ownership for non-root user
RUN chown -R appuser:appuser /app

# Configure environment variables
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# Add application health check
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8080/api/v1/gists/health || exit 1

# Switch to non-root user for security
USER appuser

# Start the application
ENTRYPOINT ["dotnet", "GistBackend.dll"]
