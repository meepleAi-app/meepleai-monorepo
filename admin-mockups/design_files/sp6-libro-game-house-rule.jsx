/* ============================================================================
   sp6-libro-game-house-rule.jsx
   Mockup: House Rule Drawer (§F) — bottom-sheet drawer dentro
           /gamebook/[gameId]/play (NO route propria).
   Pattern: drawer tabbed bottom-sheet mobile / side-panel desktop (380px).
            Canonical ref: 03-drawer-variants.html (V1 bottom sheet).
   Brief:   SP6-libro-game-amended-for-mockup6.md §F (post-#2045, 3 DEC + 3 MAJ).

   FREEZE compliance:
     - SOLO CSS variables da tokens.css (zero hex hardcoded).
     - Helper entityHsl(entity, alpha?) inline per i 9 entity color.
     - Esento: color:'#fff' su background entity (pattern .e-bg).
     - Zero menzioni hosting esterno proprietario (usa "asset di hosting esterno").

   13 JSX exports: State01_DrawerClosed .. State13_DeleteImpossibleSoftDeleteOptOut
   ========================================================================== */

const { useState } = React;

/* ─── entityHsl helper (9 entity color, alpha composable) ─── */
function entityHsl(entity, alpha) {
  const v = `var(--c-${entity})`;
  return alpha == null ? `hsl(${v})` : `hsl(${v} / ${alpha})`;
}

/* ─── Persona dati (brief §F) ───────────────────────────────────────────── */
// La domanda low-confidence che innesca il drawer (entry-point primary).
const ORIGINAL_Q = 'Se due giocatori muoiono nello stesso turno, chi diventa il leader?';
// Estratto manuale ufficiale poco chiaro (citazione kb p.34) — glossario: Niamh.
const MANUAL_EXCERPT =
  'Quando il gruppo perde il proprio leader, lo Stato del gruppo entra in stallo ' +
  'finché Niamh non designa un nuovo portavoce secondo le regole del capitolo corrente.';

// Tainted Grail rules (2) + ISS Vanguard (1) — group headers per gioco.
const RULES_TG = [
  {
    id: 'r1',
    title: 'Morte simultanea del leader',
    body:
      'In caso di morte simultanea, diventa leader chi ha più punti Vita massimi. ' +
      'Se permane il pareggio, decide il giocatore con più sessioni completate nella campagna.',
    origin: ORIGINAL_Q,
    page: 'p.34 — Stato del gruppo',
    tags: ['Voto', 'Niamh'],
    applied: 3, ago: '2 giorni fa', by: 'Sara',
  },
  {
    id: 'r2',
    title: 'Requisito Spada di Avalon',
    body:
      'La Spada di Avalon può essere impugnata solo da un personaggio con Vigore ≥ 4. ' +
      'Conferisce +2 al danno contro le creature toccate dal Wyrdness.',
    origin: 'La Spada di Avalon ha un requisito di Vigore minimo?',
    page: 'p.51 — Equipaggiamento leggendario',
    tags: ['Combat'],
    applied: 1, ago: '5 giorni fa', by: 'Sara',
  },
];
const RULES_IV = [
  {
    id: 'r3',
    title: 'Riparazioni d’emergenza',
    body:
      'Le riparazioni d’emergenza eseguite fuori dal proprio turno costano 2 azioni ' +
      'invece di 1 se la sezione della nave è in fiamme.',
    origin: 'Quanto costa riparare fuori turno una sezione in fiamme?',
    page: 'p.22 — Gestione della nave',
    tags: ['Esplorazione'],
    applied: 2, ago: '1 settimana fa', by: 'Sara',
  },
];

// Testo ~150 char per state-03 (typed) e prefill per state-08 (edit).
const TYPED_DRAFT =
  'In caso di morte simultanea, diventa leader chi ha più punti Vita massimi; ' +
  'a parità, decide chi ha completato più sessioni.';

/* ─── EntityChip / Pip ──────────────────────────────────────────────────── */
function EntityChip({ entity, icon, children, size, role, ariaLabel, solid }) {
  const sm = size === 'sm';
  const base = {
    display: 'inline-flex', alignItems: 'center', gap: sm ? 4 : 5,
    padding: sm ? '1px 7px' : '3px 9px',
    borderRadius: 'var(--r-pill)',
    fontFamily: 'var(--f-display)', fontWeight: 'var(--fw-bold)',
    fontSize: sm ? 'var(--fs-xs)' : 'var(--fs-sm)',
    lineHeight: 1.4, whiteSpace: 'nowrap',
  };
  const style = solid
    ? { ...base, background: entityHsl(entity), color: '#fff' } // .e-bg pattern (esento)
    : { ...base, background: entityHsl(entity, 0.12), color: entityHsl(entity) };
  return (
    <span style={style} role={role} aria-label={ariaLabel}>
      {icon ? <span aria-hidden="true">{icon}</span> : null}
      <span>{children}</span>
    </span>
  );
}

/* ─── ConnectionBar (DEC-2: counts = MOCKUP DATA) ───────────────────────── */
function ConnectionBar() {
  // DEC-2 (lockata 2026-06-22): counts = MOCKUP DATA; FE wire via
  // useHouseRulesMetadata({ gameId }) hook in follow-up issue post-#2027 handoff.
  // NO hardcoded counts in production — replace with hook return value.
  const connections = [
    { entityType: 'agent',   count: 2,  label: 'Rules attive',  icon: '🤖', isEmpty: false }, // MOCKUP PLACEHOLDER
    { entityType: 'game',    count: 1,  label: 'Gioco target',  icon: '🎲', isEmpty: false }, // MOCKUP PLACEHOLDER
    { entityType: 'session', count: 8,  label: 'Applicate in',  icon: '🎯', isEmpty: false }, // MOCKUP PLACEHOLDER
    { entityType: 'kb',      count: 12, label: 'Sostituiscono', icon: '📚', isEmpty: false }, // MOCKUP PLACEHOLDER
  ];
  return (
    <div style={{
      display: 'flex', gap: 'var(--s-2)', padding: 'var(--s-2) var(--s-4)',
      borderBottom: '1px solid var(--border-light)', flexWrap: 'wrap',
    }}>
      {connections.map((c) => {
        const empty = c.isEmpty;
        return (
          <div
            key={c.entityType}
            aria-label={empty ? c.label : `${c.label}: ${c.count}`}
            style={{
              display: 'inline-flex', alignItems: 'center', gap: 5,
              padding: '3px 9px', borderRadius: 999,
              background: empty ? 'transparent' : entityHsl(c.entityType, 0.1),
              color: entityHsl(c.entityType),
              border: empty ? `1px dashed ${entityHsl(c.entityType, 0.6)}` : '1px solid transparent',
              opacity: empty ? 0.6 : 1,
              fontFamily: 'var(--f-mono)', fontSize: 'var(--fs-xs)', fontWeight: 'var(--fw-bold)',
              whiteSpace: 'nowrap', flexShrink: 0,
            }}
          >
            <span aria-hidden="true">{empty ? '＋' : c.icon}</span>
            {!empty && <strong style={{ fontFamily: 'var(--f-display)' }}>{c.count}</strong>}
            <span style={{ color: 'var(--text-muted)', fontWeight: 'var(--fw-med)' }}>{c.label}</span>
          </div>
        );
      })}
    </div>
  );
}

