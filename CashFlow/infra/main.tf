terraform {
  required_version = ">= 1.6"
  required_providers {
    google = {
      source  = "hashicorp/google"
      version = "~> 5.0"
    }
  }
}

provider "google" {
  project = var.project_id
  region  = var.region
}

# ── Artifact Registry ────────────────────────────────────────────────────────
resource "google_artifact_registry_repository" "cashflow" {
  location      = var.region
  repository_id = "cashflow"
  format        = "DOCKER"
  description   = "Imagens Docker dos microsserviços CashFlow"
}

# ── Pub/Sub ──────────────────────────────────────────────────────────────────
resource "google_pubsub_topic" "lancamento_registrado" {
  name = "lancamento-registrado"

  message_retention_duration = "604800s" # 7 dias
}

resource "google_pubsub_topic" "lancamento_dlq" {
  name = "lancamento-registrado-dlq"
}

resource "google_pubsub_subscription" "consolidado_sub" {
  name  = "consolidado-sub"
  topic = google_pubsub_topic.lancamento_registrado.name

  ack_deadline_seconds = 30

  retry_policy {
    minimum_backoff = "10s"
    maximum_backoff = "300s"
  }

  dead_letter_policy {
    dead_letter_topic     = google_pubsub_topic.lancamento_dlq.id
    max_delivery_attempts = 5
  }
}

# ── Cloud SQL — SQL Server (Lançamentos) ─────────────────────────────────────
resource "google_sql_database_instance" "lancamentos" {
  name             = "cashflow-lancamentos"
  database_version = "SQLSERVER_2022_EXPRESS"
  region           = var.region

  settings {
    tier              = var.db_tier_sqlserver
    availability_type = "REGIONAL"

    backup_configuration {
      enabled = true
      start_time = "03:00"
    }

    ip_configuration {
      ipv4_enabled    = false
      private_network = google_compute_network.cashflow_vpc.id
    }
  }

  deletion_protection = true
}

resource "google_sql_database" "lancamentos_db" {
  name     = "cashflow_lancamentos"
  instance = google_sql_database_instance.lancamentos.name
}

resource "google_sql_user" "lancamentos_user" {
  name     = "cashflow"
  instance = google_sql_database_instance.lancamentos.name
  password = var.db_password
  type     = "BUILT_IN"
}

# ── Cloud SQL — SQL Server (Consolidado) ──────────────────────────────────────
resource "google_sql_database_instance" "consolidado" {
  name             = "cashflow-consolidado"
  database_version = "SQLSERVER_2022_EXPRESS"
  region           = var.region

  settings {
    tier              = var.db_tier_sqlserver
    availability_type = "REGIONAL"

    backup_configuration {
      enabled    = true
      start_time = "03:30"
    }

    ip_configuration {
      ipv4_enabled    = false
      private_network = google_compute_network.cashflow_vpc.id
    }
  }

  deletion_protection = true
}

resource "google_sql_database" "consolidado_db" {
  name     = "cashflow_consolidado"
  instance = google_sql_database_instance.consolidado.name
}

resource "google_sql_user" "consolidado_user" {
  name     = "cashflow"
  instance = google_sql_database_instance.consolidado.name
  password = var.db_password
  type     = "BUILT_IN"
}

# ── Memorystore Redis ─────────────────────────────────────────────────────────
resource "google_redis_instance" "consolidado_cache" {
  name           = "cashflow-redis"
  tier           = "BASIC"
  memory_size_gb = 1
  region         = var.region

  authorized_network = google_compute_network.cashflow_vpc.id
  connect_mode       = "PRIVATE_SERVICE_ACCESS"

  redis_version = "REDIS_7_0"
  display_name  = "CashFlow Consolidado Cache"
}

# ── VPC ───────────────────────────────────────────────────────────────────────
resource "google_compute_network" "cashflow_vpc" {
  name                    = "cashflow-vpc"
  auto_create_subnetworks = false
}

resource "google_compute_subnetwork" "cashflow_subnet" {
  name          = "cashflow-subnet"
  ip_cidr_range = "10.0.0.0/24"
  region        = var.region
  network       = google_compute_network.cashflow_vpc.id
}

# ── Secret Manager ────────────────────────────────────────────────────────────
resource "google_secret_manager_secret" "jwt_key" {
  secret_id = "cashflow-jwt-key"
  replication {
    auto {}
  }
}

resource "google_secret_manager_secret" "db_password" {
  secret_id = "cashflow-db-password"
  replication {
    auto {}
  }
}

# ── Cloud Run — Lançamentos ───────────────────────────────────────────────────
resource "google_cloud_run_v2_service" "lancamentos" {
  name     = "cashflow-lancamentos"
  location = var.region

  template {
    scaling {
      min_instance_count = 1
      max_instance_count = 10
    }

    containers {
      image = "${var.region}-docker.pkg.dev/${var.project_id}/cashflow/lancamentos:latest"

      resources {
        limits = {
          cpu    = "1"
          memory = "512Mi"
        }
      }

      env {
        name  = "ASPNETCORE_ENVIRONMENT"
        value = "Production"
      }

      env {
        name  = "PubSub__ProjectId"
        value = var.project_id
      }

      env {
        name  = "PubSub__TopicId"
        value = google_pubsub_topic.lancamento_registrado.name
      }

      env {
        name = "Jwt__Key"
        value_source {
          secret_key_ref {
            secret  = google_secret_manager_secret.jwt_key.secret_id
            version = "latest"
          }
        }
      }
    }

    vpc_access {
      network_interfaces {
        network    = google_compute_network.cashflow_vpc.name
        subnetwork = google_compute_subnetwork.cashflow_subnet.name
      }
      egress = "ALL_TRAFFIC"
    }
  }
}

# ── Cloud Run — Consolidado ───────────────────────────────────────────────────
resource "google_cloud_run_v2_service" "consolidado" {
  name     = "cashflow-consolidado"
  location = var.region

  template {
    scaling {
      min_instance_count = 1
      max_instance_count = 20 # mais instâncias pois recebe 50 req/s
    }

    containers {
      image = "${var.region}-docker.pkg.dev/${var.project_id}/cashflow/consolidado:latest"

      resources {
        limits = {
          cpu    = "1"
          memory = "512Mi"
        }
      }

      env {
        name  = "ASPNETCORE_ENVIRONMENT"
        value = "Production"
      }

      env {
        name  = "Redis__ConnectionString"
        value = "${google_redis_instance.consolidado_cache.host}:6379"
      }

      env {
        name  = "PubSub__ProjectId"
        value = var.project_id
      }

      env {
        name  = "PubSub__SubscriptionId"
        value = google_pubsub_subscription.consolidado_sub.name
      }

      env {
        name = "Jwt__Key"
        value_source {
          secret_key_ref {
            secret  = google_secret_manager_secret.jwt_key.secret_id
            version = "latest"
          }
        }
      }
    }

    vpc_access {
      network_interfaces {
        network    = google_compute_network.cashflow_vpc.name
        subnetwork = google_compute_subnetwork.cashflow_subnet.name
      }
      egress = "ALL_TRAFFIC"
    }
  }
}
