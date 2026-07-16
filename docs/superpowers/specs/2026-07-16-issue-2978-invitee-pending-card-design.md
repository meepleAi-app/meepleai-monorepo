# Issue #2978 — Card pending lato invitato (invariante #17)

**Data**: 2026-07-16 · **Branch**: `feature/issue-2978-invitee-pending-card` · **Target**: `main-dev`

## Contesto

Invariante #17 (domain model 2026-06-04): l'invitato deve vedere la GameNight nella
propria dashboard "Prossimi" e nella list `/game-nights` come **card pending**
(semitrasparente + badge "Da confermare" + RSVP inline) finché non fa RSVP. Post-conferma
→ card normale.

**Gap reale**: il `GameNightDto` non porta lo stato RSVP del viewer, quindi né la dashboard
né la list possono distinguere l'invitato-pending. Le superfici mostrano solo contatori
aggregati host-side.

## Decisioni (confermate con l'owner 2026-07-16)

1. **3 bottoni RSVP**: Conferma (`Accepted`) / Forse (`Maybe`) / Declina (`Declined`) —
   coerenti con `GameNightRsvpActionBar` della detail.
2. **List wira anche non-pending**: tutte le card *invited* ottengono RSVP funzionante con
   lo stato corrente evidenziato; il trattamento visivo pending si aggiunge solo quando
   `myRsvpStatus === 'Pending'`.
3. **Dashboard focalizzata su #17**: RSVP inline + trattamento pending **solo** per
   `Pending`. Le altre card upcoming restano preview cliccabili (la dashboard "Prossimi"
   include anche serate di altri, dove il viewer non è invitato).

## Backend (`GameManagement`, aggregate `GameNightEvent`)

Pattern di riferimento: `VotedByMe` (`GameNightVoteTallyDto`) / `IsViewerOrganizer`
(`GameNightSummaryDto`) — query porta `CallerUserId`, endpoint estrae
`httpContext.User.GetUserId()`, handler passa al mapper.

1. `GameNightDto` + `RsvpStatus? MyRsvpStatus` (nullable — il viewer può non essere invitato).
2. `GameNightMapperHelper.MapToDto(gn, organizerName, Guid viewerUserId)` →
   `MyRsvpStatus = gn.GetRsvp(viewerUserId)?.Status` (metodo dominio già esistente; gli RSVP
   sono già eager-loaded via `.Include(e => e.Rsvps)` → 0 query extra).
3. `GetMyGameNightsQueryHandler` passa `query.UserId` (già disponibile).
4. `GetUpcomingGameNightsQuery` + `GetCompletedGameNightsQuery` → `+ Guid CallerUserId`;
   `HandleGetUpcomingGameNights` / `HandleGetCompletedGameNights` iniettano `HttpContext` +
   `User.GetUserId()` (unica modifica non banale: oggi non hanno `HttpContext`).
5. L'organizer non ha un RSVP → `MyRsvpStatus = null`, quindi il trattamento pending non lo
   tocca mai.

**Non tocco** il filtro di `GetUpcomingAsync` (globale, pre-esistente): fuori scope #2978.
`myRsvpStatus` è puramente additivo/non-breaking.

## Frontend

### Zod
- `GameNightDtoSchema` + `myRsvpStatus: RsvpStatusSchema.nullable()` (nullable → i fixture
  esistenti continuano a parsare, zero rotture).

### Dashboard (`ProssimiSection` + `DashboardClient`)
- `ProssimiGameNightCard` + `myRsvpStatus`.
- `myRsvpStatus === 'Pending'` → card `opacity-70` + badge "Da confermare" + action bar
  Conferma/Forse/Declina inline. **De-nesting**: oggi la card è un unico `<button>` → diventa
  `<article>` con blocco testo cliccabile (apre drawer) + bottoni RSVP separati (no button
  annidati).
- Nuova prop `onRsvp?(id, response)` (resta props-driven puro); `DashboardClient` wira
  `useRsvpGameNight` + mappa `myRsvpStatus` dal DTO.

### List (`GameNightListCard` + `_content.tsx`)
- `GameNightVM` + `myRsvpStatus`; `toGameNightVM` lo mappa.
- Card *invited* (role !== organizer): CTA Conferma/Forse/Declina funzionanti, con lo stato
  corrente evidenziato. `myRsvpStatus === 'Pending'` → + `opacity` + badge "Da confermare".
- Wiring `onAction` → `useRsvpGameNight` in `_content.tsx` (`ListView` + `DayDetailDrawer`).
  La struttura è già `<article>` → nessun de-nesting.
- `GameNightListCardAction` esteso con `'decline'`; mapping azione → `RsvpStatus`.

### i18n
- `gameNightsIndex.list` + `pendingBadge` / `cta.confirm` / `cta.decline` (it + en).
- Dashboard usa stringhe inline IT (come già fa oggi).

## Test

- **BE (nuovi)**: `GetUpcomingGameNightsQueryHandlerTests` / `GetMyGameNightsQueryHandlerTests`
  + mapper — `MyRsvpStatus` valorizzato per invitato pending/accepted, `null` per
  organizer/non-invitato.
- **FE**: aggiorno `ProssimiSection.test`, `GameNightListCard.test`, `view-model.test`,
  `_content.test`, `DashboardClient.test` + casi pending/non-pending RSVP.

## Out of scope

- Filtro scoping globale di `GetUpcomingAsync` (pre-esistente).
- "Forse" resta anche nella detail page (invariato).

## Rischi

- Modifica firma `MapToDto` → aggiorna tutti i call-site (3 handler lista + eventuali altri).
- Endpoint upcoming/completed acquisiscono `HttpContext`: verificare che restino
  `RequireAuthenticatedUser()` (il viewer è sempre autenticato).
- Test list esistenti "invited → accept + maybe" cambiano semantica (ora 3 bottoni funzionanti).
