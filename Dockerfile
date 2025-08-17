FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends clang zlib1g-dev
COPY src/*.csproj ./
RUN dotnet restore
COPY src ./
RUN dotnet publish -r linux-amd64 -c Release -o out
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/out ./
ENTRYPOINT ["./api"]