# Specifica — Cancello d'accesso per un test di MeepleAI su Azure

**Data**: 2026-11-04 · **Stato**: proposta, **non implementata**. Nessuna risorsa Azure o Cloudflare è stata creata.
**Origine**: revisione a pannello (modalità critique, focus requisiti e architettura) sulla richiesta «proteggermi da bot e crawler che farebbero attivare i container; come admin decidere gli accessi, prima solo dal mio computer, poi con email di invito».
**Correlati**: [costo di un test su Azure](../research/2026-11-04-azure-test-deploy-cost-estimate.md) · [ricerca Azure/AI-200](../research/2026-10-08-azure-ai-200-provider-switch-research.md) · epic #4419 (profilo `azure-parity`).
**Convenzione**: nessun totale volatile in questo documento. Dove serve un numero c'è la fonte e la data; i limiti dei piani Cloudflare sono quelli letti il 2026-11-04.

---

## 1. Il problema

Con Container Apps a scale-to-zero ogni richiesta HTTP che raggiunge un'app a zero repliche la **accende** e la fa pagare. Un crawler o un bot che chiede `/` o `/api/v1/discover/recent-games` costa denaro anche se l'applicazione poi risponde «non autorizzato»: il rifiuto arriva dopo il risveglio di web, api e Postgres. Il cancello va quindi **prima** del risveglio.

## 2. Evidenza nel repo

- **Cloudflare Access è già in uso.** `infra/secrets/cf-access.secret.example` e il [runbook di staging](../operations/deploy-staging-runbook.md) descrivono un'application su `meepleai.app` con la policy «Service auth for staging smoke» (service token per la CI). Lo staging oggi non è deployato: lo stato dell'application nel dashboard Cloudflare **non è verificato**.
- **Web e API condividono l'hostname** (`/health` e `/api/v1/...` sullo stesso dominio nel runbook): un'application Access, un cookie.
- **Nessun rate limit globale, per scelta** (`apps/api/src/Api/Extensions/RateLimitingServiceExtensions.cs`, intestazione): le letture pubbliche non sono limitate, ed è ciò che un crawler tocca. Con scale-to-zero la scelta diventa un costo; il cancello a monte la copre senza toccare il codice.
- **Gli inviti esistono.** `SendInvitationCommand` manda l'email con il link `{FrontendBaseUrl}/accept-invite?token=…` (`apps/api/src/Api/Services/Email/EmailService.Auth.cs`). `RegistrationMode` si cambia a runtime dall'admin.

## 3. Decisione proposta: due cancelli, due responsabilità

```
bot / crawler ─┐
tu ────────────┼─ azure.meepleai.app ─ [Cloudflare Access] ─ tunnel ─ cloudflared (min 1) ─┬─ web       (interno, 0..2)
invitato ──────┘      cancello 1: chi può causare calcolo │                                ├─ api       (interno, 0..2)
                      i non autorizzati si fermano qui ───┘ (302 al login)                 └─ embedding (interno, 0..1)

cancello 2 (applicazione): chi ottiene un account e un ruolo = inviti e RegistrationMode, già esistenti
```

| Cancello | Dove | Decide | Costo in Azure di una richiesta respinta |
|---|---|---|---|
| 1 | Cloudflare Access, davanti al tunnel | chi può far partire calcolo | nessuno: Azure non la vede |
| 2 | applicazione | chi ha un account e quale ruolo | tutto il risveglio |

Il cancello 2 da solo non basta (§1). Il cancello 1 da solo non basta per il prodotto: non assegna ruoli.

## 4. Requisiti

| ID | Requisito | Verifica |
|---|---|---|
| AG-1 | `api`, `web` ed `embedding` hanno ingress limitato all'ambiente (`external: false`). `cloudflared` non ha ingress: apre solo connessioni in uscita. | Da fuori l'FQDN interno non risponde 200 e `Replicas` resta 0. |
| AG-2 | Un'application Access su un hostname dedicato (default `azure.meepleai.app`), default deny. Il tunnel instrada `/api/*` verso `api` e il resto verso `web`. | Una richiesta senza cookie riceve 302 verso `*.cloudflareaccess.com`. |
| AG-3 | **Fase 1.** Allow alla sola email dell'admin (codice OTP via email), Require paese IT. | Un'email fuori policy non riceve il codice (da verificare al primo test). |
| AG-4 | **Fase 2.** L'invitato deve essere nella lista email di Access **e** avere l'invito applicativo. Ordine fisso: prima Access, poi l'invito. | Un invitato nuovo apre il link, passa l'OTP e arriva a `/accept-invite`. |
| AG-5 | `FrontendBaseUrl` punta all'hostname di AG-2 nell'ambiente Azure. | L'email di invito contiene quell'host. |
| AG-6 | Se il cancello cede, il danno ha un tetto: `maxReplicas` 2 per `api`, 2 per `web`, 1 per `embedding`; budget Azure con alert a soglie sotto il mensile atteso. | Un burst di richieste non supera i tetti; l'alert scatta in un esercizio di prova. |
| AG-7 | Un controllo schedulato verifica che una richiesta anonima riceva 302 o 403 da Access; se riceve 200 scatta un allarme. | Costo in Azure zero: la richiesta si ferma al bordo. |
| AG-8 | Nessun bypass di path nel test, quindi niente webhook pubblici. Ogni bypass riapre il buco. | La policy contiene solo regole Allow (più il service token di AG-7). |
| AG-9 | Interruttore: fermare `cloudflared` (operazione Stop di Container Apps) oppure togliere la route dal dashboard Cloudflare. | Dopo il periodo di raffreddamento `Replicas` vale 0 su tutte le app. |

