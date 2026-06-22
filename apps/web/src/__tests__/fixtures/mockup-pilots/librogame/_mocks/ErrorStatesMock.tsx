/**
 * Template K presentational mock for librogame-runthrough-error-states states.
 *
 * No real standalone component exists for the error banner system as of DS-17
 * Phase D-2 (2026-06-22). Error states are distributed across hooks and error
 * boundaries inside the librogame play shell and are not exposed as a single
 * component. This mock renders each error banner faithfully from the mockup
 * spec, driven by a single `variant` prop.
 *
 * Ten variants map to the ten mockup sections (Stati A–J):
 *   stream-timeout      — SSE stream stalled > 30s inline in chat (warn-colored)
 *   ocr-fail            — OCR confidence < 0.3, photo illegible (danger-colored)
 *   llm-503             — Provider down, 3-retry exponential backoff + offline fallback
 *   segmentation-fail   — OCR ok (0.88) but no § pattern found, manual input
 *   ocr-low-conf        — Confidence 0.64, highlighted uncertain regions (warn-colored)
 *   photo-blur          — Client-side pre-OCR reject, focus score 0.28 (warn-colored)
 *   translation-timeout — AI translate request aborted > 20s (warn-colored)
 *   source-lang-unknown — LLM lang confidence 0.38 < 0.5, lang confirm dialog
 *   network-mid-ocr     — Connection lost mid-upload, saved to IndexedDB (danger-colored)
 *   quota-exhausted     — Daily token quota exhausted mid-stream, bridge to credits
 *
 * Uses canonical design tokens only (no hardcoded neutral utilities; the
 * `local/no-hardcoded-color-utility` ESLint rule applies).
 *
 * BGG guard: no BGG refs in this file (#2123). Internal catalog framing only
 * (game title "Eldoria", campaign "Sera con i ragazzi").
 *
 * @mockup admin-mockups/design_files/librogame-runthrough-error-states.html
 *   - Stato A · stream-timeout       (chat agente N2: nessun token > 30s)
 *   - Stato B · ocr-fail             (foto illeggibile, conf 0.27)
 *   - Stato C · llm-503              (provider down, 3 retry, fallback offline)
 *   - Stato D · segmentation-fail    (OCR ok 0.88, nessun § rilevato)
 *   - Stato E · ocr-low-conf         (confidence 0.64, regioni dubbie highlight)
 *   - Stato F · photo-blur           (client-side pre-OCR blur reject, focus 0.28)
 *   - Stato G · translation-timeout  (AI translate abort > 20s)
 *   - Stato H · source-lang-unknown  (lang confidence < 0.5, 5-option confirm)
 *   - Stato I · network-mid-ocr      (loss mid-upload, IndexedDB queue, retry on-reconnect)
 *   - Stato J · quota-exhausted      (token quota 100%, bridge a quota-credits)
 *
 * Refs: DS-17 Phase D-2, umbrella #2063, sub-issue #2174.
 */

import type { ReactElement, ReactNode } from 'react';

// ---------------------------------------------------------------------------
// Variant union type
// ---------------------------------------------------------------------------

export type ErrorStatesMockVariant =
  | 'stream-timeout'
  | 'ocr-fail'
  | 'llm-503'
  | 'segmentation-fail'
  | 'ocr-low-conf'
  | 'photo-blur'
  | 'translation-timeout'
  | 'source-lang-unknown'
  | 'network-mid-ocr'
  | 'quota-exhausted';

// ---------------------------------------------------------------------------
// Props
// ---------------------------------------------------------------------------

export interface ErrorStatesMockProps {
  /** Which error variant to render. Drives the banner content statically. */
  readonly variant?: ErrorStatesMockVariant;
  /** Session name shown in the top bar. */
  readonly sessionName?: string;
}

// ---------------------------------------------------------------------------
// Shared sub-components
// ---------------------------------------------------------------------------

/** Mobile-style status bar */
function PhoneStatusBar({ time, signal }: { time: string; signal: string }): ReactElement {
  return (
    <div className="flex shrink-0 items-center justify-between px-5 pt-1.5 font-quicksand text-xs font-bold text-muted-foreground">
      <span>{time}</span>
      <span>{signal}</span>
    </div>
  );
}

/** App top bar */
function AppBar({ title, rightIcon = '⋯' }: { title: string; rightIcon?: string }): ReactElement {
  return (
    <div className="flex shrink-0 items-center gap-3 border-b border-border px-4 py-2">
      <button
        type="button"
        aria-label="Indietro"
        className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-muted text-lg"
      >
        ←
      </button>
      <span className="flex-1 font-mono text-xs text-muted-foreground">
        <strong className="text-foreground">Sera con i ragazzi</strong>
        {' · '}
        {title}
      </span>
      <button
        type="button"
        className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-muted text-sm"
      >
        {rightIcon}
      </button>
    </div>
  );
}

type BannerVariant = 'danger' | 'warn';

/** Shared error banner wrapper */
function ErrBanner({
  type,
  badge,
  title,
  children,
}: {
  type: BannerVariant;
  badge: string;
  title: string;
  children: ReactNode;
}): ReactElement {
  const isDanger = type === 'danger';
  return (
    <div
      role="alert"
      className="mx-4 my-0 rounded-xl p-4"
      style={{
        background: isDanger ? 'hsl(var(--c-danger) / 0.08)' : 'hsl(var(--c-warning) / 0.10)',
        border: `1px solid ${isDanger ? 'hsl(var(--c-danger) / 0.4)' : 'hsl(var(--c-warning) / 0.4)'}`,
      }}
    >
      {/* Header */}
      <div className="mb-2 flex items-center gap-2">
        <span
          className="rounded-full px-2 py-0.5 font-mono text-[10px] font-bold text-white"
          style={{ background: isDanger ? 'hsl(var(--c-danger))' : 'hsl(var(--c-warning))' }}
        >
          {badge}
        </span>
        <span
          className="font-quicksand font-bold"
          style={{
            fontSize: 'var(--fs-md, 0.9375rem)',
            color: isDanger ? 'hsl(var(--c-danger))' : 'hsl(var(--c-warning))',
          }}
        >
          {title}
        </span>
      </div>
      {children}
    </div>
  );
}

