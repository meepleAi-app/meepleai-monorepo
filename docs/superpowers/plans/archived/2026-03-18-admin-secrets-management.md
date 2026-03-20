# Admin Secrets Management Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an admin panel to view, edit, and save `.secret` files on the server, plus a restart button to apply changes.

**Architecture:** Backend reads/writes `.secret` files via `SECRETS_DIRECTORY` env var. Frontend adds a SecretsPanel component to the existing `/admin/monitor/services` page. Restart uses `IHostApplicationLifetime.StopApplication()` with Docker `restart: always`.

**Tech Stack:** .NET 9 Minimal API, Next.js 16, React 19, Tailwind 4, React Query

**Spec:** `docs/superpowers/specs/2026-03-18-admin-secrets-management-design.md`

---

## File Structure

### New Files (Backend)
| File | Responsibility |
|------|---------------|
| `apps/api/src/Api/Routing/AdminSecretsEndpoints.cs` | GET/PUT/POST endpoints for secrets management |

### New Files (Frontend)
| File | Responsibility |
|------|---------------|
| `apps/web/src/components/admin/secrets/SecretsPanel.tsx` | Main panel with file cards, save button, restart banner |
| `apps/web/src/components/admin/secrets/SecretEntryInput.tsx` | Masked input with reveal toggle |
| `apps/web/src/components/admin/secrets/__tests__/SecretsPanel.test.tsx` | Unit tests |
| `apps/web/src/lib/api/clients/adminSecretsClient.ts` | API client functions |

### Modified Files
| File | Change |
|------|--------|
| `apps/api/src/Api/Program.cs` | Register `MapAdminSecretsEndpoints()` |
| `apps/web/src/app/admin/(dashboard)/monitor/services/page.tsx` | Add `<SecretsPanel />` |

---

## Chunk 1: Backend Endpoints

### Task 1: AdminSecretsEndpoints — GET, PUT, POST restart

**Files:**
- Create: `apps/api/src/Api/Routing/AdminSecretsEndpoints.cs`
- Modify: `apps/api/src/Api/Program.cs`

- [ ] **Step 1: Create the endpoint file with GET**