/* ─── Drawer header (tutti gli stati lo renderizzano) ───────────────────── */
function DrawerHeader({ viewport, onClose }) {
  return (
    <div>
      {viewport === 'mobile' && (
        <div aria-hidden="true" style={{
          width: 40, height: 4, borderRadius: 2, background: 'var(--border-strong)',
          margin: '8px auto 2px',
        }} />
      )}
      <div style={{
        display: 'flex', alignItems: 'center', gap: 'var(--s-3)',
        padding: 'var(--s-3) var(--s-4)',
      }}>
        <div aria-hidden="true" style={{
          width: 36, height: 36, borderRadius: 'var(--r-md)', flexShrink: 0,
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          background: entityHsl('agent', 0.15), fontSize: 19,
        }}>🤝</div>
        <h2 id="drawer-title" style={{
          flex: 1, minWidth: 0, fontFamily: 'var(--f-display)', fontSize: 'var(--fs-xl)',
          fontWeight: 'var(--fw-bold)', color: 'var(--text)',
        }}>House rules</h2>
        <EntityChip entity="game" icon="🎲">Tainted Grail</EntityChip>
        <button
          type="button"
          aria-label="Chiudi drawer house rule"
          onClick={onClose}
          style={{
            width: 30, height: 30, borderRadius: '50%', border: 'none', flexShrink: 0,
            background: 'var(--bg-muted)', color: 'var(--text-sec)', fontSize: 13,
          }}
        >✕</button>
      </div>
    </div>
  );
}

/* ─── Tabs (animated underline, a11y tablist) ───────────────────────────── */
function Tabs({ active, collapsedTab1 }) {
  const tabs = [
    { key: 'create', label: 'Crea', id: 'tab-create' },
    { key: 'list', label: 'Le tue rules', id: 'tab-list' },
  ];
  return (
    <div role="tablist" aria-label="Sezioni house rules" style={{
      display: 'flex', padding: '0 var(--s-4)', gap: 'var(--s-4)',
      borderBottom: '1px solid var(--border-light)',
    }}>
      {tabs.map((t) => {
        const sel = active === t.key;
        const hidden = collapsedTab1 && t.key === 'create';
        return (
          <button
            key={t.key}
            role="tab"
            id={t.id}
            aria-selected={sel}
            aria-controls={`panel-${t.key}`}
            type="button"
            style={{
              padding: 'var(--s-3) 2px', background: 'transparent', border: 'none',
              fontFamily: 'var(--f-display)', fontSize: 'var(--fs-base)',
              fontWeight: 'var(--fw-bold)',
              color: sel ? entityHsl('agent') : 'var(--text-muted)',
              opacity: hidden ? 0.4 : 1,
              borderBottom: `2px solid ${sel ? entityHsl('agent') : 'transparent'}`,
              marginBottom: -1, position: 'relative',
            }}
          >
            {t.label}
          </button>
        );
      })}
    </div>
  );
}

/* ─── kicker mono label ─── */
function Kicker({ children, color }) {
  return (
    <div style={{
      fontFamily: 'var(--f-mono)', fontSize: 'var(--fs-xs)', fontWeight: 'var(--fw-bold)',
      textTransform: 'uppercase', letterSpacing: '0.08em',
      color: color || 'var(--text-muted)', marginBottom: 'var(--s-1)',
    }}>{children}</div>
  );
}

