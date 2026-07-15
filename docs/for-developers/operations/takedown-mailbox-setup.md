# Runbook — Casella `takedown@meepleai.app`

**Owner:** MeepleAI Trust & Legal + DevOps
**Parent ADR:** [ADR-051 — Mechanic Extractor IP Policy](../../for-claude/architecture/adr/adr-051-mechanic-extractor-ip-policy.md)
**Policy correlata:** [Takedown Policy](../../legal/takedown-policy.md)
**Epic:** Mechanic Extractor — issue [#2954](https://github.com/meepleAi-app/meepleai-monorepo/issues/2954)
**Status:** Da eseguire (prerequisito go-live card pubbliche)

---

## 1. Scopo e rischio

Questo runbook configura e verifica la casella **`takedown@meepleai.app`**, canale ufficiale per le
richieste di rimozione (takedown) delle *comprehension card* pubblicate dal Mechanic Extractor.

**Perché è obbligatoria.** Sia la pagina pubblica **`/legal/takedown`**
(`apps/web/src/app/(public)/legal/takedown/page.tsx`) sia il documento
[`docs/legal/takedown-policy.md`](../../legal/takedown-policy.md) pubblicizzano
`takedown@meepleai.app` come recapito diretto e come destinatario del form (`mailto:` precompilato).
ADR-051 (§ "Follow-up obbligatori") elenca esplicitamente la pubblicazione del template takedown su
`takedown@meepleai.app` tra i requisiti prima del lancio delle card pubbliche.

**Rischio se la casella è morta.** Il form e la policy invitano editori e detentori di diritti a
scrivere a un indirizzo che, se non instradato, **fa bounce o scarta in silenzio** le richieste. Una
richiesta di takedown non ricevuta = card potenzialmente lesiva che resta online oltre l'SLA della
policy (presa in carico ≤ 3 gg lav., risoluzione ≤ 10 gg lav.) → **esposizione legale diretta** e
smentita del posizionamento "trust chain visibile" di ADR-051. La policy prevede rimozione **a
effetto immediato** (flag `is_suppressed`), ma la rimozione può partire solo se la notifica arriva.

> **Nota di scope.** Il setup di questo alias era esplicitamente marcato *fuori scope* di #529
> (policy + pagina) e rinviato a task infrastrutturale separato — questo runbook, tracciato da #2954.

---

## 2. Setup via Cloudflare Email Routing

Il dominio `meepleai.app` usa **Cloudflare Email Routing** (già attivo). Fatti verificati:

- MX: `route1.mx.cloudflare.net` (pref 48), `route2.mx.cloudflare.net` (82), `route3.mx.cloudflare.net` (45)
- SPF: `v=spf1 include:_spf.mx.cloudflare.net ~all`
- Dominio dietro proxy Cloudflare (staging Hetzner `204.168.135.69`)

Poiché Email Routing è già inizializzato, resta solo da creare la **custom address** `takedown@` e
puntarla a una inbox reale monitorata.

> **Decisione richiesta all'owner:** scegliere la **destinazione** prima di iniziare. Opzioni tipiche:
> un **Google Group** (es. `trust-legal@…`) o una **casella condivisa** presidiata da Trust & Legal.
> Preferire un indirizzo di gruppo/condiviso a una casella personale (continuità, no single point of
> failure). L'indirizzo scelto sarà chiamato `<DEST_INBOX>` nei passi seguenti.

### 2A. Via dashboard Cloudflare (percorso primario)

1. Login su <https://dash.cloudflare.com> → seleziona il dominio **`meepleai.app`**.
2. Menu laterale → **Email** → **Email Routing**.
3. Tab **Destination addresses** → **Add destination address** → inserisci `<DEST_INBOX>`.
   - Cloudflare invia una email di conferma a `<DEST_INBOX>`. **Apri quella email e clicca il link
     di verifica.** Finché lo stato non è **Verified**, l'inoltro non funziona.
4. Tab **Routing rules** → sezione **Custom addresses** → **Create address**.
   - **Custom address:** `takedown` (dominio `@meepleai.app` preselezionato).
   - **Action:** `Send to an email` → seleziona `<DEST_INBOX>` (deve risultare *Verified*).
   - **Save.**
5. Verifica che **Email Routing** sia globalmente **Enabled** (toggle in alto) e che i record MX
   mostrati coincidano con quelli attesi (`route1/2/3.mx.cloudflare.net`). Non modificare gli MX.

### 2B. Via Cloudflare API (alternativa / IaC)

Serve un **API token** con permesso **Zone → Email Routing Rules → Edit** sulla zona `meepleai.app`.
Recupera lo `zone_id` dalla dashboard (Overview → API, colonna destra) o via API.

```bash
# 0. Variabili
export CF_API_TOKEN="<token con permesso Email Routing Rules:Edit>"
export ZONE_ID="<zone_id di meepleai.app>"
export DEST_INBOX="trust-legal@example.com"   # deve essere già Verified

# 1. (facoltativo) verifica lo zone_id per nome
curl -s -X GET "https://api.cloudflare.com/client/v4/zones?name=meepleai.app" \
  -H "Authorization: Bearer ${CF_API_TOKEN}" | jq '.result[].id'

# 2. Crea la routing rule: matcher su "to" = takedown@meepleai.app, action = forward
curl -s -X POST \
  "https://api.cloudflare.com/client/v4/zones/${ZONE_ID}/email/routing/rules" \
  -H "Authorization: Bearer ${CF_API_TOKEN}" \
  -H "Content-Type: application/json" \
  --data @- <<JSON | jq '.success, .result.tag'
{
  "name": "takedown-forward",
  "enabled": true,
  "priority": 0,
  "matchers": [
    { "type": "literal", "field": "to", "value": "takedown@meepleai.app" }
  ],
  "actions": [
    { "type": "forward", "value": ["${DEST_INBOX}"] }
  ]
}
JSON
```

Note:
- La destinazione in `actions[].value` **deve** essere un destination address già **Verified**
  (l'API non lo verifica al posto tuo). Se non lo è, crealo prima con
  `POST /zones/{zone_id}/email/routing/addresses` e conferma dal link email.
- `"type": "literal"` + `field: "to"` fa match esatto sull'indirizzo. `success: true` e un `tag`
  non nullo confermano la creazione.
- Per rileggere le regole esistenti: `GET /zones/{zone_id}/email/routing/rules`.

---

## 3. Verifica funzionale

Obiettivo: dimostrare che un'email inviata **da un dominio esterno** a `takedown@meepleai.app` arriva
nella `<DEST_INBOX>` **senza bounce**.

1. **Invio manuale (metodo consigliato).** Da un account esterno *non* `@meepleai.app` (es. Gmail
   personale), invia un'email a `takedown@meepleai.app` con oggetto `TEST takedown routing #2954`.
2. **Conferma ricezione.** Entro pochi minuti la mail deve comparire in `<DEST_INBOX>`. Verifica anche
   che l'header `Delivered-To` / `Received` mostri il passaggio via `*.mx.cloudflare.net`.
3. **Nessun bounce.** Controlla che l'account mittente **non** riceva un Delivery Status Notification
   (bounce/`550`). Un bounce = destinazione non verificata o regola assente.
4. **Comando di test opzionale (swaks).** Se hai un relay SMTP autorizzato per un dominio di test:
   ```bash
   swaks --to takedown@meepleai.app \
         --from qa@<tuo-dominio-di-test> \
         --server <smtp-relay-autorizzato> \
         --header "Subject: TEST takedown routing #2954" \
         --body "Runbook takedown-mailbox-setup verification."
   ```
   > Non usare Gmail SMTP con FROM = account autenticato verso un destinatario esterno: Gmail scarta
   > in silenzio (vedi [email-provider-setup.md](./email-provider-setup.md) § "Why we left Gmail SMTP").
   > L'invio manuale dal client webmail è più affidabile per questo test.
5. **Evidenza.** Cattura uno **screenshot** della mail ricevuta in `<DEST_INBOX>` (con oggetto e
   header visibili) e allegalo all'issue **#2954** come prova del routing funzionante.

---

## 4. DMARC (raccomandazione)

**Stato attuale:** il dominio pubblica **SPF** ma **non** ha un record **DMARC** (`_dmarc.meepleai.app`
assente al momento della verifica). Senza DMARC un ricevente non ha una policy dichiarata per gestire
mail che falliscono SPF/DKIM: aumenta il rischio di **spoofing** di `@meepleai.app` e peggiora la
**deliverability** (alcuni provider penalizzano i domini senza DMARC).

**Azione consigliata** — aggiungere un record TXT in Cloudflare DNS (**DNS only**, cloud grigio):

| Tipo | Nome | Valore |
|------|------|--------|
| `TXT` | `_dmarc` | `v=DMARC1; p=quarantine; rua=mailto:dmarc-reports@meepleai.app; fo=1` |

Via API:

```bash
curl -s -X POST "https://api.cloudflare.com/client/v4/zones/${ZONE_ID}/dns_records" \
  -H "Authorization: Bearer ${CF_API_TOKEN}" \
  -H "Content-Type: application/json" \
  --data '{
    "type": "TXT",
    "name": "_dmarc",
    "content": "v=DMARC1; p=quarantine; rua=mailto:dmarc-reports@meepleai.app; fo=1",
    "ttl": 3600
  }' | jq '.success'
```

Note:
- Parti da `p=quarantine` (o `p=none` in warm-up per raccogliere gli aggregate report senza impatto),
  poi valuta `p=reject` una volta certi che tutti i mittenti legittimi passino SPF/DKIM.
- `rua=` deve puntare a una casella realmente presidiata (o a un tool di report aggregation).
- Il record DMARC è **complementare**, non blocca il routing di `takedown@`: la verifica del § 3
  resta valida anche prima di aggiungerlo. È però consigliato prima del go-live pubblico.

---

## 5. Monitoraggio, SLA e collegamento all'azione tecnica

**Chi controlla.** La `<DEST_INBOX>` è presidiata da **Trust & Legal** (destinazione di gruppo/
condivisa, non personale — vedi § 2).

**Frequenza.** Controllo almeno **una volta al giorno lavorativo**, coerente con l'SLA di presa in
carico della policy (**≤ 3 giorni lavorativi**). Casi di rischio evidente → **soppressione
precauzionale immediata**, valutazione a seguire (Takedown Policy § 6).

**SLA della policy** (da [takedown-policy.md](../../legal/takedown-policy.md) § 6):

| Fase | Target |
|------|--------|
| Presa in carico (acknowledge al richiedente) | ≤ 3 giorni lavorativi dalla ricezione |
| Risoluzione (rimozione o risposta motivata) | ≤ 10 giorni lavorativi dalla presa in carico |
| Rischio evidente | Soppressione precauzionale immediata |

**Collegamento all'azione tecnica di takedown.** Ricevuta e valutata una richiesta fondata, l'admin
autorizzato esegue la soppressione della card via endpoint admin:

```
POST /api/v1/admin/mechanic-analyses/{id}/suppress
```

Body (sessione admin richiesta; l'actor id è preso dalla sessione, mai dal body):

```json
{
  "reason": "Richiesta editore <Nome> ricevuta via takedown@meepleai.app il <data> — <riferimento>",
  "requestSource": "Email",
  "requestedAt": "2026-07-15T09:00:00Z"
}
```

- `reason`: 20–500 caratteri, giustificazione legalmente significativa.
- `requestSource`: `Email` | `Legal` | `Other` (usa `Email` per le richieste arrivate a questa casella).
- Effetto: imposta `is_suppressed=true` sull'aggregato `MechanicAnalysis`. Il filtro globale EF
  (`HasQueryFilter(!IsSuppressed)`) fa sì che `GET /api/v1/games/{gameId}/card` restituisca **404** e
  la pagina renda `notFound()` — **rimozione a effetto immediato senza deploy** (ADR-051 T5).
- La soppressione è ortogonale al lifecycle: ammessa da qualsiasi stato, incluso `Published`. Ritorna
  **409** se l'aggregato è già soppresso. Ogni soppressione è tracciata (audit log + colonne
  `suppressed_*`).

Riferimento implementazione: `apps/api/src/Api/Routing/AdminMechanicAnalysesEndpoints.cs`
(`AdminSuppressMechanicAnalysis`).

---

## 6. Checklist di chiusura (#2954)

- [ ] **Destinazione scelta e verificata** — `<DEST_INBOX>` (gruppo/condivisa) in stato *Verified* su Cloudflare.
- [ ] **Route creata** — custom address `takedown@meepleai.app` → forward a `<DEST_INBOX>` (dashboard § 2A o API § 2B).
- [ ] **Test passato** — email esterna → arrivata in `<DEST_INBOX>`, nessun bounce (§ 3).
- [ ] **Screenshot allegato a #2954** — prova del routing funzionante (§ 3.5).
- [ ] **(Opz. consigliato) DMARC** — record TXT `_dmarc.meepleai.app` aggiunto (§ 4).
- [ ] **Presidio confermato** — owner Trust & Legal e cadenza di controllo assegnati (§ 5).
- [ ] **Issue #2954 aggiornata/chiusa** con esito e link a questo runbook.

---

**Last Updated:** 2026-07-15 | **License:** Proprietary
