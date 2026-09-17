# Strong SQL admin password; kept in state (and mirrored to Key Vault as a
# secret so nothing needs to be checked in).
resource "random_password" "sql_admin" {
  length      = 24
  special     = false
  min_upper   = 1
  min_lower   = 1
  min_numeric = 1
}

resource "azurerm_mssql_server" "primary" {
  name                          = "sql-${local.unique}"
  resource_group_name           = azurerm_resource_group.this.name
  location                      = azurerm_resource_group.this.location
  version                       = "12.0"
  administrator_login           = var.sql_admin_login
  administrator_login_password  = random_password.sql_admin.result
  minimum_tls_version           = "1.2"
  public_network_access_enabled = false
  tags                          = local.tags
}

# Geo replica in the secondary region, joined to the primary by a failover
# group so the app's connection string (which targets the group listener)
# keeps working across regions.
resource "azurerm_mssql_server" "secondary" {
  name                          = "sql-${local.unique}-dr"
  resource_group_name           = azurerm_resource_group.this.name
  location                      = var.location_secondary
  version                       = "12.0"
  administrator_login           = var.sql_admin_login
  administrator_login_password  = random_password.sql_admin.result
  minimum_tls_version           = "1.2"
  public_network_access_enabled = false
  tags                          = local.tags
}

resource "azurerm_mssql_database" "this" {
  name        = "db-${local.name}"
  server_id   = azurerm_mssql_server.primary.id
  sku_name    = var.sql_sku_name
  max_size_gb = var.sql_max_size_gb
  tags        = local.tags
}

resource "azurerm_mssql_failover_group" "this" {
  name      = "fg-${local.unique}"
  server_id = azurerm_mssql_server.primary.id

  partner_server {
    id = azurerm_mssql_server.secondary.id
  }

  databases = [azurerm_mssql_database.this.id]

  read_write_endpoint_failover_policy {
    mode          = "Automatic"
    grace_minutes = var.sql_failover_grace_minutes
  }

  readonly_endpoint_failover_policy_enabled = true
}