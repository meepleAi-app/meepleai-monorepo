# Admin Users Section — Fix Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Correggere 15 problemi identificati dal spec-panel review nella sezione `/admin/users`: stato utente dinamico, contatore corretto, lingua unificata (IT), export CSV con feedback errore, azioni inviti complete, breadcrumb, ricerca inviti, refresh button, staleTime IAM, limite bulk visibile, e test.

**Architecture:** Tutti i fix sono frontend-only su file esistenti. Nessuna modifica al backend richiesta — il campo `isSuspended` è già presente in `AdminUserSchema`. Le modifiche sono isolate in 5 file page + 1 file test nuovo.

**Tech Stack:** Next.js 15, React 19, TanStack Query v5, Vitest, Testing Library, TypeScript, Tailwind CSS, Sonner (toast)

---

## File Map

| File | Cambia | Task |
|------|--------|------|
| `apps/web/src/app/admin/(dashboard)/users/page.tsx` | Stato dinamico, contatore, breadcrumb, revoke amber rows, refresh button | T1, T2, T5, T6, T8 |
| `apps/web/src/app/admin/(dashboard)/users/activity/page.tsx` | Silent export error, lingua IT | T3, T4 |
| `apps/web/src/app/admin/(dashboard)/users/invitations/page.tsx` | Lingua IT, ricerca email | T4, T7 |
| `apps/web/src/app/admin/(dashboard)/users/access-requests/page.tsx` | Lingua IT, bulk limit proattivo | T4, T10 |
| `apps/web/src/app/admin/(dashboard)/users/roles/page.tsx` | Lingua IT | T4 |
| `apps/web/src/components/admin/users/activity-table.tsx` | Lingua IT (testi interni) | T4 |
| `apps/web/src/app/admin/(dashboard)/users/__tests__/page.test.tsx` | **Nuovo file** | T11 |

**staleTime IAM** — Task T9 tocca 4 file di pagina esistenti (inline).

---

## Task 1: Stato utente dinamico

**Problema:** `users/page.tsx:246` ha un badge "Attivo" hardcoded. Il campo `isSuspended: boolean` esiste già in `AdminUser`.

**File:** Modify `apps/web/src/app/admin/(dashboard)/users/page.tsx`

- [ ] **Step 1: Apri il file e individua la riga**

  Il badge attuale è a riga ~246:
  ```tsx
  <Badge variant="outline" className="border-green-300 bg-green-50 text-green-900">
    Attivo
  </Badge>
  ```

- [ ] **Step 2: Sostituisci con badge dinamico**

  Sostituisci le righe 244-251 (la `<td>` dello Stato) con:
  ```tsx
  <td className="px-3 py-2">
    {u.isSuspended ? (
      <Badge variant="outline" className="border-red-300 bg-red-50 text-red-900 dark:border-red-700 dark:bg-red-950/30 dark:text-red-300">
        Sospeso
      </Badge>
    ) : (
      <Badge variant="outline" className="border-green-300 bg-green-50 text-green-900 dark:border-green-700 dark:bg-green-950/30 dark:text-green-300">
        Attivo
      </Badge>
    )}
  </td>
  ```

- [ ] **Step 3: Verifica build TypeScript**
  ```bash
  cd apps/web && pnpm typecheck 2>&1 | grep -i "users/page"
  ```
  Expected: nessun errore su `users/page.tsx`

- [ ] **Step 4: Commit**
  ```bash
  git add apps/web/src/app/admin/\(dashboard\)/users/page.tsx
  git commit -m "fix(admin-users): stato utente dinamico basato su isSuspended"
  ```

---

## Task 2: Contatore "totali" corretto

**Problema:** L'header mostra `totalUsers` (solo registrati) ma la tabella contiene anche le righe amber degli inviti. `2 totali` con 4 righe visibili.

**File:** Modify `apps/web/src/app/admin/(dashboard)/users/page.tsx`

- [ ] **Step 1: Aggiorna la riga del sottotitolo (circa riga 124-130)**

  Attuale:
  ```tsx
  <p className="text-muted-foreground">
    Gestisci utenti e inviti in sospeso.
    {totalUsers > 0 && (
      <span className="ml-1 text-foreground font-medium">{totalUsers} totali</span>
    )}
  </p>
  ```

  Sostituisci con:
  ```tsx
  <p className="text-muted-foreground">
    Gestisci utenti e inviti in sospeso.
    {(totalUsers > 0 || pendingInvitations.length > 0) && (
      <span className="ml-1 text-foreground font-medium">
        {totalUsers} utenti
        {pendingInvitations.length > 0 && `, ${pendingInvitations.length} inviti in attesa`}
      </span>
    )}
  </p>
  ```

- [ ] **Step 2: Verifica visuale**
  Naviga su `http://localhost:3000/admin/users`. L'header deve mostrare "2 utenti, 2 inviti in attesa".

- [ ] **Step 3: Commit**
  ```bash
  git add apps/web/src/app/admin/\(dashboard\)/users/page.tsx
  git commit -m "fix(admin-users): contatore header include inviti pending"
  ```

---

## Task 3: Export CSV con feedback errore

