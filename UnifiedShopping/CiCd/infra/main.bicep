// Bicep infrastructure for UnifiedShopping: two App Services (API + web) with
// staging slots, Azure SQL, Key Vault, Application Insights. TerraformAzureSite in
// this repo shows the "front door + WAF + private endpoints" shape; this bicep is
// the minimal production starting point sized for CI/CD-driven deploys.

@description('Prefix for resource names')
param namePrefix string = 'shop'

@description('Azure region')
param location string = resourceGroup().location

@description('Environment tag')
param environment string = 'prod'

@secure()
@description('SQL admin password (pass via parameter file or Key Vault)')
param sqlAdminPassword secure string

var sqlServerName = '${namePrefix}-sql-${uniqueString(resourceGroup().id)}'
var appNameApi = 'app-${namePrefix}-api'
var appNameWeb = 'app-${namePrefix}-web'
var keyVaultName = 'kv-${namePrefix}-${uniqueString(resourceGroup().id)}'

resource insights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'appi-${namePrefix}'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    RetentionInDays: 30
  }
}

resource plan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: 'asp-${namePrefix}'
  location: location
  sku: {
    name: 'P1v3' // swap to B1/B2 for demos
    capacity: 1
  }
  properties: {
    reserved: true // Linux
  }
}

resource sqlServer 'Microsoft.Sql/servers@2022-05-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administrators: null
  }
  resource firewall 'firewallRules@2022-05-01-preview' = {
    name: 'allow-azure-services'
    properties: {
      startIpAddress: '0.0.0.0'
      endIpAddress: '0.0.0.0'
    }
  }
  resource database 'databases@2022-05-01-preview' = {
    name: 'shopdb'
    location: location
    sku: {
      name: 'GP_S_Gen5'
      tier: 'GeneralPurpose'
      family: 'Gen5'
      capacity: 1
    }
    properties: {
      minCapacity: 0.5
      autoPauseDelay: 60 // serverless: pause when idle (demo-friendly)
    }
  }
}

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    sku: { name: 'standard', family: 'A' }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
  }
}

resource appServiceApi 'Microsoft.Web/sites@2023-01-01' = {
  name: appNameApi
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNET|8.0'
      healthCheckPath: '/health'
      appSettings: [
        { name: 'Database__Provider', value: 'SqlServer' }
        { name: 'ConnectionStrings__Shop', value: 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Database=shopdb;Authentication=Active Directory Default;' }
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: insights.properties.connectionString }
        { name: 'ShopApi__BaseUrl', value: 'https://${appNameApi}.azurewebsites.net/' }
        { name: 'ASPNETCORE_ENVIRONMENT', value: environment }
      ]
    }
  }
  resource stagingSlot 'slots@2023-01-01' = {
    name: 'staging'
    location: location
    properties: {
      serverFarmId: plan.id
    }
  }
}

resource appServiceWeb 'Microsoft.Web/sites@2023-01-01' = {
  name: appNameWeb
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNET|8.0'
      healthCheckPath: '/health'
      appSettings: [
        { name: 'Database__Provider', value: 'SqlServer' }
        { name: 'ConnectionStrings__Shop', value: 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Database=shopdb;Authentication=Active Directory Default;' }
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: insights.properties.connectionString }
        { name: 'ShopApi__BaseUrl', value: 'https://${appNameApi}.azurewebsites.net/' }
        { name: 'Admin__Passphrase', value: '@Microsoft.KeyVault(SecretUri=${keyVault.properties.vaultUri}secrets/admin-passphrase)' }
      ]
    }
  }
  resource stagingSlot 'slots@2023-01-01' = {
    name: 'staging'
    location: location
    properties: {
      serverFarmId: plan.id
    }
  }
}

output apiHostName string = '${appNameApi}.azurewebsites.net'
output webHostName string = '${appNameWeb}.azurewebsites.net'
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output keyVaultUri string = keyVault.properties.vaultUri
