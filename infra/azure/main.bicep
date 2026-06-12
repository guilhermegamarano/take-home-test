@description('Short environment name used in resource names.')
@minLength(2)
@maxLength(12)
param environmentName string = 'preview'

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Container image for the API, including tag.')
param apiImage string

@description('Container image for the frontend, including tag.')
param frontendImage string

@description('Container registry server, for example ghcr.io or myregistry.azurecr.io.')
param containerRegistryServer string = 'ghcr.io'

@description('Container registry username. Use a GitHub actor for GHCR or an ACR service principal when managed pulls are not configured.')
param containerRegistryUsername string

@secure()
@description('Container registry password or token.')
param containerRegistryPassword string

@description('SQL administrator login. Prefer a deployment-only identity and rotate it after provisioning.')
param sqlAdministratorLogin string

@secure()
@description('SQL administrator password.')
param sqlAdministratorPassword string

@secure()
@minLength(32)
@description('JWT signing key used by the assessment credential provider.')
param jwtSigningKey string

@description('Operator username with read/write permissions.')
param appUsername string

@secure()
@description('Operator password.')
param appPassword string

@description('Read-only username used by reviewers or auditors.')
param appReadOnlyUsername string = 'auditor'

@secure()
@description('Read-only password.')
param appReadOnlyPassword string

@description('Optional explicit CORS origin. Same-origin frontend traffic is proxied through Nginx, so this is mainly for direct API diagnostics.')
param corsAllowedOrigin string = ''

var resourceToken = toLower(uniqueString(resourceGroup().id, environmentName))
var namePrefix = 'fundo-${environmentName}-${resourceToken}'
var sqlServerName = take(replace('${namePrefix}-sql', '-', ''), 63)
var databaseName = 'LoanManagement'
var apiAppName = '${namePrefix}-api'
var frontendAppName = '${namePrefix}-web'

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${namePrefix}-log'
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
    }
  }
}

resource apiIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${namePrefix}-api-mi'
  location: location
}

resource frontendIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${namePrefix}-web-mi'
  location: location
}

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: take('${namePrefix}-kv', 24)
  location: location
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: false
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
    enablePurgeProtection: true
    publicNetworkAccess: 'Enabled'
    accessPolicies: [
      {
        tenantId: subscription().tenantId
        objectId: apiIdentity.properties.principalId
        permissions: {
          secrets: [
            'get'
            'list'
          ]
        }
      }
    ]
  }
}

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: sqlAdministratorLogin
    administratorLoginPassword: sqlAdministratorPassword
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

resource database 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: databaseName
  location: location
  sku: {
    name: 'Basic'
    tier: 'Basic'
    capacity: 5
  }
  properties: {
    zoneRedundant: false
    readScale: 'Disabled'
  }
}

resource allowAzureServices 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource sqlConnectionSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'loans-database-connection'
  properties: {
    value: 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Initial Catalog=${database.name};Persist Security Info=False;User ID=${sqlAdministratorLogin};Password=${sqlAdministratorPassword};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
  }
}

resource jwtSigningKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'jwt-signing-key'
  properties: {
    value: jwtSigningKey
  }
}

resource appPasswordSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'app-password'
  properties: {
    value: appPassword
  }
}

resource appReadOnlyPasswordSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'app-readonly-password'
  properties: {
    value: appReadOnlyPassword
  }
}

resource containerEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: '${namePrefix}-cae'
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
  }
}