**Problema:** `activity/page.tsx:44` ha `catch { // export failed silently }` — l'utente non sa se l'export è fallito.

**File:** Modify `apps/web/src/app/admin/(dashboard)/users/activity/page.tsx`

- [ ] **Step 1: Aggiungi import toast** (se non già presente)

  In cima al file, aggiungi:
  ```tsx
  import { toast } from 'sonner';
  ```

- [ ] **Step 2: Sostituisci il catch silenzioso**

  Attuale (righe 43-47):
  ```tsx
  } catch {
    // export failed silently
  } finally {
    setExporting(false);
  }
  ```

  Sostituisci con:
  ```tsx
  } catch (err) {
    toast.error(err instanceof Error ? err.message : 'Esportazione fallita. Riprova.');
  } finally {
    setExporting(false);
  }
  ```

- [ ] **Step 3: Verifica build**
  ```bash
  cd apps/web && pnpm typecheck 2>&1 | grep "activity/page"
  ```
  Expected: nessun errore

- [ ] **Step 4: Commit**
  ```bash
  git add apps/web/src/app/admin/\(dashboard\)/users/activity/page.tsx
  git commit -m "fix(admin-users): mostra errore toast su export CSV fallito"
  ```

---

## Task 4: Unificazione lingua → Italiano

**Problema:** 4 pagine usano inglese mentre la pagina principale usa italiano.

### 4a — `invitations/page.tsx`

**File:** Modify `apps/web/src/app/admin/(dashboard)/users/invitations/page.tsx`

- [ ] **Step 1: Sostituisci tutte le stringhe UI in italiano**

  | Inglese (originale) | Italiano (target) |
  |---------------------|-------------------|
  | `Invitations` (h1) | `Inviti` |
  | `Manage user invitations and track their status.` | `Gestisci gli inviti utente e monitora il loro stato.` |
  | `Refresh` | `Aggiorna` |
  | `Bulk Invite` | `Invito Multiplo` |
  | `Invite User` | `Invita Utente` |
  | `Total` | `Totale` |
  | `Pending` | `In attesa` |
  | `Accepted` | `Accettati` |
  | `Expired` | `Scaduti` |
  | `Revoked` | `Revocati` |
  | `All Statuses` | `Tutti gli stati` |
  | `Email` (th) | `Email` (invariato) |
  | `Role` (th) | `Ruolo` |
  | `Status` (th) | `Stato` |
  | `Sent` (th) | `Inviato` |
  | `Expires` (th) | `Scade` |
  | `Actions` (th) | `Azioni` |
  | `Failed to load invitations` | `Caricamento inviti fallito` |
  | `Unable to load invitations. Please try again or contact support if the problem persists.` | `Impossibile caricare gli inviti. Riprova o contatta il supporto.` |
  | `Retry` | `Riprova` |
  | `No invitations match this filter` | `Nessun invito corrisponde al filtro` |
  | `No invitations sent yet` | `Nessun invito inviato finora` |
  | `Invitation resent successfully` | `Invito reinviato con successo` |
  | `Failed to resend invitation` | `Errore nel reinvio dell'invito` |
  | `Invitation revoked` | `Invito revocato` |
  | `Failed to revoke invitation` | `Errore nella revoca dell'invito` |
  | `Page {page} of {totalPages}` | `Pagina {page} di {totalPages}` |
  | `Showing X–Y of Z` | `{X}–{Y} di {Z}` |
  | `Filter by status` | `Filtra per stato` |

  Esegui le sostituzioni con Edit. Esempio per il titolo (riga ~124-125):
  ```tsx
  <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
    Inviti
  </h1>
  <p className="text-sm text-muted-foreground mt-1">
    Gestisci gli inviti utente e monitora il loro stato.
  </p>
  ```

  I toast success/error mutation (righe 84-90, 98-103):
  ```tsx
  onSuccess: () => {
    toast.success('Invito reinviato con successo');
    ...
  },
  onError: (err: unknown) => {
    toast.error(err instanceof Error ? err.message : "Errore nel reinvio dell'invito");
  },
  ```
  ```tsx
  onSuccess: () => {
    toast.success('Invito revocato');
    ...
  },
  onError: (err: unknown) => {
    toast.error(err instanceof Error ? err.message : 'Errore nella revoca dell\'invito');
  },
  ```

  Intestazioni tabella (righe ~287-291):
  ```tsx
  <th className="text-left p-3 font-medium text-muted-foreground">Email</th>
  <th className="text-left p-3 font-medium text-muted-foreground">Ruolo</th>
  <th className="text-left p-3 font-medium text-muted-foreground">Stato</th>
  <th className="text-left p-3 font-medium text-muted-foreground">Inviato</th>
  <th className="text-left p-3 font-medium text-muted-foreground">Scade</th>
  <th className="text-left p-3 font-medium text-muted-foreground">Azioni</th>
  ```

  Select options (righe ~253-258):
  ```tsx
  <SelectItem value="all">Tutti gli stati</SelectItem>
  <SelectItem value="Pending">In attesa</SelectItem>
  <SelectItem value="Accepted">Accettati</SelectItem>
  <SelectItem value="Expired">Scaduti</SelectItem>
  <SelectItem value="Revoked">Revocati</SelectItem>
  ```

  Error state (righe ~264-278):
  ```tsx
  <p className="text-sm font-medium text-red-800 dark:text-red-200">
    Caricamento inviti fallito
  </p>
  <p className="text-xs text-red-600 dark:text-red-400 mt-1">
    Impossibile caricare gli inviti. Riprova o contatta il supporto.
  </p>
  ```
  ```tsx
  <Button variant="outline" size="sm" onClick={() => refetch()}>
    Riprova
  </Button>
  ```

  Empty state (righe ~322-328):
  ```tsx
  <p>
    {statusFilter !== 'all'
      ? 'Nessun invito corrisponde al filtro'
      : 'Nessun invito inviato finora'}
  </p>
  ```

  Pagination (righe ~350-375):
  ```tsx
  <p className="text-sm text-muted-foreground">
    {(page - 1) * PAGE_SIZE + 1}&ndash;{Math.min(page * PAGE_SIZE, totalCount)} di {totalCount}
  </p>
  ...
  <span className="px-3 text-sm">
    Pagina {page} di {totalPages}
  </span>
  ```

  Bottoni header:
  ```tsx
  <Button variant="outline" size="sm" onClick={() => refetch()} disabled={isRefetching} className="gap-2">
    <RefreshCwIcon className={`h-3.5 w-3.5 ${isRefetching ? 'animate-spin' : ''}`} />
    Aggiorna
  </Button>
  <Button variant="outline" size="sm" onClick={() => setBulkDialogOpen(true)} className="gap-2">
    <UploadIcon className="h-3.5 w-3.5" />
    Invito Multiplo
  </Button>
  <Button size="sm" onClick={() => setInviteDialogOpen(true)} className="gap-2">
    <PlusIcon className="h-3.5 w-3.5" />
    Invita Utente
  </Button>
  ```

  Stats cards labels:
  ```tsx
  <p className="text-sm text-muted-foreground">Totale</p>
  ...
  <p className="text-sm text-muted-foreground">In attesa</p>
  ...
  <p className="text-sm text-muted-foreground">Accettati</p>
  ...
  <p className="text-sm text-muted-foreground">Scaduti</p>
  ...
  <p className="text-sm text-muted-foreground">Revocati</p>
  ```

