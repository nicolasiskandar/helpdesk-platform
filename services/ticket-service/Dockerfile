# syntax=docker/dockerfile:1.7
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY TicketService.sln .
COPY src/TicketService.Domain/TicketService.Domain.csproj src/TicketService.Domain/
COPY src/TicketService.Application/TicketService.Application.csproj src/TicketService.Application/
COPY src/TicketService.Infrastructure/TicketService.Infrastructure.csproj src/TicketService.Infrastructure/
COPY src/TicketService.Api/TicketService.Api.csproj src/TicketService.Api/
RUN --mount=type=cache,target=/root/.nuget/packages dotnet restore TicketService.sln

COPY src/ src/
RUN --mount=type=cache,target=/root/.nuget/packages dotnet publish src/TicketService.Api/TicketService.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

RUN adduser --disabled-password --gecos '' appuser

COPY --from=build /app/publish .
COPY entrypoint.sh /app/entrypoint.sh
RUN chmod +x /app/entrypoint.sh
EXPOSE 8080
ENTRYPOINT ["/app/entrypoint.sh"]