Il service token di AG-7 riusa il meccanismo di `cf-access.secret.example` (`CF_ACCESS_CLIENT_ID`, `CF_ACCESS_CLIENT_SECRET`). Se il controllo è un workflow `*-monitor.yml` vale [ADR-078](../../for-claude/architecture/adr/adr-078-auto-issue-noise-thresholds.md): blocco `concurrency` dichiarato.

## 5. Scenari

- **Bot anonimo.** *Given* un crawler che chiede `/api/v1/discover/recent-games`. *When* la richiesta arriva a Cloudflare. *Then* riceve 302, il tunnel non vede nulla e `Replicas` di `api` e `web` resta 0.
- **Admin, fase 1.** *Given* l'admin è nella policy e in Italia. *When* inserisce il codice OTP. *Then* entra, `web` e `api` si accendono (la prima richiesta è lenta) e tornano a zero dopo l'inattività.
- **Invitato, fase 2.** *Given* l'email dell'invitato è nella lista Access e l'admin ha mandato l'invito applicativo. *When* apre il link. *Then* passa l'OTP, arriva a `/accept-invite` e crea l'account.
- **Cancello mal configurato.** *Given* la policy è stata resa pubblica per errore. *When* il controllo di AG-7 vede 200. *Then* scatta l'allarme, e nel frattempo AG-6 limita il danno.

## 6. Fasi

1. **Fase 1 — solo admin.** AG-1, AG-2, AG-3, AG-6, AG-7, AG-9. L'applicazione resta in modalità invite-only.
2. **Fase 2 — inviti.** AG-4 e AG-5: l'admin aggiunge l'email alla lista Access, poi manda l'invito dal pannello. Aggiungere l'email dopo l'invito porta l'invitato davanti a un muro.

## 7. Rischi

1. **L'OTP lega la persona, non la macchina.** Chi legge la casella dell'admin entra. Per «solo questo PC» serve WARP con enrollment del dispositivo (il selettore di policy va verificato nel dashboard). L'IP non è affidabile: dinamico, forse CGNAT.
2. **Se Access è mal configurato il sistema si apre.** Lo coprono AG-6 e AG-7.
3. **Le sonde esterne tengono accese le repliche.** Nessun monitor o scrape sull'origin durante il test.
4. **Posti di Access.** Secondo la pagina prezzi di Cloudflare (2026-11-04) il piano Free ha un limite di 50 utenti e il pay-as-you-go costa $7 per utente al mese senza limite. Una risposta di un funzionario Cloudflare sul forum (non documentazione) dice che al 50° posto non se ne aggiungono altri senza l'upgrade, e che dopo l'upgrade si pagano **tutti** i posti, non solo gli extra. Su cosa succeda al 51° (blocco o addebito) non c'è una fonte ufficiale, e alcuni post sul forum chiedono se esista un tetto rigido a $0 perché il checkout del Free chiede il consenso a un possibile addebito oltre la soglia. Un posto è una persona che si autentica in Access, non un account MeepleAI, e le email le aggiunge l'admin a mano: il limite non si supera per distrazione. Prima di superare 50 persone, scrivere al supporto Cloudflare.
5. **L'invitato fa due passaggi** (OTP e invito applicativo). Accettato: prova il comportamento invite-only reale. L'alternativa è la registrazione aperta dietro Access, con un solo invito, ma il test non proverebbe più il prodotto.

## 8. Decisioni aperte (con default)

| Decisione | Default |
|---|---|
| Hostname | `azure.meepleai.app`, con un'application Access nuova e separata |
| Lista inviti | gestita a mano nel dashboard in questa fase |
| Automazione: l'handler di invito aggiunge l'email ad Access via API Cloudflare | lavoro successivo: serve un token con permesso di modifica sulle liste e un `HttpClient` nominato con SSRF pin |

## 9. Non verificato

- Lo stato dell'application Access esistente nel dashboard Cloudflare.
- Se un'email fuori policy riceve davvero un codice OTP.
- Il selettore di policy per WARP.
- La risposta dell'FQDN interno visto da fuori: le fonti Q&A di Microsoft sono in parziale conflitto.
- Se un tentativo di accesso respinto crea un posto in Access.

## 10. Fuori perimetro

- Il firewall di Postgres e l'accesso privato al database: non fanno accendere container, ma vanno decisi nel piano di deploy.
- Rate limit applicativo per gli utenti già invitati.
- L'automazione dell'aggiunta delle email ad Access.

## Fonti

- [Prezzi Cloudflare Zero Trust](https://www.cloudflare.com/plans/sase-zero-trust.md)
- [Configurare l'ingress in Container Apps](https://learn.microsoft.com/en-us/azure/container-apps/ingress-how-to)
- [Scaling in Container Apps](https://learn.microsoft.com/en-us/azure/container-apps/scale-app)
- Forum Cloudflare Community, citati come testimonianza non ufficiale (non apribili in automatico: protetti da verifica anti-bot): `community.cloudflare.com/t/zero-trust-free-hard-0-cap-or-blocked-at-50-seats/976840`, `community.cloudflare.com/t/what-happens-when-you-go-over-the-free-plan/806187`
