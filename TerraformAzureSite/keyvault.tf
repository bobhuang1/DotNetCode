data "azurerm_client_config" "current" {}

resource "azurerm_key_vault" "this" {
  name                          = "kv-${local.unique}"
  location                      = azurerm_resource_group.this.location
  resource_group_name           = azurerm_resource_group.this.name
  tenant_id                     = data.azurerm_client_config.current.tenant_id
  sku_name                      = "standard"
  rbac_authorization_enabled    = true
  public_network_access_enabled = false

  # Soft-delete and purge protection are enabled by default in Azure; keep
  # them on (do not disable) or Terraform cannot delete the vault later.
  purge_protection_enabled   = false
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