```csharp
// apps/api/src/Api/Routing/AdminSecretsEndpoints.cs
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.Extensions;

namespace Api.Routing;

internal static class AdminSecretsEndpoints
{
    private static readonly HashSet<string> InfraFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "database.secret", "redis.secret", "qdrant.secret", "jwt.secret", "admin.secret"
    };

    private static readonly Dictionary<string, string> CategoryMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["openrouter.secret"] = "OpenRouter",
        ["database.secret"] = "Database",
        ["redis.secret"] = "Redis",
        ["qdrant.secret"] = "Qdrant",
        ["jwt.secret"] = "JWT",
        ["admin.secret"] = "Admin",
        ["oauth.secret"] = "OAuth",
        ["bgg.secret"] = "BGG",
        ["email.secret"] = "Email",
        ["embedding-service.secret"] = "Embedding Service",
        ["reranker-service.secret"] = "Reranker Service",
        ["smoldocling-service.secret"] = "SmolDocling",
        ["unstructured-service.secret"] = "Unstructured",
        ["monitoring.secret"] = "Monitoring",
        ["storage.secret"] = "Storage",
        ["slack.secret"] = "Slack",
        ["traefik.secret"] = "Traefik",
        ["n8n.secret"] = "n8n",
    };

    public static IEndpointRouteBuilder MapAdminSecretsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/secrets")
            .WithTags("Admin", "Secrets")
            .RequireAuthorization();

        group.MapGet("/", HandleGetSecrets).WithName("GetSecrets");
        group.MapPut("/", HandleUpdateSecrets).WithName("UpdateSecrets");
        group.MapPost("/restart", HandleRestart).WithName("RestartApi");

        return app;
    }

    private static IResult HandleGetSecrets(HttpContext context, IConfiguration config, ILogger<Program> logger)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var reveal = context.Request.Query.ContainsKey("reveal");
        var secretsDir = GetSecretsDirectory(config);
        if (secretsDir == null)
            return Results.Problem("SECRETS_DIRECTORY not configured", statusCode: 500);

        var files = Directory.GetFiles(secretsDir, "*.secret")
            .OrderBy(f => InfraFiles.Contains(Path.GetFileName(f)) ? 1 : 0)
            .ThenBy(f => Path.GetFileName(f))
            .Select(filePath =>
            {
                var fileName = Path.GetFileName(filePath);
                var lines = File.ReadAllLines(filePath);
                var entries = lines
                    .Where(l => !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith('#') && l.Contains('='))
                    .Select(l =>
                    {
                        var eqIdx = l.IndexOf('=');
                        var key = l[..eqIdx].Trim();
                        var value = l[(eqIdx + 1)..].Trim();
                        return new
                        {
                            key,
                            maskedValue = reveal ? value : MaskValue(value),
                            hasValue = !string.IsNullOrEmpty(value),
                            isPlaceholder = IsPlaceholder(value),
                        };
                    })
                    .ToList();

                return new
                {
                    fileName,
                    category = CategoryMap.GetValueOrDefault(fileName, TitleCase(fileName.Replace(".secret", ""))),
                    isInfra = InfraFiles.Contains(fileName),
                    entries,
                };
            })
            .ToList();

        logger.LogInformation("Admin {UserId} viewed secrets ({FileCount} files)", session!.User!.Id, files.Count);
        return Results.Ok(new { secretsDirectory = secretsDir, files });
    }

    private static async Task<IResult> HandleUpdateSecrets(
        HttpContext context, IConfiguration config, ILogger<Program> logger, CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var secretsDir = GetSecretsDirectory(config);
        if (secretsDir == null)
            return Results.Problem("SECRETS_DIRECTORY not configured", statusCode: 500);

        var body = await context.Request.ReadFromJsonAsync<UpdateSecretsRequest>(ct).ConfigureAwait(false);
        if (body?.Updates == null || body.Updates.Count == 0)
            return Results.BadRequest(new { error = "No updates provided" });

        var updatedFiles = new HashSet<string>();
        foreach (var update in body.Updates)
        {
            // Validate fileName — no path traversal
            if (update.FileName.Contains("..") || update.FileName.Contains('/') || update.FileName.Contains('\\'))
                return Results.BadRequest(new { error = $"Invalid fileName: {update.FileName}" });

            // Validate key format
            if (!System.Text.RegularExpressions.Regex.IsMatch(update.Key, @"^[A-Z][A-Z0-9_]*$"))
                return Results.BadRequest(new { error = $"Invalid key format: {update.Key}" });

            var filePath = Path.Combine(secretsDir, update.FileName);
            if (!File.Exists(filePath))
                return Results.BadRequest(new { error = $"File not found: {update.FileName}" });

            var lines = await File.ReadAllLinesAsync(filePath, ct).ConfigureAwait(false);
            var found = false;
            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].TrimStart().StartsWith('#') || !lines[i].Contains('=')) continue;
                var eqIdx = lines[i].IndexOf('=');
                var lineKey = lines[i][..eqIdx].Trim();
                if (string.Equals(lineKey, update.Key, StringComparison.Ordinal))
                {
                    lines[i] = $"{update.Key}={update.Value}";
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                // Append new key
                var newLines = lines.ToList();
                newLines.Add($"{update.Key}={update.Value}");
                lines = newLines.ToArray();
            }

            await File.WriteAllLinesAsync(filePath, lines, ct).ConfigureAwait(false);
            updatedFiles.Add(update.FileName);

            // Audit log: key name only, never value
            logger.LogInformation("Admin {UserId} updated secret {Key} in {File}",
                session!.User!.Id, update.Key, update.FileName);
        }

        return Results.Ok(new { updatedFiles = updatedFiles.ToList(), updatedKeys = body.Updates.Count });
    }

    private static IResult HandleRestart(
        HttpContext context, IHostApplicationLifetime lifetime, ILogger<Program> logger)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        logger.LogWarning("Admin {UserId} initiated API restart via secrets management", session!.User!.Id);

        // Delay to allow response to be sent
        _ = Task.Run(async () =>
        {
            await Task.Delay(2000).ConfigureAwait(false);
            lifetime.StopApplication();
        });

        return Results.Accepted(value: new
        {
            message = "API restart initiated. Service will be back in ~10 seconds.",
            restartedAt = DateTime.UtcNow,
        });
    }

    private static string? GetSecretsDirectory(IConfiguration config)
    {
        var dir = config["SECRETS_DIRECTORY"]
            ?? Environment.GetEnvironmentVariable("SECRETS_DIRECTORY");
        if (string.IsNullOrEmpty(dir)) return null;

        // Resolve relative paths
        if (!Path.IsPathRooted(dir))
            dir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, dir));

        return Directory.Exists(dir) ? dir : null;
    }

    private static string MaskValue(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Length <= 4) return "****";
        if (value.Length <= 10) return $"{value[..2]}****{value[^2..]}";
        return $"{value[..6]}****{value[^4..]}";
    }

    private static bool IsPlaceholder(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        var lower = value.ToLowerInvariant();
        return lower.Contains("placeholder") || lower.Contains("change_me")
            || lower.StartsWith("your_") || lower == "changeme";
    }

    private static string TitleCase(string s) =>
        string.IsNullOrEmpty(s) ? s :
        string.Join(' ', s.Split('-', '_').Select(w =>
            w.Length > 0 ? char.ToUpperInvariant(w[0]) + w[1..] : w));

    private sealed record UpdateSecretsRequest(List<SecretUpdate> Updates);
    private sealed record SecretUpdate(string FileName, string Key, string Value);
}
```

