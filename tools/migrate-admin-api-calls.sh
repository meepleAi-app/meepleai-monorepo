#!/bin/bash
# Script to migrate admin API calls from deprecated methods to adminClient
# Issue #1679 - API client deprecation cleanup

set -e

cd "$(dirname "$0")/.."

echo "🔄 Migrating admin API calls..."

# Define files to migrate
FILES=(
  "apps/web/src/app/admin/users/client.tsx"
  "apps/web/src/app/admin/prompts/[id]/audit/client.tsx"
  "apps/web/src/app/admin/prompts/[id]/client.tsx"
  "apps/web/src/app/admin/prompts/[id]/compare/client.tsx"
  "apps/web/src/app/admin/prompts/[id]/versions/[versionId]/client.tsx"
  "apps/web/src/app/admin/prompts/[id]/versions/new/client.tsx"
)

# Pattern 1: api.admin.getUsers()
for file in "${FILES[@]}"; do
  if [ -f "$file" ]; then
    echo "  Processing $file..."

    # Replace api.get for /api/v1/admin/users
    sed -i 's/api\.get<PagedResult<User>>(.*admin\/users.*)/api.admin.getUsers({ page, pageSize, search, role: roleFilter })/g' "$file"

    # Replace api.get for /api/v1/admin/prompts/:id
    sed -i 's/api\.get<PromptTemplate>(`\/api\/v1\/admin\/prompts\/\${id}`)/api.admin.getPromptById(id)/g' "$file"

    # Replace api.get for /api/v1/admin/prompts/:id/versions
    sed -i 's/api\.get<PromptVersion\[\]>(`\/api\/v1\/admin\/prompts\/\${id}\/versions`)/api.admin.getPromptVersions(id)/g' "$file"

    # Replace api.get for /api/v1/admin/prompts/:id/versions/:versionId
    sed -i 's/api\.get<PromptVersion>(`\/api\/v1\/admin\/prompts\/\${id}\/versions\/\${versionId}`)/api.admin.getPromptVersion(id, versionId)/g' "$file"

    # Replace api.get for /api/v1/admin/prompts/:id/audit
    sed -i 's/api\.get<{[^}]*}>(`\/api\/v1\/admin\/prompts\/\${id}\/audit.*`)/api.admin.getPromptAuditLogs(id, { page, pageSize })/g' "$file"

    # Replace api.post for /api/v1/admin/prompts/:id/versions
    sed -i 's/api\.post<{[^}]*}>(`\/api\/v1\/admin\/prompts\/\${id}\/versions`, payload)/api.admin.createPromptVersion(id, payload)/g' "$file"

    # Replace api.post for activate version
    sed -i 's/api\.post<{}>(`\/api\/v1\/admin\/prompts\/\${id}\/versions\/\${.*}\/activate`, {})/api.admin.activatePromptVersion(id, result.id)/g' "$file"
  fi
done

echo "✅ Migration complete! Run 'pnpm typecheck' to verify."
