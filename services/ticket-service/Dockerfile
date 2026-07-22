FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY TicketService.sln .
COPY src/TicketService.Domain/TicketService.Domain.csproj src/TicketService.Domain/
COPY src/TicketService.Application/TicketService.Application.csproj src/TicketService.Application/
COPY src/TicketService.Infrastructure/TicketService.Infrastructure.csproj src/TicketService.Infrastructure/
COPY src/TicketService.Api/TicketService.Api.csproj src/TicketService.Api/
RUN dotnet restore TicketService.sln

COPY src/ src/
RUN dotnet publish src/TicketService.Api/TicketService.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

RUN adduser --disabled-password --gecos '' appuser
USER appuser

COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "TicketService.Api.dll"]
