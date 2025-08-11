# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

# Install native dependencies for AOT compilation
RUN apt-get update && apt-get install -y clang zlib1g-dev

# Copy csproj and restore dependencies
COPY *.csproj ./
RUN dotnet restore

# Copy source code
COPY . ./

# Publish with AOT
RUN dotnet publish -r linux-x64 -c Release -o out

# Runtime stage - use runtime-deps for AOT apps
FROM mcr.microsoft.com/dotnet/runtime-deps:9.0 AS runtime
WORKDIR /app

# Create app user for security
RUN groupadd -g 1001 appgroup && \
  useradd -u 1001 -g appgroup -s /bin/sh appuser

# Copy the published app
COPY --from=build --chown=appuser:appgroup /app/out .

# Switch to non-root user
USER appuser

# Expose port
EXPOSE 8080

# Set entrypoint
ENTRYPOINT ["./backend"]