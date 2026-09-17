resource "azurerm_virtual_network" "this" {
  name                = "vnet-${local.name}"
  location            = azurerm_resource_group.this.location
  resource_group_name = azurerm_resource_group.this.name
  address_space       = [local.vnet_cidr]
  tags                = local.tags
}

# The web app VNet-integration subnet. Linux App Service requires this
# delegation for regional VNet integration.
resource "azurerm_subnet" "app_integration" {
  name                 = "snet-app"
  resource_group_name  = azurerm_resource_group.this.name
  virtual_network_name = azurerm_virtual_network.this.name
  address_prefixes     = [local.app_subnet_cidr]

  delegation {
    name = "delegation"

    service_delegation {
      name    = "Microsoft.Web/serverFarms"
      actions = ["Microsoft.Network/virtualNetworks/subnets/action"]
    }
  }
}

# Shared subnet for every private endpoint in this sample.
resource "azurerm_subnet" "private_endpoints" {
  name                 = "snet-private-endpoints"
  resource_group_name  = azurerm_resource_group.this.name
  virtual_network_name = azurerm_virtual_network.this.name
  address_prefixes     = [local.pe_subnet_cidr]
}

# Private DNS zones used by the private endpoints below.
resource "azurerm_private_dns_zone" "this" {
  for_each = {
    web   = "privatelink.azurewebsites.net"
    sql   = "privatelink.database.windows.net"
    redis = "privatelink.redis.cache.windows.net"
    kv    = "privatelink.vaultcore.azure.net"
  }

  name                = each.value
  resource_group_name = azurerm_resource_group.this.name
  tags                = local.tags
}

resource "azurerm_private_dns_zone_virtual_network_link" "this" {
  for_each = azurerm_private_dns_zone.this

  name                  = "link-${local.name}"
  resource_group_name   = azurerm_resource_group.this.name
  private_dns_zone_name = each.value.name
  virtual_network_id    = azurerm_virtual_network.this.id
}

# One private endpoint per service, all into the shared endpoint subnet. The
# DNS zone group wires each endpoint's FQDNs into the matching private zone.
locals {
  private_links = {
    web = {
      name        = "pe-web"
      location    = var.location
      resource_id = azurerm_linux_web_app.this.id
      subresource = "sites"
      zone_key    = "web"
      manual      = false
    }
    sql1 = {
      name        = "pe-sql-primary"
      location    = var.location
      resource_id = azurerm_mssql_server.primary.id
      subresource = "sqlServer"
      zone_key    = "sql"
      manual      = false
    }
    sql2 = {
      name        = "pe-sql-secondary"
      location    = var.location_secondary
      resource_id = azurerm_mssql_server.secondary.id
      subresource = "sqlServer"
      zone_key    = "sql"
      manual      = false
    }
    redis = {
      name        = "pe-redis"
      location    = var.location
      resource_id = azurerm_redis_cache.this.id
      subresource = "redisCache"
      zone_key    = "redis"
      manual      = false
    }
    kv = {
      name        = "pe-kv"
      location    = var.location
      resource_id = azurerm_key_vault.this.id
      subresource = "vault"
      zone_key    = "kv"
      manual      = false
    }
  }
}

resource "azurerm_private_endpoint" "this" {
  for_each = local.private_links

  name                = each.value.name
  location            = each.value.location
  resource_group_name = azurerm_resource_group.this.name
  subnet_id           = azurerm_subnet.private_endpoints.id

  private_service_connection {
    name                           = "${each.value.name}-conn"
    private_connection_resource_id = each.value.resource_id
    subresource_names              = [each.value.subresource]
    is_manual_connection           = each.value.manual
  }

  private_dns_zone_group {
    name                 = "${each.value.name}-dns"
    private_dns_zone_ids = [azurerm_private_dns_zone.this[each.value.zone_key].id]
  }

  tags = local.tags
}

# The SQL failover group listener is not covered by the endpoint's DNS zone
# group on its own, so publish it explicitly against the primary's IP.
resource "azurerm_private_dns_a_record" "sql_listener" {
  name                = azurerm_mssql_failover_group.this.name
  zone_name           = azurerm_private_dns_zone.this["sql"].name
  resource_group_name = azurerm_resource_group.this.name
  ttl                 = 60
  records             = [azurerm_private_endpoint.this["sql1"].private_service_connection[0].private_ip_address]
}