### 4b — `access-requests/page.tsx`

**File:** Modify `apps/web/src/app/admin/(dashboard)/users/access-requests/page.tsx`

- [ ] **Step 2: Sostituisci stringhe UI in italiano**

  | Inglese | Italiano |
  |---------|----------|
  | `Access Requests` (h1) | `Richieste di Accesso` |
  | `Review and manage user access requests for invite-only registration.` | `Gestisci le richieste di accesso per la registrazione su invito.` |
  | `Refresh` | `Aggiorna` |
  | `Approve Selected` | `Approva Selezionati` |
  | `Total` | `Totale` |
  | `Pending` | `In attesa` |
  | `Approved` | `Approvati` |
  | `Rejected` | `Rifiutati` |
  | `All Statuses` | `Tutti gli stati` |
  | `Email` (th) | `Email` |
  | `Status` (th) | `Stato` |
  | `Requested At` (th) | `Richiesto il` |
  | `Reviewed By` (th) | `Revisionato da` |
  | `Actions` (th) | `Azioni` |
  | `Select all on page` | `Seleziona tutti` |
  | `Select {email}` | `Seleziona {email}` |
  | `Failed to load access requests` | `Caricamento richieste fallito` |
  | `Retry` | `Riprova` |
  | `No requests match this filter` | `Nessuna richiesta corrisponde al filtro` |
  | `No access requests yet` | `Nessuna richiesta di accesso` |
  | `Access request approved` | `Richiesta approvata` |
  | `Failed to approve request` | `Errore nell'approvazione` |
  | `Access request rejected` | `Richiesta rifiutata` |
  | `Failed to reject request` | `Errore nel rifiuto` |
  | `Bulk approve failed` | `Approvazione multipla fallita` |
  | `Approved X of Y requests (Z failed)` | `Approvati X di Y (Z falliti)` |
  | `Approve` (button) | `Approva` |
  | `Reject` (button) | `Rifiuta` |
  | `Page {page} of {totalPages}` | `Pagina {page} di {totalPages}` |
  | `Showing X–Y of Z` | `{X}–{Y} di {Z}` |
  | `Select up to N requests for bulk approve` | `Seleziona al massimo {N} richieste per l'approvazione multipla` |

  Per il toast del bulk approve (righe ~135-141):
  ```tsx
  onSuccess: result => {
    toast.success(
      `Approvati ${result.succeeded} di ${result.processed}` +
        (result.failed > 0 ? ` (${result.failed} falliti)` : '')
    );
    setSelectedIds(new Set());
    invalidateAll();
  },
  onError: (err: unknown) => {
    toast.error(err instanceof Error ? err.message : 'Approvazione multipla fallita');
  },
  ```

### 4c — `roles/page.tsx`

**File:** Modify `apps/web/src/app/admin/(dashboard)/users/roles/page.tsx`

