param namePrefix string
param suffix string
param location string

resource ns 'Microsoft.ServiceBus/namespaces@2024-01-01' = {
  name: '${namePrefix}-sb-${suffix}'
  location: location
  sku: { name: 'Basic', tier: 'Basic' }
}

resource pdfUploaded 'Microsoft.ServiceBus/namespaces/queues@2024-01-01' = {
  parent: ns
  name: 'pdf-uploaded'
  properties: {
    lockDuration: 'PT1M'
    maxDeliveryCount: 3
    deadLetteringOnMessageExpiration: true
    defaultMessageTimeToLive: 'P1D'
  }
}

output namespaceName string = ns.name
output queueName string = pdfUploaded.name
