FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY src/Api/Api.csproj ./src/Api/
RUN dotnet restore ./src/Api/Api.csproj
COPY src/Api ./src/Api
RUN dotnet publish ./src/Api/Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

# Copy published files and remove debug/doc artifacts to reduce image size (~10MB savings)
COPY --from=build /app/publish .
RUN find . -name "*.pdb" -delete && find . -name "*.xml" -delete

# Install curl for healthcheck and clean up in same layer
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Create non-root user for security
RUN adduser --disabled-password --gecos '' --uid 1001 apiuser \
    && chown -R apiuser:apiuser /app \
    && mkdir -p /app/pdf_uploads \
    && chown -R apiuser:apiuser /app/pdf_uploads

# Switch to non-root user
USER apiuser

EXPOSE 8080
ENTRYPOINT ["dotnet","Api.dll"]
