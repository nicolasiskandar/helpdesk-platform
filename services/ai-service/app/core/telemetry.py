"""OpenTelemetry wiring for the AI service.

Exports spans (OTLP gRPC) to the shared collector at ``otel-collector:4317`` so
AI service requests show up in Jaeger alongside the .NET services. Telemetry is
a no-op when the endpoint is empty (e.g. local pytest runs).
"""

import logging

from fastapi import FastAPI

from app.core.config import Settings

logger = logging.getLogger(__name__)

_initialized = False


def setup_telemetry(app: FastAPI, settings: Settings) -> None:
    global _initialized

    if not settings.otel_exporter_otlp_endpoint or _initialized:
        return

    try:
        from opentelemetry import trace
        from opentelemetry.exporter.otlp.proto.grpc.trace_exporter import OTLPSpanExporter
        from opentelemetry.instrumentation.fastapi import FastAPIInstrumentor
        from opentelemetry.sdk.resources import Resource
        from opentelemetry.sdk.trace import TracerProvider
        from opentelemetry.sdk.trace.export import BatchSpanProcessor

        resource = Resource.create({"service.name": settings.service_name})
        provider = TracerProvider(resource=resource)
        provider.add_span_processor(BatchSpanProcessor(OTLPSpanExporter(endpoint=settings.otel_exporter_otlp_endpoint)))
        trace.set_tracer_provider(provider)
        FastAPIInstrumentor.instrument_app(app)
        _initialized = True
        logger.info("OpenTelemetry enabled (OTLP -> %s)", settings.otel_exporter_otlp_endpoint)
    except Exception:
        logger.exception("Failed to initialize OpenTelemetry")
