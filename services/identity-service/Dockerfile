FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY IdentityService.sln .
COPY src/IdentityService.Domain/IdentityService.Domain.csproj src/IdentityService.Domain/
COPY src/IdentityService.Application/IdentityService.Application.csproj src/IdentityService.Application/
COPY src/IdentityService.Infrastructure/IdentityService.Infrastructure.csproj src/IdentityService.Infrastructure/
COPY src/IdentityService.Api/IdentityService.Api.csproj src/IdentityService.Api/
RUN dotnet restore IdentityService.sln

COPY src/ src/
RUN dotnet publish src/IdentityService.Api/IdentityService.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

RUN adduser --disabled-password --gecos '' appuser
USER appuser

COPY --from=build /app/publish .
EXPOSE 8080

ENTRYPOINT ["dotnet", "IdentityService.Api.dll"]