/** Cost transparency note */
function CostNote({ children }: { children: ReactNode }): ReactElement {
  return (
    <div
      className="mb-3 flex items-center gap-2 rounded-md border border-dashed px-3 py-2 font-mono text-xs text-muted-foreground"
      style={{
        background: 'hsl(var(--c-success) / 0.06)',
        borderColor: 'hsl(var(--c-success) / 0.35)',
      }}
    >
      💰 {children}
    </div>
  );
}

/** Error action button */
function ErrAction({
  primary,
  icon,
  label,
  sub,
}: {
  primary?: boolean;
  icon: string;
  label: string;
  sub?: string;
}): ReactElement {
  return (
    <button
      type="button"
      className="flex w-full cursor-pointer items-center gap-3 rounded-lg px-4 py-3 text-left font-quicksand font-semibold"
      style={
        primary
          ? { background: 'hsl(var(--c-warning))', color: '#fff', border: 'none' }
          : {
              background: 'hsl(var(--card))',
              border: '1px solid hsl(var(--border))',
              color: 'hsl(var(--foreground))',
            }
      }
    >
      <span className="shrink-0 text-lg">{icon}</span>
      <span className="flex-1">
        <span className="block font-bold text-sm">{label}</span>
        {sub && (
          <span
            className="mt-0.5 block font-mono text-xs"
            style={{ opacity: primary ? 0.9 : 0.75 }}
          >
            {sub}
          </span>
        )}
      </span>
    </button>
  );
}

/** Error action danger-primary button */
function ErrActionDanger({
  icon,
  label,
  sub,
}: {
  icon: string;
  label: string;
  sub?: string;
}): ReactElement {
  return (
    <button
      type="button"
      className="flex w-full cursor-pointer items-center gap-3 rounded-lg px-4 py-3 text-left font-quicksand font-bold text-white"
      style={{ background: 'hsl(var(--c-danger))', border: 'none' }}
    >
      <span className="shrink-0 text-lg">{icon}</span>
      <span className="flex-1">
        <span className="block font-bold text-sm">{label}</span>
        {sub && (
          <span className="mt-0.5 block font-mono text-xs" style={{ opacity: 0.9 }}>
            {sub}
          </span>
        )}
      </span>
    </button>
  );
}

/** Debug footer strip */
function ErrDebug({ items }: { items: Record<string, string> }): ReactElement {
  return (
    <div className="mt-3 flex flex-wrap items-center gap-2 rounded-sm px-3 py-2 font-mono text-[10px] text-muted-foreground bg-muted/40">
      {Object.entries(items).map(([k, v]) => (
        <span key={k} className="flex gap-1">
          <span className="font-bold uppercase tracking-wider">{k}</span>
          <span className="text-foreground/70">{v}</span>
        </span>
      ))}
    </div>
  );
}

/** Telemetry footer */
function Telemetry({ label }: { label: string }): ReactElement {
  return (
    <div className="flex shrink-0 items-center justify-between border-t border-border px-4 py-3 font-mono text-xs text-muted-foreground">
      <span>📡 {label}</span>
      <button type="button" className="text-[hsl(var(--c-kb))] underline underline-offset-2">
        + nota dogfood
      </button>
    </div>
  );
}

/** Action list container */
function ErrActions({ children }: { children: ReactNode }): ReactElement {
  return <div className="flex flex-col gap-2">{children}</div>;
}

// ---------------------------------------------------------------------------
// Variant: stream-timeout (Stato A)
// ---------------------------------------------------------------------------

function StreamTimeoutVariant(): ReactElement {
  return (
    <div className="flex h-screen flex-col bg-background" data-theme="light">
      <PhoneStatusBar time="22:14" signal="📶 🔋 71%" />
      <AppBar title="Chat Arbitro" />

      {/* Chat shell */}
      <div className="flex flex-1 flex-col gap-3 overflow-y-auto p-4">
        {/* User bubble */}
        <div
          className="max-w-[85%] self-end rounded-2xl rounded-br-sm px-4 py-3 text-sm leading-snug"
          style={{ background: 'hsl(var(--c-chat) / 0.12)' }}
        >
          Posso usare lo scudo dopo aver già attaccato in questo turno?
          <div className="mt-1 font-mono text-[10px] text-muted-foreground">21:14 · 22:14</div>
        </div>

        {/* Partial assistant bubble */}
        <div
          className="max-w-[90%] self-start rounded-2xl rounded-bl-sm border px-4 py-3 text-sm leading-snug text-foreground"
          style={{
            background: 'hsl(var(--card))',
            borderColor: 'hsl(var(--c-warning) / 0.5)',
          }}
        >
          Sì, lo scudo è una reazione e non consuma l&#39;azione principale. Tuttavia, per usarlo
          dopo un attacco devi avere <em>almeno…</em>
          <div
            className="mt-2 flex items-center gap-1 font-mono text-xs"
            aria-live="polite"
            style={{ color: 'hsl(var(--c-warning))' }}
          >
            <span
              className="inline-block h-1.5 w-1.5 rounded-full"
              style={{ background: 'hsl(var(--c-warning))' }}
            />
            <span
              className="inline-block h-1.5 w-1.5 rounded-full"
              style={{ background: 'hsl(var(--c-warning))' }}
            />
            <span
              className="inline-block h-1.5 w-1.5 rounded-full"
              style={{ background: 'hsl(var(--c-warning))' }}
            />
            <span className="ml-1">in attesa risposta · 32 s</span>
          </div>
          <div className="mt-1 font-mono text-[10px] text-muted-foreground">
            <span style={{ color: 'hsl(var(--c-agent))', fontWeight: 700 }}>
              🤖 Arbitro Eldoria
            </span>
            {' · stream interrotto'}
          </div>
        </div>
      </div>

      {/* Error banner */}
      <ErrBanner type="warn" badge="⏱ Timeout" title="L'agente è bloccato">
        <p className="mb-3 text-sm leading-snug text-foreground">
          Risposta interrotta dopo 32 s. La parte sopra è quello che siamo riusciti a leggere prima
          del blocco. Probabile rallentamento provider, riprova tra qualche secondo.
        </p>
        <CostNote>
          <strong style={{ color: 'hsl(var(--c-success))' }}>Non addebitato</strong>
          {' · stream incompleto = 0 token al costo'}
        </CostNote>
        <ErrActions>
          <ErrAction primary icon="🔄" label="Riprova" sub="stesso prompt · nuovo stream" />
          <ErrAction
            icon="🤖"
            label="Cambia agente"
            sub='prova "Spiega regola base" (più veloce)'
          />
          <ErrAction
            icon="📋"
            label="Copia risposta parziale"
            sub="e continua a giocare al tavolo"
          />
        </ErrActions>
        <ErrDebug
          items={{
            'req-id': 'chat_msg_8a3b1c',
            model: 'deepseek-chat',
            ttfb: '1.2s',
            flush: '12 tokens (parziali)',
          }}
        />
      </ErrBanner>
      <Telemetry label="anomalia salvata in telemetry sessione" />
    </div>
  );
}

