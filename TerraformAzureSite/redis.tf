# Azure Managed Redis. (Azure Cache for Redis is retiring and no longer accepts
# new caches.) Reached only through its private endpoint inside the VNet.
resource "azurerm_managed_redis" "this" {
  name                  = "redis-${local.unique}"
  location              = azurerm_resource_group.this.location
  resource_group_name   = azurerm_resource_group.this.name
  sku_name              = var.redis_sku
  public_network_access = "Disabled"
  tags                  = local.tags

  default_database {
    # The app connects with a key (kept in Key Vault). Switch to Entra ID auth
    # and set this to false for a key-free setup.
    access_keys_authentication_enabled = true
    client_protocol                    = "Encrypted"
  }
}
