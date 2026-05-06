# Alpha Golden Path Gaps — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix 5 UX gaps in the alpha golden path so the invite→browse→chat flow is smooth for 10 testers.

**Architecture:** Frontend-only changes (4 tasks) + 1 backend endpoint for invitation link. No schema changes, no migrations. All changes in `apps/web/` except Task 1 which also touches `apps/api/`.

**Tech Stack:** Next.js (React 19), TypeScript, Tailwind, shadcn/ui, .NET 9 Minimal API

**Priority Order:** Task 1 (bloccante senza SMTP) → Task 2 (core UX) → Task 3-5 (polish)

---

## File Map

| Task | Files to Create/Modify | Responsibility |
|------|----------------------|----------------|
| 1 | `InvitationRow.tsx`, `invitationsClient.ts` | Copy invitation link button in admin UI |
| 2 | `games/[id]/page.tsx`, `libraryClient.ts` | Add to Library button on game detail |
| 3 | `ChatThreadView.tsx` or parent | Model downgrade toast notification |
| 4 | `ChatThreadView.tsx` or error handler | Rate limit user-facing message |
| 5 | `EmailService.Auth.cs` | Unify invitation email URL to single path |

---

### Task 1: Copy Invitation Link in Admin UI (CRITICAL)

Senza SMTP configurato, l'admin non può inviare email. Serve un bottone "Copy Link" per copiare il link di attivazione e condividerlo manualmente (WhatsApp, email manuale, etc.).

**Files:**
- Modify: `apps/web/src/components/admin/invitations/InvitationRow.tsx:67-112`
- Modify: `apps/web/src/lib/api/clients/invitationsClient.ts`
- Modify: `apps/api/src/Api/Routing/Admin/AdminUserInvitationEndpoints.cs`
- Modify: `apps/api/src/Api/BoundedContexts/Authentication/Application/Queries/Invitation/`
- Test: `apps/web/__tests__/components/admin/invitations/InvitationRow.test.tsx`