// ---------------------------------------------------------------------------
// Variant: ocr-fail (Stato B)
// ---------------------------------------------------------------------------

function OcrFailVariant(): ReactElement {
  return (
    <div className="flex h-screen flex-col bg-background" data-theme="light">
      <PhoneStatusBar time="22:21" signal="📶 🔋 70%" />
      <AppBar title="Foto §147" rightIcon="✕" />

      <div className="flex flex-1 flex-col items-center gap-4 overflow-y-auto p-4">
        {/* Blurred photo preview */}
        <div
          aria-hidden="true"
          className="flex shrink-0 items-center justify-center rounded-xl text-center font-mono text-xs"
          style={{
            width: 200,
            aspectRatio: '3/4',
            filter: 'blur(1.5px)',
            background:
              'linear-gradient(rgba(0,0,0,0.45),rgba(0,0,0,0.45)), linear-gradient(135deg,#2a2218,#1a140d)',
            border: '1px solid hsl(var(--border))',
            color: 'rgba(255,255,255,0.55)',
          }}
        >
          foto §147
          <br />
          21:14 · 22:21
        </div>

        <ErrBanner type="danger" badge="⚠ Illeggibile" title="Non riesco a leggere la pagina">
          <p className="mb-3 text-sm leading-snug text-foreground">
            L&#39;OCR ha confidence 27% — sotto la soglia minima utile (50%). Probabile foto mossa,
            troppo buia o controluce. Niente paragrafi traducibili da questo scatto.
          </p>
          <CostNote>
            <strong style={{ color: 'hsl(var(--c-success))' }}>Non addebitato</strong>
            {' · OCR fallito = 0 token traduzione'}
          </CostNote>
          <ErrActions>
            <ErrActionDanger
              icon="📷"
              label="Rifotografa"
              sub="consigliato · cerca luce diretta · tieni telefono fermo"
            />
            <ErrAction icon="🔢" label="Numero § manuale" sub="se conosci il paragrafo" />
            <ErrAction icon="↩" label="Annulla" sub="torna a Storybook precedente" />
          </ErrActions>
          <ErrDebug
            items={{
              'OCR conf': '0.27',
              soglia: '0.50',
              size: '380 KB',
              brightness: 'low',
            }}
          />
        </ErrBanner>
      </div>

      <Telemetry label="telemetry: ocr_fail · brightness_low" />
    </div>
  );
}

// ---------------------------------------------------------------------------
// Variant: llm-503 (Stato C)
// ---------------------------------------------------------------------------

function Llm503Variant(): ReactElement {
  return (
    <div className="flex h-screen flex-col bg-background" data-theme="light">
      <PhoneStatusBar time="22:33" signal="📶 🔋 68%" />
      <AppBar title="Servizio AI" />

      <div className="flex-1 overflow-y-auto">
        <ErrBanner type="danger" badge="🚨 503" title="Provider AI non risponde">
          <p className="mb-3 text-sm leading-snug text-foreground">
            Il servizio AI sta avendo problemi. Sto provando con un secondo provider, poi ripiego
            sulle regole base offline (subset Press Start in cache).
          </p>

          {/* Retry strip */}
          <div
            className="mb-3 flex flex-wrap items-center gap-2 rounded-md p-3"
            aria-live="polite"
            style={{ background: 'hsl(var(--c-danger) / 0.04)' }}
          >
            <span className="font-mono text-xs font-bold uppercase tracking-wider text-muted-foreground">
              Retry:
            </span>
            {[
              { label: '1', state: 'fail', title: 'DeepSeek 503 · 2s' },
              { label: '2', state: 'fail', title: 'DeepSeek 503 · 4s' },
              { label: '3', state: 'try', title: 'OpenRouter · in corso' },
            ].map(pip => (
              <span
                key={pip.label}
                title={pip.title}
                className="flex h-6 w-6 items-center justify-center rounded-full font-mono text-[11px] font-bold"
                style={
                  pip.state === 'fail'
                    ? {
                        background: 'hsl(var(--c-danger) / 0.15)',
                        color: 'hsl(var(--c-danger))',
                      }
                    : {
                        background: 'hsl(var(--c-warning))',
                        color: '#fff',
                      }
                }
              >
                {pip.label}
              </span>
            ))}
            <span className="font-mono text-[10px] text-muted-foreground">
              backoff exp · attesa 6 s
            </span>
          </div>

          <CostNote>
            <strong style={{ color: 'hsl(var(--c-success))' }}>Non addebitato</strong>
            {' · retry falliti = 0 token'}
          </CostNote>
          <ErrActions>
            <ErrActionDanger icon="⏸" label="Annulla retry" sub="passa subito a regole offline" />
            <ErrAction
              icon="📡"
              label="Mostra status pubblico"
              sub="status.meepleai.app · ultima incident"
            />
          </ErrActions>
          <ErrDebug
            items={{
              err: '503 Upstream',
              provider: 'deepseek → openrouter',
              incident: 'INC-0234',
            }}
          />
        </ErrBanner>

        {/* Offline fallback card */}
        <div
          className="mx-4 mb-4 rounded-xl p-4"
          style={{
            background: 'hsl(var(--c-kb) / 0.06)',
            border: '1px dashed hsl(var(--c-kb) / 0.3)',
          }}
        >
          <div className="mb-2 flex items-center gap-2">
            <span className="text-lg" style={{ color: 'hsl(var(--c-kb))' }}>
              📒
            </span>
            <span
              className="font-quicksand text-sm font-bold"
              style={{ color: 'hsl(var(--c-kb))' }}
            >
              Disponibili offline mentre aspetti
            </span>
          </div>
          <div className="text-sm leading-snug text-muted-foreground">
            • Glossario campagna (32 termini) — accesso istantaneo
            <br />
            • Setup tutorial Press Start (24 pag) — solo lookup, no chat
            <br />• Stato sessione + paragrafo corrente — sempre disponibile
          </div>
        </div>
      </div>

      <Telemetry label="outage in corso · status.meepleai.app" />
    </div>
  );
}