/* ─── Tab 1 — Crea house rule ───────────────────────────────────────────── */
function CreateTab({ mode, value, saving }) {
  const isEdit = mode === 'edit';
  const len = (value || '').length;
  const enabled = len >= 10 && !saving;
  return (
    <div
      id="panel-create"
      role="tabpanel"
      aria-labelledby="tab-create"
      style={{ display: 'flex', flexDirection: 'column', gap: 'var(--s-3)' }}
    >
      <Kicker color={entityHsl('agent')}>
        {isEdit ? 'Modifica house rule' : 'Crea house rule'}
      </Kicker>

      {/* Sezione 1 — Domanda originale (read-only echo) */}
      <div style={{
        background: 'var(--bg-sunken)', border: '1px solid var(--border-light)',
        borderRadius: 'var(--r-md)', padding: 'var(--s-3)',
      }}>
        <Kicker>Domanda</Kicker>
        <p style={{
          margin: 0, fontSize: 'var(--fs-md)', color: 'var(--text)',
          fontStyle: 'italic', lineHeight: 'var(--lh-snug)',
        }}>“{ORIGINAL_Q}”</p>
        <div style={{
          marginTop: 'var(--s-2)', fontFamily: 'var(--f-mono)', fontSize: 'var(--fs-xs)',
          color: 'var(--text-muted)',
        }}>Posto da Marco · 14 min fa</div>
      </div>

      {/* Sezione 2 — Regola ufficiale (citation) */}
      <div style={{
        background: entityHsl('kb', 0.04), border: '1px solid var(--border-light)',
        borderRadius: 'var(--r-md)', padding: 'var(--s-3)',
      }}>
        <Kicker color={entityHsl('kb')}>Regola ufficiale — poco chiara</Kicker>
        <p style={{
          margin: '0 0 var(--s-2)', fontSize: 'var(--fs-base)', color: 'var(--text-sec)',
          fontStyle: 'italic', lineHeight: 'var(--lh-body)',
        }}>{MANUAL_EXCERPT}</p>
        <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--s-2)', flexWrap: 'wrap' }}>
          <EntityChip
            entity="kb" icon="📖" size="sm"
            role="link" ariaLabel="Riferimento manuale pagina 34"
          >p.34 — Stato del gruppo</EntityChip>
        </div>
        <div style={{
          marginTop: 'var(--s-2)', fontSize: 'var(--fs-xs)', color: entityHsl('event'),
          fontWeight: 'var(--fw-semi)',
        }}>⚠️ La nostra confidence per questa regola è 0.42 (bassa)</div>
      </div>

      {/* Sezione 3 — La nostra regola (input principale) */}
      <div>
        <label htmlFor="rule-input" style={{
          display: 'block', fontFamily: 'var(--f-display)', fontWeight: 'var(--fw-bold)',
          fontSize: 'var(--fs-md)', marginBottom: 'var(--s-1)', color: 'var(--text)',
        }}>La nostra regola</label>
        <div style={{ position: 'relative' }}>
          <textarea
            id="rule-input"
            rows={4}
            maxLength={280}
            readOnly
            value={value || ''}
            placeholder="Es: 'In caso di morte simultanea, chi ha più punti vita massimi diventa leader. Se pareggio, decide il giocatore con più sessioni completate.'"
            style={{
              width: '100%', resize: 'none', boxSizing: 'border-box',
              padding: 'var(--s-3)', paddingBottom: 'var(--s-6)',
              borderRadius: 'var(--r-md)', border: `1px solid ${len > 0 ? entityHsl('agent', 0.5) : 'var(--border)'}`,
              background: 'var(--bg-card)', color: 'var(--text)',
              fontFamily: 'var(--f-body)', fontSize: 'var(--fs-md)', lineHeight: 'var(--lh-body)',
              outline: 'none',
            }}
          />
          <span style={{
            position: 'absolute', right: 10, bottom: 8, fontFamily: 'var(--f-mono)',
            fontSize: 'var(--fs-xs)', color: len > 260 ? entityHsl('event') : 'var(--text-muted)',
          }}>{len} / 280</span>
        </div>
        <div style={{ marginTop: 'var(--s-1)', fontSize: 'var(--fs-xs)', color: 'var(--text-muted)' }}>
          Sii specifico — tutti i casi futuri useranno questa regola
        </div>
      </div>

      {/* Sezione 4 — Tag opzionali (collapsed default) */}
      <div>
        <button type="button" style={{
          background: 'transparent', border: 'none', color: entityHsl('agent'),
          fontFamily: 'var(--f-display)', fontWeight: 'var(--fw-bold)', fontSize: 'var(--fs-sm)',
          padding: 0, display: 'inline-flex', alignItems: 'center', gap: 4,
        }} aria-expanded="false">＋ Aggiungi tag</button>
      </div>

      {/* CTA sticky */}
      <div style={{
        position: 'sticky', bottom: 0, marginTop: 'var(--s-2)',
        paddingTop: 'var(--s-2)', background: 'linear-gradient(to top, var(--bg-card) 70%, transparent)',
        display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 'var(--s-2)',
      }}>
        <button
          type="button"
          disabled={!enabled}
          aria-busy={saving ? 'true' : 'false'}
          style={{
            width: '100%', padding: 'var(--s-3)', borderRadius: 'var(--r-md)', border: 'none',
            background: enabled || saving ? entityHsl('agent') : entityHsl('agent', 0.3),
            color: '#fff', // .e-bg pattern (esento)
            fontFamily: 'var(--f-display)', fontWeight: 'var(--fw-bold)', fontSize: 'var(--fs-md)',
            cursor: enabled ? 'pointer' : 'not-allowed',
            pointerEvents: saving ? 'none' : 'auto',
            display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 'var(--s-2)',
          }}
        >
          {saving ? (
            <>
              <span className="hr-spinner" aria-hidden="true" />
              <span>Salvataggio…</span>
              <span style={{
                position: 'absolute', width: 1, height: 1, overflow: 'hidden',
                clip: 'rect(0 0 0 0)', whiteSpace: 'nowrap',
              }}>Salvataggio house rule in corso</span>
            </>
          ) : (
            isEdit ? 'Aggiorna house rule' : 'Salva house rule per Tainted Grail'
          )}
        </button>
        <button type="button" style={{
          background: 'transparent', border: 'none', color: 'var(--text-muted)',
          fontSize: 'var(--fs-sm)', fontWeight: 'var(--fw-semi)',
        }}>Annulla</button>
      </div>
    </div>
  );
}

