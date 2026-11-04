param namePrefix string
param location string
param profile string
param apiImage string
param webImage string
param logAnalyticsWorkspaceName string
param appInsightsConnectionString string
param storageAccountBlobEndpoint string
param serviceBusNamespace string
param keyVaultUri string
param appConfigEndpoint string

resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' existing = {
  name: logAnalyticsWorkspaceName
}

resource cae 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: '${namePrefix}-cae'
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: workspace.properties.customerId
        sharedKey: workspace.listKeys().primarySharedKey
      }
    }
  }
}

resource api 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${namePrefix}-api'
  location: location
  identity: { type: 'SystemAssigned' }
  properties: {
    managedEnvironmentId: cae.id
    configuration: {
      ingress: { external: true, targetPort: 8080, transport: 'http' }
    }
    template: {
      containers: [
        {
          name: 'api'
          image: apiImage
          resources: { cpu: json('0.5'), memory: '1Gi' }
          env: [
            { name: 'MEEPLEAI_PROFILE', value: profile }
            { name: 'STORAGE_PROVIDER', value: 'azureblob' }
            { name: 'AZURE_STORAGE_ACCOUNT_URL', value: storageAccountBlobEndpoint }
            { name: 'AZURE_STORAGE_CONTAINER', value: 'meepleai-uploads' }
            { name: 'AZURE_SERVICEBUS_QUEUE', value: 'pdf-uploaded' }
            { name: 'AZURE_SERVICEBUS_NAMESPACE', value: '${serviceBusNamespace}.servicebus.windows.net' }
            { name: 'AZURE_KEYVAULT_URI', value: keyVaultUri }
            { name: 'AZURE_APPCONFIG_ENDPOINT', value: appConfigEndpoint }
            { name: 'Azure__Sdk__AllowedHostSuffixes__0', value: '.blob.${environment().suffixes.storage}' }
            { name: 'Azure__Sdk__AllowedHostSuffixes__1', value: '.${environment().suffixes.keyvaultDns}' }
            { name: 'Azure__Sdk__AllowedHostSuffixes__2', value: '.azconfig.io' }
            { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsightsConnectionString }
          ]
        }
      ]
      scale: { minReplicas: 0, maxReplicas: 2 }
    }
  }
}

resource web 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${namePrefix}-web'
  location: location
  properties: {
    managedEnvironmentId: cae.id
    configuration: { ingress: { external: true, targetPort: 3000, transport: 'http' } }
    template: {
      containers: [
        {
          name: 'web'
          image: webImage
          resources: { cpu: json('0.25'), memory: '0.5Gi' }
          env: [ { name: 'API_BASE_URL', value: 'https://${api.properties.configuration.ingress.fqdn}' } ]
        }
      ]
      scale: { minReplicas: 0, maxReplicas: 1 }
    }
  }
}

output apiUrl string = 'https://${api.properties.configuration.ingress.fqdn}'
output webUrl string = 'https://${web.properties.configuration.ingress.fqdn}'
output apiPrincipalId string = api.identity.principalId
