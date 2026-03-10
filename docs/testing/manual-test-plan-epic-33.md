# Manual Test Plan — Epic #33: Email, Notifiche & Calendario

**Epic**: #33
**Date**: 2026-03-10
**Tester**: Manual QA
**Prerequisites**: Docker running, Mailpit on `localhost:8025`, API on `localhost:8080`, Web on `localhost:3000`

---

## Phase 0: Dev Infrastructure (Mailpit)

### T0.1 — Mailpit Service Startup
| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | `docker compose --profile dev up -d mailpit` | Container `mailpit` running |
| 2 | Open `http://localhost:8025` | Mailpit Web UI loads, empty inbox |
| 3 | `curl -s http://localhost:8025/api/v1/messages \| jq '.total'` | Returns `0` |

### T0.2 — EmailService Uses Mailpit in Dev
| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | Start API: `cd apps/api/src/Api && dotnet run` | No SMTP connection errors in logs |
| 2 | Call `POST /api/v1/admin/emails/test` with `{"to":"test@example.com"}` | 200 OK |
| 3 | Check Mailpit UI (`localhost:8025`) | Test email visible with subject, sender `noreply@meepleai.local` |
| 4 | Click email in Mailpit | HTML body renders correctly |

### T0.3 — No Impact on Production Config
| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | Check `docker compose --profile prod config` | No mailpit service in prod profile |
| 2 | Check `appsettings.Production.json` | No Mailpit references |

---

## Phase 1: Quick Wins

### T1.1 — Correlation ID
| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | Upload a PDF via API | Processing completes |
| 2 | `GET /api/v1/notifications?limit=1` | Notification has `correlationId` (non-null GUID) |
| 3 | Check Mailpit for the email | Email headers/metadata contain same `correlationId` |
| 4 | Query DB: `SELECT correlation_id FROM email_queue_items ORDER BY created_at DESC LIMIT 1` | Same GUID as notification |

### T1.2 — Unsubscribe Link (GDPR)
| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | Trigger any email notification (e.g., PDF ready) | Email received in Mailpit |
| 2 | Inspect email HTML footer | Contains "Disattiva queste notifiche" link |
| 3 | Check email headers | `List-Unsubscribe` and `List-Unsubscribe-Post` headers present |
| 4 | Click unsubscribe link in browser | Confirmation page: "Hai disattivato le email per [tipo]" |
| 5 | `GET /api/v1/notifications/preferences` | The email preference for that type is now `false` |
| 6 | Trigger same notification type again | In-app notification created, but NO email in Mailpit |
| 7 | Click expired unsubscribe link (>30 days) | Error: "Link scaduto" |

### T1.3 — Admin Email Management (Real API)
| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | Login as admin, go to `/admin/monitor` → Email Management tab | Page loads without errors |
| 2 | Check KPI cards | Shows real numbers (Sent 24h, Pending, Failed, Dead Letter) |
| 3 | Send a test email from "Send Test" form | Email appears in Mailpit within 30s |
| 4 | Force an email to fail (invalid SMTP temp) | Email appears in "Failed" list after retries |
| 5 | Wait for email to reach Dead Letter (3 retries) | Appears in Dead Letter table |
| 6 | Click "Retry" on dead letter email | Status resets to Pending, disappears from Dead Letter |
| 7 | Click "Retry All" | All dead letter emails reset |
| 8 | Wait 30s | KPI cards auto-refresh with updated numbers |

### T1.4 — DeadLetterMonitorJob
| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | Insert 11+ dead letter emails in DB | Records exist with status `DeadLetter` |
| 2 | Wait for job execution (hourly, or trigger manually) | Admin receives alert notification |
| 3 | Check alert: severity Warning, message contains count | "⚠️ 11 email in dead letter queue" |
| 4 | Wait another hour | NO duplicate alert (throttled 6h) |