- [ ] **Step 3: Aggiorna titolo e sottotitolo**

  ```tsx
  <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
    Ruoli e Permessi
  </h1>
  <p className="text-sm text-muted-foreground mt-1">
    Gestisci ruoli utente e controllo accessi sulla piattaforma
  </p>
  ```

  Aggiorna anche il metadata (righe 7-10):
  ```tsx
  export const metadata: Metadata = {
    title: 'Ruoli e Permessi',
    description: 'Gestisci ruoli utente e controllo accessi sulla piattaforma',
  };
  ```

### 4d — `activity/page.tsx`

**File:** Modify `apps/web/src/app/admin/(dashboard)/users/activity/page.tsx`

- [ ] **Step 4: Aggiorna h1 e sottotitolo**

  Attenzione: il titolo h1 è già "Audit Log" — lascia invariato (è un termine tecnico). Aggiorna il bottone:
  ```tsx
  <Button variant="outline" size="sm" onClick={handleExport} disabled={exporting}>
    <Download className="mr-2 h-4 w-4" aria-hidden="true" />
    {exporting ? 'Esportazione...' : 'Esporta CSV'}
  </Button>
  ```

  Aggiorna il sottotitolo:
  ```tsx
  <p className="text-sm text-muted-foreground mt-1">
    {totalCount > 0
      ? `${totalCount.toLocaleString('it-IT')} registrazioni trovate`
      : 'Monitora le azioni degli utenti e gli eventi di sistema'}
  </p>
  ```

### 4e — `activity-table.tsx`

**File:** Modify `apps/web/src/components/admin/users/activity-table.tsx`

- [ ] **Step 5: Aggiorna testi interni**

  | Inglese | Italiano |
  |---------|----------|
  | `Loading activity log...` | `Caricamento log...` |
  | `Failed to load activity log.` | `Impossibile caricare il log di attività.` |
  | `No activity found.` | `Nessuna attività trovata.` |
  | `Previous` | `Precedente` |
  | `Next` | `Successivo` |
  | `Showing X–Y of Z` | `{X}–{Y} di {Z}` (già presente come template) |
  | `System` (utente senza nome) | `Sistema` |

- [ ] **Step 6: Commit tutto il task 4**
  ```bash
  git add apps/web/src/app/admin/\(dashboard\)/users/invitations/page.tsx
  git add apps/web/src/app/admin/\(dashboard\)/users/access-requests/page.tsx
  git add apps/web/src/app/admin/\(dashboard\)/users/roles/page.tsx
  git add apps/web/src/app/admin/\(dashboard\)/users/activity/page.tsx
  git add apps/web/src/components/admin/users/activity-table.tsx
  git commit -m "fix(admin-users): unificazione lingua italiano su tutte le pagine users"
  ```

---

## Task 5: Azione Revoca nelle righe amber (inviti inline)

**Problema:** Le righe amber in `users/page.tsx` mostrano solo "Reinvia". La pagina `/invitations` ha anche "Revoca".

**File:** Modify `apps/web/src/app/admin/(dashboard)/users/page.tsx`

- [ ] **Step 1: Aggiungi mutation revoke** dopo la `resendMutation` esistente (circa riga 81):
  ```tsx
  const revokeMutation = useMutation({
    mutationFn: (id: string) => api.invitations.revokeInvitation(id),
    onSuccess: () => {
      toast.success('Invito revocato');
      queryClient.invalidateQueries({ queryKey: ['admin', 'invitations'] });
    },
    onError: err => {
      toast.error(err instanceof Error ? err.message : "Errore nella revoca dell'invito");
    },
  });
  ```

- [ ] **Step 2: Aggiungi import `XIcon` se non presente**
  ```tsx
  import {
    ChevronLeftIcon,
    ChevronRightIcon,
    MailIcon,
    PlusIcon,
    RefreshCwIcon,
    SearchIcon,
    UsersIcon,
    XIcon,
  } from 'lucide-react';
  ```

- [ ] **Step 3: Sostituisci la cella Azioni delle righe amber** (circa riga 204-215):

  Attuale:
  ```tsx
  <td className="px-3 py-2">
    <Button
      variant="ghost"
      size="sm"
      onClick={() => resendMutation.mutate(inv.id)}
      aria-label={`Reinvia invito a ${inv.email}`}
    >
      <RefreshCwIcon className="mr-1 h-3 w-3" />
      Reinvia
    </Button>
  </td>
  ```

  Sostituisci con:
  ```tsx
  <td className="px-3 py-2">
    <div className="flex items-center gap-1">
      <Button
        variant="ghost"
        size="sm"
        onClick={() => resendMutation.mutate(inv.id)}
        disabled={resendMutation.isPending}
        aria-label={`Reinvia invito a ${inv.email}`}
      >
        <RefreshCwIcon className="mr-1 h-3 w-3" />
        Reinvia
      </Button>
      <Button
        variant="ghost"
        size="sm"
        onClick={() => revokeMutation.mutate(inv.id)}
        disabled={revokeMutation.isPending}
        aria-label={`Revoca invito per ${inv.email}`}
        className="text-red-600 hover:text-red-700 hover:bg-red-50 dark:text-red-400 dark:hover:bg-red-950/20"
      >
        <XIcon className="mr-1 h-3 w-3" />
        Revoca
      </Button>
    </div>
  </td>
  ```

