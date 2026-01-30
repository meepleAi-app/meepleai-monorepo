#!/bin/bash
# investigate-anonymous-volume.sh
# Investigates anonymous Docker volume to identify creator

set -e

VOLUME_HASH="26cb8e29619dcb476170ddc27a5ca7ddba922d77cb5e98bef083e1fd45f7bf8f"

echo "🔍 Investigating Anonymous Volume"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "Volume: $VOLUME_HASH"
echo ""

# 1. Check if volume exists
echo "1️⃣ Checking volume existence..."
if ! docker volume inspect "$VOLUME_HASH" >/dev/null 2>&1; then
  echo "❌ Volume not found! May have been deleted."
  exit 1
fi
echo "✅ Volume exists"
echo ""

# 2. Inspect volume metadata
echo "2️⃣ Volume Metadata:"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
docker volume inspect "$VOLUME_HASH" | jq '.[0] | {
  Name: .Name,
  Driver: .Driver,
  Mountpoint: .Mountpoint,
  CreatedAt: .CreatedAt,
  Labels: .Labels,
  Scope: .Scope
}'
echo ""

# 3. Check compose project
echo "3️⃣ Docker Compose Association:"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
PROJECT=$(docker volume inspect "$VOLUME_HASH" | jq -r '.[0].Labels["com.docker.compose.project"] // "none"')
SERVICE=$(docker volume inspect "$VOLUME_HASH" | jq -r '.[0].Labels["com.docker.compose.volume"] // "none"')
echo "Project: $PROJECT"
echo "Service: $SERVICE"
echo ""

# 4. Find containers using this volume
echo "4️⃣ Containers Using This Volume:"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
FOUND_CONTAINER=false
for container in $(docker ps -aq); do
  CONTAINER_NAME=$(docker inspect --format='{{.Name}}' "$container" | sed 's/\///')
  CONTAINER_IMAGE=$(docker inspect --format='{{.Config.Image}}' "$container")

  if docker inspect "$container" | grep -q "$VOLUME_HASH"; then
    echo "✅ FOUND: $CONTAINER_NAME ($CONTAINER_IMAGE)"
    echo "   Status: $(docker inspect --format='{{.State.Status}}' "$container")"
    echo "   Mount Point:"
    docker inspect "$container" | jq -r '.[] | .Mounts[] | select(.Name == "'$VOLUME_HASH'") | "   Source: " + .Source + "\n   Destination: " + .Destination + "\n   RW: " + (.RW|tostring)'
    FOUND_CONTAINER=true
  fi
done

if [ "$FOUND_CONTAINER" = false ]; then
  echo "⚠️  No containers currently using this volume (dangling)"
fi
echo ""

# 5. Check volume contents
echo "5️⃣ Volume Contents (first 20 items):"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
docker run --rm -v "$VOLUME_HASH":/data alpine ls -lah /data | head -20
echo ""

# 6. Identify file types
echo "6️⃣ File Type Analysis:"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "Total files: $(docker run --rm -v "$VOLUME_HASH":/data alpine find /data -type f | wc -l)"
echo "Total size: $(docker run --rm -v "$VOLUME_HASH":/data alpine du -sh /data | cut -f1)"
echo ""

# Check for specific file patterns
echo "File patterns detected:"
docker run --rm -v "$VOLUME_HASH":/data alpine find /data -type f -name "*.db" -o -name "PG_VERSION" -o -name "*.pdf" -o -name "*.json" -o -name "*.log" | head -10
echo ""

# 7. Search in docker-compose files
echo "7️⃣ Searching docker-compose.yml for potential source:"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
cd "$(dirname "$0")/../infra"

# Check for services without named volumes
echo "Services without explicit volume declarations:"
docker compose config 2>/dev/null | grep -A 10 "services:" | grep -E "^\s{2}[a-z]" | while read service; do
  SERVICE_NAME=$(echo "$service" | xargs | sed 's/://')
  VOLUMES=$(docker compose config 2>/dev/null | grep -A 50 "^  $SERVICE_NAME:" | grep -A 20 "volumes:" | grep -E "^\s{6}-" | head -5)
  if [ -n "$VOLUMES" ]; then
    echo "  $SERVICE_NAME:"
    echo "$VOLUMES"
  fi
done
echo ""

# 8. Check Dockerfiles for VOLUME directives
echo "8️⃣ Checking Dockerfiles for VOLUME directives:"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
find ../apps -name "Dockerfile" -exec echo "File: {}" \; -exec grep -H "^VOLUME" {} \; 2>/dev/null || echo "No VOLUME directives found in custom Dockerfiles"
echo ""

# 9. Summary and Recommendations
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "📋 SUMMARY & RECOMMENDATIONS"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

# Check if dangling
if docker volume ls --filter "dangling=true" | grep -q "$VOLUME_HASH"; then
  echo "Status: ⚠️  DANGLING (not used by any container)"
  echo ""
  echo "Safe to remove:"
  echo "  docker volume rm $VOLUME_HASH"
  echo ""
  echo "Or backup first (recommended):"
  echo "  docker run --rm -v $VOLUME_HASH:/src:ro -v /backups:/dst alpine \\"
  echo "    tar czf /dst/anonymous-volume-$(date +%Y%m%d).tar.gz -C /src ."
  echo "  docker volume rm $VOLUME_HASH"
else
  echo "Status: ⚠️  IN USE by a container"
  echo ""
  echo "Actions:"
  echo "  1. Identify the container (see section 4 above)"
  echo "  2. Check if data is important"
  echo "  3. Migrate to named volume (see migration guide)"
  echo "  4. Update docker-compose.yml to prevent recreation"
fi

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "Investigation complete!"