### T1.5 — Notification Cleanup Job
| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | Insert read notification with `created_at = 100 days ago` | Record in DB |
| 2 | Insert unread notification with `created_at = 100 days ago` | Record in DB |
| 3 | Insert read notification with `created_at = 30 days ago` | Record in DB |
| 4 | Trigger cleanup job (or wait for 3 AM UTC) | Job executes |
| 5 | Check DB | Old read notification DELETED |
| 6 | Check DB | Old unread notification STILL EXISTS |
| 7 | Check DB | Recent read notification STILL EXISTS |
| 8 | Check logs | "Cleaned up N notifications older than 90 days" |

---

## Phase 2: Game Night Backend

### T2.1 — Create Game Night
| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | `POST /api/v1/game-nights` with valid data | 201 Created, returns event ID |
| 2 | `GET /api/v1/game-nights/{id}` | Returns event with status `Draft` |
| 3 | Create with `scheduledAt` in the past | 400 Validation error |
| 4 | Create with empty title | 400 Validation error |
| 5 | Create with `maxPlayers: 1` | 400 "MaxPlayers must be >= 2" |

### T2.2 — Publish & Invitations
| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | Create game night with 3 invited users | Draft event created |
| 2 | `POST /api/v1/game-nights/{id}/publish` | Status changes to `Published` |
| 3 | Check Mailpit | 3 invitation emails sent (one per invitee) |
| 4 | Check invitation email content | Title, date, location, RSVP buttons (Accetta/Rifiuta/Forse) |
| 5 | `GET /api/v1/notifications` (as invitee) | In-app notification: "X ti ha invitato a una serata giochi" |
| 6 | Try to publish already Published event | 400 "Evento già pubblicato" |
| 7 | Try to publish as non-organizer | 403 Forbidden |

### T2.3 — RSVP Flow
| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | As invitee: `POST /api/v1/game-nights/{id}/rsvp` with `Accepted` | 200 OK |
| 2 | Check organizer notifications | In-app: "Luigi ha accettato il tuo invito" |
| 3 | `GET /api/v1/game-nights/{id}/rsvps` | Shows user with `Accepted` status |
| 4 | Change RSVP to `Declined` | Status updated |
| 5 | Try RSVP as non-invited user | 403 "Non sei invitato" |
| 6 | Fill MaxPlayers, try another Accept | 400 "Evento pieno" |

### T2.4 — One-Click RSVP from Email
| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | Open invitation email in Mailpit | Contains 3 RSVP action links |
| 2 | Copy "Accetta" link URL | Contains JWT token |
| 3 | Open URL in browser | RSVP registered, redirect to event detail page |
| 4 | Open same URL again | "Hai già risposto" or updates RSVP (idempotent) |
| 5 | Open URL after event date passed | Error: "Link scaduto" |

### T2.5 — Reminder 24h
| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | Create game night for ~24h from now | Published with invitees |
| 2 | Wait for `GameNightReminderJob` to run (every 15 min) | Job detects event in 24h window |
| 3 | Check Mailpit | Reminder emails to Accepted users |
| 4 | Check Mailpit for Pending users | Different email: "Rispondi all'invito!" |
| 5 | Check `Reminder24hSentAt` in DB | Set to current timestamp |
| 6 | Wait for next job run | NO duplicate reminders sent |

### T2.6 — Reminder 1h
| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | Create game night for ~1h from now | Published with Accepted invitees |
| 2 | Wait for job to detect 1h window | Job fires |
| 3 | Check Mailpit | NO new emails (1h reminder is push-only) |
| 4 | Check notification DB (or push logs) | Push notification attempted for Accepted users |

### T2.7 — Cancellation
| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | `POST /api/v1/game-nights/{id}/cancel` (as organizer) | Status → Cancelled |
| 2 | Check Mailpit | Cancellation emails to ALL invitees |
| 3 | Check in-app notifications | All invitees have cancellation notification |
| 4 | Try to RSVP to cancelled event | 400 "Evento annullato" |

### T2.8 — Notification Preferences Respected
| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | Disable `emailOnGameNightInvitation` for test user | Preference saved |
| 2 | Invite user to new game night | In-app notification created |
| 3 | Check Mailpit | NO invitation email for that user |
| 4 | Re-enable preference | Future invitations include email |

---

## Phase 3: Game Night Frontend

