variable "project_id" {
  description = "ID do projeto GCP"
  type        = string
}

variable "region" {
  description = "Região GCP para os recursos"
  type        = string
  default     = "us-east1"
}

variable "db_tier_sqlserver" {
  description = "Tier do Cloud SQL SQL Server"
  type        = string
  default     = "db-custom-2-4096"  # 2 vCPU, 4GB RAM — mínimo recomendado para SQL Server
}

variable "db_password" {
  description = "Senha do usuário do banco de dados"
  type        = string
  sensitive   = true
}
