#!/bin/sh
set -e

# Named volumes are initialized root:root by Docker; fix ownership before
# dropping privileges (same pattern as the ticket service entrypoint).
if [ "$(id -u)" = "0" ]; then
    mkdir -p /data
    chown -R appuser:appuser /data
    exec su -s /bin/sh appuser -c "./entrypoint.sh"
fi

# Pull Ollama models in the BACKGROUND so the service is up and healthy
# immediately. First boot warms models over a few minutes; the /health/ready
# endpoint and chat route report model readiness until they arrive.
echo "Starting AI service (models warm up in the background)..."
nohup python /app/app/scripts/pull_models.py > /data/model-pull.log 2>&1 &

echo "Starting uvicorn..."
exec uvicorn app.main:app --host 0.0.0.0 --port "${PORT:-8080}"
