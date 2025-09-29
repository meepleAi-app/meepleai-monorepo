FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/Api/Api.csproj ./src/Api/
RUN dotnet restore ./src/Api/Api.csproj
COPY src/Api ./src/Api
RUN dotnet publish ./src/Api/Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
EXPOSE 8080
ENTRYPOINT ["dotnet","Api.dll"]
