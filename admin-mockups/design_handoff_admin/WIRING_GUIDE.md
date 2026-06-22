# Wiring Guide — Admin mockup → componente reale

## Differenze vs handoff utente

Le pagine admin **hanno regole più strette**:

| Aspetto | Demo utente | Admin Console |
|---|---|---|
| Density | Comfortable | High (-1 step) |
| Mobile | Fullscreen | Solo desktop (fallback msg) |
| Dark mode | Auto | Default dark |
| Permission | viewer dell'entity | RBAC server-checked + UI gate |
| Mutazioni | Form + toast | + audit_log + typed-confirm per sensibili |
| Performance | <500 entities | virtualize per 10k+ rows |
| Realtime | Opzionale | SSE/WS spesso obbligatori (live event log, KB upload) |

## Anatomia di un mock admin

```
sp5-admin-X.html
```

Ogni file è **vanilla HTML** (no React) che importa:
- `tokens.css` (design system base)
- `components.css` (design system base)
- `admin-base.css` (admin overrides: density, dark default, admin components)
- `admin-nav.js` (sidebar nav data + renderer)

Pattern HTML ricorrente:

```html
<body class="admin-page">
  <div class="admin-mobile-fallback">...</div>
  <div class="admin-shell">
    <div data-admin-nav-mount></div>
    <main>
      <header class="admin-top">...</header>
      <div class="admin-body">
        <!-- contenuto specifico pagina -->
      </div>
    </main>
  </div>
  <script src="admin-nav.js"></script>
  <script>renderAdminNav('overview');</script>
</body>
```

Da portare in React:

```tsx
<AdminLayout activeNavId="overview">
  <AdminTopbar
    title="Overview sistema"
    crumbs={['Admin', 'Overview', 'ultimo refresh 14:23:08']}
    actions={<>
      <ShortcutHint keys={['g', 'o']}>torna qui</ShortcutHint>
      <Button onClick={refresh}>⟳ Refresh <Kbd>R</Kbd></Button>
      <IconButton onClick={toggleTheme}>🌗</IconButton>
    </>}
  />
  <AdminBody>
    {/* page-specific content */}
  </AdminBody>
</AdminLayout>
```

`<AdminLayout>` decide internamente:
- Se viewport <880px → renderizza solo `<MobileFallback>`
- Altrimenti → shell completo con sidebar + topbar + children

## Estrazione componenti dai mock

Per ogni mock, identifica:

1. **Dati statici** (KPI values, table rows, log lines) → vanno API-ificati
2. **Stati interattivi** (selected row, tab active, form input) → `useState` o URL state
3. **Componenti riusabili** → estrai in `src/components/admin/v2/`
4. **Permessi** → wrap con `<RequireRole role="admin|superadmin">`
5. **Mutazioni** → audit_log middleware + ConfirmModal se sensibili
6. **Realtime** → SSE/WebSocket hook

## Pattern: tabella admin

I mock admin usano una tabella ricorrente. Esempio (sp5-admin-users):

```html
<table class="admin-table">
  <thead><tr>...</tr></thead>
  <tbody>
    <tr class="selected"><td>...</td></tr>
  </tbody>
</table>
```

Da React:

```tsx
<AdminDataTable
  data={users}
  columns={[
    { id: 'user', header: 'Utente', sortable: true, render: u => <UserCell user={u}/> },
    { id: 'role', header: 'Ruolo', render: u => <RoleChip role={u.role}/> },
    { id: 'joined', header: 'Joined', sortable: true, render: u => <DateMono date={u.joined}/> },
    { id: 'lastActive', header: 'Last active', sortable: true, render: u => <RelativeTime date={u.lastActive}/> },
    { id: 'status', header: 'Status', render: u => <StatusChip kind={u.status}/> },
  ]}
  enableMultiSelect
  bulkActions={[
    { id: 'changeRole', label: '✏️ Cambia ruolo', onClick: openChangeRoleModal },
    { id: 'suspend', label: '⏸ Sospendi', onClick: openSuspendModal, requireRole: 'admin' },
    { id: 'delete', label: '🗑 Elimina', danger: true, onClick: openDeleteModal, requireRole: 'superadmin', requireStepUp: true },
  ]}
  onRowClick={user => openDrillDown(user)}
  onSort={(col, dir) => setSorting({ col, dir })}
  virtualizeIf={rows => rows.length > 200}
/>
```

## Pattern: drill-down sidebar (A2, A4, A5)

Sidebar destra che si apre quando una riga è selezionata. Pattern URL: `/admin/users?selected=u-marco`

```tsx
function AdminUsersPage() {
  const [selectedId, setSelectedId] = useSearchParam('selected');
  const { data: users } = useUsers(filters);
  const { data: drillData } = useUserDrillDown(selectedId);

  return (
    <div className="users-grid">
      <AdminDataTable {...} onRowClick={u => setSelectedId(u.id)} />
      {selectedId && (
        <UserDetailDrawer
          user={drillData}
          onClose={() => setSelectedId(null)}
          showAuditLog
          showDangerZone={currentUser.role === 'superadmin'}
        />
      )}
    </div>
  );
}
```

## Pattern: bulk actions con typed-confirm