// ---------------------------------------------------------------------------
// Variant: segmentation-fail (Stato D)
// ---------------------------------------------------------------------------

function SegmentationFailVariant(): ReactElement {
  return (
    <div className="flex h-screen flex-col bg-background" data-theme="light">
      <PhoneStatusBar time="22:51" signal="📶 🔋 66%" />
      <AppBar title="Pagina senza §" rightIcon="✕" />

      <div className="flex flex-1 flex-col items-center gap-3 overflow-y-auto p-4">
        {/* Photo preview (no blur) */}
        <div
          aria-hidden="true"
          className="flex shrink-0 items-center justify-center rounded-xl text-center font-mono text-xs"
          style={{
            width: 160,
            aspectRatio: '3/4',
            background:
              'linear-gradient(rgba(0,0,0,0.45),rgba(0,0,0,0.45)), linear-gradient(135deg,#2a2218,#1a140d)',
            border: '1px solid hsl(var(--border))',
            color: 'rgba(255,255,255,0.55)',
          }}
        >
          foto pagina
          <br />
          (no § visibili)
        </div>

        <ErrBanner
          type="warn"
          badge="🔍 No §"
          title="Pagina riconosciuta ma senza paragrafi numerati"
        >
          <p className="mb-3 text-sm leading-snug text-foreground">
            Ho letto il testo (confidence 88%) ma non trovo numeri di paragrafo (§). Forse è una
            pagina di introduzione, glossario o indice. Inserisci tu il § oppure dimmi che è una
            pagina speciale.
          </p>
          <CostNote>
            <strong style={{ color: 'hsl(var(--c-success))' }}>Solo OCR addebitato</strong>
            {' · traduzione ancora non partita'}
          </CostNote>
        </ErrBanner>

        {/* Manual § input */}
        <div className="w-full rounded-xl border border-border bg-card p-4">
          <label
            htmlFor="pn-input-mock"
            className="mb-2 block font-quicksand text-sm font-bold text-foreground"
          >
            A che paragrafo è questa pagina?
          </label>
          <div className="flex items-center gap-2">
            <span
              className="font-quicksand text-2xl font-bold"
              style={{ color: 'hsl(var(--c-kb))' }}
            >
              §
            </span>
            <input
              id="pn-input-mock"
              type="text"
              inputMode="numeric"
              placeholder="147"
              className="flex-1 rounded-md border border-border bg-background p-3 font-mono text-2xl font-bold text-foreground"
              readOnly
            />
          </div>
          <div className="mt-3 flex flex-col gap-2 border-t border-border/50 pt-3">
            <button
              type="button"
              className="font-mono text-xs text-left underline underline-offset-2"
              style={{ color: 'hsl(var(--c-kb))', background: 'none', border: 'none' }}
            >
              📚 È una pagina di glossario / indice (skip §)
            </button>
            <button
              type="button"
              className="font-mono text-xs text-left underline underline-offset-2"
              style={{ color: 'hsl(var(--c-kb))', background: 'none', border: 'none' }}
            >
              📑 È un range (es. §147-149) — inserisci range
            </button>
            <button
              type="button"
              className="font-mono text-xs text-left underline underline-offset-2"
              style={{ color: 'hsl(var(--c-kb))', background: 'none', border: 'none' }}
            >
              ↩ Annulla, è una pagina sbagliata
            </button>
          </div>
        </div>

        <ErrAction
          primary
          icon="→"
          label="Conferma e traduci"
          sub="parte la traduzione N3 standard"
        />
        <ErrDebug
          items={{
            'OCR conf': '0.88',
            tokens: '~420',
            regex: '/§\\s*\\d+/g · 0 match',
          }}
        />
      </div>

      <Telemetry label="segmentation_fail · ocr_ok" />
    </div>
  );
}

// ---------------------------------------------------------------------------
// Variant: ocr-low-conf (Stato E)
// ---------------------------------------------------------------------------