### T3.1 — Game Nights Page
| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | Navigate to `/game-nights` | Page loads with tabs |
| 2 | Check "Prossime" tab (no events) | Empty state: "Organizza la tua prima serata giochi!" |
| 3 | Create a game night (via API or form) | Event appears in "Organizzate da me" tab |
| 4 | As invitee, check "I miei inviti" tab | Invitation card visible with RSVP buttons |
| 5 | Check responsive layout (resize to mobile) | Cards stack vertically |

### T3.2 — Create Game Night Form
| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | Click "+" FAB or "Crea" button | Form opens at `/game-nights/new` |
| 2 | Fill required fields (title, date, invitees) | Form validates in real-time |
| 3 | Select past date | Validation error shown |
| 4 | Search and add games from library | Games appear in selection |
| 5 | Search and add players | Players appear in invitee list |
| 6 | Click "Salva come bozza" | Redirects to event detail, status: Draft |
| 7 | Click "Pubblica e invia inviti" | Redirects, status: Published, emails sent |

### T3.3 — RSVP Interaction
| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | Open event detail as invitee | 3 RSVP buttons visible |
| 2 | Click "Accetto" | Button highlights, count updates immediately (optimistic) |
| 3 | Network error during RSVP | Rollback to previous state, error toast |
| 4 | Refresh page | RSVP persisted correctly |
| 5 | Change to "Forse" | Previous selection clears, new one highlights |

### T3.4 — Notification Bell
| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | Receive game night invitation | Bell badge increments |
| 2 | Open notification dropdown | Game night notification with calendar icon |
| 3 | Click notification | Navigates to `/game-nights/{id}` |
| 4 | Go to `/settings/notifications` | "Serate Giochi" section with toggles |
| 5 | Toggle off email for game nights | Preference saved |

---

## Phase 4: Admin Template Editor

### T4.1 — Template List
| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | Navigate to `/admin/content/email-templates` | Page loads with template list |
| 2 | Filter by type: "PDF" | Only PDF-related templates shown |
| 3 | Filter by locale: "EN" | Only English templates shown |
| 4 | Check badge on each template | "Attivo" (green) or "Bozza" (grey) |

### T4.2 — Edit Template
| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | Click on a template | Editor opens with current content |
| 2 | Modify subject line | Change reflected in editor |
| 3 | Use WYSIWYG toolbar (bold, link, color) | Formatting applied |
| 4 | Click "Inserisci placeholder" → select `{{userName}}` | Placeholder inserted at cursor |
| 5 | Switch to "HTML" tab | Raw HTML visible and editable |
| 6 | Switch back to "Visuale" | Rendered view matches |
| 7 | Click "Salva bozza" | New version created (Version +1), not active |
| 8 | Click "Pubblica" | This version becomes active |

### T4.3 — Preview & Test Send
| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | Click "Anteprima" on a template | Preview modal opens |
| 2 | Default test data pre-populated | Placeholders replaced with sample values |
| 3 | Modify test data (change userName) | Preview updates |
| 4 | Toggle desktop/mobile preview | Width changes (responsive check) |
| 5 | Enter email address, click "Invia test" | Email sent to Mailpit |
| 6 | Check Mailpit | Email matches preview exactly |

### T4.4 — Template Loading from DB
| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | Edit "pdf_ready" IT template, publish new version | Active template updated |
| 2 | Trigger PDF processing completion | Email sent |
| 3 | Check Mailpit | Email uses NEW template content |
| 4 | Delete active template from DB | No active template |
| 5 | Trigger same notification | Email uses FALLBACK hardcoded template |

### T4.5 — i18n (IT/EN)
| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | Set user locale to "en" (`PUT /api/v1/users/me/locale`) | Preference saved |
| 2 | Trigger notification | Email sent in English |
| 3 | Set user locale to "it" | Preference saved |
| 4 | Trigger same notification | Email sent in Italian |
| 5 | Delete EN template for a type | Fallback to IT template |
| 6 | Check admin editor status | Shows "EN ⚠️ mancante" badge |
| 7 | Click "Copia da IT" | EN template created from IT content |

---

## Phase 5: n8n Integration (Optional)

