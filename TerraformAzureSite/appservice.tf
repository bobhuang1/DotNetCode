# Premium v3 Linux plan; autoscales between 1 and 5 workers on CPU.
resource "azurerm_service_plan" "this" {
  name                = "plan-${local.name}"
  location            = azurerm_resource_group.this.location
  resource_group_name = azurerm_resource_group.this.name
  os_type             = "Linux"
  sku_name            = var.service_plan_sku
  worker_count        = var.service_plan_capacity
  tags                = local.tags
}

resource "azurerm_monitor_autoscale_setting" "this" {
  name                = "as-${local.name}"
  location            = azurerm_resource_group.this.location
  resource_group_name = azurerm_resource_group.this.name
  target_resource_id  = azurerm_service_plan.this.id
  enabled             = true

  profile {
    name = "cpu-autoscale"

    capacity {
      minimum = 1
      maximum = 5
      default = var.service_plan_capacity
    }

    rule {
      metric_trigger {
        metric_name        = "CpuPercentage"
        metric_resource_id = azurerm_service_plan.this.id
        metric_namespace   = "Microsoft.Web/serverfarms"
        time_grain         = "PT1M"
        statistic          = "Average"
        time_window        = "PT5M"
        time_aggregation   = "Average"
        operator           = "GreaterThan"
        threshold          = 70
      }

      scale_action {
        direction = "Increase"
        type      = "ChangeCount"
        value     = "2"
        cooldown  = "PT5M"
      }
    }

    rule {
      metric_trigger {
        metric_name        = "CpuPercentage"
        metric_resource_id = azurerm_service_plan.this.id
        metric_namespace   = "Microsoft.Web/serverfarms"
        time_grain         = "PT1M"
        statistic          = "Average"
        time_window        = "PT10M"
        time_aggregation   = "Average"
        operator           = "LessThan"
        threshold          = 30
      }

      scale_action {
        direction = "Decrease"
        type      = "ChangeCount"
        value     = "1"
        cooldown  = "PT10M"
      }
    }
  }

  tags = local.tags
}

# The app itself. Runs from a zip deploy, uses the VNet for outbound traffic,
# reads config from environment, and exposes a private endpoint so Front Door
# Premium can reach it through a private link. Secrets are Key Vault references
# (resolved by App Service with the app's managed identity over the VNet), so
# the SQL password and Redis key never appear in the app settings themselves.
resource "azurerm_linux_web_app" "this" {
  name                = "app-${local.unique}"
  location            = azurerm_resource_group.this.location
  resource_group_name = azurerm_resource_group.this.name
  service_plan_id     = azurerm_service_plan.this.id
  https_only          = true

  site_config {
    always_on              = true
    minimum_tls_version    = "1.2"
    vnet_route_all_enabled = true
    application_stack {
      dotnet_version = var.webapp_dotnet_version
    }
  }

  app_settings = {
    "WEBSITE_RUN_FROM_PACKAGE"              = "1"
    "APPLICATIONINSIGHTS_CONNECTION_STRING" = azurerm_application_insights.this.connection_string
    "APPINSIGHTS_INSTRUMENTATIONKEY"        = azurerm_application_insights.this.instrumentation_key
    "ConnectionStrings__DefaultConnection"  = local.sql_connection_string_reference
    "REDIS_CONNECTION"                      = local.redis_connection_string_reference
  }

  identity {
    type = "SystemAssigned"
  }

  tags = local.tags
}

# Same shape for the staging slot; a CI/CD pipeline deploys there then swaps.
resource "azurerm_linux_web_app_slot" "staging" {
  name                      = "staging"
  app_service_id            = azurerm_linux_web_app.this.id
  virtual_network_subnet_id = azurerm_subnet.app_integration.id

  site_config {
    always_on              = true
    minimum_tls_version    = "1.2"
    vnet_route_all_enabled = true
    application_stack {
      dotnet_version = var.webapp_dotnet_version
    }
  }

  app_settings = {
    "WEBSITE_RUN_FROM_PACKAGE"              = "1"
    "APPLICATIONINSIGHTS_CONNECTION_STRING" = azurerm_application_insights.this.connection_string
    "APPINSIGHTS_INSTRUMENTATIONKEY"        = azurerm_application_insights.this.instrumentation_key
    "ConnectionStrings__DefaultConnection"  = local.sql_connection_string_reference
    "REDIS_CONNECTION"                      = local.redis_connection_string_reference
  }

  identity {
    type = "SystemAssigned"
  }

  tags = local.tags
}

resource "azurerm_app_service_virtual_network_swift_connection" "this" {
  app_service_id = azurerm_linux_web_app.this.id
  subnet_id      = azurerm_subnet.app_integration.id
}