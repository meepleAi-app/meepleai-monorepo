# SSH tunnel for integration environment
# Forwards Postgres, Redis, Qdrant from staging server to local ports

param(
    [ValidateSet("start", "stop", "status")]
    [string]$Action = "start",
    [string]$Server = "deploy@204.168.135.69",
    [string]$KeyPath = "$HOME/.ssh/meepleai-staging",
    [int]$PostgresLocal = 15432,
    [int]$RedisLocal = 16379,
    [int]$QdrantLocal = 16333
)

$controlSocket = "$HOME/.ssh/meepleai-tunnel.sock"

switch ($Action) {
    "start" {
        # Check if tunnel already exists
        $check = ssh -O check -S $controlSocket $Server 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Tunnel already active." -ForegroundColor Yellow
            return
        }

        Write-Host "Opening SSH tunnels to $Server..." -ForegroundColor Cyan
        ssh -fN `
            -o ControlMaster=auto `
            -o ControlPath=$controlSocket `
            -o ControlPersist=yes `
            -o ExitOnForwardFailure=yes `
            -o ServerAliveInterval=30 `
            -o ServerAliveCountMax=3 `
            -L "${PostgresLocal}:localhost:5432" `
            -L "${RedisLocal}:localhost:6379" `
            -L "${QdrantLocal}:localhost:6333" `
            -i $KeyPath $Server

        if ($LASTEXITCODE -eq 0) {
            Write-Host "Tunnels open:" -ForegroundColor Green
            Write-Host "  PostgreSQL: localhost:$PostgresLocal -> server:5432"
            Write-Host "  Redis:      localhost:$RedisLocal -> server:6379"
            Write-Host "  Qdrant:     localhost:$QdrantLocal -> server:6333"
        } else {
            Write-Host "Failed to open tunnels. Check SSH key and server." -ForegroundColor Red
        }
    }
    "stop" {
        Write-Host "Closing SSH tunnels..." -ForegroundColor Cyan
        ssh -O exit -S $controlSocket $Server 2>$null
        if (Test-Path $controlSocket) { Remove-Item $controlSocket -Force }
        Write-Host "Tunnels closed." -ForegroundColor Green
    }
    "status" {
        $check = ssh -O check -S $controlSocket $Server 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Tunnels ACTIVE" -ForegroundColor Green
            Write-Host "  PostgreSQL: localhost:$PostgresLocal"
            Write-Host "  Redis:      localhost:$RedisLocal"
            Write-Host "  Qdrant:     localhost:$QdrantLocal"
        } else {
            Write-Host "No active tunnels." -ForegroundColor Yellow
        }
    }
}