/* ─── Rule card (Tab 2) ─────────────────────────────────────────────────── */
function RuleCard({ rule, highlight }) {
  return (
    <div style={{
      border: highlight ? `2px solid ${entityHsl('agent', 0.6)}` : '1px solid var(--border-light)',
      borderRadius: 'var(--r-md)', padding: 'var(--s-3)', background: 'var(--bg-card)',
      display: 'flex', flexDirection: 'column', gap: 'var(--s-2)',
    }}>
      <div>
        <Kicker color={entityHsl('agent')}>Regola</Kicker>
        <div style={{
          fontFamily: 'var(--f-display)', fontWeight: 'var(--fw-bold)', fontSize: 'var(--fs-md)',
          color: 'var(--text)', marginBottom: 2,
        }}>{rule.title}</div>
        <p style={{
          margin: 0, fontSize: 'var(--fs-base)', color: 'var(--text-sec)',
          lineHeight: 'var(--lh-body)',
          display: '-webkit-box', WebkitLineClamp: 3, WebkitBoxOrient: 'vertical', overflow: 'hidden',
        }}>{rule.body}</p>
      </div>
      <div style={{
        fontSize: 'var(--fs-xs)', color: 'var(--text-muted)',
        whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis',
      }}>📚 Originata da: “{rule.origin}”</div>
      <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--s-2)', flexWrap: 'wrap' }}>
        <EntityChip entity="agent" icon="🤖" size="sm">Agente regole</EntityChip>
        <EntityChip
          entity="kb" icon="📖" size="sm"
          role="link" ariaLabel={`Riferimento manuale ${rule.page}`}
        >{rule.page}</EntityChip>
      </div>
      <div style={{
        display: 'flex', gap: 'var(--s-4)', fontFamily: 'var(--f-mono)', fontSize: 'var(--fs-xs)',
        color: 'var(--text-muted)', flexWrap: 'wrap',
      }}>
        <span>Applicata {rule.applied} {rule.applied === 1 ? 'volta' : 'volte'}</span>
        <span>Creata {rule.ago} da {rule.by}</span>
      </div>
      <div style={{ display: 'flex', gap: 'var(--s-2)', borderTop: '1px solid var(--border-light)', paddingTop: 'var(--s-2)' }}>
        <button type="button" aria-label="Modifica house rule" style={iconBtn}>✏️ <span style={iconBtnLbl}>Modifica</span></button>
        <button type="button" aria-label="Elimina house rule" style={iconBtn}>🗑️ <span style={iconBtnLbl}>Elimina</span></button>
        <button type="button" aria-label="Condividi house rule" style={iconBtn}>📤 <span style={iconBtnLbl}>Condividi</span></button>
      </div>
    </div>
  );
}
const iconBtn = {
  flex: 1, padding: 'var(--s-2)', borderRadius: 'var(--r-sm)', border: '1px solid var(--border-light)',
  background: 'var(--bg)', color: 'var(--text-sec)', fontFamily: 'var(--f-display)',
  fontWeight: 'var(--fw-semi)', fontSize: 'var(--fs-xs)',
  display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 4,
};
const iconBtnLbl = { fontSize: 'var(--fs-xs)' };

/* ─── Tab 2 — Le tue rules (list) ───────────────────────────────────────── */
function ListTab({ empty, highlightId }) {
  if (empty) {
    return (
      <div
        id="panel-list" role="tabpanel" aria-labelledby="tab-list"
        style={{ display: 'flex', flexDirection: 'column' }}
      >
        <div role="status" aria-live="polite" style={{
          display: 'flex', flexDirection: 'column', alignItems: 'center', textAlign: 'center',
          padding: 'var(--s-8) var(--s-4)', gap: 'var(--s-2)',
        }}>
          <div aria-hidden="true" style={{
            width: 64, height: 64, borderRadius: '50%', display: 'flex', alignItems: 'center',
            justifyContent: 'center', background: entityHsl('agent', 0.12), fontSize: 30,
          }}>🤝</div>
          <div style={{
            fontFamily: 'var(--f-display)', fontWeight: 'var(--fw-bold)', fontSize: 'var(--fs-lg)',
            color: 'var(--text)',
          }}>Nessuna house rule definita per Tainted Grail</div>
          <p style={{ margin: 0, fontSize: 'var(--fs-base)', color: 'var(--text-muted)', maxWidth: 280, lineHeight: 'var(--lh-body)' }}>
            Le rules personalizzate sostituiscono o estendono il manuale ufficiale per il vostro gruppo.
          </p>
          <button type="button" style={{
            marginTop: 'var(--s-2)', padding: 'var(--s-2) var(--s-4)', borderRadius: 'var(--r-md)',
            border: 'none', background: entityHsl('agent'), color: '#fff',
            fontFamily: 'var(--f-display)', fontWeight: 'var(--fw-bold)', fontSize: 'var(--fs-base)',
          }}>＋ Crea la prima rule</button>
        </div>
      </div>
    );
  }
  const Group = ({ name, emoji, entity, rules }) => (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--s-2)' }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--s-2)', marginTop: 'var(--s-1)' }}>
        <EntityChip entity={entity} icon={emoji}>{name}</EntityChip>
        <span style={{ fontFamily: 'var(--f-mono)', fontSize: 'var(--fs-xs)', color: 'var(--text-muted)' }}>
          {rules.length} {rules.length === 1 ? 'rule' : 'rules'}
        </span>
      </div>
      {rules.map((r) => <RuleCard key={r.id} rule={r} highlight={r.id === highlightId} />)}
    </div>
  );
  return (
    <div
      id="panel-list" role="tabpanel" aria-labelledby="tab-list"
      style={{ display: 'flex', flexDirection: 'column', gap: 'var(--s-4)' }}
    >
      <Group name="Tainted Grail" emoji="🎲" entity="game" rules={RULES_TG} />
      <Group name="ISS Vanguard" emoji="🎲" entity="game" rules={RULES_IV} />
    </div>
  );
}

/* ─── Toast (success / error) ───────────────────────────────────────────── */
function Toast({ kind, children, onRetry }) {
  const err = kind === 'error';
  return (
    <div
      role={err ? 'alert' : 'status'}
      aria-live={err ? 'assertive' : 'polite'}
      className="hr-toast"
      style={{
        position: 'absolute', left: 'var(--s-3)', right: 'var(--s-3)', bottom: 'var(--s-3)',
        zIndex: 'var(--z-toast)',
        background: err ? entityHsl('event') : entityHsl('agent'),
        color: '#fff', // .e-bg pattern (esento)
        borderRadius: 'var(--r-md)', padding: 'var(--s-3)', boxShadow: 'var(--shadow-lg)',
        display: 'flex', alignItems: 'center', gap: 'var(--s-2)',
        fontFamily: 'var(--f-body)', fontSize: 'var(--fs-sm)', fontWeight: 'var(--fw-semi)',
        lineHeight: 'var(--lh-snug)',
      }}
    >
      <span style={{ flex: 1 }}>{children}</span>
      {err && (
        <button
          type="button"
          aria-label="Riprova salvataggio house rule"
          onClick={onRetry}
          style={{
            background: 'rgba(255,255,255,0.22)', color: '#fff', border: 'none',
            borderRadius: 'var(--r-sm)', padding: '4px 10px',
            fontFamily: 'var(--f-display)', fontWeight: 'var(--fw-bold)', fontSize: 'var(--fs-xs)',
            flexShrink: 0,
          }}
        >Riprova</button>
      )}
    </div>
  );
}