- [ ] **Step 2: Register in Program.cs**

In `apps/api/src/Api/Program.cs`, find the admin endpoint registrations (around line 590) and add:

```csharp
v1Api.MapAdminSecretsEndpoints();        // Admin secrets management
```

Actually, the endpoint self-registers its group, so add after line ~591:

```csharp
app.MapAdminSecretsEndpoints();          // Admin secrets management
```

- [ ] **Step 3: Verify build**

Run: `cd apps/api/src/Api && dotnet build --no-restore 2>&1 | tail -5`
Expected: 0 errors

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/Routing/AdminSecretsEndpoints.cs apps/api/src/Api/Program.cs
git commit -m "feat(admin): add secrets management API endpoints (GET/PUT/restart)"
```

---

## Chunk 2: Frontend Components

### Task 2: API client + SecretEntryInput

**Files:**
- Create: `apps/web/src/lib/api/clients/adminSecretsClient.ts`
- Create: `apps/web/src/components/admin/secrets/SecretEntryInput.tsx`

- [ ] **Step 1: Create API client**

```typescript
// apps/web/src/lib/api/clients/adminSecretsClient.ts
import { apiClient } from '../apiClient';

export interface SecretEntry {
  key: string;
  maskedValue: string;
  hasValue: boolean;
  isPlaceholder: boolean;
}

export interface SecretFile {
  fileName: string;
  category: string;
  isInfra: boolean;
  entries: SecretEntry[];
}

export interface SecretsResponse {
  secretsDirectory: string;
  files: SecretFile[];
}

export interface SecretUpdate {
  fileName: string;
  key: string;
  value: string;
}

export const adminSecretsClient = {
  getSecrets: (reveal = false) =>
    apiClient.get<SecretsResponse>(`/admin/secrets${reveal ? '?reveal=true' : ''}`),

  updateSecrets: (updates: SecretUpdate[]) =>
    apiClient.put<{ updatedFiles: string[]; updatedKeys: number }>('/admin/secrets', { updates }),

  restartApi: () =>
    apiClient.post<{ message: string; restartedAt: string }>('/admin/secrets/restart'),
};
```

- [ ] **Step 2: Create SecretEntryInput component**

```tsx
// apps/web/src/components/admin/secrets/SecretEntryInput.tsx
'use client';

import { useState } from 'react';

import { AlertTriangle, Eye, EyeOff } from 'lucide-react';

import { Input } from '@/components/ui/input';
import { cn } from '@/lib/utils';

interface SecretEntryInputProps {
  entryKey: string;
  maskedValue: string;
  hasValue: boolean;
  isPlaceholder: boolean;
  onChange: (key: string, value: string) => void;
  isDirty: boolean;
}

