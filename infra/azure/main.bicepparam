using './main.bicep'

param namePrefix = 'meepleai'
param profile = 'azure-exam'
param apiImage = 'ghcr.io/meepleai-app/meepleai-monorepo/api:latest'
param webImage = 'ghcr.io/meepleai-app/meepleai-monorepo/web:latest'
param postgresAdminPassword = readEnvironmentVariable('POSTGRES_ADMIN_PASSWORD', 'replace-me-at-provision-time')
