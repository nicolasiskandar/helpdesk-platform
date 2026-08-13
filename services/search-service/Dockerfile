# syntax=docker/dockerfile:1.7
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY SearchService.sln .
COPY src/SearchService.Api/SearchService.Api.csproj src/SearchService.Api/
RUN --mount=type=cache,target=/root/.nuget/packages dotnet restore SearchService.sln
COPY src/ src/
RUN --mount=type=cache,target=/root/.nuget/packages dotnet publish src/SearchService.Api/SearchService.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/* && adduser --disabled-password --gecos '' appuser
COPY --from=build /app/publish .
USER appuser
EXPOSE 8080
ENTRYPOINT ["dotnet", "SearchService.Api.dll"]