function OcrLowConfVariant(): ReactElement {
  return (
    <div className="flex h-screen flex-col bg-background" data-theme="light">
      <PhoneStatusBar time="22:27" signal="📶 🔋 69%" />
      <AppBar title="Foto §147 · controllo" />

      <div className="flex flex-1 flex-col items-center gap-3 overflow-y-auto p-4">
        {/* OCR page preview with uncertain regions highlighted */}
        <div
          aria-label="Anteprima OCR con regioni dubbie evidenziate"
          className="relative w-full max-w-[280px] shrink-0 overflow-hidden rounded-md border border-border p-3 font-mono text-[10px] leading-relaxed"
          style={{
            aspectRatio: '3/4',
            background: 'linear-gradient(180deg,#fdf8f0,#f4ead8)',
            color: '#3a2f1e',
          }}
        >
          <span
            className="mb-1 block font-quicksand text-[13px] font-bold"
            style={{ color: 'hsl(var(--c-kb))' }}
          >
            § 147
          </span>
          <span className="block" style={{ color: '#3a2f1e' }}>
            Il guardiano della soglia
          </span>
          <span className="block">
            <span style={{ color: '#3a2f1e' }}>ti osserva con occhi </span>
            <span
              className="rounded-sm px-0.5"
              style={{
                background: 'hsl(var(--c-warning) / 0.32)',
                boxShadow: '0 0 0 2px hsl(var(--c-warning) / 0.55)',
                color: '#2a1e07',
              }}
            >
              ferrigni
            </span>
            <span style={{ color: '#3a2f1e' }}>.</span>
          </span>
          <span className="block">
            <span style={{ color: '#3a2f1e' }}>{'"Solo chi possiede la '}</span>
            <span
              className="rounded-sm px-0.5"
              style={{
                background: 'hsl(var(--c-warning) / 0.32)',
                boxShadow: '0 0 0 2px hsl(var(--c-warning) / 0.55)',
                color: '#2a1e07',
              }}
            >
              runa
            </span>
            <span style={{ color: '#3a2f1e' }}> potrà</span>
          </span>
          <span className="block" style={{ color: '#3a2f1e' }}>
            passare oltre&#34;, sussurra.
          </span>
          <span className="block">&nbsp;</span>
          <span className="block">
            <span style={{ color: '#3a2f1e' }}>Hai con te la </span>
            <span
              className="rounded-sm px-0.5"
              style={{
                background: 'hsl(var(--c-warning) / 0.32)',
                boxShadow: '0 0 0 2px hsl(var(--c-warning) / 0.55)',
                color: '#2a1e07',
              }}
            >
              Runa di Ardenel
            </span>
            <span style={{ color: '#3a2f1e' }}>?</span>
          </span>
          <span className="block" style={{ color: '#3a2f1e' }}>
            → Sì, mostrala (vai a § 152)
          </span>
          <span className="block">
            <span style={{ color: '#3a2f1e' }}>→ No, prova a forzare il pa</span>
            <span
              className="rounded-sm px-0.5"
              style={{
                background: 'hsl(var(--c-warning) / 0.32)',
                boxShadow: '0 0 0 2px hsl(var(--c-warning) / 0.55)',
                color: '#2a1e07',
              }}
            >
              ssaggio
            </span>
          </span>
          <span className="block" style={{ color: '#3a2f1e' }}>
            &nbsp;&nbsp;&nbsp;(vai a § 198)
          </span>
          {/* Legend */}
          <div
            className="absolute bottom-2 right-2 flex items-center gap-1 rounded-sm px-2 py-0.5 font-mono text-[9px] text-muted-foreground"
            style={{ background: 'rgba(255,255,255,0.9)' }}
          >
            <span
              className="h-2.5 w-2.5 rounded-sm"
              style={{ background: 'hsl(var(--c-warning) / 0.55)' }}
            />
            dubbio
          </div>
        </div>

        <ErrBanner type="warn" badge="🔎 0.64" title="Testo letto, 4 parole sono poco chiare">
          <p className="mb-3 text-sm leading-snug text-foreground">
            Confidence media 64% (soglia minima 70%). Le parti evidenziate in giallo potrebbero
            essere lette male — ho indovinato in base al contesto ma non sono sicuro al 100%.
          </p>

          {/* Confidence strip */}
          <div
            className="mb-3 flex items-center gap-2 rounded-md px-3 py-2 font-mono text-xs text-muted-foreground"
            style={{ background: 'hsl(var(--c-warning) / 0.06)' }}
            aria-label="Confidence indicator"
          >
            <span>conf</span>
            <div className="relative h-1.5 flex-1 overflow-hidden rounded-full bg-muted">
              <div
                className="absolute inset-y-0 left-0 rounded-full"
                style={{
                  width: '64%',
                  background:
                    'linear-gradient(90deg, hsl(var(--c-danger)), hsl(var(--c-warning)) 50%, hsl(var(--c-success)))',
                }}
              />
              <div
                className="absolute inset-y-[-3px] w-0.5 opacity-40"
                style={{ left: '70%', background: 'hsl(var(--foreground))' }}
              />
            </div>
            <span className="font-bold" style={{ color: 'hsl(var(--c-warning))' }}>
              0.64
            </span>
          </div>

          <CostNote>
            <strong style={{ color: 'hsl(var(--c-success))' }}>Solo OCR addebitato</strong>
            {' · traduzione N3 non ancora partita'}
          </CostNote>
          <ErrActions>
            <ErrAction
              primary
              icon="📷"
              label="Ritocca foto"
              sub="consigliato · più luce · nuovo OCR gratis"
            />
            <ErrAction
              icon="⚠"
              label="Procedi comunque"
              sub="accetto rischio errori sulle parole dubbie"
            />
            <ErrAction
              icon="✏"
              label="Correggi manualmente"
              sub="tap sulle parole gialle per editare"
            />
          </ErrActions>
          <ErrDebug
            items={{
              'OCR conf': '0.64',
              soglia: '0.70',
              'regioni low': '4 di 32',
              tokens: '~210',
            }}
          />
        </ErrBanner>
      </div>

      <Telemetry label="telemetry: ocr_low_confidence · 4 regions" />
    </div>
  );
}

// ---------------------------------------------------------------------------
// Variant: photo-blur (Stato F)
// ---------------------------------------------------------------------------