- [ ] **Step 4: Verifica build**
  ```bash
  cd apps/web && pnpm typecheck 2>&1 | grep "users/page"
  ```

- [ ] **Step 5: Commit**
  ```bash
  git add apps/web/src/app/admin/\(dashboard\)/users/page.tsx
  git commit -m "fix(admin-users): aggiungi azione Revoca agli inviti inline"
  ```

---

## Task 6: Breadcrumb nella pagina All Users

**Problema:** `/admin/users` non mostra il breadcrumb (solo icona 🏠), mentre tutte le sotto-pagine lo mostrano.

**File:** Modify `apps/web/src/app/admin/(dashboard)/users/page.tsx`

- [ ] **Step 1: Trova il componente breadcrumb nel layout**

  Il breadcrumb è reso automaticamente dal layout `(dashboard)` tramite il componente `banner`. La pagina principale di `/admin/users` non mostra il breadcrumb perché potrebbe essere una questione del layout che lo mostra solo per le sotto-pagine. Verifica il layout:
  ```bash
  find D:/Repositories/meepleai-monorepo-backend/apps/web/src/app/admin -name "layout.tsx" | head -5
  ```

- [ ] **Step 2: Se il breadcrumb manca, aggiungilo in-page**

  Se il layout non lo inietta automaticamente, aggiungi un breadcrumb esplicito nell'header della pagina `users/page.tsx` (riga ~119, prima del `<div className="space-y-6">`):

  ```tsx
  import Link from 'next/link';

  // Dentro il return, prima dell'header:
  <nav aria-label="Breadcrumb" className="flex items-center gap-1 text-sm text-muted-foreground mb-2">
    <Link href="/admin" className="hover:text-foreground transition-colors">Admin</Link>
    <span>›</span>
    <span className="text-foreground font-medium">Utenti</span>
  </nav>
  ```

  _(Link è già importato a riga ~27, quindi non serve reimportarlo)_

- [ ] **Step 3: Commit**
  ```bash
  git add apps/web/src/app/admin/\(dashboard\)/users/page.tsx
  git commit -m "fix(admin-users): aggiungi breadcrumb alla pagina All Users"
  ```

---

## Task 7: Ricerca testuale nella pagina Invitations

**Problema:** `/admin/users/invitations` ha solo filtro status, nessuna ricerca per email.

**File:** Modify `apps/web/src/app/admin/(dashboard)/users/invitations/page.tsx`

- [ ] **Step 1: Aggiungi stato per la ricerca**

  Dopo `const [page, setPage] = useState(1);` (circa riga 51), aggiungi:
  ```tsx
  const [emailSearch, setEmailSearch] = useState('');
  const debouncedEmailSearch = useDebounce(emailSearch, 300);
  ```

  Aggiungi import mancanti in cima al file:
  ```tsx
  import { SearchIcon } from 'lucide-react';
  import { Input } from '@/components/ui/primitives/input';
  import { useDebounce } from '@/hooks/useDebounce';
  ```

- [ ] **Step 2: Passa la ricerca alla query**

  Modifica la queryKey e queryFn (circa righe 63-78):
  ```tsx
  const {
    data: invitationsData,
    isLoading,
    error,
    refetch,
    isRefetching,
  } = useQuery({
    queryKey: ['admin', 'invitations', { page, pageSize: PAGE_SIZE, status: statusFilter, search: debouncedEmailSearch }],
    queryFn: () =>
      api.invitations.getInvitations({
        status: statusFilter === 'all' ? undefined : statusFilter,
        page,
        pageSize: PAGE_SIZE,
        search: debouncedEmailSearch || undefined,
      }),
    staleTime: 5_000,
  });
  ```

  Resetta la pagina quando cambia la ricerca — aggiorna il handler del filtro status:
  ```tsx
  value={statusFilter}
  onValueChange={v => {
    setStatusFilter(v as StatusFilter);
    setPage(1);
    setEmailSearch('');
  }}
  ```

- [ ] **Step 3: Aggiungi la search input nell'UI** (subito prima del Select status, circa riga ~241):
  ```tsx
  {/* Search + Filter Bar */}
  <div className="flex items-center gap-3 flex-wrap">
    <div className="relative flex-1 min-w-[200px]">
      <SearchIcon className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
      <Input
        placeholder="Cerca per email..."
        value={emailSearch}
        onChange={e => {
          setEmailSearch(e.target.value);
          setPage(1);
        }}
        className="pl-9 bg-white/70 dark:bg-zinc-800/70"
      />
    </div>
    <Select
      value={statusFilter}
      onValueChange={v => {
        setStatusFilter(v as StatusFilter);
        setPage(1);
      }}
    >
      ...
    </Select>
  </div>
  ```

  **Nota:** il parametro `search` va aggiunto all'API client se non già supportato. Verifica il backend: l'endpoint `/api/v1/admin/users/invitations` accetta `search`? Se il backend non lo supporta, la ricerca avviene lato client filtrando `invitations`:
  ```tsx
  const invitations = (invitationsData?.items ?? []).filter(inv =>
    !debouncedEmailSearch || inv.email.toLowerCase().includes(debouncedEmailSearch.toLowerCase())
  );
  ```

  Preferisci il client-side filter se il backend non supporta il parametro. Commenta `search: debouncedEmailSearch || undefined` nella queryFn se non supportato.

