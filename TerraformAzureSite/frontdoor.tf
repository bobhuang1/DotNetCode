# Front Door Premium is required for WAF (managed rules) and private-link
# origins. It fronts the App Service, and the origin reaches the app through
# its private endpoint rather than the public internet.
resource "azurerm_cdn_frontdoor_profile" "this" {
  name                = "fd-${local.unique}"
  resource_group_name = azurerm_resource_group.this.name
  sku_name            = "Premium_AzureFrontDoor"
  tags                = local.tags
}

resource "azurerm_cdn_frontdoor_firewall_policy" "this" {
  name                = "fdfw-${local.unique}"
  resource_group_name = azurerm_resource_group.this.name
  sku_name            = "Premium_AzureFrontDoor"
  mode                = "Prevention"
  enabled             = true

  # OWASP core rules plus a managed bot rule set (start in Log / Detection
  # first when you roll this sample into a real environment).
  managed_rule {
    type    = "Microsoft_DefaultRuleSet"
    version = "2.1"
    action  = "Block"
  }

  managed_rule {
    type    = "Microsoft_BotManagerRuleSet"
    version = "1.0"
    action  = "Log"
  }

  custom_rule {
    name     = "BlockOffRegion"
    enabled  = true
    priority = 100
    type     = "MatchRule"
    action   = "Block"

    match_condition {
      match_variable = "RemoteAddr"
      operator       = "GeoMatch"
      match_values   = ["AQ"]
    }
  }

  tags = local.tags
}

resource "azurerm_cdn_frontdoor_endpoint" "this" {
  name                     = "fdep-${local.unique}"
  cdn_frontdoor_profile_id = azurerm_cdn_frontdoor_profile.this.id
  tags                     = local.tags
}

# Register the endpoint's own .azureedge.net host as a domain so the WAF
# security policy has a concrete domain to bind to.
resource "azurerm_cdn_frontdoor_custom_domain" "this" {
  name                     = "cd-${local.unique}"
  cdn_frontdoor_profile_id = azurerm_cdn_frontdoor_profile.this.id
  host_name                = azurerm_cdn_frontdoor_endpoint.this.host_name

  tls {
    certificate_type = "ManagedCertificate"
    minimum_version  = "TLS12"
  }
}

# WAF policies are attached via a security policy, not directly on the route.
resource "azurerm_cdn_frontdoor_security_policy" "this" {
  name                     = "sp-waf-${local.unique}"
  cdn_frontdoor_profile_id = azurerm_cdn_frontdoor_profile.this.id

  security_policies {
    firewall {
      cdn_frontdoor_firewall_policy_id = azurerm_cdn_frontdoor_firewall_policy.this.id

      association {
        patterns_to_match = ["/*"]

        domain {
          cdn_frontdoor_domain_id = azurerm_cdn_frontdoor_custom_domain.this.id
        }
      }
    }
  }
}

resource "azurerm_cdn_frontdoor_origin_group" "this" {
  name                     = "og-app"
  cdn_frontdoor_profile_id = azurerm_cdn_frontdoor_profile.this.id

  session_affinity_enabled = false

  health_probe {
    path                = "/api/health"
    request_type        = "HEAD"
    protocol            = "Https"
    interval_in_seconds = 60
  }

  load_balancing {
    sample_size                        = 4
    successful_samples_required        = 3
    additional_latency_in_milliseconds = 50
  }
}

resource "azurerm_cdn_frontdoor_origin" "app" {
  name                           = "origin-app"
  cdn_frontdoor_origin_group_id  = azurerm_cdn_frontdoor_origin_group.this.id
  host_name                      = azurerm_linux_web_app.this.default_hostname
  origin_host_header             = azurerm_linux_web_app.this.default_hostname
  http_port                      = 80
  https_port                     = 443
  priority                       = 1
  weight                         = 1000
  certificate_name_check_enabled = false
  enabled                        = true

  # Reach the App Service through its private endpoint (Front Door Premium
  # requests a private link to the app's private endpoint provisioned above).
  private_link {
    private_link_target_id = azurerm_linux_web_app.this.id
    location               = azurerm_resource_group.this.location
    request_message        = "Private link approved by Terraform sample."
  }
}

resource "azurerm_cdn_frontdoor_route" "this" {
  name                            = "route-default"
  cdn_frontdoor_endpoint_id       = azurerm_cdn_frontdoor_endpoint.this.id
  cdn_frontdoor_origin_group_id   = azurerm_cdn_frontdoor_origin_group.this.id
  cdn_frontdoor_origin_ids        = [azurerm_cdn_frontdoor_origin.app.id]
  cdn_frontdoor_custom_domain_ids = [azurerm_cdn_frontdoor_custom_domain.this.id]

  patterns_to_match      = ["/*"]
  supported_protocols    = ["Http", "Https"]
  https_redirect_enabled = true
  forwarding_protocol    = "HttpsOnly"
  link_to_default_domain = true
  enabled                = true
}