resource apiApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: apiAppName
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${apiIdentity.id}': {}
    }
  }
  properties: {
    managedEnvironmentId: containerEnvironment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: false
        targetPort: 8080
        transport: 'auto'
      }
      registries: [
        {
          server: containerRegistryServer
          username: containerRegistryUsername
          passwordSecretRef: 'registry-password'
        }
      ]
      secrets: [
        {
          name: 'registry-password'
          value: containerRegistryPassword
        }
        {
          name: 'loans-database-connection'
          keyVaultUrl: sqlConnectionSecret.properties.secretUri
          identity: apiIdentity.id
        }
        {
          name: 'jwt-signing-key'
          keyVaultUrl: jwtSigningKeySecret.properties.secretUri
          identity: apiIdentity.id
        }
        {
          name: 'app-password'
          keyVaultUrl: appPasswordSecret.properties.secretUri
          identity: apiIdentity.id
        }
        {
          name: 'app-readonly-password'
          keyVaultUrl: appReadOnlyPasswordSecret.properties.secretUri
          identity: apiIdentity.id
        }
      ]
    }
    template: {
      scale: {
        minReplicas: 1
        maxReplicas: 3
        rules: [
          {
            name: 'http-concurrency'
            http: {
              metadata: {
                concurrentRequests: '50'
              }
            }
          }
        ]
      }
      containers: [
        {
          name: 'api'
          image: apiImage
          env: [
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Production'
            }
            {
              name: 'ASPNETCORE_HTTP_PORTS'
              value: '8080'
            }
            {
              name: 'ConnectionStrings__LoansDatabase'
              secretRef: 'loans-database-connection'
            }
            {
              name: 'Database__ApplyMigrations'
              value: 'false'
            }
            {
              name: 'Authentication__Issuer'
              value: 'Fundo.Loans.Api'
            }
            {
              name: 'Authentication__Audience'
              value: 'Fundo.Loans.Client'
            }
            {
              name: 'Authentication__SigningKey'
              secretRef: 'jwt-signing-key'
            }
            {
              name: 'Authentication__Username'
              value: appUsername
            }
            {
              name: 'Authentication__Password'
              secretRef: 'app-password'
            }
            {
              name: 'Authentication__ReadOnlyUsername'
              value: appReadOnlyUsername
            }
            {
              name: 'Authentication__ReadOnlyPassword'
              secretRef: 'app-readonly-password'
            }
            {
              name: 'Authentication__TokenLifetimeMinutes'
              value: '30'
            }
            {
              name: 'Cors__AllowedOrigins__0'
              value: corsAllowedOrigin
            }
          ]
          probes: [
            {
              type: 'Liveness'
              httpGet: {
                path: '/health/live'
                port: 8080
              }
              initialDelaySeconds: 10
              periodSeconds: 30
            }
            {
              type: 'Readiness'
              httpGet: {
                path: '/health/ready'
                port: 8080
              }
              initialDelaySeconds: 15
              periodSeconds: 30
            }
          ]
          resources: {
            cpu: json('0.5')
            memory: '1.0Gi'
          }
        }
      ]
    }
  }
}

resource frontendApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: frontendAppName
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${frontendIdentity.id}': {}
    }
  }
  properties: {
    managedEnvironmentId: containerEnvironment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
      }
      registries: [
        {
          server: containerRegistryServer
          username: containerRegistryUsername
          passwordSecretRef: 'registry-password'
        }
      ]
      secrets: [
        {
          name: 'registry-password'
          value: containerRegistryPassword
        }
      ]
    }
    template: {
      scale: {
        minReplicas: 1
        maxReplicas: 3
        rules: [
          {
            name: 'http-concurrency'
            http: {
              metadata: {
                concurrentRequests: '80'
              }
            }
          }
        ]
      }
      containers: [
        {
          name: 'frontend'
          image: frontendImage
          env: [
            {
              name: 'API_UPSTREAM'
              value: 'https://${apiApp.properties.configuration.ingress.fqdn}'
            }
          ]
          probes: [
            {
              type: 'Liveness'
              httpGet: {
                path: '/health'
                port: 8080
              }
              initialDelaySeconds: 5
              periodSeconds: 30
            }
            {
              type: 'Readiness'
              httpGet: {
                path: '/health'
                port: 8080
              }
              initialDelaySeconds: 5
              periodSeconds: 30
            }
          ]
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
        }
      ]
    }
  }
}

output frontendUrl string = 'https://${frontendApp.properties.configuration.ingress.fqdn}'
output apiInternalUrl string = 'https://${apiApp.properties.configuration.ingress.fqdn}'
output keyVaultName string = keyVault.name
output sqlServerFullyQualifiedDomainName string = sqlServer.properties.fullyQualifiedDomainName