export function SecretEntryInput({
  entryKey,
  maskedValue,
  hasValue,
  isPlaceholder,
  onChange,
  isDirty,
}: SecretEntryInputProps) {
  const [revealed, setRevealed] = useState(false);
  const [editValue, setEditValue] = useState('');
  const [isEditing, setIsEditing] = useState(false);

  const displayValue = isEditing ? editValue : maskedValue;

  const handleFocus = () => {
    if (!isEditing) {
      setEditValue('');
      setIsEditing(true);
    }
  };

  const handleChange = (val: string) => {
    setEditValue(val);
    onChange(entryKey, val);
  };

  return (
    <div className="flex items-center gap-2">
      <label className="min-w-[200px] shrink-0 text-xs font-mono text-muted-foreground truncate" title={entryKey}>
        {entryKey}
      </label>
      <div className="relative flex-1">
        <Input
          type={revealed || isEditing ? 'text' : 'password'}
          value={displayValue}
          placeholder={hasValue ? '(set)' : '(empty)'}
          onFocus={handleFocus}
          onChange={e => handleChange(e.target.value)}
          className={cn(
            'font-mono text-xs h-8 pr-8',
            isDirty && 'border-amber-400 bg-amber-50/50',
            isPlaceholder && !isDirty && 'border-red-300 bg-red-50/30'
          )}
        />
        <button
          type="button"
          onClick={() => setRevealed(!revealed)}
          className="absolute right-2 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground"
          tabIndex={-1}
        >
          {revealed ? <EyeOff className="h-3.5 w-3.5" /> : <Eye className="h-3.5 w-3.5" />}
        </button>
      </div>
      {isPlaceholder && !isDirty && (
        <AlertTriangle className="h-4 w-4 shrink-0 text-amber-500" title="Placeholder value" />
      )}
    </div>
  );
}
```

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/lib/api/clients/adminSecretsClient.ts apps/web/src/components/admin/secrets/SecretEntryInput.tsx
git commit -m "feat(admin): add secrets API client and SecretEntryInput component"
```

---

### Task 3: SecretsPanel + wire into page

**Files:**
- Create: `apps/web/src/components/admin/secrets/SecretsPanel.tsx`
- Modify: `apps/web/src/app/admin/(dashboard)/monitor/services/page.tsx`

- [ ] **Step 1: Create SecretsPanel**