```tsx
async function handleBulkDelete(ids: string[]) {
  if (ids.length === 0) return;

  const confirmed = await openConfirmModal({
    title: `Elimina ${ids.length} utenti?`,
    danger: true,
    body: (
      <>
        <p>Questa operazione è <strong>permanente</strong>. {ids.length} account, le loro sessioni, agenti personali e KB privati saranno cancellati irreversibilmente.</p>
        <p>Audit log: questa azione sarà registrata con il tuo ID e l'IP corrente.</p>
      </>
    ),
    typedConfirm: 'ELIMINA UTENTI',  // utente deve digitare esattamente questo
    confirmLabel: 'Elimina definitivamente',
    requireStepUp: true,             // 2FA challenge
  });
  if (!confirmed) return;

  await api.bulkDelete(ids);
  // audit log scritto lato BE dal middleware
  toast.success(`${ids.length} utenti eliminati`);
}
```

## Pattern: SSE per realtime (A8, A5b)

```tsx
function LiveEventLog() {
  const [events, setEvents] = useState<Event[]>([]);

  useSSE('/api/admin/events/live', {
    onEvent: (e) => setEvents(prev => [parseEvent(e), ...prev].slice(0, 200)),
    onError: () => toast.error('Stream disconnesso, riconnetto…'),
    reconnect: { interval: 2000, maxAttempts: Infinity },
  });

  return (
    <EventLog>
      {events.map(e => <EventRow key={e.id} event={e}/>)}
    </EventLog>
  );
}
```

## Pattern: KB upload con FSM

```tsx
type UploadFSM =
  | { state: 'idle' }
  | { state: 'idempotency-check', hash: string }
  | { state: 'idempotency-warn', match: KbDoc }
  | { state: 'uploading', progress: number }
  | { state: 'processing', jobId: string, currentStage: number }
  | { state: 'complete', doc: KbDoc }
  | { state: 'error', code: string, retryable: boolean };

function useKbUpload() {
  const [fsm, setFsm] = useState<UploadFSM>({ state: 'idle' });

  async function upload(file: File, settings: UploadSettings) {
    setFsm({ state: 'idempotency-check', hash: await sha256(file) });
    const dup = await api.kbIdempotencyCheck(hash);
    if (dup) {
      setFsm({ state: 'idempotency-warn', match: dup });
      return; // wait for user decision
    }
    setFsm({ state: 'uploading', progress: 0 });
    const { jobId } = await api.kbUpload(file, settings, p => {
      setFsm({ state: 'uploading', progress: p });
    });
    setFsm({ state: 'processing', jobId, currentStage: 0 });
    // subscribe SSE for stage progress…
  }

  return { fsm, upload, ... };
}
```

## Pattern: feature flags con dirty bar (A7)

```tsx
function ConfigPage() {
  const { data: serverFlags } = useFlags();
  const [dirty, setDirty] = useState<Record<string, FlagValue>>({});

  const hasDirty = Object.keys(dirty).length > 0;

  return (
    <>
      {flags.map(f => (
        <FlagRow
          flag={f}
          value={dirty[f.key] ?? serverFlags[f.key]}
          isDirty={dirty[f.key] !== undefined}
          onChange={v => setDirty(d => ({ ...d, [f.key]: v }))}
        />
      ))}

      {hasDirty && (
        <DirtyStateBar
          count={Object.keys(dirty).length}
          onRevert={() => setDirty({})}
          onPreview={() => openDiffModal(dirty)}
          onApply={async () => {
            await api.applyFlags(dirty); // audit-logged
            setDirty({});
            toast.success('Modifiche applicate in prod');
          }}
          applyRequiresStepUp={env === 'prd'}
        />
      )}
    </>
  );
}
```

## Performance: virtualization

`<AdminDataTable>` deve usare virtualization quando rows > 200:

```tsx
import { useVirtualizer } from '@tanstack/react-virtual';

function VirtualizedTableBody({ rows }: { rows: Row[] }) {
  const parentRef = useRef<HTMLDivElement>(null);
  const v = useVirtualizer({
    count: rows.length,
    getScrollElement: () => parentRef.current,
    estimateSize: () => 40,
    overscan: 10,
  });

  return (
    <div ref={parentRef} style={{ overflow: 'auto', height: 'calc(100vh - 240px)' }}>
      <div style={{ height: v.getTotalSize(), position: 'relative' }}>
        {v.getVirtualItems().map(vi => (
          <Row key={vi.key} style={{ position: 'absolute', top: 0, transform: `translateY(${vi.start}px)` }}>
            {/* row content */}
          </Row>
        ))}
      </div>
    </div>
  );
}
```

## Definition of Done per ogni pagina admin

- [ ] Render 1:1 con il mock (desktop 1440px)
- [ ] Mobile fallback funzionante (<880px)
- [ ] RBAC: route protetta + ogni button gates su role check
- [ ] Audit log: ogni mutazione produce entry verificabile in DB
- [ ] Confirm modal per azioni distruttive
- [ ] Typed-confirm per superadmin actions
- [ ] Step-up 2FA per impersonate/promote/broadcast
- [ ] Stati: default + loading skeleton + empty + error
- [ ] Pagination cursor-based (NO offset)
- [ ] Server-side filter+sort per liste >100 row
- [ ] Virtualization per >200 row visibili
- [ ] Keyboard shortcuts implementati (almeno ⌘K, j/k, Escape)
- [ ] Tests: unit per componenti, integration per flow critici, E2E per impersonate/delete/broadcast
- [ ] Performance: P95 render <200ms da cache, <800ms da fresh fetch
- [ ] data-comment-anchor preservati se nei mock ci sono
- [ ] PR review da almeno 2 reviewer (admin pages = alto rischio)