/* ─── Offline banner ────────────────────────────────────────────────────── */
function OfflineBanner() {
  return (
    <div role="status" aria-live="polite" style={{
      display: 'flex', alignItems: 'center', gap: 'var(--s-2)',
      padding: 'var(--s-2) var(--s-4)',
      background: entityHsl('agent', 0.14), color: entityHsl('agent'),
      borderBottom: `1px solid ${entityHsl('agent', 0.3)}`,
      fontFamily: 'var(--f-body)', fontSize: 'var(--fs-sm)', fontWeight: 'var(--fw-bold)',
    }}>
      <span aria-hidden="true">📴</span>
      <span>Offline · Salveremo questa rule quando torni online</span>
    </div>
  );
}

/* ─── Delete confirm modal (state 09 + 13 extends) ──────────────────────── */
function DeleteModal({ impossible }) {
  return (
    <div style={{
      position: 'absolute', inset: 0, zIndex: 'var(--z-modal)',
      display: 'flex', alignItems: 'flex-end', justifyContent: 'center',
      background: 'rgba(0,0,0,0.45)', padding: 'var(--s-4)',
    }}>
      <div
        role="dialog" aria-modal="true" aria-labelledby="delete-confirm-title"
        aria-describedby={impossible ? 'delete-impossible-counter' : undefined}
        style={{
          width: '100%', maxWidth: 360, background: 'var(--bg-card)',
          borderRadius: 'var(--r-lg)', padding: 'var(--s-5)', boxShadow: 'var(--shadow-lg)',
          display: 'flex', flexDirection: 'column', gap: 'var(--s-3)',
        }}
      >
        <div aria-hidden="true" style={{
          width: 44, height: 44, borderRadius: '50%', alignSelf: 'flex-start',
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          background: entityHsl('event', 0.14), fontSize: 22,
        }}>🗑️</div>
        <h3 id="delete-confirm-title" style={{
          fontFamily: 'var(--f-display)', fontSize: 'var(--fs-lg)', fontWeight: 'var(--fw-bold)',
          color: 'var(--text)',
        }}>Elimina house rule?</h3>
        <p style={{ margin: 0, fontSize: 'var(--fs-base)', color: 'var(--text-sec)', lineHeight: 'var(--lh-body)' }}>
          Le risposte future tornano al manuale ufficiale.
        </p>

        {impossible && (
          <div style={{
            background: entityHsl('event', 0.08), border: `1px solid ${entityHsl('event', 0.3)}`,
            borderRadius: 'var(--r-md)', padding: 'var(--s-3)',
            display: 'flex', flexDirection: 'column', gap: 'var(--s-2)',
          }}>
            <div id="delete-impossible-counter" style={{
              fontSize: 'var(--fs-base)', color: entityHsl('event'), fontWeight: 'var(--fw-bold)',
              display: 'flex', alignItems: 'center', gap: 6,
            }}>
              <span aria-hidden="true">⚠️</span>
              Questa rule è applicata in 5 sessioni attive
            </div>
            <label style={{
              display: 'flex', alignItems: 'flex-start', gap: 'var(--s-2)',
              fontSize: 'var(--fs-sm)', color: 'var(--text-sec)', cursor: 'pointer',
            }}>
              <input type="checkbox" defaultChecked style={{ marginTop: 2, accentColor: entityHsl('agent') }} />
              <span><strong style={{ color: 'var(--text)' }}>Disattiva invece di eliminare</strong> (preserva storico)</span>
            </label>
          </div>
        )}

        <div style={{ display: 'flex', gap: 'var(--s-2)', marginTop: 'var(--s-1)' }}>
          <button type="button" style={{
            flex: 1, padding: 'var(--s-3)', borderRadius: 'var(--r-md)', border: '1px solid var(--border)',
            background: 'var(--bg-muted)', color: 'var(--text-sec)',
            fontFamily: 'var(--f-display)', fontWeight: 'var(--fw-bold)', fontSize: 'var(--fs-base)',
          }}>Annulla</button>
          <button type="button" style={{
            flex: 1, padding: 'var(--s-3)', borderRadius: 'var(--r-md)', border: 'none',
            background: impossible ? entityHsl('agent') : entityHsl('event'), color: '#fff',
            fontFamily: 'var(--f-display)', fontWeight: 'var(--fw-bold)', fontSize: 'var(--fs-base)',
          }}>{impossible ? 'Disattiva' : 'Elimina'}</button>
        </div>
      </div>
    </div>
  );
}

/* ─── Drawer shell ──────────────────────────────────────────────────────── */
/* config: { tab, mode, value, saving, empty, highlightId, collapsedTab1,
            toast:{kind,node}, offline, deleteModal, dragging } */
function Drawer({ viewport, config }) {
  const c = config || {};
  const tab = c.tab || 'create';
  const isMobile = viewport === 'mobile';

  const panelStyle = isMobile
    ? {
        position: 'absolute', left: 0, right: 0, bottom: 0,
        maxHeight: '92%',
        transform: c.dragging ? 'translateY(64px)' : 'none',
        borderRadius: 'var(--r-2xl) var(--r-2xl) 0 0',
        boxShadow: 'var(--shadow-drawer)',
      }
    : {
        position: 'absolute', top: 0, right: 0, bottom: 0, width: 380,
        borderLeft: '1px solid var(--border)',
        boxShadow: 'var(--shadow-lg)',
      };

  return (
    <div
      role="dialog" aria-modal="true" aria-labelledby="drawer-title"
      className="hr-drawer"
      style={{
        ...panelStyle, zIndex: 'var(--z-drawer)',
        background: 'var(--bg-card)', display: 'flex', flexDirection: 'column',
        overflow: 'hidden',
      }}
    >
      {c.offline && <OfflineBanner />}
      <DrawerHeader viewport={viewport} />
      <ConnectionBar />
      <Tabs active={tab} collapsedTab1={c.collapsedTab1} />

      <div style={{
        flex: 1, overflowY: 'auto', padding: 'var(--s-4)', position: 'relative',
        minHeight: 0,
      }}>
        {/* state-05: Tab 1 collapse (height auto→0, opacity 1→0) reso come collapsed */}
        {c.collapsedTab1 && (
          <div aria-hidden="true" className="hr-collapse" style={{ height: 0, opacity: 0, overflow: 'hidden' }} />
        )}
        {tab === 'create' && !c.collapsedTab1 && (
          <>
            <CreateTab mode={c.mode} value={c.value} saving={c.saving} />
            {c.offline && (
              <div style={{ marginTop: 'var(--s-2)', fontSize: 'var(--fs-xs)', color: 'var(--text-muted)', textAlign: 'center' }}>
                Salveremo automaticamente al ritorno della connessione
              </div>
            )}
          </>
        )}
        {tab === 'list' && <ListTab empty={c.empty} highlightId={c.highlightId} />}
      </div>

      {c.toast && <Toast kind={c.toast.kind} onRetry={() => {}}>{c.toast.node}</Toast>}
      {c.deleteModal && <DeleteModal impossible={c.deleteModal === 'impossible'} />}
    </div>
  );
}