function PhotoBlurVariant(): ReactElement {
  return (
    <div className="flex h-screen flex-col bg-background" data-theme="light">
      <PhoneStatusBar time="22:18" signal="📶 🔋 70%" />
      <AppBar title="Scatta foto §" rightIcon="💡" />

      {/* Camera viewfinder area — dark background */}
      <div
        className="flex flex-1 flex-col items-center gap-4 overflow-y-auto p-4"
        style={{ background: '#0a0805' }}
      >
        {/* Viewfinder */}
        <div
          className="relative flex w-full max-w-[260px] shrink-0 items-center justify-center overflow-hidden rounded-xl"
          style={{
            aspectRatio: '3/4',
            filter: 'blur(3.5px)',
            background:
              'linear-gradient(rgba(0,0,0,0.5),rgba(0,0,0,0.5)), linear-gradient(135deg,#1f1810,#0e0a06)',
            border: '2px dashed hsl(var(--c-danger) / 0.7)',
          }}
          aria-label="Viewfinder fotocamera · foto sfocata"
        >
          {/* crosshair reticle */}
          <div
            className="absolute inset-0"
            style={{
              background:
                'linear-gradient(90deg, transparent calc(50% - 1px), hsl(var(--c-danger) / 0.4) 50%, transparent calc(50% + 1px)), linear-gradient(0deg, transparent calc(50% - 1px), hsl(var(--c-danger) / 0.4) 50%, transparent calc(50% + 1px))',
            }}
          />
          {/* Focus warning pill */}
          <span
            className="absolute bottom-3 left-1/2 -translate-x-1/2 whitespace-nowrap rounded-full px-3 py-1 font-mono text-xs font-bold uppercase tracking-wider text-white"
            style={{ background: 'hsl(var(--c-danger))', filter: 'none' }}
          >
            ⚠ FUORI FUOCO
          </span>
        </div>

        {/* Disabled shutter button */}
        <button
          type="button"
          disabled
          aria-label="Shutter disabilitato"
          aria-disabled="true"
          className="flex h-16 w-16 shrink-0 cursor-not-allowed items-center justify-center rounded-full opacity-40"
          style={{
            background: 'hsl(var(--card))',
            border: '4px solid hsl(var(--border-strong, var(--border)))',
          }}
        >
          <span
            className="h-12 w-12 rounded-full"
            style={{ background: 'hsl(var(--c-danger) / 0.4)' }}
          />
        </button>

        {/* Error banner on dark bg — needs card surface */}
        <div className="w-full rounded-xl bg-background p-0">
          <ErrBanner type="warn" badge="🌀 Blur" title="Tieni fermo il telefono">
            <p className="mb-3 text-sm leading-snug text-foreground">
              Lo scatto è sfocato. Non lo invio nemmeno — risparmio tempo e costi. Aspetta che il
              riquadro diventi nitido e riprova.
            </p>

            {/* Focus meter */}
            <div
              className="mb-3 flex items-center gap-2 rounded-md border px-3 py-2 font-mono text-xs text-muted-foreground"
              aria-label="Focus score live"
              style={{
                background: 'hsl(var(--c-danger) / 0.08)',
                borderColor: 'hsl(var(--c-danger) / 0.3)',
              }}
            >
              <span>focus</span>
              <div className="relative h-2 flex-1 overflow-hidden rounded-full bg-muted">
                <div
                  className="absolute inset-y-0 left-0 rounded-full"
                  style={{ width: '28%', background: 'hsl(var(--c-danger))' }}
                />
                <div
                  className="absolute inset-y-[-3px] w-0.5 opacity-40"
                  style={{ left: '60%', background: 'hsl(var(--foreground))' }}
                />
              </div>
              <span className="font-bold" style={{ color: 'hsl(var(--c-danger))' }}>
                0.28
              </span>
            </div>

            <CostNote>
              <strong style={{ color: 'hsl(var(--c-success))' }}>Niente addebitato</strong>
              {' · reject client-side · 0 byte inviati, 0 token'}
            </CostNote>
            <ErrActions>
              <ErrAction
                primary
                icon="🔄"
                label="Riprova"
                sub="attendi che il riquadro sia nitido"
              />
              <ErrAction icon="💡" label="Attiva flash" sub="migliora luce · riduce mosso" />
              <ErrAction
                icon="🔢"
                label="Salta: inserisci § manuale"
                sub="se conosci il paragrafo"
              />
              <ErrAction
                icon="⌨"
                label="Digita manualmente"
                sub="trascrivi il paragrafo · skip OCR"
              />
            </ErrActions>
            <ErrDebug
              items={{
                focus: '0.28',
                soglia: '0.60',
                'round-trip': '0 ms (skip server)',
                device: 'user-agent camera',
              }}
            />
          </ErrBanner>
        </div>
      </div>

      <div className="bg-background">
        <Telemetry label="telemetry: photo_blur_reject · client-side" />
      </div>
    </div>
  );
}

// ---------------------------------------------------------------------------
// Variant: translation-timeout (Stato G)
// ---------------------------------------------------------------------------

function TranslationTimeoutVariant(): ReactElement {
  return (
    <div className="flex h-screen flex-col bg-background" data-theme="light">
      <PhoneStatusBar time="22:42" signal="📶 🔋 67%" />
      <AppBar title="Traduco §147" />

      <div className="flex-1 overflow-y-auto">
        <ErrBanner type="warn" badge="⏱ 22 s" title="La traduzione è lenta">
          <p className="mb-3 text-sm leading-snug text-foreground">
            Ho aspettato 22 s ma l&#39;AI non ha ancora risposto. Probabile traffico provider. Il
            testo originale è già pronto qui sotto — puoi tradurlo a mano oppure ritentare.
          </p>
          <CostNote>
            <strong style={{ color: 'hsl(var(--c-success))' }}>Non addebitato</strong>
            {' · request abortita = 0 token traduzione'}
          </CostNote>
          <ErrActions>
            <ErrAction
              primary
              icon="🔄"
              label="Riprova traduzione"
              sub="stesso testo · nuovo tentativo · ~5 s"
            />
            <ErrAction
              icon="✏"
              label="Traduci a mano"
              sub="testo originale qui sotto, edita liberamente"
            />
            <ErrAction
              icon="↩"
              label="Continua in lingua originale"
              sub="salta traduzione, mostra testo OCR"
            />
          </ErrActions>
          <ErrDebug
            items={{
              'req-id': 'tr_e8a3b1c',
              timeout: '20.0 s',
              model: 'openrouter:gemini-pro',
            }}
          />
        </ErrBanner>

        {/* Manual edit area */}
        <div className="mx-4 mb-4 rounded-xl border border-border bg-card p-3">
          <label
            htmlFor="manual-tr-mock"
            className="mb-2 block font-quicksand text-sm font-bold text-foreground"
          >
            Testo originale OCR (puoi tradurre qui)
          </label>
          <textarea
            id="manual-tr-mock"
            readOnly
            rows={5}
            className="w-full resize-y rounded-sm border border-border bg-background p-3 font-sans text-sm leading-snug text-foreground"
            defaultValue={`The guardian of the threshold watches you with iron eyes. "Only the rune-bearer shall pass," he whispers.\n\nDo you have the Rune of Ardenel?\n→ Yes, show it (go to § 152)\n→ No, force the passage (go to § 198)`}
          />
        </div>
      </div>

      <Telemetry label="telemetry: translation_timeout · 22.4 s" />
    </div>
  );
}

// ---------------------------------------------------------------------------
// Variant: source-lang-unknown (Stato H)
// ---------------------------------------------------------------------------

