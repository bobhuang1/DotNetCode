data "azurerm_client_config" "current" {}

resource "azurerm_key_vault" "this" {
  name                          = "kv-${local.unique}"
  location                      = azurerm_resource_group.this.location
  resource_group_name           = azurerm_resource_group.this.name
  tenant_id                     = data.azurerm_client_config.current.tenant_id
  sku_name                      = "standard"
  rbac_authorization_enabled = true

  # The app reads the vault through its private endpoint. The machine running
  # `terraform apply` also has to reach the data plane to write the secrets
  # below: either run it from inside the VNet, or list the runner's public IPs
  # in key_vault_allowed_ip_ranges (public access is then on, deny-by-default).
  public_network_access_enabled = length(var.key_vault_allowed_ip_ranges) > 0

  network_acls {
    default_action = "Deny"
    bypass         = "AzureServices"
    ip_rules       = var.key_vault_allowed_ip_ranges
  }

  # Soft delete is always on (deleted vaults are kept for the retention days).
  # Purge protection is opt-in: once enabled it can never be turned off, and a
  # deleted vault (and its name) cannot be purged until retention ends, so
  # `terraform destroy` followed by a re-apply with the same name will fail.
  # Turn it on for production.
  purge_protection_enabled   = var.key_vault_purge_protection_enabled
  soft_delete_retention_days = 90

  tags = local.tags
}

# Give the web app's system-assigned identity read access to secrets, so the
# app can pull real secrets at runtime instead of relying on app settings.
resource "azurerm_role_assignment" "app_secrets_user" {
  scope                = azurerm_key_vault.this.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_linux_web_app.this.identity[0].principal_id
}

# The staging slot has its own identity and resolves the same Key Vault
# references, so it needs the same role.
resource "azurerm_role_assignment" "staging_secrets_user" {
  scope                = azurerm_key_vault.this.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_linux_web_app_slot.staging.identity[0].principal_id
}

# Sample secrets created from values Terraform already manages. Your az CLI
# principal needs a Key Vault data-plane role (e.g. "Key Vault Administrator")
# for the apply step; see the README.
resource "azurerm_key_vault_secret" "sql_admin_password" {
  name         = "sql-admin-password"
  value        = random_password.sql_admin.result
  key_vault_id = azurerm_key_vault.this.id
}

resource "azurerm_key_vault_secret" "sql_connection_string" {
  name         = "sql-connection-string"
  value        = local.sql_connection_string
  key_vault_id = azurerm_key_vault.this.id
}

resource "azurerm_key_vault_secret" "redis_connection_string" {
  name         = "redis-connection-string"
  value        = azurerm_redis_cache.this.primary_connection_string
  key_vault_id = azurerm_key_vault.this.id
}