# Multi-stage build for Embedded Finance API
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

# Set working directory
WORKDIR /src

# Copy solution and project files
COPY *.sln ./
COPY CoreBanking/*.csproj ./CoreBanking/
COPY SharedKernel/*.csproj ./SharedKernel/
COPY Payments/*.csproj ./Payments/
COPY Compliance/*.csproj ./Compliance/
COPY Security/*.csproj ./Security/
COPY FraudDetection/*.csproj ./FraudDetection/
COPY ApiGateway/*.csproj ./ApiGateway/

# Restore dependencies
RUN dotnet restore

# Copy source code
COPY . .

# Build applications
RUN dotnet build --no-restore --configuration Release

# Publish applications
RUN dotnet publish CoreBanking/CoreBanking.csproj --no-build --configuration Release --output /app/CoreBanking
RUN dotnet publish Compliance/Compliance.csproj --no-build --configuration Release --output /app/Compliance
RUN dotnet publish FraudDetection/FraudDetection.csproj --no-build --configuration Release --output /app/FraudDetection
RUN dotnet publish ApiGateway/ApiGateway.csproj --no-build --configuration Release --output /app/ApiGateway

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

# Create non-root user for security
RUN groupadd -r appuser && useradd -r -g appuser appuser

# Set working directory
WORKDIR /app

# Copy published applications
COPY --from=build /app/CoreBanking ./CoreBanking
COPY --from=build /app/Compliance ./Compliance
COPY --from=build /app/FraudDetection ./FraudDetection
COPY --from=build /app/ApiGateway ./ApiGateway

# Create directories for logs and data
RUN mkdir -p /app/logs /app/data && chown -R appuser:appuser /app

# Switch to non-root user
USER appuser

# Expose ports
EXPOSE 5000 5001 5002 5003

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD curl -f http://localhost:5000/health || exit 1

# Default command (can be overridden)
CMD ["dotnet", "ApiGateway/ApiGateway.dll"] 