### T5.1 — Webhook Endpoint
| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | `POST /api/v1/webhooks/n8n` with valid HMAC signature | 200 OK, action executed |
| 2 | `POST /api/v1/webhooks/n8n` with invalid signature | 401 Unauthorized |
| 3 | Send 101 requests in 1 minute | Rate limited after 100 |
| 4 | Send unknown action type | 400 "Unknown action" |

### T5.2 — API → n8n Trigger
| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | Publish a game night (with n8n enabled) | n8n receives webhook |
| 2 | Check n8n execution history (`localhost:5678`) | Workflow triggered with event data |
| 3 | Disable n8n feature flag | No webhook sent on publish |
| 4 | Break n8n connection | API continues normally (fire-and-forget), error logged |

### T5.3 — n8n Workflow
| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | Import template from `infra/n8n/templates/` | Workflow appears in n8n |
| 2 | Trigger with test data | Workflow executes all steps |
| 3 | Check timing nodes | Wait nodes configured for 3d/24h/1h/+2h |

### T5.4 — Admin n8n UI
| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | Navigate to `/admin/config/n8n` | Page loads |
| 2 | Click "Test Connection" | Shows Connected/Disconnected status |
| 3 | View webhook logs | Recent webhooks visible with timestamps |

---

## Cross-Phase Integration Tests

### TC.1 — Full Game Night Lifecycle (E2E)
| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | Create game night via frontend | Draft created |
| 2 | Add games and invite 2 players | Invitees added |
| 3 | Publish | Emails + in-app notifications sent |
| 4 | Player 1 accepts via email one-click | RSVP registered, organizer notified |
| 5 | Player 2 accepts via frontend UI | RSVP registered, organizer notified |
| 6 | Wait for 24h reminder | Reminder emails sent to both |
| 7 | Wait for 1h reminder | Push attempted (or logged) |
| 8 | Admin checks monitor page | All emails visible in stats |

### TC.2 — Notification Preferences E2E
| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | User disables ALL email notifications | Preferences saved |
| 2 | Upload PDF | Only in-app notification, NO email |
| 3 | Receive game night invite | Only in-app, NO email |
| 4 | Re-enable PDF emails only | Preference updated |
| 5 | Upload another PDF | In-app + email |
| 6 | Receive another game night invite | Only in-app (game night email still off) |

### TC.3 — Admin Operations E2E
| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | Admin edits "pdf_ready" template, publishes | New version active |
| 2 | User uploads PDF, completes | Email uses new template |
| 3 | Admin checks email stats | Sent count incremented |
| 4 | Admin sends test email | Visible in Mailpit |
| 5 | Simulate SMTP failure | Emails queue, retry, dead letter |
| 6 | Admin retries dead letter | Email resent successfully |

### TC.4 — GDPR Compliance E2E
| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | User receives any email | Unsubscribe link in footer |
| 2 | Click unsubscribe | Preference disabled, confirmation shown |
| 3 | Check audit log | Preference change recorded with source "email_unsubscribe" |
| 4 | Verify `List-Unsubscribe` header | Present on every email |
| 5 | User re-enables via settings | Preference restored |

---

## Test Environment Checklist

Before testing, verify:
- [ ] Docker containers running: PostgreSQL, Redis, Qdrant, Mailpit
- [ ] API running on `localhost:8080` with dev configuration
- [ ] Web running on `localhost:3000`
- [ ] Mailpit UI accessible on `localhost:8025`
- [ ] Admin user created and can login
- [ ] At least 3 test users created
- [ ] At least 2 games in user library (for game night selection)

## Test Summary Template

| Phase | Tests | Pass | Fail | Skip | Notes |
|-------|-------|------|------|------|-------|
| P0: Mailpit | 3 | | | | |
| P1: Quick Wins | 5 | | | | |
| P2: Game Night BE | 8 | | | | |
| P3: Game Night FE | 4 | | | | |
| P4: Template Editor | 5 | | | | |
| P5: n8n Integration | 4 | | | | |
| Cross-Phase | 4 | | | | |
| **Total** | **33** | | | | |