```tsx
// apps/web/src/components/admin/secrets/SecretsPanel.tsx
'use client';

import { useCallback, useState } from 'react';

import { useQuery } from '@tanstack/react-query';
import { AlertTriangle, Key, Loader2, RefreshCw, Save, Shield } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { toast } from '@/components/layout/Toast';
import { cn } from '@/lib/utils';
import { adminSecretsClient, type SecretUpdate } from '@/lib/api/clients/adminSecretsClient';

import { SecretEntryInput } from './SecretEntryInput';

export function SecretsPanel() {
  const [dirtyValues, setDirtyValues] = useState<Record<string, Record<string, string>>>({});
  const [isSaving, setIsSaving] = useState(false);
  const [isRestarting, setIsRestarting] = useState(false);
  const [showRestartBanner, setShowRestartBanner] = useState(false);

  const { data, isLoading, error, refetch } = useQuery({
    queryKey: ['admin', 'secrets'],
    queryFn: () => adminSecretsClient.getSecrets(),
    staleTime: 60_000,
  });

  const handleChange = useCallback((fileName: string, key: string, value: string) => {
    setDirtyValues(prev => ({
      ...prev,
      [fileName]: { ...(prev[fileName] ?? {}), [key]: value },
    }));
  }, []);

  const dirtyCount = Object.values(dirtyValues).reduce(
    (acc, fileEntries) => acc + Object.keys(fileEntries).length, 0
  );

  const handleSave = async () => {
    const updates: SecretUpdate[] = [];
    for (const [fileName, entries] of Object.entries(dirtyValues)) {
      for (const [key, value] of Object.entries(entries)) {
        if (value) updates.push({ fileName, key, value });
      }
    }
    if (updates.length === 0) return;

    setIsSaving(true);
    try {
      await adminSecretsClient.updateSecrets(updates);
      toast.success(`${updates.length} secret aggiornati`);
      setDirtyValues({});
      setShowRestartBanner(true);
      refetch();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Errore durante il salvataggio');
    } finally {
      setIsSaving(false);
    }
  };

  const handleRestart = async () => {
    setIsRestarting(true);
    try {
      await adminSecretsClient.restartApi();
      toast.info('Riavvio API in corso...');
      // Poll health every 2s for up to 30s
      let attempts = 0;
      const poll = setInterval(async () => {
        attempts++;
        try {
          const resp = await fetch('/api/v1/health');
          if (resp.ok) {
            clearInterval(poll);
            setIsRestarting(false);
            setShowRestartBanner(false);
            toast.success('API riavviata con successo');
            refetch();
          }
        } catch {
          // Still restarting
        }
        if (attempts >= 15) {
          clearInterval(poll);
          setIsRestarting(false);
          toast.error("L'API non risponde. Verifica i log del server.");
        }
      }, 2000);
    } catch (err) {
      setIsRestarting(false);
      toast.error('Errore durante il riavvio');
    }
  };

  if (isLoading) {
    return (
      <div className="flex items-center gap-2 text-sm text-muted-foreground py-8 justify-center">
        <Loader2 className="h-4 w-4 animate-spin" /> Loading secrets...
      </div>
    );
  }

  if (error || !data) {
    return (
      <div className="rounded-lg border border-red-200 bg-red-50/50 p-4 text-sm text-red-800">
        Impossibile caricare i secret. Verifica che SECRETS_DIRECTORY sia configurato.
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <Key className="h-5 w-5 text-muted-foreground" />
          <h2 className="font-quicksand text-lg font-semibold">Secrets Management</h2>
        </div>
        <span className="text-xs text-muted-foreground font-mono">{data.secretsDirectory}</span>
      </div>

      {showRestartBanner && (
        <div className="rounded-lg border border-amber-200 bg-amber-50 p-3 flex items-center justify-between">
          <div className="flex items-center gap-2 text-sm text-amber-900">
            <RefreshCw className="h-4 w-4" />
            Secret aggiornati. Riavvia l'API per applicare.
          </div>
          <Button
            size="sm"
            variant="outline"
            onClick={handleRestart}
            disabled={isRestarting}
            className="border-amber-300 text-amber-900 hover:bg-amber-100"
          >
            {isRestarting ? <Loader2 className="h-3.5 w-3.5 animate-spin mr-1" /> : <RefreshCw className="h-3.5 w-3.5 mr-1" />}
            {isRestarting ? 'Riavvio...' : 'Riavvia ora'}
          </Button>
        </div>
      )}

      <div className="grid gap-3">
        {data.files.map(file => (
          <div
            key={file.fileName}
            className={cn(
              'rounded-lg border p-4 space-y-2.5',
              file.isInfra ? 'border-amber-200/60 bg-amber-50/20' : 'border-border'
            )}
          >
            <div className="flex items-center gap-2 mb-3">
              <h3 className="text-sm font-semibold">{file.category}</h3>
              <span className="text-[10px] text-muted-foreground font-mono">{file.fileName}</span>
              {file.isInfra && (
                <span className="inline-flex items-center gap-1 text-[10px] text-amber-700 bg-amber-100 rounded-full px-1.5 py-0.5">
                  <Shield className="h-2.5 w-2.5" /> Infra
                </span>
              )}
            </div>
            {file.entries.map(entry => (
              <SecretEntryInput
                key={entry.key}
                entryKey={entry.key}
                maskedValue={entry.maskedValue}
                hasValue={entry.hasValue}
                isPlaceholder={entry.isPlaceholder}
                isDirty={!!dirtyValues[file.fileName]?.[entry.key]}
                onChange={(key, value) => handleChange(file.fileName, key, value)}
              />
            ))}
          </div>
        ))}
      </div>

      {dirtyCount > 0 && (
        <div className="sticky bottom-4">
          <Button
            onClick={handleSave}
            disabled={isSaving}
            className="w-full bg-amber-600 hover:bg-amber-700 text-white"
          >
            {isSaving ? <Loader2 className="h-4 w-4 animate-spin mr-2" /> : <Save className="h-4 w-4 mr-2" />}
            Salva modifiche ({dirtyCount} {dirtyCount === 1 ? 'campo' : 'campi'})
          </Button>
        </div>
      )}
    </div>
  );
}
```

- [ ] **Step 2: Wire into the services page**

In `apps/web/src/app/admin/(dashboard)/monitor/services/page.tsx`, add import and component:

```tsx
import { SecretsPanel } from '@/components/admin/secrets/SecretsPanel';
```

Add after `<RestartServicePanel />`:

```tsx
      {/* Admin Secrets Management */}
      <SecretsPanel />
```

- [ ] **Step 3: Verify typecheck**

Run: `cd apps/web && pnpm typecheck 2>&1 | tail -5`
Expected: No errors

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/admin/secrets/SecretsPanel.tsx apps/web/src/app/admin/\(dashboard\)/monitor/services/page.tsx
git commit -m "feat(admin): add SecretsPanel with save and restart flow"
```

---

## Chunk 3: Tests + Final Verification

### Task 4: Unit tests

**Files:**
- Create: `apps/web/src/components/admin/secrets/__tests__/SecretsPanel.test.tsx`

- [ ] **Step 1: Write tests**

```tsx
// apps/web/src/components/admin/secrets/__tests__/SecretsPanel.test.tsx
import { render, screen } from '@testing-library/react';
import { vi, describe, it, expect } from 'vitest';

