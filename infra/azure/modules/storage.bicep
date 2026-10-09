@minLength(3)
@maxLength(8)
param namePrefix string
@minLength(1)
@maxLength(10)
param suffix string
param location string

resource account 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: toLower('${namePrefix}st${suffix}')
  location: location
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
  properties: { allowBlobPublicAccess: false, minimumTlsVersion: 'TLS1_2', supportsHttpsTrafficOnly: true }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: account
  name: 'default'
}

resource uploads 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: 'meepleai-uploads'
  properties: { publicAccess: 'None' }
}

output accountName string = account.name
output blobEndpoint string = account.properties.primaryEndpoints.blob
