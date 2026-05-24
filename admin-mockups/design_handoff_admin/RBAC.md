# RBAC — Role-Based Access Control per Admin Console

## 4 ruoli

| Ruolo | Permessi | Note |
|---|---|---|
| `user` | Solo proprio profilo + content pubblico | Default |
| `premium` | come user + feature premium | Subscription |
| `admin` | Tutto user + /admin/* (read+write content moderation, KB, monitor read) | Staff |
| `superadmin` | Tutto admin + impersonate + danger zone (delete user, force logout, broadcast, emergency shutdown) | Aaron + cofounder |

## Matrice permessi per route

| Route | guest | user | premium | admin | superadmin |
|---|---|---|---|---|---|
| `/`, `/faq`, `/how-it-works` | ✓ | ✓ | ✓ | ✓ | ✓ |
| `/login`, `/signup`, `/join` | ✓ | — | — | — | — |
| `/dashboard`, `/library`, `/games/*` | — | ✓ | ✓ | ✓ | ✓ |
| `/game-nights/*`, `/sessions/*` | — | ✓ | ✓ | ✓ | ✓ |
| `/librogame/*` | — | partial | ✓ | ✓ | ✓ |
| `/settings/*` (proprio) | — | ✓ | ✓ | ✓ | ✓ |
| `/admin/overview` | — | — | — | ✓ read | ✓ |
| `/admin/users` (lista, drill) | — | — | — | ✓ read | ✓ |
| `/admin/users` · cambia ruolo `user→premium` | — | — | — | ✓ | ✓ |
| `/admin/users` · cambia ruolo `→admin` | — | — | — | — | ✓ |
| `/admin/users` · cambia ruolo `→superadmin` | — | — | — | — | ✓ + 2FA step-up |
| `/admin/users` · impersonate | — | — | — | — | ✓ + 2FA + audit ogni req |
| `/admin/users` · suspend | — | — | — | ✓ | ✓ |
| `/admin/users` · delete account | — | — | — | — | ✓ + typed-confirm |
| `/admin/users` · force logout | — | — | — | ✓ | ✓ |
| `/admin/content` (mod queue) | — | — | — | ✓ | ✓ |
| `/admin/ai` | — | — | — | ✓ read | ✓ |
| `/admin/kb`, `/admin/kb/upload` | — | — | — | ✓ | ✓ |
| `/admin/kb` · delete doc | — | — | — | ✓ + confirm | ✓ |
| `/admin/catalog` | — | — | — | ✓ | ✓ |
| `/admin/config` (read flags) | — | — | — | ✓ | ✓ |
| `/admin/config` · cambia flag in `prd` | — | — | — | — | ✓ + audit |
| `/admin/monitor` | — | — | — | ✓ | ✓ |
| `/admin/notifications` (template) | — | — | — | ✓ | ✓ |
| `/admin/notifications` · broadcast | — | — | — | — | ✓ + typed-confirm |
| `/editor`, `/upload`, `/versions`, `/play-records` | — | — | — | ✓ | ✓ |
| `/pipeline-builder` | — | — | — | ✓ | ✓ |
| `/pipeline-builder` · publish to prod | — | — | — | — | ✓ |
| `/n8n` | — | — | — | ✓ read | ✓ |
| `/n8n` · rotate API key | — | — | — | — | ✓ |
| `/private-games` (proprio) | — | ✓ | ✓ | ✓ | ✓ |
| `/dev` | — | — | — | — | ✓ + env=dev |

Legenda: ✓ = permesso · — = negato · `read` = solo lettura · `+ X` = richiede anche X

## Step-up authentication (2FA challenge)

Operazioni che richiedono **step-up** anche per superadmin già loggato:

- Promote user to `superadmin`
- Start impersonate
- Generate/rotate API key
- Emergency shutdown
- Mass delete (>5 entity)
- Change config flag in `prd`
- Approve marketplace toolkit pubblicato

**Implementazione**:
- Frontend: prima di chiamare l'endpoint, controlla se `lastStepUpAt > Date.now() - 5min`. Se no, apri `<StepUpModal>` con TOTP input.
- Backend: il middleware accetta sia il JWT normale che un header `X-StepUp-Token` valido. Senza, restituisce `403 STEP_UP_REQUIRED`.

## Impersonate (la più pericolosa)

Flow:
1. SuperAdmin clicca "Impersonate" su user u-marco
2. `<StepUpModal>` 2FA challenge
3. Backend: `POST /api/admin/users/u-marco/impersonate-start`
   - verifica step-up token
   - genera JWT short-lived (15 min) con claim:
     ```json
     { "sub": "u-marco", "actor": "u-aaron", "exp": ..., "ttl": 900 }
     ```
   - audit_log: action='impersonate.start', target='u-marco'
4. Frontend: salva il token in sessionStorage (NON localStorage), mostra banner persistente in TUTTE le pagine:
   ```
   👁 IMPERSONATE · Stai navigando come Marco Rossi · termina »
   ```
5. Tutte le API call usano il JWT impersonate. Lato backend, ogni request audit-logga `actor=u-aaron, impersonated=u-marco`.
6. Dopo 15 min o click "termina", `POST /api/admin/impersonate-end` chiude la sessione. audit_log: `action='impersonate.end'`.

**Cosa NON è permesso durante impersonate**:
- Mutazioni che richiedono 2FA dell'utente target (cambio password, 2FA settings)
- Reset password proprio
- Cancellazione account
- Pagamenti / billing changes
- Accesso a /admin/*

## Audit log payload

Ogni entry:

```ts
type AuditLog = {
  id: string;                          // auditlog-${ulid}
  actor_id: string;                    // chi ha fatto l'azione
  impersonated_user_id?: string;       // se sotto impersonate
  action: string;                      // slug: "user.role.change", "kb.doc.delete"
  target_type: string;                 // "user" | "game" | "kb-doc" | ...
  target_id: string;
  before_json?: object;                // stato precedente (per mutate)
  after_json?: object;                 // stato risultante
  ip: string;
  user_agent: string;
  step_up_token_id?: string;           // se l'azione ha richiesto step-up
  created_at: string;                  // ISO timestamp
};
```

Action slugs canonici (estendere):

- `user.role.change`, `user.suspend`, `user.unsuspend`, `user.delete`, `user.force_logout`
- `user.impersonate.start`, `user.impersonate.end`
- `content.game.approve`, `content.game.reject`, `content.comment.delete`
- `kb.doc.upload`, `kb.doc.reindex`, `kb.doc.delete`
- `kb.chunk.delete`
- `config.flag.change`
- `notif.template.edit`, `notif.template.publish`, `notif.broadcast.send`
- `agent.deploy`, `agent.rollback`
- `apikey.create`, `apikey.rotate`, `apikey.revoke`
- `system.cache.flush`, `system.rate_limit.reset`, `system.emergency_shutdown`

## UI pattern audit-aware

In ogni drill-down (user, agent, kb-doc) mostra `<AuditLogTimeline>` con gli ultimi N eventi che hanno toccato quella entity:

```tsx
<AuditLogTimeline
  targetType="user"
  targetId={user.id}
  limit={20}
  showActor
  showImpersonate
/>
```

Backend: `GET /api/admin/audit-log?target_type=user&target_id=u-marco&limit=20`
