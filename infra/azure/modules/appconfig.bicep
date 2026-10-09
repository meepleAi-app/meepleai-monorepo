param namePrefix string
param suffix string
param location string

resource store 'Microsoft.AppConfiguration/configurationStores@2024-05-01' = {
  name: '${namePrefix}-appconfig-${suffix}'
  location: location
  sku: { name: 'free' }
  properties: { disableLocalAuth: false }
}

output endpoint string = store.properties.endpoint