function SourceLangUnknownVariant(): ReactElement {
  const langs = [
    { flag: '🇬🇧', name: 'English', meta: 'conf 38% · proposta', selected: true },
    { flag: '🇮🇹', name: 'Italiano', meta: 'conf 24%', selected: false },
    { flag: '🇫🇷', name: 'Français', meta: 'conf 11%', selected: false },
    { flag: '🇩🇪', name: 'Deutsch', meta: 'conf 9%', selected: false },
  ];

  return (
    <div className="flex h-screen flex-col bg-background" data-theme="light">
      <PhoneStatusBar time="22:38" signal="📶 🔋 68%" />
      <AppBar title="Lingua testo · §147" />

      <div
        className="flex flex-1 flex-col items-center justify-center p-3"
        style={{ background: 'hsl(var(--c-danger) / 0.04)' }}
      >
        {/* Lang dialog */}
        <div
          role="dialog"
          aria-modal="true"
          aria-labelledby="lang-dialog-title"
          className="w-full max-w-xs rounded-xl p-5"
          style={{
            background: 'hsl(var(--card))',
            border: '1px solid hsl(var(--c-danger) / 0.4)',
            boxShadow: '0 8px 32px rgba(0,0,0,0.1)',
          }}
        >
          <div className="mb-3 flex items-center gap-2">
            <span
              className="flex h-9 w-9 items-center justify-center rounded-full text-xl"
              style={{ background: 'hsl(var(--c-danger) / 0.12)', color: 'hsl(var(--c-danger))' }}
            >
              🌐
            </span>
            <span id="lang-dialog-title" className="font-quicksand font-bold text-foreground">
              Quale lingua è questa?
            </span>
          </div>
          <p className="mb-3 text-sm leading-snug text-muted-foreground">
            Confidence lingua 38% (sotto soglia 50%). Il testo OCR contiene neologismi fantasy che
            confondono l&#39;auto-detect.
          </p>

          {/* Language grid */}
          <div
            role="radiogroup"
            aria-label="Seleziona lingua sorgente"
            className="mb-3 grid grid-cols-2 gap-2"
          >
            {langs.map(lang => (
              <button
                key={lang.name}
                type="button"
                role="radio"
                aria-checked={lang.selected}
                className="flex cursor-pointer items-center gap-2 rounded-xl border px-3 py-3 text-left font-quicksand text-sm"
                style={
                  lang.selected
                    ? {
                        background: 'hsl(var(--c-kb) / 0.08)',
                        borderColor: 'hsl(var(--c-kb))',
                        color: 'hsl(var(--c-kb))',
                        fontWeight: 700,
                      }
                    : {
                        background: 'hsl(var(--background))',
                        borderColor: 'hsl(var(--border))',
                        color: 'hsl(var(--foreground))',
                      }
                }
              >
                <span className="text-lg">{lang.flag}</span>
                <span>
                  <span className="block">{lang.name}</span>
                  <span
                    className="mt-0.5 block font-mono text-[10px]"
                    style={{
                      color: lang.selected
                        ? 'hsl(var(--c-kb) / 0.8)'
                        : 'hsl(var(--muted-foreground))',
                    }}
                  >
                    {lang.meta}
                  </span>
                </span>
              </button>
            ))}
            {/* Español spans full width */}
            <button
              type="button"
              role="radio"
              aria-checked={false}
              className="col-span-2 flex cursor-pointer items-center gap-2 rounded-xl border border-border bg-background px-3 py-3 text-left font-quicksand text-sm text-foreground"
            >
              <span className="text-lg">🇪🇸</span>
              <span>
                <span className="block">Español</span>
                <span className="mt-0.5 block font-mono text-[10px] text-muted-foreground">
                  conf 7%
                </span>
              </span>
            </button>
          </div>

          <CostNote>
            <strong style={{ color: 'hsl(var(--c-success))' }}>Solo OCR addebitato</strong>
            {' · lang-detect free · traduzione N3 dopo conferma'}
          </CostNote>
          <ErrActions>
            <ErrActionDanger
              icon="→"
              label='Conferma "English"'
              sub="e traduci in italiano · ~6 s · ~0,01 €"
            />
            <ErrAction
              icon="↩"
              label="Annulla"
              sub="salta traduzione, mostra testo OCR così com'è"
            />
          </ErrActions>
          <ErrDebug
            items={{
              'lang-conf': '0.38',
              soglia: '0.50',
              tokens: '~210',
            }}
          />
        </div>
      </div>

      <Telemetry label="telemetry: source_lang_unknown · 0.38" />
    </div>
  );
}

// ---------------------------------------------------------------------------
// Variant: network-mid-ocr (Stato I)
// ---------------------------------------------------------------------------

function NetworkMidOcrVariant(): ReactElement {
  return (
    <div className="flex h-screen flex-col bg-background" data-theme="light">
      <PhoneStatusBar time="22:47" signal="✈ 🔋 65%" />
      <AppBar title="Offline · 1 in coda" rightIcon="⚡" />

      {/* Persistent offline strip */}
      <div
        className="mx-4 mt-3 flex items-center gap-3 rounded-md px-4 py-3 font-quicksand font-semibold text-sm text-white"
        role="status"
        aria-live="polite"
        style={{
          background:
            'linear-gradient(90deg, hsl(var(--c-danger) / 0.95), hsl(var(--c-danger) / 0.7))',
        }}
      >
        <span
          className="h-2.5 w-2.5 shrink-0 rounded-full"
          aria-hidden="true"
          style={{ background: '#fff' }}
        />
        <span className="flex-1">
          Offline · 1 foto in coda
          <span className="mt-0.5 block font-mono text-[10px] opacity-85">
            retry automatico appena torni online
          </span>
        </span>
      </div>

      {/* Queue card */}
      <div className="mx-4 mt-3 rounded-xl border border-border bg-card p-3">
        <div className="mb-2 flex items-center gap-2 font-quicksand text-sm font-bold text-foreground">
          <span>📥 Foto in upload</span>
          <span
            className="rounded-full px-2 py-0.5 font-mono text-[10px] font-bold"
            style={{
              background: 'hsl(var(--c-warning) / 0.18)',
              color: 'hsl(var(--c-warning))',
            }}
          >
            1 in attesa
          </span>
        </div>
        <div className="flex items-center gap-2 border-b border-dashed border-border/50 py-2 text-sm text-muted-foreground last:border-b-0">
          <div
            className="flex h-12 w-9 shrink-0 items-center justify-center rounded-sm font-mono text-[10px]"
            style={{
              background: 'linear-gradient(135deg,#2a2218,#1a140d)',
              color: 'rgba(255,255,255,0.5)',
            }}
          >
            §147
          </div>
          <div className="flex-1">
            <span className="block font-quicksand font-semibold text-foreground">Foto §147</span>
            <span className="mt-0.5 block font-mono text-[10px] text-muted-foreground">
              salvata locale · 380 KB · in attesa rete
            </span>
          </div>
          <span
            className="rounded-full px-2 py-0.5 font-mono text-[10px] font-bold"
            style={{
              background: 'hsl(var(--c-warning) / 0.15)',
              color: 'hsl(var(--c-warning))',
            }}
          >
            ⏳ attesa
          </span>
        </div>
      </div>

      <div className="flex-1 overflow-y-auto p-0">
        <ErrBanner type="danger" badge="📶 Offline" title="Niente rete">
          <p className="mb-3 text-sm leading-snug text-foreground">
            L&#39;upload della foto si è interrotto a metà. Niente paura: ho salvato lo scatto sul
            tuo telefono e riprovo da solo appena la rete torna. Puoi continuare a usare l&#39;app
            offline: il glossario campagna è disponibile.
          </p>
          <CostNote>
            <strong style={{ color: 'hsl(var(--c-success))' }}>Non addebitato</strong>
            {' · upload incompleto = 0 byte ricevuti server-side'}
          </CostNote>
          <ErrActions>
            <ErrAction
              icon="🔄"
              label="Riprova subito"
              sub="forza retry · utile se rete è tornata ora"
            />
            <ErrAction
              icon="📒"
              label="Vai a Glossario offline"
              sub="32 termini campagna cached · accesso istantaneo"
            />
            <ErrAction
              icon="🗑"
              label="Cancella coda"
              sub="elimina foto in attesa (irrecuperabile)"
            />
          </ErrActions>
          <ErrDebug
            items={{
              err: 'net::ERR_NETWORK_CHANGED',
              salvato: 'IndexedDB · photo_queue_b3a',
              retry: 'on connectivity-change',
            }}
          />
        </ErrBanner>
      </div>

      <Telemetry label="in attesa retry · telemetry posticipata" />
    </div>
  );
}

