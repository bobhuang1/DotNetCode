output "resource_group_name" {
  value = azurerm_resource_group.this.name
}

output "web_app_hostname" {
  description = "App Service default hostname (private; reachable via Front Door)."
  value       = azurerm_linux_web_app.this.default_hostname
}

output "staging_slot_hostname" {
  value = azurerm_linux_web_app_slot.staging.default_hostname
}

output "front_door_endpoint" {
  description = "Public endpoint your users hit (Front Door + WAF)."
  value       = format("https://%s/", azurerm_cdn_frontdoor_endpoint.this.host_name)
}

output "sql_failover_listener_fqdn" {
  value = format("%s.database.windows.net", azurerm_mssql_failover_group.this.name)
}

output "redis_host" {
  value = azurerm_redis_cache.this.hostname
}

output "key_vault_uri" {
  value = azurerm_key_vault.this.vault_uri
}

output "web_app_principal_id" {
  description = "System-assigned identity of the web app (for granting roles in CI/CD)."
  value       = azurerm_linux_web_app.this.identity[0].principal_id
}

output "instrumentation_key" {
  value     = azurerm_application_insights.this.instrumentation_key
  sensitive = true
}

output "redis_primary_access_key" {
  value     = azurerm_redis_cache.this.primary_access_key
  sensitive = true
}