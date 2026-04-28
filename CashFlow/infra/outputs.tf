output "lancamentos_url" {
  description = "URL do serviço de Lançamentos no Cloud Run"
  value       = google_cloud_run_v2_service.lancamentos.uri
}

output "consolidado_url" {
  description = "URL do serviço de Consolidado no Cloud Run"
  value       = google_cloud_run_v2_service.consolidado.uri
}

output "pubsub_topic" {
  description = "Nome do tópico Pub/Sub"
  value       = google_pubsub_topic.lancamento_registrado.name
}

output "redis_host" {
  description = "Host do Redis (Memorystore)"
  value       = google_redis_instance.consolidado_cache.host
}
