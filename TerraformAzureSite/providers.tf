terraform {
  required_version = ">= 1.9"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.6"
    }
    time = {
      source  = "hashicorp/time"
      version = "~> 0.12"
    }
  }
}

provider "azurerm" {
  features {
    # Application Insights creates a "Smart Detection" action group in the resource
    # group that Terraform doesn't manage; without this, destroy stops on it.
    resource_group {
      prevent_deletion_if_contains_resources = false
    }
  }

  # Optional; omit to use your az CLI default subscription.
  subscription_id = var.subscription_id
}