/* ─── Parent screen (state-01 drawer closed) ────────────────────────────── */
function ParentChat() {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%', background: 'var(--bg)' }}>
      {/* Tab Chat header — EntityChip "Rules attive · 2" = entry-point secondary (DEC-1) */}
      <div style={{
        display: 'flex', alignItems: 'center', gap: 'var(--s-2)',
        padding: 'var(--s-3) var(--s-4)', borderBottom: '1px solid var(--border-light)',
        background: 'var(--bg-card)', flexShrink: 0,
      }}>
        <span style={{ flex: 1, fontFamily: 'var(--f-display)', fontWeight: 'var(--fw-bold)', fontSize: 'var(--fs-md)' }}>
          💬 Chat · Tainted Grail
        </span>
        {/* DEC-1 entry-point secondary → apre drawer con defaultTab='list' */}
        <button type="button" aria-label="Apri house rules attive" style={{ background: 'none', border: 'none', padding: 0 }}>
          <EntityChip entity="agent" icon="🤖">Rules attive · 2</EntityChip>
        </button>
      </div>

      {/* Q&A low-confidence */}
      <div style={{ flex: 1, overflowY: 'auto', padding: 'var(--s-4)', display: 'flex', flexDirection: 'column', gap: 'var(--s-3)' }}>
        <div style={{
          alignSelf: 'flex-end', maxWidth: '85%', padding: 'var(--s-3)',
          borderRadius: 'var(--r-md) var(--r-md) var(--r-xs) var(--r-md)',
          background: 'var(--bg-muted)', fontSize: 'var(--fs-base)', color: 'var(--text)',
        }}>{ORIGINAL_Q}</div>

        <div style={{
          alignSelf: 'flex-start', maxWidth: '92%', padding: 'var(--s-3)',
          borderRadius: 'var(--r-md) var(--r-md) var(--r-md) var(--r-xs)',
          background: 'var(--bg-card)', border: '1px solid var(--border)',
          display: 'flex', flexDirection: 'column', gap: 'var(--s-2)',
        }}>
          <div style={{ fontSize: 'var(--fs-base)', color: 'var(--text-sec)', fontStyle: 'italic', lineHeight: 'var(--lh-body)' }}>
            {MANUAL_EXCERPT}
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--s-2)', flexWrap: 'wrap' }}>
            <EntityChip entity="kb" icon="📖" size="sm" role="link" ariaLabel="Riferimento manuale pagina 34">p.34</EntityChip>
            <span style={{
              fontFamily: 'var(--f-mono)', fontSize: 'var(--fs-xs)', fontWeight: 'var(--fw-bold)',
              color: entityHsl('event'), background: entityHsl('event', 0.12),
              padding: '2px 7px', borderRadius: 'var(--r-pill)',
            }}>Confidence 0.42 · bassa</span>
          </div>
          {/* DEC-1 entry-point primary → apre drawer con defaultTab='create' */}
          <button type="button" style={{
            marginTop: 'var(--s-1)', alignSelf: 'flex-start',
            padding: 'var(--s-2) var(--s-3)', borderRadius: 'var(--r-md)', border: 'none',
            background: entityHsl('agent'), color: '#fff',
            fontFamily: 'var(--f-display)', fontWeight: 'var(--fw-bold)', fontSize: 'var(--fs-base)',
            display: 'inline-flex', alignItems: 'center', gap: 6,
          }}>🤝 Definisci house rule</button>
        </div>

        <div style={{
          fontSize: 'var(--fs-xs)', color: 'var(--text-muted)', textAlign: 'center',
          fontFamily: 'var(--f-mono)', marginTop: 'auto', paddingTop: 'var(--s-3)',
        }}>
          DEC-1 · 2 entry-point: risposta low-confidence (primary, defaultTab=create) ·
          chip “Rules attive · 2” (secondary, defaultTab=list)
        </div>
      </div>
    </div>
  );
}

/* ============================================================================
   13 STATI CANONICI — JSX exports
   Ognuno restituisce il nodo INTERNO al frame (drawer panel o parent screen).
   Il frame chrome (device / browser + backdrop + theme) è fornito dal Gallery.
   ========================================================================== */

// 1 — Drawer chiuso; trigger nel parent screen C.
function State01_DrawerClosed() {
  return <ParentChat />;
}

// 2 — Tab Crea, mode=create, textarea vuota, CTA disabled.
function State02_CreateEmpty({ viewport }) {
  return <Drawer viewport={viewport} config={{ tab: 'create', mode: 'create', value: '' }} />;
}

// 3 — Tab Crea, mode=create, ~150 char, CTA enabled.
function State03_CreateTyped({ viewport }) {
  return <Drawer viewport={viewport} config={{ tab: 'create', mode: 'create', value: TYPED_DRAFT }} />;
}

// 4 — Tab Crea, spinner inline, CTA disabled + aria-busy.
function State04_CreateSaving({ viewport }) {
  return <Drawer viewport={viewport} config={{ tab: 'create', mode: 'create', value: TYPED_DRAFT, saving: true }} />;
}

// 5 — Transition Tab 1 → Tab 2 (MAJ-1 timing). Frame intermedio t≈600ms:
//     Tab 1 collapsed (height auto→0, opacity 1→0 · 300-600ms) +
//     Tab 2 visible con la rule appena creata in cima +
//     highlight border 2px entityHsl('agent',0.6) (600-2600ms fade) +
//     toast entity=agent visible (200-500ms in, autodismiss 3000ms).
function State05_CreateSavedSuccess({ viewport }) {
  return (
    <Drawer
      viewport={viewport}
      config={{
        tab: 'list', collapsedTab1: true, highlightId: 'r1',
        toast: { kind: 'success', node: '✅ House rule salvata · Marco e gli altri vedranno questa regola nelle prossime risposte' },
      }}
    />
  );
}

