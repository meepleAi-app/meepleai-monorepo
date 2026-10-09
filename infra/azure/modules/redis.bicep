param namePrefix string
param suffix string
param location string

resource redis 'Microsoft.Cache/redisEnterprise@2024-10-01' = {
  name: '${namePrefix}-redis-${suffix}'
  location: location
  sku: { name: 'Balanced_B0' }
  properties: { minimumTlsVersion: '1.2' }
}

resource db 'Microsoft.Cache/redisEnterprise/databases@2024-10-01' = {
  parent: redis
  name: 'default'
  properties: { clientProtocol: 'Encrypted', port: 10000, evictionPolicy: 'VolatileLRU', clusteringPolicy: 'EnterpriseCluster' }
}

output hostName string = redis.properties.hostName
