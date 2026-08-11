from functools import lru_cache

from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env", extra="ignore")

    service_name: str = "helpdesk-ai-service"
    ollama_url: str = "http://ollama:11434"
    qdrant_url: str = "http://qdrant:6333"
    rabbitmq_url: str = "amqp://guest:guest@rabbitmq:5672/"
    rabbitmq_exchange: str = "ticket.events"
    index_queue: str = "ai-index.q"
    collection_name: str = "helpdesk_index"
    vector_size: int = 768

    chat_model: str = "llama3.2:3b"
    embedding_model: str = "nomic-embed-text"

    jwt_public_key_path: str = "/app/certs/public.pem"
    jwt_audience: str = "it-helpdesk-api"
    ticket_service_base_url: str = "http://ticket-service:8080"
    ai_service_key: str = ""

    chunk_size: int = 800
    chunk_overlap: int = 80
    top_k: int = 5
    similar_scan: int = 20
    max_tokens: int = 512
    temperature: float = 0.3

    dedup_db_path: str = "/data/dedup.db"
    dedup_ttl_seconds: int = 604800


@lru_cache
def get_settings() -> Settings:
    return Settings()
