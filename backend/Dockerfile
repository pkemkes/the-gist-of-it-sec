# =============================================================================
# BUILD STAGE
# =============================================================================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

# Set up build workspace
WORKDIR /src

# Copy project file and restore dependencies (for better caching)
COPY GistBackend/*.csproj ./GistBackend/
WORKDIR /src/GistBackend
RUN dotnet restore

# Copy source code and build application
COPY . /src
RUN dotnet build

# Install Playwright CLI and set up browser automation
RUN dotnet tool install --global Microsoft.Playwright.CLI
ENV PATH="${PATH}:/root/.dotnet/tools"
RUN playwright install chromium

# Publish application for production
RUN dotnet publish -c Release -o /app

# =============================================================================
# RUNTIME STAGE
# =============================================================================
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final

# Set up runtime workspace
WORKDIR /app

# Install system dependencies for Playwright/Chromium (headless mode)
RUN apt-get update && apt-get install -y \
    wget \
    curl \
    ca-certificates \
    fonts-liberation \
    libasound2 \
    libatk-bridge2.0-0 \
    libatk1.0-0 \
    libatspi2.0-0 \
    libcairo2 \
    libcups2 \
    libdbus-1-3 \
    libdrm2 \
    libexpat1 \
    libgbm1 \
    libglib2.0-0 \
    libnss3 \
    libpango-1.0-0 \
    libuuid1 \
    libx11-6 \
    libx11-xcb1 \
    libxcb1 \
    libxcomposite1 \
    libxdamage1 \
    libxext6 \
    libxfixes3 \
    libxkbcommon0 \
    libxrandr2 \
    libbrotli1 \
    x11-utils \
    xauth \
    && rm -rf /var/lib/apt/lists/*

# Create non-root user for security
RUN adduser --disabled-password --gecos "" --no-create-home appuser

# Copy application and Playwright browsers from build stage
COPY --from=build /app ./
COPY --from=build /root/.cache/ms-playwright /home/appuser/.cache/ms-playwright

# Set proper ownership for non-root user
RUN chown -R appuser:appuser /app && \
    chown -R appuser:appuser /home/appuser/.cache

# Configure environment variables
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    PLAYWRIGHT_BROWSERS_PATH=/home/appuser/.cache/ms-playwright

# Add application health check
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8080/api/v1/gists/health || exit 1

# Switch to non-root user for security
USER appuser

# Start the application
ENTRYPOINT ["dotnet", "GistBackend.dll"]
