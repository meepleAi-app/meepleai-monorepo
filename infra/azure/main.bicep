targetScope = 'resourceGroup'

@description('Prefisso dei nomi (minuscole).')
@minLength(3)
@maxLength(8)
param namePrefix string = 'meepleai'

@description('Regione.')
param location string = resourceGroup().location

@description('Profilo applicativo iniettato nell API (MEEPLEAI_PROFILE).')
param profile string = 'azure-exam'

@secure()
@description('Password dell amministratore Postgres.')
param postgresAdminPassword string

@description('Immagine dell API (registry/repo:tag).')
param apiImage string

@description('Immagine del web (registry/repo:tag).')
param webImage string

var suffix = take(uniqueString(resourceGroup().id), 10)

module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring'
  params: { namePrefix: namePrefix, location: location }
}

module storage 'modules/storage.bicep' = {
  name: 'storage'
  params: { namePrefix: namePrefix, suffix: suffix, location: location }
}

module servicebus 'modules/servicebus.bicep' = {
  name: 'servicebus'
  params: { namePrefix: namePrefix, suffix: suffix, location: location }
}

module keyvault 'modules/keyvault.bicep' = {
  name: 'keyvault'
  params: { namePrefix: namePrefix, suffix: suffix, location: location }
}

module appconfig 'modules/appconfig.bicep' = {
  name: 'appconfig'
  params: { namePrefix: namePrefix, suffix: suffix, location: location }
}

module postgres 'modules/postgres.bicep' = {
  name: 'postgres'
  params: { namePrefix: namePrefix, suffix: suffix, location: location, adminPassword: postgresAdminPassword }
}

module redis 'modules/redis.bicep' = {
  name: 'redis'
  params: { namePrefix: namePrefix, suffix: suffix, location: location }
}

module containerapps 'modules/containerapps.bicep' = {
  name: 'containerapps'
  params: {
    namePrefix: namePrefix
    location: location
    profile: profile
    apiImage: apiImage
    webImage: webImage
    logAnalyticsWorkspaceName: monitoring.outputs.workspaceName
    appInsightsConnectionString: monitoring.outputs.appInsightsConnectionString
    storageAccountBlobEndpoint: storage.outputs.blobEndpoint
    serviceBusNamespace: servicebus.outputs.namespaceName
    keyVaultUri: keyvault.outputs.vaultUri
    appConfigEndpoint: appconfig.outputs.endpoint
  }
}

module functions 'modules/functions.bicep' = {
  name: 'functions'
  params: {
    namePrefix: namePrefix
    suffix: suffix
    location: location
    storageAccountName: storage.outputs.accountName
    serviceBusNamespace: servicebus.outputs.namespaceName
    appInsightsConnectionString: monitoring.outputs.appInsightsConnectionString
    apiBaseUrl: containerapps.outputs.apiUrl
  }
}

output apiUrl string = containerapps.outputs.apiUrl
output webUrl string = containerapps.outputs.webUrl
output keyVaultUri string = keyvault.outputs.vaultUri
output appConfigEndpoint string = appconfig.outputs.endpoint