- [ ] **Step 4: Verifica typecheck**
  ```bash
  cd apps/web && pnpm typecheck 2>&1 | grep "invitations/page"
  ```

- [ ] **Step 5: Commit**
  ```bash
  git add apps/web/src/app/admin/\(dashboard\)/users/invitations/page.tsx
  git commit -m "feat(admin-users): ricerca per email nella pagina Inviti"
  ```

---

## Task 8: Refresh button nella pagina All Users

**Problema:** Invitations e Access Requests hanno il refresh esplicito, All Users no.

**File:** Modify `apps/web/src/app/admin/(dashboard)/users/page.tsx`

- [ ] **Step 1: Estrai `refetch` dalla query**

  Modifica `usersQuery` per esporre `refetch` e `isRefetching`:
  ```tsx
  const {
    data: usersData,
    isLoading,
    refetch: refetchUsers,
    isRefetching,
  } = useQuery({
    queryKey: ['admin', 'users', { search: debouncedSearch, role: roleFilter, page }],
    queryFn: () =>
      api.admin.getAllUsers({
        search: debouncedSearch || undefined,
        role: roleFilter === 'all' ? undefined : roleFilter,
        page,
        limit: PAGE_SIZE,
      }),
    staleTime: 5_000,
  });
  ```

  Aggiorna le righe che usano `usersQuery.data` e `usersQuery.isLoading`:
  ```tsx
  const users = usersData?.items ?? [];
  const totalUsers = usersData?.total ?? 0;
  const totalPages = Math.ceil(totalUsers / PAGE_SIZE);
  const isLoading = isLoading; // già estratto sopra
  ```

  **Nota:** la riga attuale è `const isLoading = usersQuery.isLoading;` — assicurati di rinominarla coerentemente dopo la destructure.

- [ ] **Step 2: Aggiungi il bottone Refresh nell'header** (affianco al bottone "Invita Utente", circa riga 131):
  ```tsx
  <div className="flex items-center gap-2">
    <Button
      variant="outline"
      size="sm"
      onClick={() => refetchUsers()}
      disabled={isRefetching}
      className="gap-2"
    >
      <RefreshCwIcon className={`h-3.5 w-3.5 ${isRefetching ? 'animate-spin' : ''}`} />
      Aggiorna
    </Button>
    <Button onClick={() => setInviteDialogOpen(true)}>
      <PlusIcon className="mr-2 h-4 w-4" />
      Invita Utente
    </Button>
  </div>
  ```

- [ ] **Step 3: Commit**
  ```bash
  git add apps/web/src/app/admin/\(dashboard\)/users/page.tsx
  git commit -m "feat(admin-users): aggiungi bottone Aggiorna alla lista utenti"
  ```

---

## Task 9: Riduzione staleTime per dati IAM

**Problema:** `staleTime: 30_000` su query che contengono ruoli/permessi utente. Un cambio di ruolo può non essere visibile per 30 secondi.

**File:** Modify — tutti i file di pagina già toccati nei task precedenti

- [ ] **Step 1: Aggiorna staleTime in `users/page.tsx`**

  Nelle due query (usersQuery e pendingInvitationsQuery):
  ```tsx
  staleTime: 5_000,
  ```

- [ ] **Step 2: Aggiorna staleTime in `invitations/page.tsx`**
  ```tsx
  staleTime: 5_000,
  ```
  (già fatto nel Task 7 per la lista; aggiorna anche `statsQuery`)

- [ ] **Step 3: Aggiorna staleTime in `access-requests/page.tsx`**

  Entrambe le query:
  ```tsx
  staleTime: 5_000,
  ```

- [ ] **Step 4: `activity-table.tsx`** — la query audit log è meno critica per sicurezza, lascia a `30_000`.

- [ ] **Step 5: Commit**
  ```bash
  git add apps/web/src/app/admin/\(dashboard\)/users/page.tsx
  git add apps/web/src/app/admin/\(dashboard\)/users/invitations/page.tsx
  git add apps/web/src/app/admin/\(dashboard\)/users/access-requests/page.tsx
  git commit -m "fix(admin-users): riduzione staleTime a 5s per dati IAM"
  ```

---

## Task 10: Limite bulk approve proattivo

**Problema:** In `access-requests/page.tsx` il limite `BULK_APPROVE_MAX = 25` viene comunicato solo quando superato. L'utente non sa del limite prima di selezionare.

**File:** Modify `apps/web/src/app/admin/(dashboard)/users/access-requests/page.tsx`

