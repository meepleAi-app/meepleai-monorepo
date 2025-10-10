FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY src/Api/Api.csproj ./src/Api/
RUN dotnet restore ./src/Api/Api.csproj
COPY src/Api ./src/Api
RUN dotnet publish ./src/Api/Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

# Copy published files
COPY --from=build /app/publish .

# Install curl for healthcheck and clean up in same layer
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Create non-root user for security
RUN adduser --disabled-password --gecos '' --uid 1001 apiuser \
    && chown -R apiuser:apiuser /app

# Switch to non-root user
USER apiuser

EXPOSE 8080
ENTRYPOINT ["dotnet","Api.dll"]