vi.mock('@/lib/api/clients/adminSecretsClient', () => ({
  adminSecretsClient: {
    getSecrets: vi.fn().mockResolvedValue({
      secretsDirectory: '/app/secrets',
      files: [
        {
          fileName: 'openrouter.secret',
          category: 'OpenRouter',
          isInfra: false,
          entries: [
            { key: 'OPENROUTER_API_KEY', maskedValue: 'sk-or-****LDER', hasValue: true, isPlaceholder: true },
          ],
        },
        {
          fileName: 'database.secret',
          category: 'Database',
          isInfra: true,
          entries: [
            { key: 'POSTGRES_USER', maskedValue: 'meepleai', hasValue: true, isPlaceholder: false },
          ],
        },
      ],
    }),
    updateSecrets: vi.fn().mockResolvedValue({ updatedFiles: [], updatedKeys: 0 }),
    restartApi: vi.fn().mockResolvedValue({ message: 'OK' }),
  },
}));

// Mock React Query
vi.mock('@tanstack/react-query', async () => {
  const actual = await vi.importActual('@tanstack/react-query');
  return {
    ...actual,
    useQuery: vi.fn().mockReturnValue({
      data: {
        secretsDirectory: '/app/secrets',
        files: [
          {
            fileName: 'openrouter.secret',
            category: 'OpenRouter',
            isInfra: false,
            entries: [
              { key: 'OPENROUTER_API_KEY', maskedValue: 'sk-or-****LDER', hasValue: true, isPlaceholder: true },
            ],
          },
          {
            fileName: 'database.secret',
            category: 'Database',
            isInfra: true,
            entries: [
              { key: 'POSTGRES_USER', maskedValue: 'meepleai', hasValue: true, isPlaceholder: false },
            ],
          },
        ],
      },
      isLoading: false,
      error: null,
      refetch: vi.fn(),
    }),
  };
});

vi.mock('@/components/layout/Toast', () => ({
  toast: { success: vi.fn(), error: vi.fn(), info: vi.fn() },
}));

import { SecretsPanel } from '../SecretsPanel';

describe('SecretsPanel', () => {
  it('renders secret file cards', () => {
    render(<SecretsPanel />);
    expect(screen.getByText('OpenRouter')).toBeInTheDocument();
    expect(screen.getByText('Database')).toBeInTheDocument();
  });

  it('shows Infra badge for infrastructure secrets', () => {
    render(<SecretsPanel />);
    expect(screen.getByText('Infra')).toBeInTheDocument();
  });

  it('shows secret keys', () => {
    render(<SecretsPanel />);
    expect(screen.getByText('OPENROUTER_API_KEY')).toBeInTheDocument();
    expect(screen.getByText('POSTGRES_USER')).toBeInTheDocument();
  });

  it('shows secrets directory path', () => {
    render(<SecretsPanel />);
    expect(screen.getByText('/app/secrets')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run tests**

Run: `cd apps/web && pnpm vitest run src/components/admin/secrets/__tests__/SecretsPanel.test.tsx`
Expected: All 4 tests PASS

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/admin/secrets/__tests__/SecretsPanel.test.tsx
git commit -m "test(admin): add unit tests for SecretsPanel"
```

---

### Task 5: Build verification + PR

- [ ] **Step 1: Run full typecheck**

Run: `cd apps/web && pnpm typecheck`
Expected: No errors

- [ ] **Step 2: Run production build**

Run: `cd apps/web && pnpm build 2>&1 | tail -10`
Expected: Build succeeds

- [ ] **Step 3: Verify backend build**

Run: `cd apps/api/src/Api && dotnet build --no-restore 2>&1 | tail -5`
Expected: 0 errors

- [ ] **Step 4: Create PR**

```bash
git push -u origin $(git branch --show-current)
gh pr create \
  --base main-dev \
  --title "feat(admin): secrets management panel with save and restart" \
  --body "$(cat <<'EOF'
## Summary
- Add `/admin/monitor/services` secrets panel to view/edit `.secret` files
- Masked values with reveal toggle, grouped by file
- Save button writes to `.secret` files on disk
- Restart button triggers graceful API restart via `StopApplication()`
- Infrastructure secrets (DB, Redis, JWT) marked with warning badge

## Test plan
- [ ] Navigate to /admin/monitor/services — SecretsPanel visible
- [ ] Values masked by default, eye icon reveals
- [ ] Edit a value → field turns yellow → Save → success toast
- [ ] Click Restart → API restarts → health poll shows success
- [ ] Non-admin user gets 403

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```