- [ ] **Step 1: Aggiungi hint proattivo accanto al bottone header**

  Trova il bottone "Approva Selezionati" (circa riga 213-221) e aggiungi un testo informativo sopra il componente header o sotto il bottone:
  ```tsx
  <div className="flex flex-col items-end gap-1">
    <Button
      size="sm"
      onClick={() => bulkApproveMutation.mutate([...selectedIds])}
      disabled={bulkDisabled}
      className="gap-2"
    >
      <UserCheckIcon className="h-3.5 w-3.5" />
      Approva Selezionati
      {selectedCount > 0 && ` (${selectedCount})`}
    </Button>
    <p className="text-xs text-muted-foreground">
      Max {BULK_APPROVE_MAX} per volta
    </p>
  </div>
  ```

- [ ] **Step 2: Commit**
  ```bash
  git add apps/web/src/app/admin/\(dashboard\)/users/access-requests/page.tsx
  git commit -m "fix(admin-users): mostra limite bulk approve in modo proattivo"
  ```

---

## Task 11: Test per `users/page.tsx`

**Problema:** La pagina principale degli utenti non ha test (`invitations/__tests__/page.test.tsx` esiste ma non `users/__tests__/page.test.tsx`).

**File:** Create `apps/web/src/app/admin/(dashboard)/users/__tests__/page.test.tsx`

- [ ] **Step 1: Crea il file di test**

  ```tsx
  import { screen, waitFor } from '@testing-library/react';
  import userEvent from '@testing-library/user-event';
  import { describe, it, expect, vi, beforeEach } from 'vitest';

  import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

  import AdminUsersPage from '../page';

  // ── Mock API ────────────────────────────────────────────────────────────────
  const mockGetAllUsers = vi.hoisted(() => vi.fn());
  const mockGetInvitations = vi.hoisted(() => vi.fn());
  const mockResendInvitation = vi.hoisted(() => vi.fn());
  const mockRevokeInvitation = vi.hoisted(() => vi.fn());

  vi.mock('@/lib/api', () => ({
    api: {
      admin: {
        getAllUsers: mockGetAllUsers,
      },
      invitations: {
        getInvitations: mockGetInvitations,
        resendInvitation: mockResendInvitation,
        revokeInvitation: mockRevokeInvitation,
      },
    },
  }));

  vi.mock('@/hooks/useSetNavConfig', () => ({
    useSetNavConfig: () => vi.fn(),
  }));

  vi.mock('sonner', () => ({
    toast: { success: vi.fn(), error: vi.fn() },
  }));

  // ── Fixtures ─────────────────────────────────────────────────────────────────
  const mockUsers = [
    {
      id: 'user-1',
      email: 'alice@example.com',
      displayName: 'Alice Smith',
      role: 'user',
      isSuspended: false,
      createdAt: '2026-01-01T00:00:00Z',
    },
    {
      id: 'user-2',
      email: 'bob@example.com',
      displayName: 'Bob Jones',
      role: 'admin',
      isSuspended: true,
      createdAt: '2026-01-02T00:00:00Z',
    },
  ];

  const mockPendingInvitations = [
    {
      id: 'inv-1',
      email: 'charlie@example.com',
      role: 'User',
      status: 'Pending' as const,
      createdAt: '2026-04-01T12:00:00Z',
      expiresAt: '2026-04-08T12:00:00Z',
      acceptedAt: null,
    },
  ];

  // ── Tests ─────────────────────────────────────────────────────────────────────
  describe('AdminUsersPage', () => {
    const user = userEvent.setup();

    beforeEach(() => {
      vi.clearAllMocks();
      mockGetAllUsers.mockResolvedValue({
        items: mockUsers,
        total: 2,
        page: 1,
        pageSize: 20,
      });
      mockGetInvitations.mockResolvedValue({
        items: mockPendingInvitations,
        totalCount: 1,
      });
    });

    it('renders page title and Invita Utente button', async () => {
      renderWithQuery(<AdminUsersPage />);
      expect(screen.getByText('Utenti')).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /invita utente/i })).toBeInTheDocument();
    });

    it('renders registered users from API', async () => {
      renderWithQuery(<AdminUsersPage />);
      await waitFor(() => {
        expect(screen.getByText('Alice Smith')).toBeInTheDocument();
      });
      expect(screen.getByText('Bob Jones')).toBeInTheDocument();
    });

    it('mostra badge Attivo per utenti non sospesi', async () => {
      renderWithQuery(<AdminUsersPage />);
      await waitFor(() => {
        expect(screen.getByText('Alice Smith')).toBeInTheDocument();
      });
      const rows = screen.getAllByText('Attivo');
      expect(rows.length).toBeGreaterThanOrEqual(1);
    });

    it('mostra badge Sospeso per utenti con isSuspended=true', async () => {
      renderWithQuery(<AdminUsersPage />);
      await waitFor(() => {
        expect(screen.getByText('Bob Jones')).toBeInTheDocument();
      });
      expect(screen.getByText('Sospeso')).toBeInTheDocument();
    });

    it('mostra righe amber degli inviti pending', async () => {
      renderWithQuery(<AdminUsersPage />);
      await waitFor(() => {
        expect(screen.getByText('charlie@example.com')).toBeInTheDocument();
      });
    });

    it('mostra contatore corretto: utenti + inviti', async () => {
      renderWithQuery(<AdminUsersPage />);
      await waitFor(() => {
        expect(screen.getByText(/2 utenti/)).toBeInTheDocument();
        expect(screen.getByText(/1 inviti in attesa/)).toBeInTheDocument();
      });
    });

    it('chiama resendInvitation quando si clicca Reinvia', async () => {
      mockResendInvitation.mockResolvedValueOnce(undefined);
      renderWithQuery(<AdminUsersPage />);

      await waitFor(() => {
        expect(screen.getByText('charlie@example.com')).toBeInTheDocument();
      });

      await user.click(
        screen.getByRole('button', { name: /reinvia invito a charlie@example\.com/i })
      );

      await waitFor(() => {
        expect(mockResendInvitation).toHaveBeenCalledWith('inv-1');
      });
    });

    it('chiama revokeInvitation quando si clicca Revoca', async () => {
      mockRevokeInvitation.mockResolvedValueOnce(undefined);
      renderWithQuery(<AdminUsersPage />);

      await waitFor(() => {
        expect(screen.getByText('charlie@example.com')).toBeInTheDocument();
      });

      await user.click(
        screen.getByRole('button', { name: /revoca invito per charlie@example\.com/i })
      );

      await waitFor(() => {
        expect(mockRevokeInvitation).toHaveBeenCalledWith('inv-1');
      });
    });

    it('mostra empty state quando non ci sono utenti né inviti', async () => {
      mockGetAllUsers.mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 20 });
      mockGetInvitations.mockResolvedValue({ items: [], totalCount: 0 });

      renderWithQuery(<AdminUsersPage />);

      await waitFor(() => {
        expect(screen.getByText('Nessun utente trovato.')).toBeInTheDocument();
      });
    });

    it('mostra messaggio di ricerca quando non ci sono risultati per la query', async () => {
      mockGetAllUsers.mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 20 });
      mockGetInvitations.mockResolvedValue({ items: [], totalCount: 0 });

      renderWithQuery(<AdminUsersPage />);

      const searchInput = screen.getByPlaceholderText(/cerca per nome o email/i);
      await user.type(searchInput, 'zzz');

      await waitFor(() => {
        expect(screen.getByText(/nessun utente trovato per "zzz"/i)).toBeInTheDocument();
      });
    });
  });
  ```

