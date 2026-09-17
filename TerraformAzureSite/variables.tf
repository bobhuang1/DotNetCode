variable "subscription_id" {
  type        = string
  default     = null
  description = "Azure subscription id. Leave null to use the az CLI default."
}

variable "location" {
  type        = string
  default     = "eastus2"
  description = "Primary region for most resources."
}

variable "location_secondary" {
  type        = string
  default     = "westus2"
  description = "Secondary region for the SQL geo-replica."
}

variable "environment" {
  type        = string
  default     = "dev"
  description = "Environment label; appended to names and the resource tags."
}

variable "name_prefix" {
  type        = string
  default     = "mcs"
  description = "Short prefix for resource names (a random suffix keeps them unique)."
}

variable "tags" {
  type        = map(string)
  default     = {}
  description = "Extra tags merged into every resource."
}

## App Service
variable "service_plan_sku" {
  type        = string
  default     = "P2v3"
  description = "App Service plan SKU (Premium v3 recommended for prod)."
}

variable "service_plan_capacity" {
  type        = number
  default     = 2
  description = "Initial worker count (autoscale scales between 1 and its max)."
}

variable "webapp_dotnet_version" {
  type        = string
  default     = "10.0"
  description = ".NET runtime version the App Service application stack uses."
}

## Azure SQL
variable "sql_admin_login" {
  type        = string
  default     = "sqladmin"
  description = "SQL admin login (a strong random password is generated for you)."
}

variable "sql_sku_name" {
  type        = string
  default     = "BC_Gen5_2"
  description = "Azure SQL database SKU (Business Critical with 2 vCores by default)."
}

variable "sql_max_size_gb" {
  type    = number
  default = 100
}

variable "sql_failover_grace_minutes" {
  type        = number
  default     = 60
  description = "Automatic failover grace period (minutes)."
}

## Redis
variable "redis_sku" {
  type    = string
  default = "Premium"
}

variable "redis_family" {
  type    = string
  default = "P"
}

variable "redis_capacity" {
  type        = number
  default     = 1
  description = "Premium capacity 1 = P1."
}