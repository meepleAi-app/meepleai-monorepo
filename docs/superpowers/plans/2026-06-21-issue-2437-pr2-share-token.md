# #2437-2 — Play Records share-token + public view — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Condividere un PlayRecord via link pubblico con token rotabile (decisione **M1**): il creator genera/revoca un token; chiunque con il link vede il record read-only senza autenticazione. Replica 1:1 il pattern share-token di **GameNightPlaylist**.

**Architecture:** BE greenfield che clona il pattern `GameNightPlaylist` share-token (entity `ShareToken`/`IsShared` + `GenerateShareToken()`/`RevokeShareToken()` + 2 command + 1 query anonima + 3 endpoint + migration + index unique nullable). FE: refactor `PlayRecordDetailView` → estrae `PlayRecordDetailBody` puro (prop-driven, decisione utente), riusato da detail autenticato e da una nuova `PlayRecordPublicView` (route `(public)/play-records/shared/[token]`); share dialog semplice (genera/copia/revoca, decisione utente).

**Tech Stack:** .NET 9 (MediatR, EF Core, migration, Moq/xUnit/Testcontainers) · Next.js/React 19 (TanStack Query, Zod, Vitest).

**Spec:** `docs/superpowers/specs/2026-06-20-issue-2436-prc-2437-spec-panel.md` (#2437 sub-PR 2). **Decisioni utente 2026-06-21**: public view = **refactor estrai DetailBody condiviso**; share UI = **semplice token-style**.

## Reuse map (verificata, file:line)
- **BE template GameNightPlaylist**: domain `GameNightPlaylist.cs:21-22,194-213` (ShareToken/IsShared + Generate/Revoke); handlers `GenerateShareLinkCommandHandler.cs:27-46` + `RevokeShareLinkCommandHandler.cs:26-40` (creator-only); query `GetPlaylistByShareTokenQueryHandler.cs:23-32`; endpoints `PlaylistEndpoints.cs:176-222`; EF config `GameNightPlaylistEntityConfiguration.cs:35-42,79-82` (index unique nullable filter); repo `GameNightPlaylistRepository.cs:72-84` (GetByShareToken con `&& p.IsShared`); response `ShareLinkResponse(ShareToken, ShareUrl)`.
- **BE PlayRecord innesto**: `PlayRecord.cs` (domain), `PlayRecordEntity.cs`, `PlayRecordEntityConfiguration.cs`, `PlayRecordRepository.cs:217-385` (MapToDomain/MapToPersistence + `SetXmin` pattern @293), `IPlayRecordRepository.cs`, `PlayRecordEndpoints.cs` (auth **per-endpoint**, non group → GET pubblico usa `.AllowAnonymous()`), `GetPlayRecordQueryHandler.cs` (mapping+presigning da estrarre).
- **FE**: `PlayRecordDetailView.tsx` (da refactorare), `play-records.api.ts`, `usePlayRecords.ts` (`playRecordsKeys`), `ShareSuccessToast` `share-success-toast.tsx`, route pubblica esempio `app/(public)/library/shared/[token]/page.tsx`, primitive `@/components/ui/overlays/dialog` + `@/components/ui/primitives/button`.

---

## Task 1: BE — persistence foundation (domain + entity + repo + migration)

**Files:** `PlayRecord.cs`, `PlayRecordEntity.cs`, `PlayRecordEntityConfiguration.cs`, `PlayRecordRepository.cs`, `IPlayRecordRepository.cs` (modify); migration (generate).

- [ ] **Step 1: Domain** — in `PlayRecord.cs`, after `Location` add props; add the two methods (clone of GameNightPlaylist) + an internal restore:
```csharp
    public string? ShareToken { get; private set; }
    public bool IsShared { get; private set; }
```
```csharp
    /// <summary>Generates a URL-safe share token for public read access (#2437-2, GameNightPlaylist pattern).</summary>
    public string GenerateShareToken()
    {
        ShareToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .Replace("/", "_").Replace("+", "-").TrimEnd('=');
        IsShared = true;
        UpdatedAt = TimeProvider.System.GetUtcNow().UtcDateTime;
        return ShareToken;
    }

    /// <summary>Revokes the share token, disabling public access.</summary>
    public void RevokeShareToken()
    {
        ShareToken = null;
        IsShared = false;
        UpdatedAt = TimeProvider.System.GetUtcNow().UtcDateTime;
    }

    /// <summary>Repository-only: restore share state after loading from persistence.</summary>
    internal void SetShareState(string? shareToken, bool isShared)
    {
        ShareToken = shareToken;
        IsShared = isShared;
    }
```

- [ ] **Step 2: Infra entity + config** — in `PlayRecordEntity.cs` after `Location`:
```csharp
    public string? ShareToken { get; set; }
    public bool IsShared { get; set; }
```
In `PlayRecordEntityConfiguration.cs` (after the Location/other property config, before the photo relationship):
```csharp
        builder.Property(e => e.ShareToken).HasMaxLength(50);
        builder.Property(e => e.IsShared).HasDefaultValue(false).IsRequired();
        builder.HasIndex(e => e.ShareToken)
            .HasDatabaseName("IX_play_records_share_token")
            .IsUnique()
            .HasFilter("\"ShareToken\" IS NOT NULL");
```
(Match the column-naming convention of the table — the photo config used PascalCase EF defaults; verify whether play_records uses snake_case or PascalCase columns and align the `HasFilter` quote accordingly. Read the existing `PlayRecordEntityConfiguration` to confirm.)

- [ ] **Step 3: Repository round-trip + GetByShareToken** — in `PlayRecordRepository.cs`:
  - `MapToDomain` (after the `SetXmin(entity.Xmin)` at ~line 293): `record.SetShareState(entity.ShareToken, entity.IsShared);`
  - `MapToPersistence` (in the entity initializer after `Location`): `ShareToken = record.ShareToken, IsShared = record.IsShared,`
  - Add the method:
```csharp
    public async Task<PlayRecord?> GetByShareTokenAsync(string shareToken, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(shareToken);
        var entity = await DbContext.PlayRecords
            .AsNoTracking()
            .Include(r => r.Players).ThenInclude(p => p.Scores)
            .Include(r => r.Photos)
            .FirstOrDefaultAsync(r => r.ShareToken == shareToken && r.IsShared, cancellationToken)
            .ConfigureAwait(false);
        return entity != null ? MapToDomain(entity) : null;
    }
```
  In `IPlayRecordRepository.cs` add: `Task<PlayRecord?> GetByShareTokenAsync(string shareToken, CancellationToken cancellationToken = default);`

- [ ] **Step 4: Migration** — run (from `apps/api/src/Api`):
```bash
dotnet ef migrations add AddPlayRecordShareToken
```
Review the generated migration: it must `AddColumn` `ShareToken` (nullable, maxLength 50) + `IsShared` (bool, default false) on `play_records`, and `CreateIndex` unique with the `IS NOT NULL` filter. Do NOT edit old migrations.

- [ ] **Step 5: Build** — `dotnet build D:/Repositories/meepleai-monorepo-frontend/apps/api/src/Api/Api.csproj 2>&1 | grep -E "error" || echo BUILD_OK` → BUILD_OK

- [ ] **Step 6: Commit**
```bash
git add -A
git commit -m "feat(play-records): #2437-2 BE share-token persistence + migration"
```

---

## Task 2: BE — generate/revoke commands + endpoints

**Files (create):** `Application/Commands/PlayRecords/GeneratePlayRecordShareTokenCommand.cs` + `Handler` + `Validator`; `RevokePlayRecordShareTokenCommand.cs` + `Handler` + `Validator`; `Application/DTOs/PlayRecords/ShareLinkResponse.cs`. **Modify:** `PlayRecordEndpoints.cs`. **Test:** handler unit tests.

- [ ] **Step 1: ShareLinkResponse DTO** (PlayRecord-specific, avoid cross-context coupling):
```csharp
namespace Api.BoundedContexts.GameManagement.Application.DTOs.PlayRecords;
/// <summary>Share-link response for a play record (#2437-2).</summary>
public record ShareLinkResponse(string ShareToken, string ShareUrl);
```

- [ ] **Step 2: Commands + handlers + validators** (clone GameNightPlaylist generate/revoke, creator-only via `record.CreatedByUserId != command.UserId → UnauthorizedAccessException`). `GeneratePlayRecordShareTokenCommand(Guid PlayRecordId, Guid UserId) : ICommand<ShareLinkResponse>`; handler loads via `_repository.GetByIdAsync`, checks creator, `record.GenerateShareToken()`, `UpdateAsync` + `SaveChangesAsync`, returns `new ShareLinkResponse(token, $"/play-records/shared/{token}")`. `RevokePlayRecordShareTokenCommand(Guid PlayRecordId, Guid UserId) : ICommand`; handler checks creator, `record.RevokeShareToken()`, save. Validators: `NotEmpty` on PlayRecordId + UserId. (Full code mirrors `GenerateShareLinkCommandHandler.cs:27-46` / `RevokeShareLinkCommandHandler.cs:26-40` — read them and adapt names PlayRecord. Note: `UnauthorizedAccessException` maps to 403 via the existing exception middleware — verify it does; if not, use `ForbiddenException` like `UpdatePlayRecordCommandHandler` does.)

> **Authz note:** `UpdatePlayRecordCommandHandler` uses `ForbiddenException` (not `UnauthorizedAccessException`) for the not-creator case (via `PlayRecordPermissionChecker.CanEditAsync`). For consistency, the share handlers should throw `ForbiddenException("Only the record creator can ...")` → 403. Prefer this over GameNightPlaylist's `UnauthorizedAccessException`.

- [ ] **Step 3: Endpoints** — in `PlayRecordEndpoints.cs`, add after the photos endpoint (~line 98), in the Commands region:
```csharp
        group.MapPost("/play-records/{recordId:guid}/share", HandleGenerateShareLink)
            .RequireAuthenticatedUser()
            .Produces<ShareLinkResponse>(200).Produces(401).Produces(StatusCodes.Status403Forbidden).Produces(404)
            .WithTags("PlayRecords").WithSummary("Generate a public share link (creator-only)");

        group.MapDelete("/play-records/{recordId:guid}/share", HandleRevokeShareLink)
            .RequireAuthenticatedUser()
            .Produces(204).Produces(401).Produces(StatusCodes.Status403Forbidden).Produces(404)
            .WithTags("PlayRecords").WithSummary("Revoke the share link (creator-only)");
```
Add the handler methods in the Command Handlers region:
```csharp
    private static async Task<IResult> HandleGenerateShareLink(Guid recordId, [FromServices] IMediator mediator, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GeneratePlayRecordShareTokenCommand(recordId, httpContext.User.GetUserId()), cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }
    private static async Task<IResult> HandleRevokeShareLink(Guid recordId, [FromServices] IMediator mediator, HttpContext httpContext, CancellationToken cancellationToken)
    {
        await mediator.Send(new RevokePlayRecordShareTokenCommand(recordId, httpContext.User.GetUserId()), cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }
```

- [ ] **Step 4: Unit tests** — create handler unit tests (mirror existing GameManagement handler tests with Moq `IPlayRecordRepository` + `IUnitOfWork`): generate sets a token + saves; generate by non-creator throws `ForbiddenException`; revoke clears the token. Run the GameManagement filter → PASS.

- [ ] **Step 5: Build + commit**
```bash
dotnet build D:/Repositories/meepleai-monorepo-frontend/apps/api/src/Api/Api.csproj 2>&1 | grep -E "error" || echo BUILD_OK
git add -A && git commit -m "feat(play-records): #2437-2 BE generate/revoke share-link commands + endpoints"
```

---

## Task 3: BE — public query (GetByShareToken) + shared DTO mapper + endpoint

**Files:** create `PlayRecordDtoMapper.cs` (extract from `GetPlayRecordQueryHandler`), `GetPlayRecordByShareTokenQuery.cs` + `Handler`; modify `GetPlayRecordQueryHandler.cs`, `PlayRecordEndpoints.cs`; integration test.

- [ ] **Step 1: Extract `PlayRecordDtoMapper`** — create `Application/Services/PlayRecordDtoMapper.cs` with a static `MapAsync(PlayRecordEntity entity, IBlobStorageService blobStorage, int presignExpirySeconds)` that produces a `PlayRecordDto` (the photo-presigning + winner/outcome + scoring-config deserialization currently inline in `GetPlayRecordQueryHandler.Handle`). Then refactor `GetPlayRecordQueryHandler` to call it (keeps its authz check + load). Run `GetPlayRecordQueryHandlerTests` → still PASS (no behaviour change).

- [ ] **Step 2: Query + handler (anonymous)** — `GetPlayRecordByShareTokenQuery(string ShareToken) : IQuery<PlayRecordDto>`. Handler injects `MeepleAiDbContext` + `IBlobStorageService`, loads the entity by token (`_context.PlayRecords.AsNoTracking().Include(Players).ThenInclude(Scores).Include(Photos).FirstOrDefaultAsync(r => r.ShareToken == query.ShareToken && r.IsShared)`), throws `NotFoundException("PlayRecord", query.ShareToken)` if null, returns `await PlayRecordDtoMapper.MapAsync(entity, _blobStorage, 3600)`. (No authz — access IS the token.)

- [ ] **Step 3: Endpoint (AllowAnonymous)** — in `PlayRecordEndpoints.cs` Queries region:
```csharp
        group.MapGet("/play-records/shared/{token}", HandleGetSharedPlayRecord)
            .AllowAnonymous()
            .Produces<PlayRecordDto>(200).Produces(404)
            .WithTags("PlayRecords").WithSummary("Get a shared play record by token (public, no auth)");
```
```csharp
    private static async Task<IResult> HandleGetSharedPlayRecord(string token, [FromServices] IMediator mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetPlayRecordByShareTokenQuery(token), cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }
```

- [ ] **Step 4: Integration test** (Testcontainers) — create `PlayRecordShareTokenTests.cs`: create a record, generate a share token, fetch by token (returns the DTO), revoke, fetch by token again → `NotFoundException`. Also: fetch by a bogus token → NotFound. Run → PASS (Docker; if unavailable, ensure compile + report).

- [ ] **Step 5: Build + commit**
```bash
git add -A && git commit -m "feat(play-records): #2437-2 BE public get-by-share-token + DTO mapper"
```

---

## Task 4: FE — refactor: extract `PlayRecordDetailBody`

**Files:** create `apps/web/src/components/play-records/PlayRecordDetailBody.tsx`; modify `PlayRecordDetailView.tsx`; tests stay green.

- [ ] **Step 1** — Read `PlayRecordDetailView.tsx`. Extract everything AFTER the loading/error guards (the derivation helpers usage + the full JSX composition: HeroPodium, ConnectionBar, KpiGrid, Photos section, Classifica, ScoreBreakdown, Notes, Rematch + the creator-only photo upload) into a new pure component:
```tsx
export interface PlayRecordDetailBodyProps {
  record: PlayRecordDto;
  currentUserId: string | null;
}
export function PlayRecordDetailBody({ record, currentUserId }: PlayRecordDetailBodyProps): ReactElement { /* moved body */ }
```
`isCreator` is computed inside from `currentUserId === record.createdByUserId`. The derivation helpers (`buildRankedScores`, `derivePerspective`, etc.) move with it (or stay module-level and are imported). `PlayRecordDetailView` becomes a thin wrapper: `usePlayRecord` + `useCurrentUser` + loading/error guards + `<PlayRecordDetailBody record={record} currentUserId={currentUser?.id ?? null} />`.

- [ ] **Step 2** — Run the existing `PlayRecordDetailView.test.tsx` + `play-records-axe.test.tsx` → MUST stay green (pure refactor, same DOM). Fix any import/mock fallout. Typecheck clean.

- [ ] **Step 3: Commit**
```bash
git add -A && git commit -m "refactor(play-records): #2437-2 extract PlayRecordDetailBody (prop-driven)"
```

---

## Task 5: FE — share API client + hooks

**Files:** modify `play-records.api.ts`, `play-records.schemas.ts` (DTO already has needed fields; add `ShareLinkResponse` schema), `usePlayRecords.ts` (hooks + query keys); tests.

- [ ] **Step 1: schema + api** — add Zod `ShareLinkResponseSchema = z.object({ shareToken: z.string(), shareUrl: z.string() })`. In `play-records.api.ts` add (all with `credentials: 'include'`):
  - `generateShareToken(recordId): Promise<ShareLinkResponse>` → `POST /play-records/{id}/share`
  - `revokeShareToken(recordId): Promise<void>` → `DELETE /play-records/{id}/share`
  - `getSharedRecord(token): Promise<PlayRecordDto>` → `GET /play-records/shared/{token}` (no credentials needed — public, but harmless to include)
  TDD: a test asserting `generateShareToken` POSTs to the right URL and parses the response; `getSharedRecord` GETs `/shared/{token}`.

- [ ] **Step 2: hooks** — in `usePlayRecords.ts` (or a sibling), add `useGeneratePlayRecordShareToken(recordId)` + `useRevokePlayRecordShareToken(recordId)` (mutations, invalidate `playRecordsKeys.detail(recordId)`) and `useSharedPlayRecord(token)` (query, key `['play-records','shared',token]`, `retry:false`, enabled when token truthy). TDD per hook.

- [ ] **Step 3: Commit**
```bash
git add -A && git commit -m "feat(play-records): #2437-2 share API client + hooks"
```

---

## Task 6: FE — SharePlayRecordDialog + wire button (creator-only) + i18n

**Files:** create `apps/web/src/components/play-records/SharePlayRecordDialog.tsx`; modify `PlayRecordDetailBody.tsx`; i18n it/en; tests.

- [ ] **Step 1: Dialog** — `SharePlayRecordDialog({ recordId, currentShareToken, open, onClose })`. If no token: a "Genera link" button → `useGeneratePlayRecordShareToken`. Once a token exists: show the **FE** share URL `${window.location.origin}/play-records/shared/${token}` (NOT the API path), a Copy button (`navigator.clipboard.writeText(...)` → success toast via `toast`/`ShareSuccessToast`), and a "Revoca" button → `useRevokePlayRecordShareToken`. Pure-ish, labels via `useTranslation`. TDD: generate flow calls the hook; copy writes to clipboard (mock `navigator.clipboard`); revoke calls the hook.

- [ ] **Step 2: Wire in DetailBody** — in `PlayRecordDetailBody`, add a creator-only "🔗 Condividi" button (next to the photo section header or in a header action area) that opens `SharePlayRecordDialog` with `currentShareToken={record.shareToken}`. (Requires `shareToken` on the FE DTO — add `shareToken: z.string().nullable().optional()` to `PlayRecordDtoSchema`. Verify the BE GET DTO exposes shareToken: the `PlayRecordDto` does NOT currently include it; **either** add `ShareToken` to the BE DTO + mapper (so the detail view knows the current token) **or** the dialog fetches it on open. Simplest: add `string? ShareToken` to `PlayRecordDto` + mapper in Task 3, and `shareToken` to the FE schema here. Note this cross-task dependency.)

> **Cross-task note:** add `string? ShareToken` to `PlayRecordDto` (BE, Task 3 mapper) so the authenticated detail view can show the current share state. The PUBLIC `getSharedRecord` response also carries it but that's harmless (the viewer already has the token). Update `PlayRecordDtoSchema` accordingly.

- [ ] **Step 3: i18n** — `playRecords.share.*` (it/en): button, dialogTitle, generate, generating, copy, copied, revoke, revoking, urlLabel, revoked.

- [ ] **Step 4: Commit**
```bash
git add -A && git commit -m "feat(play-records): #2437-2 share dialog + creator-only button + i18n"
```

---

## Task 7: FE — public route + PlayRecordPublicView

**Files:** create `apps/web/src/app/(public)/play-records/shared/[token]/page.tsx` + `apps/web/src/components/play-records/PlayRecordPublicView.tsx`; tests.

- [ ] **Step 1: PublicView** — `PlayRecordPublicView({ token })`: `const { data, isLoading, error } = useSharedPlayRecord(token);` → loading skeleton / not-found state (mirror the library shared page) / `<PlayRecordDetailBody record={data} currentUserId={null} />`. `currentUserId={null}` → no creator actions (no edit/upload/share), spectator perspective. TDD: renders the body for a found record; shows not-found for an error.

- [ ] **Step 2: Public route** — `app/(public)/play-records/shared/[token]/page.tsx`: `'use client'`, `const { token } = useParams()`, render `<PlayRecordPublicView token={...} />`. (Mirror `app/(public)/library/shared/[token]/page.tsx` structure — it's in the `(public)` group, no auth.)

- [ ] **Step 3: Verify** — `pnpm test src/components/play-records "src/app/(public)/play-records" --run` → PASS. Typecheck + lint clean.

- [ ] **Step 4: Commit**
```bash
git add -A && git commit -m "feat(play-records): #2437-2 public shared play-record route + view"
```

---

## Final verification
- `pnpm test src/components/play-records src/lib/api "src/app/(public)/play-records" --run` → no regressions
- `pnpm typecheck && pnpm lint` → clean
- `dotnet build apps/api/src/Api/Api.csproj` → BUILD_OK; BE tests pass (integration needs Docker)

## Self-review notes
- **Token is URL-safe Base64** (no `/`, `+`, `=`) → safe in a path segment. The FE builds the shareable URL as `origin + /play-records/shared/ + token`; the BE `ShareUrl` (API path) is informational only.
- **`IsShared` gate**: `GetByShareTokenAsync` requires `IsShared == true`, so a revoked token (nulled) 404s. Unique index is filtered (`IS NOT NULL`) so multiple revoked rows (null token) don't collide.
- **Public DTO leakage**: the shared DTO exposes the same fields as the authenticated detail (players, scores, photos, notes). That's intended — sharing means making it public. `CreatedByUserId` is exposed (already is in the auth DTO); acceptable.
- **`ShareToken` on the DTO**: exposing it on the authenticated GET lets the creator's detail view show/revoke the current link. On the public GET it's redundant but harmless.
- **Closes nothing**: #2437 still needs sub-PR 3 (audit + restore-version).