// 6 — Tab Le tue rules con 3 rules (TG 2 + ISS Vanguard 1).
function State06_List3Rules({ viewport }) {
  return <Drawer viewport={viewport} config={{ tab: 'list' }} />;
}

// 7 — Tab Le tue rules empty + CTA "+ Crea la prima rule".
function State07_ListEmpty({ viewport }) {
  return <Drawer viewport={viewport} config={{ tab: 'list', empty: true }} />;
}

// 8 — Tab Crea, mode=edit, pre-fill (DEC-3 in-place, NON modal).
function State08_CreateEditModePrefilled({ viewport }) {
  return <Drawer viewport={viewport} config={{ tab: 'create', mode: 'edit', value: RULES_TG[0].body }} />;
}

// 9 — Modal Elimina con confirm/cancel.
function State09_ListDeleteConfirm({ viewport }) {
  return <Drawer viewport={viewport} config={{ tab: 'list', deleteModal: 'confirm' }} />;
}

// 10 — Mobile bottom-sheet drag-to-close mid-gesture (snapshot statico).
function State10_MobileBottomSheetDrag({ viewport }) {
  return <Drawer viewport={viewport} config={{ tab: 'list', dragging: viewport === 'mobile' }} />;
}

// 11 — MAJ-2: save error, toast rosso entity=event + retry + input preservato.
function State11_SaveError({ viewport }) {
  return (
    <Drawer
      viewport={viewport}
      config={{
        tab: 'create', mode: 'create', value: TYPED_DRAFT,
        toast: { kind: 'error', node: '❌ Impossibile salvare la house rule · Riprova' },
      }}
    />
  );
}

// 12 — MAJ-2: offline banner entity=agent amber + CTA disabled.
function State12_OfflineBanner({ viewport }) {
  return <Drawer viewport={viewport} config={{ tab: 'create', mode: 'create', value: TYPED_DRAFT, offline: true }} />;
}

// 13 — MAJ-2: delete impossible + counter "5 sessioni attive" + opt-out checkbox.
function State13_DeleteImpossibleSoftDeleteOptOut({ viewport }) {
  return <Drawer viewport={viewport} config={{ tab: 'list', deleteModal: 'impossible' }} />;
}

/* ─── State registry (anchor id + export + label) ───────────────────────── */
const STATES = [
  { n: '01', id: 'state-01-drawer-closed', name: 'Drawer chiuso', Comp: State01_DrawerClosed, closed: true,
    desc: 'Drawer chiuso. Trigger nel Tab Chat del parent screen — DEC-1: risposta low-confidence (entry primary, defaultTab=create) + chip “Rules attive · 2” (entry secondary, defaultTab=list).' },
  { n: '02', id: 'state-02-create-empty', name: 'Crea · vuoto', Comp: State02_CreateEmpty,
    desc: 'Tab Crea, mode=create. Textarea vuota, counter 0/280, CTA “Salva” disabled (< 10 caratteri).' },
  { n: '03', id: 'state-03-create-typed', name: 'Crea · digitato', Comp: State03_CreateTyped,
    desc: 'Tab Crea ~150 caratteri. Counter live, bordo textarea entity=agent, CTA enabled.' },
  { n: '04', id: 'state-04-create-saving', name: 'Crea · salvataggio', Comp: State04_CreateSaving,
    desc: 'Spinner inline sostituisce la copy della CTA. CTA disabled (pointer-events none) + aria-busy + sr-span.' },
  { n: '05', id: 'state-05-create-saved-success', name: 'Salvato (transition)', Comp: State05_CreateSavedSuccess,
    desc: 'MAJ-1 — frame intermedio t≈600ms: Tab 1 collapsed (height auto→0) + switch a Tab 2 con rule in cima + highlight border 2px entityHsl(agent,0.6) (fade 600-2600ms) + toast entity=agent (autodismiss 3000ms).' },
  { n: '06', id: 'state-06-list-3-rules', name: 'Le tue rules · 3', Comp: State06_List3Rules,
    desc: 'Tab Le tue rules con 3 rules: Tainted Grail (2) + ISS Vanguard (1), group header per gioco.' },
  { n: '07', id: 'state-07-list-empty', name: 'Le tue rules · empty', Comp: State07_ListEmpty,
    desc: 'Empty state illustrato (role=status aria-live=polite) + CTA “＋ Crea la prima rule”.' },
  { n: '08', id: 'state-08-create-edit-mode-prefilled', name: 'Crea · edit', Comp: State08_CreateEditModePrefilled,
    desc: 'DEC-3 — edit in-place riusa Tab 1 con mode=edit. Header “Modifica house rule”, CTA “Aggiorna house rule”, textarea pre-popolata. NO modal separato.' },
  { n: '09', id: 'state-09-list-delete-confirm', name: 'Elimina · confirm', Comp: State09_ListDeleteConfirm,
    desc: 'Modal Elimina (role=dialog aria-modal): “Elimina house rule? Le risposte future tornano al manuale ufficiale.” confirm + cancel.' },
  { n: '10', id: 'state-10-mobile-bottom-sheet-drag', name: 'Drag-to-close', Comp: State10_MobileBottomSheetDrag,
    desc: 'Mobile 375 — bottom-sheet drag-to-close mid-gesture (snapshot statico, sheet translata + backdrop). Su desktop side-panel la gesture non si applica.' },
  { n: '11', id: 'state-11-save-error', name: 'Save error', Comp: State11_SaveError,
    desc: 'MAJ-2 — toast rosso entity=event (role=alert) + button “Riprova” + dismiss. Textarea preserva l’input, CTA “Salva” re-enabled.' },
  { n: '12', id: 'state-12-offline-banner', name: 'Offline banner', Comp: State12_OfflineBanner,
    desc: 'MAJ-2 — banner top entity=agent amber (role=status). CTA “Salva” disabled + helper “Salveremo automaticamente al ritorno della connessione”.' },
  { n: '13', id: 'state-13-delete-impossible-soft-delete-opt-out', name: 'Delete impossibile', Comp: State13_DeleteImpossibleSoftDeleteOptOut,
    desc: 'MAJ-2 — modal Elimina extends con counter “applicata in 5 sessioni attive” (aria-describedby) + checkbox opt-out “Disattiva invece di eliminare (preserva storico)”, default checked.' },
];

