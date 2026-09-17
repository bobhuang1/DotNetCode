# Premium tier so the cache can sit behind a private endpoint inside the VNet.
resource "azurerm_redis_cache" "this" {
  name                          = "redis-${local.unique}"
  location                      = azurerm_resource_group.this.location
  resource_group_name           = azurerm_resource_group.this.name
  capacity                      = var.redis_capacity
  family                        = var.redis_family
  sku_name                      = var.redis_sku
  non_ssl_port_enabled          = false
  minimum_tls_version           = "1.2"
  public_network_access_enabled = false
  tags                          = local.tags
}