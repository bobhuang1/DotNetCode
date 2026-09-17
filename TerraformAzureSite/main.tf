resource "random_string" "suffix" {
  length  = 4
  upper   = false
  special = false
}

locals {
  # Stable names and tags shared across every resource.
  tags = merge(var.tags, {
    Environment = var.environment
    Project     = local.name
  })

  # Human friendly nominal name from the prefix; add the suffix to names that
  # must be globally unique (App Service, Front Door, Key Vault, SQL listener).
  name   = "${var.name_prefix}-${var.environment}"
  unique = "${var.name_prefix}-${var.environment}-${random_string.suffix.result}"

  # Networking
  vnet_cidr       = "10.115.0.0/16"
  app_subnet_cidr = "10.115.1.0/24" # delegated to Microsoft.Web/serverFarms for app VNet integration
  pe_subnet_cidr  = "10.115.2.0/24" # private endpoints

  # Connection string the app uses; it targets the failover-group listener so
  # the app follows SQL automatically on region failover.
  sql_connection_string = format(
    "Server=tcp:%s.database.windows.net,1433;Database=%s;User ID=%s;Password=%s;Encrypt=True;TrustServerCertificate=False;MultipleActiveResultSets=false;",
    azurerm_mssql_failover_group.this.name,
    azurerm_mssql_database.this.name,
    var.sql_admin_login,
    random_password.sql_admin.result,
  )
}

resource "azurerm_resource_group" "this" {
  name     = "rg-${local.name}"
  location = var.location
  tags     = local.tags
}