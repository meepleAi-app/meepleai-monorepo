# Quick Start — Admin Console Setup

> Da fare PRIMA di lanciare qualsiasi prompt di feature admin.

## 1. Audit dello stato corrente

```
Verifica lo stato corrente dell'admin nel codebase:

1. Esiste una sezione /admin/* già? Se sì, lista le pagine.
2. Modello RBAC: come sono gestiti i ruoli? Quali ruoli esistono?
3. Esiste un audit_log o equivalente? Schema della tabella?
4. Endpoint /api/admin/* esistenti? Lista.
5. Permission middleware/decorator? Path del file.
6. Esiste una "danger zone" pattern (Delete user, etc)? Path.
7. SSE/WebSocket admin (es. live event log)? Implementati?

Scrivi report in design_handoff_admin/ADMIN_AUDIT.md.
Non modificare niente.
```

## 2. Setup RBAC

```
Imposta il modello RBAC per la console admin:

1. Verifica/aggiungi al modello User:
   role: 'user' | 'premium' | 'admin' | 'superadmin'
   2faEnabled: boolean
   2faMethod: 'totp' | 'sms' | null
   impersonatedBy: userId | null  (transient JWT claim, non in DB)

2. Crea middleware/guard:
   - requireAuth() → 401 se non loggato
   - requireRole('admin') → 403 se ruolo insufficiente
   - requireRole('superadmin') → 403 + audit log
   - requireStepUp() → forza 2FA challenge se >5 min dall'ultimo check

3. Crea audit_log table:
   id, actor_id, impersonated_user_id (nullable),
   action (string slug), target_type, target_id,
   before_json, after_json, ip, user_agent, created_at

4. Helper: auditLog(action, target, before, after) chiamato in ogni
   mutazione admin. NO eccezioni.

Mostra schema migration prima di applicare.
```

## 3. Importa il design system admin

```
Copia in src/styles/:
- design/admin/admin-base.css → src/styles/admin-base.css
- design/admin/admin-nav.js → src/components/admin/admin-nav.tsx
  (convertilo a React component <AdminNav>)

In src/layouts/AdminLayout.tsx (o equivalente):
- Importa admin-base.css solo per route /admin/*
- Default theme: dark
- Render <AdminSidebar> + <main>{children}</main>

Conferma: bundle dimension admin-only resta isolato dal bundle utente.
```

## 4. Crea i componenti base

In ordine, crea questi componenti riusabili che saranno usati da TUTTE le pagine admin:

```
Sprint 1 admin base:
1. <AdminShell> con sidebar 240px + topbar 56px sticky
2. <AdminSidebar> con groups + items + active state da useRouter
3. <AdminTopbar> con title/crumbs/search/actions
4. <AdminPanel> + <AdminPanel.Head> + <AdminPanel.Body>
5. <AdminDataTable> con: sort, multi-select, bulk-actions, pagination cursor,
   sticky header, row hover, selected state, density compact/normal
6. <AdminKPICard> entity-tinted con sparkline opzionale
7. <StatusChip> + <RoleChip> + <EnvPill>
8. <BulkActionsBar> sticky-top quando >0 selezionati
9. <ConfirmModal> con typed-confirm opzionale per superadmin actions
10. <AuditLogTimeline> per drill-down user/agent

Crea Storybook story per ognuno. Test base: render + a11y.
```

## 5. Crea il `/admin/overview` come pilot

Stesso prompt che la skill base + adattamento al codebase. Conferma:
- Route protetta (admin/superadmin only)
- Dati da `GET /api/admin/overview` (mockato se BE non c'è)
- 4 KPI + activity feed + alerts + 4 charts + sidebar quick actions
- Mobile fallback funzionante
- Build size admin-only resta tree-shaken dal prod utente

## 6. Audit log middleware

```
Verifica che TUTTE le route /api/admin/* logghino in audit_log.

Test: simula 3 azioni e verifica entry in DB:
1. PATCH /api/admin/users/u-marco { role: 'admin' }
2. POST /api/admin/notifications/templates/tpl-x/test-send
3. DELETE /api/admin/kb/docs/d-old

Se manca, aggiungi a livello di middleware (NON nei singoli handler — è
un trap per dimenticarsi). Pattern: wrap il router /admin/* in un
auditLoggingMiddleware che intercetta tutti i POST/PATCH/DELETE.
```

## Checklist setup completato

- [ ] design_handoff_admin/ADMIN_AUDIT.md generato
- [ ] RBAC esteso con admin/superadmin
- [ ] audit_log table creata + middleware applicato
- [ ] admin-base.css + admin-nav importati
- [ ] 10 componenti base in `src/components/admin/v2/`
- [ ] Storybook stories per ognuno
- [ ] /admin/overview pilot funzionante
- [ ] Mobile fallback per /admin/*
- [ ] Build size verificata: admin tree-shaken da utente bundle
- [ ] 3 azioni di test audit-logged correttamente

Solo dopo questa checklist, parti con gli altri 17 mockup.