- [ ] **Step 2: Esegui i test**
  ```bash
  cd apps/web && pnpm test src/app/admin/\(dashboard\)/users/__tests__/page.test.tsx --run 2>&1 | tail -30
  ```
  Expected: tutti i test passano (o falliscono per il motivo corretto se non ancora implementati i fix)

- [ ] **Step 3: Commit**
  ```bash
  git add apps/web/src/app/admin/\(dashboard\)/users/__tests__/page.test.tsx
  git commit -m "test(admin-users): aggiunge test per pagina All Users"
  ```

---

## Test Suite Finale

Dopo tutti i task, verifica la suite completa:

```bash
cd apps/web
pnpm test src/app/admin/ --run 2>&1 | tail -40
pnpm typecheck 2>&1 | grep -E "error|warning" | grep "admin/users" | head -20
pnpm lint 2>&1 | grep "admin/users" | head -10
```

Expected: tutti i test verdi, nessun errore typecheck, nessun errore lint.

---

## Self-Review del Piano

### Copertura spec

| # | Problema spec-panel | Task |
|---|---------------------|------|
| 1 | Contatore "totali" fuorviante | T2 ✅ |
| 2 | Stato utente hardcoded | T1 ✅ |
| 3 | Inconsistenza lingua IT/EN | T4 ✅ |
| 4 | Export CSV silenzioso | T3 ✅ |
| 5 | Azioni inviti incomplete (no revoke) | T5 ✅ |
| 6 | Breadcrumb mancante | T6 ✅ |
| 7 | Double fetch ibrido | non in scope — architetturalmente non urgente, richiederebbe API change |
| 8 | Paginazione 0-indexed vs 1-indexed | non in scope — ActivityTable è internamente coerente (offset-based) |
| 9 | No ricerca su Invitations | T7 ✅ |
| 10 | staleTime troppo alto per IAM | T9 ✅ |
| 11 | Inviti duplicati stesso email | non in scope — backend concern |
| 12 | InlineRoleSelect per SuperAdmin | non in scope — comportamento attuale corretto (badge intenzionale) |
| 13 | Nessun test per users/page.tsx | T11 ✅ |
| 14 | Bulk approve max non comunicato | T10 ✅ |
| 15 | Refresh button mancante | T8 ✅ |

### Placeholder scan
Nessun "TBD" o "TODO" nel piano. Il Task 7 ha un'avvertenza sul backend — è documentata esplicitamente con una soluzione alternativa client-side.

### Coerenza dei tipi
- `AdminUser.isSuspended: boolean` — usato in T1, confermato in `admin-users.schemas.ts:24`
- `api.invitations.revokeInvitation(id: string)` — usato in T5, confermato in `adminUsersClient.ts:584`
- `api.invitations.getInvitations()` — già usato in `users/page.tsx:67`, pattern confermato
- Nomi importati (XIcon, RefreshCwIcon) — tutti da `lucide-react`, verificati nei file esistenti
