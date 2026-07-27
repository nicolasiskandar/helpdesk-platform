#!/bin/sh
chown -R appuser:appuser /app/uploads
exec su -s /bin/sh appuser -c 'exec dotnet TicketService.Api.dll'