/* ─── Frame chrome ──────────────────────────────────────────────────────── */
// Mini play-screen backdrop (dimmed) dietro al drawer.
function Backdrop({ closed }) {
  if (closed) return null;
  return (
    <div aria-hidden="true" style={{ position: 'absolute', inset: 0, overflow: 'hidden' }}>
      <div style={{ padding: 'var(--s-4)', opacity: 0.5 }}>
        <div style={{
          height: 92, borderRadius: 'var(--r-md)', marginBottom: 'var(--s-3)',
          background: `linear-gradient(135deg, ${entityHsl('game', 0.8)}, ${entityHsl('session', 0.5)})`,
          display: 'flex', alignItems: 'flex-end', padding: 'var(--s-2)', color: '#fff',
          fontFamily: 'var(--f-display)', fontWeight: 'var(--fw-bold)',
        }}>§214 · Tainted Grail</div>
        <div style={{ height: 10, width: '90%', background: 'var(--bg-muted)', borderRadius: 4, marginBottom: 8 }} />
        <div style={{ height: 10, width: '80%', background: 'var(--bg-muted)', borderRadius: 4, marginBottom: 8 }} />
        <div style={{ height: 10, width: '85%', background: 'var(--bg-muted)', borderRadius: 4 }} />
      </div>
      <div style={{ position: 'absolute', inset: 0, background: 'rgba(0,0,0,0.4)' }} />
    </div>
  );
}

function PhoneFrame({ theme, state }) {
  const Comp = state.Comp;
  return (
    <div className="frame-wrap">
      <div className="frame-cap">375 · mobile · {theme}</div>
      <div className="device-phone" data-theme={theme}>
        <div className="phone-sbar">
          <span>9:41</span>
          <span aria-hidden="true" style={{ letterSpacing: 1 }}>▦ ▮ 100%</span>
        </div>
        <div style={{ position: 'relative', flex: 1, overflow: 'hidden', background: 'var(--bg)' }}>
          {state.closed ? <Comp viewport="mobile" /> : <><Backdrop /><Comp viewport="mobile" /></>}
        </div>
      </div>
    </div>
  );
}

function DesktopFrame({ theme, state }) {
  const Comp = state.Comp;
  return (
    <div className="frame-wrap">
      <div className="frame-cap">1440 · desktop · side-panel 380px · {theme}</div>
      <div className="device-desktop" data-theme={theme}>
        <div className="win-bar">
          <span className="dot r" /><span className="dot y" /><span className="dot g" />
          <span className="win-url">meepleai.app/gamebook/tainted-grail/play</span>
        </div>
        <div style={{ position: 'relative', flex: 1, overflow: 'hidden', background: 'var(--bg)' }}>
          {state.closed
            ? <div style={{ position: 'absolute', inset: 0, display: 'flex' }}>
                <div style={{ flex: 1, position: 'relative' }}><Backdrop /></div>
                <div style={{ width: 380, position: 'relative', borderLeft: '1px solid var(--border)' }}>
                  <Comp viewport="desktop" />
                </div>
              </div>
            : <><Backdrop /><Comp viewport="desktop" /></>}
        </div>
      </div>
    </div>
  );
}

/* ─── State section (anchor + 4-frame matrix) ───────────────────────────── */
function StateSection({ state }) {
  return (
    <section id={state.id} className="state-section">
      <div className="sec-head">
        <span className="sec-label">{state.n} · {state.id}</span>
        <h2>{state.name}</h2>
        <p className="sec-desc">{state.desc}</p>
      </div>
      <div className="matrix">
        <PhoneFrame theme="light" state={state} />
        <PhoneFrame theme="dark" state={state} />
        <DesktopFrame theme="light" state={state} />
        <DesktopFrame theme="dark" state={state} />
      </div>
    </section>
  );
}

/* ─── Gallery App ───────────────────────────────────────────────────────── */
function Gallery() {
  const [theme, setTheme] = useState(
    document.documentElement.getAttribute('data-theme') || 'light'
  );
  const toggle = () => {
    const next = theme === 'light' ? 'dark' : 'light';
    document.documentElement.setAttribute('data-theme', next);
    try { localStorage.setItem('mai-theme', next); } catch (e) {}
    setTheme(next);
  };
  return (
    <>
      <nav className="hr-nav">
        <span className="hr-brand"><span className="hr-mark">M</span> House rules · §F</span>
        <div className="hr-links">
          {STATES.map((s) => (
            <a key={s.id} href={`#${s.id}`} title={s.name}>{s.n}</a>
          ))}
        </div>
        <button type="button" className="hr-toggle" onClick={toggle}>
          🌗 {theme === 'dark' ? 'Dark' : 'Light'}
        </button>
      </nav>

      <header className="hr-hero">
        <h1>House Rule Drawer <span style={{ color: entityHsl('agent') }}>§F</span></h1>
        <p>
          Bottom-sheet drawer dentro <code>/gamebook/[gameId]/play</code> — tabbed mobile / side-panel
          desktop 380px. 13 stati canonici × 2 temi × 2 viewport. Decisioni lockate post-#2045:
          <strong> DEC-1</strong> trigger Option A · <strong>DEC-2</strong> ConnectionBar counts = mockup data ·
          <strong> DEC-3</strong> edit in-place. MAJ-1 timing in state-05, MAJ-2 error/offline/soft-delete (11–13).
        </p>
        <p className="hr-note">
          Nota: il drawer house rule <strong>non è SSE-driven</strong> — il pattern skip-state ghost (Mockup C)
          non si applica qui ed è volutamente omesso.
        </p>
      </header>

      <main className="hr-stage">
        {STATES.map((s) => <StateSection key={s.id} state={s} />)}
      </main>

      <footer className="hr-foot">
        SP6 · sp6-libro-game-house-rule · 13 stati · tokens.css only · entityHsl() · AA light+dark · prefers-reduced-motion honored
      </footer>
    </>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(<Gallery />);