**Approach:** Il token raw non è salvato nel DB (solo l'hash SHA256). Serve un endpoint admin che rigenera il token e restituisce il link, oppure salvare il link al momento della creazione. L'approccio più semplice: aggiungere un endpoint `GET /admin/invitations/{id}/link` che restituisce il link se l'invito è ancora pending. Il token è generato al momento del send e inviato via email — se non salvato in chiaro, serve rigenerare. Verificare prima se il token raw è persistito.

**Alternativa pragmatica (consigliata per alpha):** Al momento del `SendInvitation`, il backend restituisce già il token nella response. Il frontend può mostrare il link subito dopo l'invio nell'InviteUserDialog, prima ancora di salvarlo nella tabella.

- [ ] **Step 1: Verificare se SendInvitationCommand restituisce il token**

Leggere `SendInvitationCommandHandler` — cosa restituisce? Se restituisce il token, il frontend può costruire il link immediatamente.

```bash
cd apps/api/src/Api
grep -rn "SendInvitationCommand" --include="*.cs" -A 5 | grep -i "return\|token\|result"
```

- [ ] **Step 2: Se il token è restituito — aggiornare InviteUserDialog**

In `InviteUserDialog.tsx`, dopo il successo del `sendInvitation()`:

```tsx
// Dopo riga 78-82 (success handler)
const invitationLink = `${window.location.origin}/accept-invite?token=${encodeURIComponent(result.token)}`;
setGeneratedLink(invitationLink);
```

Mostrare un campo copyable con il link:

```tsx
{generatedLink && (
  <div className="flex items-center gap-2 rounded-md border p-2 bg-muted">
    <Input value={generatedLink} readOnly className="text-xs" />
    <Button
      size="sm"
      variant="outline"
      onClick={() => {
        navigator.clipboard.writeText(generatedLink);
        toast.success('Link copiato!');
      }}
    >
      <CopyIcon className="h-4 w-4" />
    </Button>
  </div>
)}
```

- [ ] **Step 3: Se il token NON è restituito — aggiungere endpoint admin**

Aggiungere query `GetInvitationLinkQuery(Guid InvitationId)`:

```csharp
// In AdminUserInvitationEndpoints.cs
group.MapGet("/{id:guid}/link", async (Guid id, IMediator mediator) =>
{
    var result = await mediator.Send(new GetInvitationLinkQuery(id));
    return Results.Ok(new { link = result });
})
.RequireAuthorization("Admin");
```

Handler: leggere `InvitationToken` dal DB, verificare che sia pending, restituire il link costruito con `Frontend:BaseUrl`.

- [ ] **Step 4: Aggiungere Copy Link button in InvitationRow**

In `InvitationRow.tsx`, aggiungere bottone tra Resend e Revoke (solo se status = "Pending"):

```tsx
{invitation.status === 'Pending' && (
  <Button
    size="sm"
    variant="ghost"
    onClick={async () => {
      const { link } = await api.invitations.getInvitationLink(invitation.id);
      await navigator.clipboard.writeText(link);
      toast.success('Link di invito copiato!');
    }}
    title="Copia link invito"
  >
    <LinkIcon className="h-4 w-4" />
  </Button>
)}
```

- [ ] **Step 5: Scrivere test per InvitationRow copy link**

```tsx
// apps/web/__tests__/components/admin/invitations/InvitationRow.test.tsx
it('shows copy link button for pending invitations', () => {
  render(<InvitationRow invitation={{ ...mockInvitation, status: 'Pending' }} />);
  expect(screen.getByTitle('Copia link invito')).toBeInTheDocument();
});

it('hides copy link button for accepted invitations', () => {
  render(<InvitationRow invitation={{ ...mockInvitation, status: 'Accepted' }} />);
  expect(screen.queryByTitle('Copia link invito')).not.toBeInTheDocument();
});
```

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/admin/invitations/ apps/web/__tests__/
git commit -m "feat(admin): add copy invitation link button for manual sharing"
```

---

### Task 2: Add to Library Button on Game Detail

Il game detail page mostra solo un badge "In libreria" statico. Manca il bottone per aggiungere il gioco.

**Files:**
- Modify: `apps/web/src/app/(authenticated)/games/[id]/page.tsx:81-155`
- Read: `apps/web/src/lib/api/clients/libraryClient.ts` (per capire l'endpoint)
- Test: `apps/web/__tests__/app/games/game-detail.test.tsx`

- [ ] **Step 1: Verificare l'endpoint add-to-library**

```bash
cd apps/api/src/Api
grep -rn "AddToLibrary\|add-to-library\|library" Routing/ --include="*.cs" | head -20
```

Identificare il comando e il formato della request.

- [ ] **Step 2: Verificare il client frontend**

```bash
grep -rn "addToLibrary\|addGame" apps/web/src/lib/api/clients/libraryClient.ts
```

- [ ] **Step 3: Aggiungere bottone condizionale nel game detail**

In `games/[id]/page.tsx`, dopo il badge "In libreria" (riga ~146):

```tsx
{libraryStatus?.inLibrary ? (
  <Badge variant="secondary">In libreria</Badge>
) : (
  <Button
    size="sm"
    onClick={async () => {
      await api.library.addToLibrary(game.id);
      // Invalidare query per aggiornare badge
      queryClient.invalidateQueries({ queryKey: ['library-status', game.id] });
      toast.success('Gioco aggiunto alla libreria!');
    }}
  >
    <PlusIcon className="h-4 w-4 mr-1" />
    Aggiungi alla libreria
  </Button>
)}
```

- [ ] **Step 4: Scrivere test**

```tsx
it('shows add button when game is not in library', () => {
  mockUseGameInLibraryStatus.mockReturnValue({ data: { inLibrary: false } });
  render(<GameDetailPage params={{ id: 'game-1' }} />);
  expect(screen.getByText('Aggiungi alla libreria')).toBeInTheDocument();
});

it('shows badge when game is in library', () => {
  mockUseGameInLibraryStatus.mockReturnValue({ data: { inLibrary: true } });
  render(<GameDetailPage params={{ id: 'game-1' }} />);
  expect(screen.getByText('In libreria')).toBeInTheDocument();
});
```

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/games/
git commit -m "feat(games): add 'Add to Library' button on game detail page"
```

---

### Task 3: Model Downgrade Toast Notification

Il frontend gestisce l'evento SSE type 21 (ModelDowngrade) e salva i dati in state, ma non mostra nulla all'utente. Serve un toast informativo non-bloccante.

**Files:**
- Modify: `apps/web/src/components/chat-unified/ChatThreadView.tsx:378-396`
- Test: `apps/web/__tests__/components/chat-unified/model-downgrade.test.tsx`

- [ ] **Step 1: Trovare dove modelDowngrade è salvato in state**

```bash
grep -rn "modelDowngrade\|ModelDowngrade" apps/web/src/components/chat-unified/ --include="*.tsx" --include="*.ts"
```

- [ ] **Step 2: Aggiungere toast quando modelDowngrade viene ricevuto**

Nel handler dell'evento type 21 (riga ~378-396 di ChatThreadView.tsx o useAgentChatStream.ts):

```tsx
case StreamingEventType.ModelDowngrade: {
  const { fallbackModel, reason } = event;
  // Existing state update...
  
  // Add toast notification
  toast.info(
    reason === 'rate_limited'
      ? 'Modello temporaneamente cambiato per limiti di utilizzo'
      : 'Modello alternativo in uso per garantire la risposta',
    { duration: 5000 }
  );
  break;
}
```

- [ ] **Step 3: Scrivere test**

```tsx
it('shows toast when model downgrade event is received', () => {
  // Simulate SSE event type 21
  const event = { type: 21, fallbackModel: 'llama3:8b', reason: 'rate_limited' };
  // Trigger handler...
  expect(toast.info).toHaveBeenCalledWith(
    expect.stringContaining('limiti di utilizzo'),
    expect.any(Object)
  );
});
```

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/chat-unified/
git commit -m "feat(chat): show toast notification on LLM model downgrade"
```

---

### Task 4: Rate Limit User-Facing Message in Chat

Quando il provider LLM restituisce un errore, il messaggio all'utente è generico. Per l'alpha, mostrare un messaggio chiaro se il limite è raggiunto.

**Files:**
- Modify: `apps/web/src/components/chat-unified/ChatThreadView.tsx` (error handler SSE type 5)
- Test: `apps/web/__tests__/components/chat-unified/rate-limit-message.test.tsx`

- [ ] **Step 1: Trovare come gli errori SSE sono gestiti**

```bash
grep -rn "type.*5\|Error\|error" apps/web/src/components/chat-unified/ --include="*.tsx" --include="*.ts" | grep -i "event\|sse\|stream"
```

- [ ] **Step 2: Migliorare il messaggio di errore per rate limit**

Nel handler dell'evento type 5 (Error):

```tsx
case StreamingEventType.Error: {
  const errorCode = event.errorCode || event.code;
  const userMessage = errorCode === 'rate_limited'
    ? 'Hai raggiunto il limite di messaggi. Riprova tra qualche minuto.'
    : errorCode === 'provider_unavailable'
    ? 'Il servizio AI è temporaneamente non disponibile. Riprova tra poco.'
    : event.message || 'Si è verificato un errore. Riprova.';
  
  // Update message with user-friendly error
  setMessages(prev => [...prev, {
    role: 'assistant',
    content: userMessage,
    isError: true,
  }]);
  break;
}
```

- [ ] **Step 3: Scrivere test**

```tsx
it('shows rate limit message when error code is rate_limited', () => {
  const event = { type: 5, errorCode: 'rate_limited' };
  // Trigger handler...
  expect(screen.getByText(/limite di messaggi/)).toBeInTheDocument();
});

it('shows generic error for unknown error codes', () => {
  const event = { type: 5, errorCode: 'unknown' };
  // Trigger handler...
  expect(screen.getByText(/errore/)).toBeInTheDocument();
});
```

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/chat-unified/
git commit -m "feat(chat): show user-friendly rate limit and error messages"
```

---

### Task 5: Unificare URL Invito nelle Email

Due flow usano URL diversi (`/accept-invite` vs `/setup-account`). Per consistenza alpha, unificare su `/accept-invite` che è il path primario.

**Files:**
- Modify: `apps/api/src/Api/Services/Email/EmailService.Auth.cs:374`
- Test: `tests/Api.Tests/Services/EmailServiceTests.cs` (se esiste)

- [ ] **Step 1: Cambiare URL nel metodo enhanced**

In `EmailService.Auth.cs`, riga 374, cambiare:

```csharp
// PRIMA:
var invitationLink = $"{_frontendBaseUrl}/setup-account?token={Uri.EscapeDataString(token)}";

// DOPO:
var invitationLink = $"{_frontendBaseUrl}/accept-invite?token={Uri.EscapeDataString(token)}";
```

- [ ] **Step 2: Verificare che /accept-invite gestisca entrambi i backend endpoint**

Il path `/accept-invite` chiama `POST /auth/accept-invitation`. Verificare che il `ProvisionAndInviteUserCommandHandler` crei un `InvitationToken` compatibile con `AcceptInvitationCommandHandler` (non solo con `ActivateInvitedAccountCommandHandler`).

```bash
grep -rn "InvitationToken\|PendingUserId" apps/api/src/Api/BoundedContexts/Authentication/ --include="*.cs" | head -20
```

Se il ProvisionAndInvite crea un `PendingUserId` che solo `ActivateAccount` gestisce, allora serve adattare `AcceptInvitation` per gestire anche quel caso, oppure mantenere i due path separati.

**Se incompatibili**: mantenere entrambi i path ma assicurarsi che il link email usi sempre il path corretto per il flow che l'ha generato. In quel caso, chiudere questo task come "no change needed" — la separazione è intenzionale.

- [ ] **Step 3: Se compatibili — aggiornare e testare**

```bash
cd apps/api/src/Api
dotnet test --filter "Email" --no-build
```

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/Services/Email/
git commit -m "fix(auth): unify invitation email URL to /accept-invite path"
```

---

## Execution Order & Dependencies

```
Task 1 (Copy Link)  ──── CRITICAL, senza SMTP blocca alpha
Task 2 (Add to Lib) ──── indipendente, core UX
Task 3 (Downgrade)  ──── indipendente, polish
Task 4 (Rate Limit) ──── indipendente, polish
Task 5 (Unify URL)  ──── richiede investigazione compatibilità, potrebbe essere no-op
```

Task 1 e 2 sono indipendenti e possono essere eseguiti in parallelo.
Task 3 e 4 sono indipendenti e possono essere eseguiti in parallelo.
Task 5 è standalone.

---

## Verification Checklist

Dopo tutti i task, verificare il golden path completo:

- [ ] Admin invia invito → vede link copiabile
- [ ] Tester apre link → form password → account attivo
- [ ] Tester in catalogo → vede giochi → apre detail
- [ ] Tester clicca "Aggiungi alla libreria" → badge aggiornato
- [ ] Tester apre chat → invia domanda → riceve risposta RAG
- [ ] Se modello degradato → toast informativo appare
- [ ] Se rate limit → messaggio chiaro (non errore generico)