// ---------------------------------------------------------------------------
// Variant: quota-exhausted (Stato J)
// ---------------------------------------------------------------------------

function QuotaExhaustedVariant(): ReactElement {
  return (
    <div className="flex h-screen flex-col bg-background" data-theme="light">
      <PhoneStatusBar time="22:54" signal="📶 🔋 64%" />
      <AppBar title="Crediti finiti" />

      <div className="flex-1 overflow-y-auto">
        {/* Quota bridge card */}
        <div
          role="alert"
          className="mx-4 my-3 rounded-xl p-5"
          style={{
            background:
              'linear-gradient(135deg, hsl(var(--c-warning) / 0.16), hsl(var(--c-danger) / 0.10))',
            border: '1px solid hsl(var(--c-warning) / 0.5)',
          }}
        >
          <div className="mb-3 flex items-center gap-2">
            <span
              className="flex h-10 w-10 items-center justify-center rounded-full text-xl text-white"
              style={{ background: 'hsl(var(--c-warning))' }}
            >
              ⚡
            </span>
            <span
              className="font-quicksand text-lg font-bold"
              style={{ color: 'hsl(var(--c-warning))' }}
            >
              Crediti finiti mid-traduzione
            </span>
          </div>

          <p className="mb-3 text-sm leading-snug text-foreground">
            La traduzione si è fermata a metà — i tuoi crediti del piano &#34;Esploratore&#34; sono
            esauriti per oggi. Il testo originale del paragrafo è qui sotto: leggilo in lingua
            originale senza spendere altro, oppure ricarica crediti.
          </p>

          {/* Quota meter */}
          <div className="mb-3 rounded-md p-3" style={{ background: 'rgba(255,255,255,0.4)' }}>
            <div className="mb-2 flex items-center justify-between font-mono text-[10px] uppercase tracking-wider text-muted-foreground">
              <span>token quota giornaliera</span>
              <span className="font-bold" style={{ color: 'hsl(var(--c-danger))' }}>
                100% · 8 470 / 8 470
              </span>
            </div>
            <div className="relative h-2 overflow-hidden rounded-full bg-muted">
              <div
                className="absolute inset-y-0 left-0 rounded-full"
                style={{ width: '100%', background: 'hsl(var(--c-danger))' }}
              />
            </div>
          </div>

          <CostNote>
            <strong style={{ color: 'hsl(var(--c-success))' }}>
              Solo OCR + traduzione parziale addebitati
            </strong>
            {' · ~0,008 € · mostrare testo originale = 0 token extra'}
          </CostNote>

          <ErrActions>
            <ErrAction
              primary
              icon="💎"
              label="Ricarica crediti"
              sub="+5 000 token · 1,99 € · sblocca subito"
            />
            <ErrAction
              icon="📖"
              label="Leggi in lingua originale"
              sub="testo OCR qui sotto · 0 token extra"
            />
            <ErrAction
              icon="⏰"
              label="Aspetta reset domani"
              sub="quota si ricarica alle 00:00 · gratis"
            />
          </ErrActions>

          {/* Preserved OCR text */}
          <div
            className="mt-3 rounded-md border border-dashed p-3"
            style={{
              background: 'rgba(255,255,255,0.5)',
              borderColor: 'hsl(var(--c-success) / 0.4)',
            }}
          >
            <div
              className="mb-2 flex items-center gap-1 font-mono text-[10px] font-bold uppercase tracking-wider"
              style={{ color: 'hsl(var(--c-success))' }}
            >
              ✓ Testo OCR conservato · 0 token extra
            </div>
            <p className="font-sans text-sm italic leading-snug text-foreground">
              &ldquo;The guardian of the threshold watches you with iron eyes. Only the rune-bearer
              shall pass…&rdquo;
            </p>
          </div>

          <ErrDebug
            items={{
              quota: '8470/8470 (100%)',
              reset: '00:00 UTC+1',
              parziale: '42 / ~210 tokens',
            }}
          />
        </div>
      </div>

      <Telemetry label="quota_exhausted · piano esploratore" />
    </div>
  );
}

// ---------------------------------------------------------------------------
// Main mock component
// ---------------------------------------------------------------------------

export function ErrorStatesMock({
  variant = 'stream-timeout',
  sessionName: _sessionName = 'Sera con i ragazzi',
}: ErrorStatesMockProps): ReactElement {
  switch (variant) {
    case 'stream-timeout':
      return <StreamTimeoutVariant />;
    case 'ocr-fail':
      return <OcrFailVariant />;
    case 'llm-503':
      return <Llm503Variant />;
    case 'segmentation-fail':
      return <SegmentationFailVariant />;
    case 'ocr-low-conf':
      return <OcrLowConfVariant />;
    case 'photo-blur':
      return <PhotoBlurVariant />;
    case 'translation-timeout':
      return <TranslationTimeoutVariant />;
    case 'source-lang-unknown':
      return <SourceLangUnknownVariant />;
    case 'network-mid-ocr':
      return <NetworkMidOcrVariant />;
    case 'quota-exhausted':
      return <QuotaExhaustedVariant />;
    default: {
      const _exhaustive: never = variant;
      return _exhaustive;
    }
  }
}
