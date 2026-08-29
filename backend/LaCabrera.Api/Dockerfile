# ============================================
# LA CABRERA - Backend API Dockerfile
# Multi-stage build for optimized image
# ============================================

# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["backend/LaCabrera.Api/LaCabrera.Api.csproj", "backend/LaCabrera.Api/"]
RUN dotnet restore "backend/LaCabrera.Api/LaCabrera.Api.csproj"

# Copy source code and build
COPY backend/LaCabrera.Api/. backend/LaCabrera.Api/
WORKDIR "/src/backend/LaCabrera.Api"
RUN dotnet build "LaCabrera.Api.csproj" -c Release -o /app/build

# Stage 2: Publish
FROM build AS publish
RUN dotnet publish "LaCabrera.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Create non-root user for security
RUN adduser --disabled-password --gecos '' appuser

# Copy published app
COPY --from=publish /app/publish .

# Create logs directory
RUN mkdir -p /app/logs && chown -R appuser:appuser /app/logs

# Switch to non-root user
USER appuser

# Expose port
EXPOSE 8080

# Environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/api/health || exit 1

# Entry point
ENTRYPOINT ["dotnet", "LaCabrera.Api.dll"]
