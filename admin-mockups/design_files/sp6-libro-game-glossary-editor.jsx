/* ===================================================================
   SP6 Libro Game — Glossary Editor (sp6-libro-game-glossary-editor.jsx)
   Coverage: scenario N3.5 inline pill edit (design doc 2026-05-07)
             + spec sezione 1c context-aware glossary (2026-05-23).
   Modal sopra il viewer D translation-viewer quando Aaron tappa una
   glossary pill cliccabile inline al testo del paragrafo §147.

   Narrative: "Sera con i ragazzi · Runa di Ardenel · §147"
   Books esempio: Press Start + Rules Game

   Stati base:
     state-01-edit-pristine     — Sentinel → soldato di guardia, Salva disabled
     state-02-edited            — input dirty → "punto di osservazione strategica"
     state-03-save-error        — banner amber + Retry, value preservato
     state-04-collision         — banner red, [Sovrascrivi] + [Cambia traduzione]
     state-01-desktop           — modal centered 480px
     state-04-desktop-conflict  — modal centered + side panel 200px "Ferrigni → Razziatori"

   ─────────────────────────────────────────────────────────────────────
   NUOVI sub-screen (sezione 1c context-aware glossary):
   ─────────────────────────────────────────────────────────────────────
     state-05-conflict-dialog       — Modal w/ 3-CTA radiogroup, side-by-side
                                       definitions (esistente vs nuova)
     state-06-bulk-import-card      — Inline card + multi-select review modal
     state-07-variant-expansion     — Glossary index term row collapsed/expanded
     state-08-filter-strip          — Header search + book filter chips +
                                       "solo con varianti" toggle + context badges
                                       inline su ogni term row

   ─────────────────────────────────────────────────────────────────────
   Modello dati GlossaryEntry (proposto, implica schema migration backend):
   ─────────────────────────────────────────────────────────────────────
     GlossaryEntry {
       term: "sentinel",
       base_definition: "soldato di guardia",          // primary
       contexts: [
         { book_id: "press-start", definition: null,    // uses base
           first_seen: paragraph_id_X },
         { book_id: "rules-game",
           definition: "punto di osservazione strategica",  // variant
           first_seen: paragraph_id_Y }
       ]
     }

   Pattern decisions:
     · context_similarity threshold: 0.85 (server embedding cosine);
       auto-keep silenzioso se ≥ 0.85, conflict-dialog se < 0.85
     · bulk-import trigger: ≥3 parole (evita card-fatigue)
     · default conflict resolution: "Mantieni esistente" (soft, no destructive)
     · variant deletion: ultima variante non-base eliminata → torna single-context

   Decisione architetturale incidentale (Fowler): schema migration backend
   su `glossary_entries` per contexts[]. Spec backend separata.

   Vincolo FREEZE v2 #807/#808: solo entityHsl(entity, alpha) o token semantici.
   =================================================================== */

const { useState, useEffect } = React;

const ENTITY_HSL = {
  game:    '25 95% 45%',
  player:  '262 83% 58%',
  session: '240 60% 55%',
  agent:   '38 92% 50%',
  kb:      '174 60% 40%',
  chat:    '220 80% 55%',
  event:   '350 89% 60%',
  toolkit: '142 70% 45%',
  tool:    '195 80% 50%',
};
const entityHsl = (entity, alpha) =>
  alpha != null ? `hsl(${ENTITY_HSL[entity]} / ${alpha})` : `hsl(${ENTITY_HSL[entity]})`;

/* =====================================================================
   Faux viewer D background — il modal galleggia sopra questo testo
   tradotto. Replicato pattern translation-viewer per dare contesto.
   ===================================================================== */

function FauxViewerBackdrop({ withScrim = true }) {
  return (
    <div style={{
      position: 'absolute', inset: 0,
      overflow: 'hidden',
      background: 'var(--bg)',
    }}>
      {/* Top bar viewer */}
      <div style={{
        height: 44, padding: '10px 14px',
        display: 'flex', alignItems: 'center', gap: 10,
        borderBottom: '1px solid var(--border-light)',
        background: 'var(--bg)',
      }}>
        <button aria-label="Indietro" style={{
          width: 28, height: 28, borderRadius: 'var(--r-sm)',
          border: '1px solid var(--border)', background: 'var(--bg-card)',
          color: 'var(--text-sec)', fontSize: 14, padding: 0, lineHeight: 1,
        }}>‹</button>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{
            fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 13,
            color: 'var(--text)',
          }}>Veglia di Brace</div>
          <div style={{
            fontFamily: 'var(--f-mono)', fontSize: 9.5,
            color: 'var(--text-muted)',
            textTransform: 'uppercase', letterSpacing: '0.05em',
          }}>Nanolith · §147</div>
        </div>
        <span style={{
          padding: '2px 8px', borderRadius: 'var(--r-pill)',
          background: entityHsl('agent', 0.14), color: entityHsl('agent'),
          fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: 700,
          textTransform: 'uppercase', letterSpacing: '0.05em',
        }}>tradotto</span>
      </div>

      {/* Translated paragraph faux text */}
      <div style={{
        padding: '20px 18px',
        fontFamily: 'var(--f-body, var(--f-display))',
        fontSize: 14, lineHeight: 1.65,
        color: 'var(--text-sec)',
      }}>
        <div style={{
          fontFamily: 'var(--f-display)', fontWeight: 800,
          fontSize: 28, color: entityHsl('game'),
          letterSpacing: '-0.015em', marginBottom: 10,
          lineHeight: 1,
        }}>§147</div>
        <p style={{ margin: 0 }}>
          La{' '}
          <FauxPill text="Pietra del Vuoto" highlight />
          {' '}emanava un bagliore freddo mentre Marco si avvicinava
          al cerchio di pietre. I{' '}
          <FauxPill text="Razziatori" />
          {' '}avevano lasciato tracce di cenere lungo il sentiero, e la{' '}
          <FauxPill text="Veglia Cava" />
          {' '}sembrava ascoltare ogni respiro.
          <span style={{ opacity: 0.5 }}>
            {' '}Le sentinelle pallide non si erano ancora svegliate
            ma l'aria sapeva già di brace…
          </span>
        </p>
      </div>

      {/* Scrim — il modal lo richiede sopra */}
      {withScrim && (
        <div style={{
          position: 'absolute', inset: 0,
          background: 'rgba(0, 0, 0, 0.45)',
          backdropFilter: 'blur(2px)',
        }} />
      )}
    </div>
  );
}

function FauxPill({ text, highlight }) {
  return (
    <span style={{
      display: 'inline-flex', alignItems: 'baseline', gap: 3,
      padding: '1px 7px',
      borderRadius: 'var(--r-pill)',
      background: highlight ? entityHsl('kb', 0.18) : entityHsl('kb', 0.08),
      border: `1px solid ${highlight ? entityHsl('kb', 0.55) : entityHsl('kb', 0.22)}`,
      color: entityHsl('kb'),
      fontFamily: 'var(--f-display)', fontSize: 13, fontWeight: 600,
      whiteSpace: 'nowrap',
      boxShadow: highlight ? `0 0 0 2px ${entityHsl('kb', 0.18)}` : 'none',
    }}>{text}</span>
  );
}

/* =====================================================================
   Modal primitive — replicato dal pattern .pu-modal di mockup B
   adattato per glossary editor (gl- namespace).
   Mobile: fullscreen sheet slide-up. Desktop: centered card 480px.
   ===================================================================== */

function GlossaryEditorModal({
  /** state-NN identifier */
  variant,
  /** desktop mode renders centered card not bottom sheet */
  desktop = false,
  /** desktop conflict adds 200px right side-panel */
  withConflictPanel = false,
}) {
  // Initial values per stato
  const initialValue = 'Pietra del Vuoto';
  const editedValue = 'Pietra del Caos';
  const collisionValue = 'Razziatore';

  let value;
  let dirty;
  let bannerType;
  let actionVariant;

  if (variant === 'pristine') {
    value = initialValue; dirty = false; bannerType = null; actionVariant = 'save';
  } else if (variant === 'edited') {
    value = editedValue; dirty = true; bannerType = null; actionVariant = 'save';
  } else if (variant === 'error') {
    value = editedValue; dirty = true; bannerType = 'error'; actionVariant = 'retry';
  } else if (variant === 'collision') {
    value = collisionValue; dirty = true; bannerType = 'collision'; actionVariant = 'collision';
  }

  const Body = (
    <div style={{
      display: 'flex',
      flexDirection: desktop ? 'row' : 'column',
      gap: 0,
      width: desktop ? (withConflictPanel ? 720 : 480) : '100%',
      maxWidth: '100%',
      height: desktop ? 'auto' : '100%',
    }}>
      {/* Main modal card */}
      <div style={{
        flex: 1, minWidth: 0,
        background: 'var(--bg-card)',
        borderRadius: desktop
          ? (withConflictPanel ? 'var(--r-2xl) 0 0 var(--r-2xl)' : 'var(--r-2xl)')
          : 'var(--r-2xl) var(--r-2xl) 0 0',
        boxShadow: desktop
          ? '0 24px 60px rgba(0,0,0,.18), 0 8px 16px rgba(0,0,0,.08)'
          : 'none',
        border: `1px solid var(--border)`,
        borderRight: withConflictPanel ? '1px solid var(--border-light)' : '1px solid var(--border)',
        display: 'flex', flexDirection: 'column',
        overflow: 'hidden',
      }}>
        {/* Drag handle (mobile sheet) */}
        {!desktop && (
          <div style={{
            display: 'flex', justifyContent: 'center',
            paddingTop: 8, paddingBottom: 4, flexShrink: 0,
          }}>
            <span style={{
              width: 36, height: 4, borderRadius: 2,
              background: 'var(--border-strong)', opacity: 0.6,
            }} />
          </div>
        )}

        {/* Header */}
        <div style={{
          padding: desktop ? '20px 24px 14px' : '8px 18px 14px',
          flexShrink: 0,
        }}>
          <div style={{
            display: 'flex', alignItems: 'center', justifyContent: 'space-between',
            gap: 12, marginBottom: 6,
          }}>
            <div style={{
              display: 'inline-flex', alignItems: 'center', gap: 8,
              padding: '4px 10px',
              borderRadius: 'var(--r-pill)',
              background: entityHsl('kb', 0.12),
              color: entityHsl('kb'),
              fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: 700,
              textTransform: 'uppercase', letterSpacing: '0.06em',
              border: `1px solid ${entityHsl('kb', 0.28)}`,
            }}>
              <span style={{ fontSize: 11, lineHeight: 1 }}>⊜</span>
              Glossario · termine
            </div>
            <button aria-label="Chiudi" style={{
              width: 32, height: 32, borderRadius: 'var(--r-md)',
              border: '1px solid var(--border)',
              background: 'transparent',
              color: 'var(--text-muted)',
              fontSize: 18, lineHeight: 1, padding: 0,
              cursor: 'pointer',
            }}>×</button>
          </div>

          <h2 style={{
            fontFamily: 'var(--f-display)', fontWeight: 700,
            fontSize: 20, lineHeight: 1.25, color: 'var(--text)',
            letterSpacing: '-0.01em', margin: '8px 0 4px',
          }}>Modifica traduzione</h2>
          <p style={{
            margin: 0, fontSize: 13, color: 'var(--text-sec)', lineHeight: 1.5,
          }}>
            La traduzione resterà coerente in tutte le sessioni di questa campagna.
          </p>
        </div>

        {/* Body fields */}
        <div style={{
          padding: desktop ? '0 24px 8px' : '0 18px 8px',
          flex: desktop ? 'unset' : 1,
          overflowY: desktop ? 'visible' : 'auto',
        }}>
          {/* term_en read-only */}
          <FieldGroup label="Termine originale" sub="EN · letto da Nanolith Rules">
            <div style={{
              display: 'flex', alignItems: 'center', gap: 10,
              padding: '11px 14px',
              borderRadius: 'var(--r-md)',
              border: '1px solid var(--border-light)',
              background: 'var(--bg-muted)',
              color: 'var(--text)',
              fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 15,
            }}>
              <span style={{ flex: 1 }}>Voidstone</span>
              <span aria-label="Read-only" style={{
                fontFamily: 'var(--f-mono)', fontSize: 9.5,
                color: 'var(--text-muted)',
                textTransform: 'uppercase', letterSpacing: '0.06em',
                fontWeight: 700,
                padding: '2px 6px',
                borderRadius: 'var(--r-sm)',
                background: 'var(--bg-card)',
                border: '1px solid var(--border-light)',
              }}>read-only</span>
            </div>
          </FieldGroup>

          {/* it editable */}
          <FieldGroup label="Traduzione italiana" sub="usata in §147 e in tutte le traduzioni future">
            <TranslationInput
              value={value}
              dirty={dirty}
              previousValue={dirty ? initialValue : null}
              showDiff={variant === 'edited'}
              hasError={bannerType === 'collision'}
            />
            {/* Helper text per stato 02 edited */}
            {variant === 'edited' && (
              <div style={{
                marginTop: 8,
                padding: '8px 12px',
                borderRadius: 'var(--r-md)',
                background: entityHsl('game', 0.08),
                border: `1px solid ${entityHsl('game', 0.22)}`,
                fontSize: 12, color: 'var(--text-sec)',
                display: 'flex', alignItems: 'flex-start', gap: 8,
              }}>
                <span style={{
                  color: entityHsl('game'), fontSize: 13, flexShrink: 0,
                  lineHeight: '17px',
                }}>✓</span>
                <span style={{ lineHeight: 1.45 }}>
                  Verrà applicato a <strong>12 termini</strong> in traduzioni future.
                  Le traduzioni passate non vengono toccate.
                </span>
              </div>
            )}
          </FieldGroup>

          {/* first_seen chip */}
          <FieldGroup label="Prima occorrenza">
            <span style={{
              display: 'inline-flex', alignItems: 'center', gap: 8,
              padding: '6px 12px',
              borderRadius: 'var(--r-pill)',
              background: entityHsl('game', 0.10),
              color: entityHsl('game'),
              fontFamily: 'var(--f-mono)', fontSize: 12, fontWeight: 700,
              border: `1px solid ${entityHsl('game', 0.24)}`,
            }}>
              <span style={{
                fontFamily: 'var(--f-display)', fontWeight: 800,
                fontSize: 14, lineHeight: 1,
              }}>§147</span>
              <span style={{
                opacity: 0.7, fontWeight: 500,
                textTransform: 'none', letterSpacing: 0,
              }}>· paragrafo dove l'hai vista</span>
            </span>
          </FieldGroup>

          {/* Banner area */}
          {bannerType === 'error' && <ErrorBanner />}
          {bannerType === 'collision' && <CollisionBanner />}
        </div>

        {/* Footer actions */}
        <div style={{
          padding: desktop ? '14px 24px 20px' : '12px 18px 18px',
          borderTop: '1px solid var(--border-light)',
          background: 'var(--bg-card)',
          flexShrink: 0,
        }}>
          {actionVariant === 'save' && (
            <ActionRow
              secondary={{ label: 'Annulla' }}
              primary={{
                label: dirty ? 'Salva modifiche' : 'Salva',
                disabled: !dirty,
                accent: 'game',
              }}
            />
          )}
          {actionVariant === 'retry' && (
            <ActionRow
              secondary={{ label: 'Annulla' }}
              primary={{
                label: '↻ Riprova',
                disabled: false,
                accent: 'warning',
              }}
              caption="Il valore inserito è stato preservato — niente è andato perso."
            />
          )}
          {actionVariant === 'collision' && (
            <ActionRow
              vertical
              caption="Una traduzione coerente vale più della velocità."
              secondary={{
                label: 'Cambia traduzione',
                preferred: true,
              }}
              primary={{
                label: 'Sovrascrivi anche \'Reaver\'',
                disabled: false,
                accent: 'event',
                destructive: true,
              }}
            />
          )}
        </div>
      </div>

      {/* Desktop side panel — solo state-04 desktop conflict */}
      {desktop && withConflictPanel && (
        <aside style={{
          width: 200, flexShrink: 0,
          background: entityHsl('event', 0.04),
          borderRadius: '0 var(--r-2xl) var(--r-2xl) 0',
          border: `1px solid ${entityHsl('event', 0.22)}`,
          borderLeft: 'none',
          padding: '20px 16px',
          display: 'flex', flexDirection: 'column', gap: 12,
        }}>
          <div style={{
            fontFamily: 'var(--f-mono)', fontSize: 9.5,
            color: entityHsl('event'),
            textTransform: 'uppercase', letterSpacing: '0.08em',
            fontWeight: 700,
          }}>Termine in conflitto</div>

          <div style={{
            padding: '12px 14px',
            borderRadius: 'var(--r-md)',
            background: 'var(--bg-card)',
            border: `1px solid ${entityHsl('event', 0.32)}`,
          }}>
            <div style={{
              fontFamily: 'var(--f-display)', fontWeight: 700,
              fontSize: 14, color: 'var(--text)', marginBottom: 6,
            }}>Reaver</div>
            <div style={{
              fontFamily: 'var(--f-mono)', fontSize: 11,
              color: 'var(--text-muted)',
              textTransform: 'uppercase', letterSpacing: '0.05em',
              marginBottom: 8, fontWeight: 700,
            }}>tradotto come</div>
            <div style={{
              padding: '6px 10px',
              borderRadius: 'var(--r-pill)',
              background: entityHsl('kb', 0.12),
              color: entityHsl('kb'),
              fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 13,
              border: `1px solid ${entityHsl('kb', 0.32)}`,
              display: 'inline-block',
            }}>Razziatore</div>
            <div style={{
              marginTop: 12,
              fontFamily: 'var(--f-mono)', fontSize: 10,
              color: 'var(--text-muted)',
              fontVariantNumeric: 'tabular-nums',
            }}>
              prima vista <strong style={{ color: 'var(--text-sec)' }}>§82</strong> · usato 4 volte
            </div>
          </div>

          <p style={{
            margin: 0,
            fontSize: 12, color: 'var(--text-sec)', lineHeight: 1.5,
          }}>
            Se sovrascrivi, "Reaver" non avrà più una traduzione e dovrai
            sceglierne una nuova.
          </p>
        </aside>
      )}
    </div>
  );

  return (
    <div style={{
      position: 'absolute', inset: 0,
      background: desktop ? 'rgba(0, 0, 0, 0.55)' : 'rgba(0, 0, 0, 0.40)',
      backdropFilter: 'blur(3px)',
      display: 'flex',
      alignItems: desktop ? 'center' : 'flex-end',
      justifyContent: 'center',
      padding: desktop ? 24 : 0,
      zIndex: 10,
    }}>
      {Body}
    </div>
  );
}

/* ─── Field group reused across variants ─── */
function FieldGroup({ label, sub, children }) {
  return (
    <div style={{ marginBottom: 14 }}>
      <div style={{
        display: 'flex', alignItems: 'baseline', justifyContent: 'space-between',
        gap: 10, marginBottom: 6,
      }}>
        <label style={{
          fontFamily: 'var(--f-mono)', fontSize: 10,
          color: 'var(--text-sec)',
          textTransform: 'uppercase', letterSpacing: '0.07em',
          fontWeight: 700,
        }}>{label}</label>
        {sub && <span style={{
          fontSize: 10.5, color: 'var(--text-muted)',
          fontFamily: 'var(--f-mono)',
        }}>{sub}</span>}
      </div>
      {children}
    </div>
  );
}

/* ─── Editable input with focus ring + optional diff hint ─── */
function TranslationInput({ value, dirty, previousValue, showDiff, hasError }) {
  const ringColor = hasError
    ? entityHsl('event', 0.55)
    : dirty
      ? entityHsl('game', 0.55)
      : entityHsl('kb', 0.45);
  const bgColor = hasError ? entityHsl('event', 0.04) : 'var(--bg)';

  return (
    <div>
      {/* Diff hint — vecchio value strikethrough quando state-02 edited */}
      {showDiff && previousValue && (
        <div style={{
          marginBottom: 6,
          fontSize: 11.5, color: 'var(--text-muted)',
          fontFamily: 'var(--f-mono)',
          display: 'flex', alignItems: 'center', gap: 8,
        }}>
          <span style={{
            textTransform: 'uppercase', letterSpacing: '0.06em', fontWeight: 700,
          }}>era</span>
          <span style={{ textDecoration: 'line-through', opacity: 0.7 }}>
            {previousValue}
          </span>
          <span style={{ opacity: 0.6 }}>→</span>
          <span style={{ color: entityHsl('game'), fontWeight: 700 }}>nuovo</span>
        </div>
      )}

      <div style={{
        position: 'relative',
        borderRadius: 'var(--r-md)',
        background: bgColor,
        border: `1.5px solid ${ringColor}`,
        boxShadow: `0 0 0 4px ${hasError ? entityHsl('event', 0.10) : dirty ? entityHsl('game', 0.10) : entityHsl('kb', 0.08)}`,
        transition: 'box-shadow 120ms var(--ease-out, ease-out)',
      }}>
        <div
          contentEditable={false}
          suppressContentEditableWarning
          style={{
            display: 'block',
            width: '100%',
            padding: '11px 44px 11px 14px',
            fontFamily: 'var(--f-display)',
            fontSize: 16, fontWeight: 600,
            color: 'var(--text)',
            background: 'transparent',
            outline: 'none', border: 'none',
            letterSpacing: '-0.005em',
          }}
        >{value}</div>

        {/* Caret indicator (visual only) */}
        {dirty && !hasError && (
          <div style={{
            position: 'absolute', right: 14, top: '50%',
            transform: 'translateY(-50%)',
            display: 'flex', alignItems: 'center', gap: 6,
            fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: 700,
            color: entityHsl('game'),
            textTransform: 'uppercase', letterSpacing: '0.06em',
          }}>● modificato</div>
        )}
        {hasError && (
          <div style={{
            position: 'absolute', right: 14, top: '50%',
            transform: 'translateY(-50%)',
            display: 'flex', alignItems: 'center', gap: 6,
            fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: 700,
            color: entityHsl('event'),
            textTransform: 'uppercase', letterSpacing: '0.06em',
          }}>⚠ conflitto</div>
        )}
      </div>
    </div>
  );
}

/* ─── State-03 amber error banner ─── */
function ErrorBanner() {
  return (
    <div style={{
      marginTop: 8,
      padding: '12px 14px',
      borderRadius: 'var(--r-md)',
      background: 'hsl(var(--c-warning) / 0.10)',
      border: `1px solid hsl(var(--c-warning) / 0.36)`,
      display: 'flex', alignItems: 'flex-start', gap: 10,
    }}>
      <span style={{
        fontSize: 18, lineHeight: '20px', flexShrink: 0,
      }}>⚠️</span>
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{
          fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 13,
          color: 'hsl(var(--c-warning))', marginBottom: 3,
        }}>Errore salvataggio</div>
        <div style={{
          fontSize: 12, color: 'var(--text-sec)', lineHeight: 1.5,
        }}>
          Connessione interrotta. Il tuo testo è ancora qui — riprova tra un momento.
        </div>
      </div>
    </div>
  );
}

/* ─── State-04 red collision banner ─── */
function CollisionBanner() {
  return (
    <div style={{
      marginTop: 8,
      padding: '14px 16px',
      borderRadius: 'var(--r-md)',
      background: entityHsl('event', 0.08),
      border: `1px solid ${entityHsl('event', 0.40)}`,
    }}>
      <div style={{
        display: 'flex', alignItems: 'center', gap: 10, marginBottom: 8,
      }}>
        <span style={{
          width: 20, height: 20, borderRadius: '50%',
          background: entityHsl('event', 0.18),
          color: entityHsl('event'),
          display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
          fontSize: 12, fontWeight: 800, flexShrink: 0,
        }}>!</span>
        <div style={{
          fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 13,
          color: entityHsl('event'),
        }}>Termine già usato</div>
      </div>
      <div style={{
        fontSize: 12.5, color: 'var(--text-sec)', lineHeight: 1.55,
        marginBottom: 8,
      }}>
        <strong style={{ color: 'var(--text)' }}>"Razziatore"</strong> è già la traduzione di{' '}
        <span style={{
          display: 'inline-flex', alignItems: 'center',
          padding: '0 6px', borderRadius: 'var(--r-sm)',
          background: entityHsl('kb', 0.14), color: entityHsl('kb'),
          fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 12,
        }}>Reaver</span>{' '}
        (vista in §82).
      </div>
      <div style={{
        fontSize: 12, color: 'var(--text-muted)', lineHeight: 1.5,
        fontStyle: 'italic',
      }}>
        Conferma per sovrascrivere o cambia traduzione.
      </div>
    </div>
  );
}

/* ─── Action row primitive (reusable across variants) ─── */
function ActionRow({ primary, secondary, caption, vertical }) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
      {caption && (
        <p style={{
          margin: 0,
          fontSize: 11.5, color: 'var(--text-muted)',
          textAlign: vertical ? 'left' : 'right',
          fontStyle: 'italic', lineHeight: 1.45,
        }}>{caption}</p>
      )}
      <div style={{
        display: 'flex',
        flexDirection: vertical ? 'column-reverse' : 'row',
        gap: 8,
        alignItems: 'stretch',
      }}>
        <button style={{
          flex: vertical ? 'unset' : 1,
          padding: '11px 18px',
          borderRadius: 'var(--r-md)',
          border: secondary.preferred
            ? `1.5px solid ${entityHsl('game', 0.45)}`
            : '1px solid var(--border-strong)',
          background: secondary.preferred
            ? entityHsl('game', 0.06)
            : 'transparent',
          color: secondary.preferred ? entityHsl('game') : 'var(--text-sec)',
          fontFamily: 'var(--f-display)',
          fontWeight: secondary.preferred ? 700 : 600,
          fontSize: 14,
          cursor: 'pointer',
        }}>{secondary.label}</button>

        <button
          disabled={primary.disabled}
          style={{
            flex: vertical ? 'unset' : 1,
            padding: '11px 18px',
            borderRadius: 'var(--r-md)',
            border: 'none',
            background: primary.disabled
              ? 'var(--bg-muted)'
              : primary.accent === 'warning'
                ? `hsl(var(--c-warning))`
                : entityHsl(primary.accent),
            color: primary.disabled ? 'var(--text-muted)' : '#fff',
            fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 14,
            boxShadow: primary.disabled ? 'none' : (
              primary.destructive
                ? `0 4px 14px ${entityHsl('event', 0.30)}`
                : primary.accent === 'warning'
                  ? `0 4px 14px hsl(var(--c-warning) / 0.30)`
                  : `0 4px 14px ${entityHsl(primary.accent, 0.30)}`
            ),
            cursor: primary.disabled ? 'not-allowed' : 'pointer',
            letterSpacing: '-0.005em',
          }}
        >{primary.label}</button>
      </div>
    </div>
  );
}

/* =====================================================================
   NUOVO sub-screen 05 — ConflictDialog
   ─────────────────────────────────────────────────────────────────────
   OCR trova parola "sentinel" già nel glossario base con definizione
   "soldato di guardia", ma il nuovo paragrafo §147 di "Rules Game"
   suggerisce "punto di osservazione strategica" (context_similarity = 0.62
   < 0.85 threshold → mostra dialog).

   Layout: dialog role="dialog" aria-modal="true"
           body: definizioni side-by-side (esistente | nuova) con badge book
           CTA radiogroup verticale (3 opzioni)
           footer: [Annulla] + [Conferma] (CTA primaria game)

   Default selection: "Mantieni esistente" (soft CTA, evita destructive default).
   ===================================================================== */

function ConflictDialog({ desktop = false }) {
  const existingDef = 'soldato di guardia';
  const newDef = 'punto di osservazione strategica';
  const similarity = 0.62;
  const term = 'sentinel';

  const radioOptions = [
    {
      id: 'keep',
      label: 'Mantieni esistente',
      sublabel: 'Nessun cambiamento — il glossario rimane invariato',
      isDefault: true,
    },
    {
      id: 'replace',
      label: 'Sostituisci',
      sublabel: 'La nuova definizione rimpiazza ovunque la base',
      accent: 'warning',
    },
    {
      id: 'variant',
      label: 'Aggiungi variante per Rules Game',
      sublabel: 'Tieni base + variante book-specific (consigliato)',
      accent: 'game',
    },
  ];

  return (
    <div style={{
      position: 'absolute', inset: 0,
      background: 'rgba(0, 0, 0, 0.55)',
      backdropFilter: 'blur(3px)',
      display: 'flex',
      alignItems: desktop ? 'center' : 'flex-end',
      justifyContent: 'center',
      padding: desktop ? 24 : 0,
      zIndex: 10,
    }}>
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="conflict-dialog-title"
        aria-describedby="conflict-dialog-desc"
        style={{
          width: desktop ? 540 : '100%',
          maxWidth: '100%',
          height: desktop ? 'auto' : '100%',
          background: 'var(--bg-card)',
          borderRadius: desktop ? 'var(--r-2xl)' : 'var(--r-2xl) var(--r-2xl) 0 0',
          boxShadow: desktop
            ? '0 24px 60px rgba(0,0,0,.18), 0 8px 16px rgba(0,0,0,.08)'
            : 'none',
          border: '1px solid var(--border)',
          display: 'flex',
          flexDirection: 'column',
          overflow: 'hidden',
        }}>
        {/* Drag handle (mobile sheet) */}
        {!desktop && (
          <div style={{
            display: 'flex', justifyContent: 'center',
            paddingTop: 8, paddingBottom: 4, flexShrink: 0,
          }}>
            <span style={{
              width: 36, height: 4, borderRadius: 2,
              background: 'var(--border-strong)', opacity: 0.6,
            }} />
          </div>
        )}

        {/* Header */}
        <div style={{ padding: desktop ? '20px 24px 14px' : '8px 18px 14px', flexShrink: 0 }}>
          <div style={{
            display: 'inline-flex', alignItems: 'center', gap: 8,
            padding: '4px 10px',
            borderRadius: 'var(--r-pill)',
            background: entityHsl('event', 0.12),
            color: entityHsl('event'),
            fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: 700,
            textTransform: 'uppercase', letterSpacing: '0.06em',
            border: `1px solid ${entityHsl('event', 0.28)}`,
            marginBottom: 12,
          }}>
            <span style={{ fontSize: 11, lineHeight: 1 }}>⚠</span>
            Conflitto · similarità {similarity.toFixed(2)} &lt; 0.85
          </div>
          <h2
            id="conflict-dialog-title"
            style={{
              fontFamily: 'var(--f-display)', fontWeight: 700,
              fontSize: 20, lineHeight: 1.25, color: 'var(--text)',
              letterSpacing: '-0.01em', margin: '0 0 4px',
            }}>
            La parola "<span style={{ color: entityHsl('kb') }}>{term}</span>" è già nel glossario
          </h2>
          <p
            id="conflict-dialog-desc"
            style={{
              margin: 0, fontSize: 13, color: 'var(--text-sec)', lineHeight: 1.5,
            }}>
            Le due definizioni sono troppo diverse per fonderle automaticamente.
            Scegli come procedere.
          </p>
        </div>

        {/* Side-by-side definitions */}
        <div style={{
          padding: desktop ? '0 24px 14px' : '0 18px 14px',
          flexShrink: 0,
        }}>
          <div style={{
            display: 'grid',
            gridTemplateColumns: desktop ? '1fr 1fr' : '1fr',
            gap: 10,
          }}>
            {/* Existing */}
            <div style={{
              padding: '12px 14px',
              borderRadius: 'var(--r-md)',
              border: '1px solid var(--border-light)',
              background: 'var(--bg-muted)',
            }}>
              <div style={{
                display: 'inline-flex', alignItems: 'center', gap: 6,
                fontFamily: 'var(--f-mono)', fontSize: 9.5,
                color: 'var(--text-muted)',
                textTransform: 'uppercase', letterSpacing: '0.07em',
                fontWeight: 700, marginBottom: 8,
              }}>
                <span>📖</span> Press Start · esistente
              </div>
              <div style={{
                fontFamily: 'var(--f-display)', fontWeight: 600,
                fontSize: 14, color: 'var(--text)', lineHeight: 1.45,
              }}>{existingDef}</div>
              <div style={{
                marginTop: 8,
                fontFamily: 'var(--f-mono)', fontSize: 10,
                color: 'var(--text-muted)',
              }}>prima vista §72 · usato 3 volte</div>
            </div>
            {/* New */}
            <div style={{
              padding: '12px 14px',
              borderRadius: 'var(--r-md)',
              border: `1.5px solid ${entityHsl('game', 0.42)}`,
              background: entityHsl('game', 0.06),
            }}>
              <div style={{
                display: 'inline-flex', alignItems: 'center', gap: 6,
                fontFamily: 'var(--f-mono)', fontSize: 9.5,
                color: entityHsl('game'),
                textTransform: 'uppercase', letterSpacing: '0.07em',
                fontWeight: 700, marginBottom: 8,
              }}>
                <span>📖</span> Rules Game · nuova proposta
              </div>
              <div style={{
                fontFamily: 'var(--f-display)', fontWeight: 600,
                fontSize: 14, color: 'var(--text)', lineHeight: 1.45,
              }}>{newDef}</div>
              <div style={{
                marginTop: 8,
                fontFamily: 'var(--f-mono)', fontSize: 10,
                color: 'var(--text-muted)',
              }}>vista in §147 · primo uso</div>
            </div>
          </div>
        </div>

        {/* Radio group — 3 CTA */}
        <div
          role="radiogroup"
          aria-label="Come risolvere il conflitto"
          style={{
            padding: desktop ? '0 24px 14px' : '0 18px 14px',
            flex: desktop ? 'unset' : 1,
            overflowY: desktop ? 'visible' : 'auto',
            display: 'flex', flexDirection: 'column', gap: 8,
          }}>
          {radioOptions.map(opt => (
            <ConflictRadioOption key={opt.id} option={opt} />
          ))}
        </div>

        {/* Footer */}
        <div style={{
          padding: desktop ? '14px 24px 20px' : '12px 18px 18px',
          borderTop: '1px solid var(--border-light)',
          background: 'var(--bg-card)',
          flexShrink: 0,
          display: 'flex', gap: 8,
        }}>
          <button style={{
            flex: 1,
            padding: '11px 18px',
            borderRadius: 'var(--r-md)',
            border: '1px solid var(--border-strong)',
            background: 'transparent',
            color: 'var(--text-sec)',
            fontFamily: 'var(--f-display)', fontWeight: 600, fontSize: 14,
            cursor: 'pointer',
          }}>Annulla</button>
          <button style={{
            flex: 1,
            padding: '11px 18px',
            borderRadius: 'var(--r-md)',
            border: 'none',
            background: entityHsl('game'),
            color: '#fff',
            fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 14,
            boxShadow: `0 4px 14px ${entityHsl('game', 0.30)}`,
            cursor: 'pointer',
            letterSpacing: '-0.005em',
          }}>Conferma</button>
        </div>
      </div>
    </div>
  );
}

function ConflictRadioOption({ option }) {
  const accent = option.accent || 'kb';
  const isDefault = option.isDefault;

  return (
    <label style={{
      display: 'flex', alignItems: 'flex-start', gap: 12,
      padding: '12px 14px',
      borderRadius: 'var(--r-md)',
      border: isDefault
        ? `1.5px solid ${entityHsl(accent, 0.42)}`
        : '1px solid var(--border-light)',
      background: isDefault ? entityHsl(accent, 0.04) : 'var(--bg)',
      cursor: 'pointer',
      transition: 'background 120ms var(--ease-out, ease-out)',
    }}>
      <span
        role="radio"
        aria-checked={isDefault}
        style={{
          width: 18, height: 18, borderRadius: '50%',
          border: `2px solid ${isDefault ? entityHsl(accent) : 'var(--border-strong)'}`,
          background: 'var(--bg)',
          flexShrink: 0,
          marginTop: 2,
          display: 'inline-flex',
          alignItems: 'center', justifyContent: 'center',
        }}>
        {isDefault && (
          <span style={{
            width: 8, height: 8, borderRadius: '50%',
            background: entityHsl(accent),
          }} />
        )}
      </span>
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{
          fontFamily: 'var(--f-display)', fontWeight: 700,
          fontSize: 14, color: 'var(--text)', lineHeight: 1.35,
          marginBottom: 2,
        }}>
          {option.label}
          {isDefault && (
            <span style={{
              marginLeft: 8,
              padding: '1px 7px', borderRadius: 'var(--r-sm)',
              fontFamily: 'var(--f-mono)', fontSize: 9, fontWeight: 700,
              background: entityHsl(accent, 0.18),
              color: entityHsl(accent),
              textTransform: 'uppercase', letterSpacing: '0.06em',
              verticalAlign: 'middle',
            }}>default</span>
          )}
        </div>
        <div style={{
          fontSize: 12, color: 'var(--text-sec)', lineHeight: 1.5,
        }}>{option.sublabel}</div>
      </div>
    </label>
  );
}

/* =====================================================================
   NUOVO sub-screen 06 — BulkImportCard
   ─────────────────────────────────────────────────────────────────────
   OCR trova ≥3 parole nuove (threshold ≥3, NON ≥1 per evitare
   card-fatigue). Card inline sotto traduzione di §147.

   Layout: inline card sopra il viewer (NO scrim — non bloccante)
           title "5 nuove parole rilevate"
           chip preview (5 termini cliccabili)
           2 CTA: ["Aggiungi tutte" 1-click] / ["Rivedi" → multi-select modal]

   Multi-select modal: per-term checkbox + edit definition inline
                       role="listbox" aria-multiselectable="true"
   ===================================================================== */

function BulkImportCard({ variant = 'inline', desktop = false }) {
  // 5 termini esempio coerenti con narrative "Runa di Ardenel · §147"
  const newWords = [
    { term: 'ferrigni', proposedDef: 'razziatori di pietra-ferro' },
    { term: 'runa', proposedDef: 'simbolo magico inciso' },
    { term: 'threshold', proposedDef: 'soglia mistica del cerchio' },
    { term: 'veglia cava', proposedDef: 'spirito dormiente del bosco' },
    { term: 'ardenel', proposedDef: 'antica casata di custodi' },
  ];

  if (variant === 'inline') {
    return <BulkImportInlineCard newWords={newWords} desktop={desktop} />;
  }
  return <BulkImportReviewModal newWords={newWords} desktop={desktop} />;
}

function BulkImportInlineCard({ newWords, desktop }) {
  return (
    <div style={{
      position: 'absolute',
      left: desktop ? '50%' : 12,
      right: desktop ? 'auto' : 12,
      top: desktop ? 'auto' : 'auto',
      bottom: desktop ? 32 : 32,
      transform: desktop ? 'translateX(-50%)' : 'none',
      width: desktop ? 560 : 'auto',
      maxWidth: '100%',
      padding: '14px 16px',
      borderRadius: 'var(--r-lg)',
      background: 'var(--bg-card)',
      border: `1.5px solid ${entityHsl('kb', 0.42)}`,
      boxShadow: '0 16px 40px rgba(0,0,0,.16)',
      zIndex: 10,
    }}>
      <div style={{
        display: 'flex', alignItems: 'flex-start', gap: 10, marginBottom: 10,
      }}>
        <span style={{
          width: 30, height: 30, borderRadius: '50%',
          background: entityHsl('kb', 0.16),
          color: entityHsl('kb'),
          display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
          fontSize: 15, fontWeight: 800, flexShrink: 0,
        }}>⊕</span>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{
            fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 14,
            color: 'var(--text)', marginBottom: 2,
          }}>
            {newWords.length} nuove parole rilevate
          </div>
          <div style={{
            fontFamily: 'var(--f-mono)', fontSize: 10,
            color: 'var(--text-muted)',
            textTransform: 'uppercase', letterSpacing: '0.06em',
          }}>
            in §147 · libro "Runa di Ardenel" · trigger ≥3
          </div>
        </div>
      </div>

      {/* Chip preview */}
      <div style={{
        display: 'flex', gap: 6, flexWrap: 'wrap', marginBottom: 12,
      }}>
        {newWords.map(w => (
          <span key={w.term} style={{
            padding: '4px 10px',
            borderRadius: 'var(--r-pill)',
            background: entityHsl('kb', 0.10),
            color: entityHsl('kb'),
            fontFamily: 'var(--f-display)', fontSize: 12, fontWeight: 600,
            border: `1px solid ${entityHsl('kb', 0.24)}`,
            whiteSpace: 'nowrap',
          }}>{w.term}</span>
        ))}
      </div>

      {/* CTA row */}
      <div style={{
        display: 'flex', gap: 8,
      }}>
        <button style={{
          flex: 1,
          padding: '10px 16px',
          borderRadius: 'var(--r-md)',
          border: '1px solid var(--border-strong)',
          background: 'transparent',
          color: 'var(--text-sec)',
          fontFamily: 'var(--f-display)', fontWeight: 600, fontSize: 13,
          cursor: 'pointer',
        }}>Rivedi</button>
        <button style={{
          flex: 1.4,
          padding: '10px 16px',
          borderRadius: 'var(--r-md)',
          border: 'none',
          background: entityHsl('kb'),
          color: '#fff',
          fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 13,
          boxShadow: `0 4px 14px ${entityHsl('kb', 0.30)}`,
          cursor: 'pointer',
          letterSpacing: '-0.005em',
        }}>Aggiungi tutte ({newWords.length})</button>
      </div>
    </div>
  );
}

function BulkImportReviewModal({ newWords, desktop }) {
  return (
    <div style={{
      position: 'absolute', inset: 0,
      background: 'rgba(0, 0, 0, 0.55)',
      backdropFilter: 'blur(3px)',
      display: 'flex',
      alignItems: desktop ? 'center' : 'flex-end',
      justifyContent: 'center',
      padding: desktop ? 24 : 0,
      zIndex: 10,
    }}>
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="bulk-review-title"
        style={{
          width: desktop ? 560 : '100%',
          maxWidth: '100%',
          height: desktop ? 'auto' : '100%',
          maxHeight: desktop ? '85vh' : '100%',
          background: 'var(--bg-card)',
          borderRadius: desktop ? 'var(--r-2xl)' : 'var(--r-2xl) var(--r-2xl) 0 0',
          boxShadow: desktop
            ? '0 24px 60px rgba(0,0,0,.18), 0 8px 16px rgba(0,0,0,.08)'
            : 'none',
          border: '1px solid var(--border)',
          display: 'flex',
          flexDirection: 'column',
          overflow: 'hidden',
        }}>
        {!desktop && (
          <div style={{
            display: 'flex', justifyContent: 'center',
            paddingTop: 8, paddingBottom: 4, flexShrink: 0,
          }}>
            <span style={{
              width: 36, height: 4, borderRadius: 2,
              background: 'var(--border-strong)', opacity: 0.6,
            }} />
          </div>
        )}

        {/* Header */}
        <div style={{ padding: desktop ? '20px 24px 14px' : '8px 18px 14px', flexShrink: 0 }}>
          <h2
            id="bulk-review-title"
            style={{
              fontFamily: 'var(--f-display)', fontWeight: 700,
              fontSize: 20, lineHeight: 1.25, color: 'var(--text)',
              letterSpacing: '-0.01em', margin: '0 0 4px',
            }}>
            Rivedi le nuove parole
          </h2>
          <p style={{
            margin: 0, fontSize: 13, color: 'var(--text-sec)', lineHeight: 1.5,
          }}>
            Spunta le parole da aggiungere al glossario. Puoi editare le
            definizioni proposte inline.
          </p>
        </div>

        {/* List */}
        <div
          role="listbox"
          aria-multiselectable="true"
          aria-label="Nuove parole da importare"
          style={{
            padding: desktop ? '0 24px 14px' : '0 18px 14px',
            flex: 1,
            overflowY: 'auto',
            display: 'flex', flexDirection: 'column', gap: 8,
          }}>
          {newWords.map((w, idx) => (
            <BulkReviewRow key={w.term} word={w} checked={idx !== 2} />
          ))}
        </div>

        {/* Footer */}
        <div style={{
          padding: desktop ? '14px 24px 20px' : '12px 18px 18px',
          borderTop: '1px solid var(--border-light)',
          background: 'var(--bg-card)',
          flexShrink: 0,
          display: 'flex', flexDirection: 'column', gap: 10,
        }}>
          <div style={{
            fontFamily: 'var(--f-mono)', fontSize: 10,
            color: 'var(--text-muted)',
            textAlign: 'center',
            textTransform: 'uppercase', letterSpacing: '0.06em',
          }}>
            4 di {newWords.length} selezionate
          </div>
          <div style={{ display: 'flex', gap: 8 }}>
            <button style={{
              flex: 1,
              padding: '11px 18px',
              borderRadius: 'var(--r-md)',
              border: '1px solid var(--border-strong)',
              background: 'transparent',
              color: 'var(--text-sec)',
              fontFamily: 'var(--f-display)', fontWeight: 600, fontSize: 14,
              cursor: 'pointer',
            }}>Annulla</button>
            <button style={{
              flex: 1,
              padding: '11px 18px',
              borderRadius: 'var(--r-md)',
              border: 'none',
              background: entityHsl('kb'),
              color: '#fff',
              fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 14,
              boxShadow: `0 4px 14px ${entityHsl('kb', 0.30)}`,
              cursor: 'pointer',
            }}>Aggiungi 4 parole</button>
          </div>
        </div>
      </div>
    </div>
  );
}

function BulkReviewRow({ word, checked }) {
  return (
    <div
      role="option"
      aria-selected={checked}
      style={{
        display: 'flex', alignItems: 'flex-start', gap: 12,
        padding: '11px 12px',
        borderRadius: 'var(--r-md)',
        border: checked
          ? `1.5px solid ${entityHsl('kb', 0.36)}`
          : '1px solid var(--border-light)',
        background: checked ? entityHsl('kb', 0.05) : 'var(--bg)',
      }}>
      {/* Checkbox */}
      <span style={{
        width: 18, height: 18, borderRadius: 'var(--r-sm)',
        border: `2px solid ${checked ? entityHsl('kb') : 'var(--border-strong)'}`,
        background: checked ? entityHsl('kb') : 'var(--bg)',
        flexShrink: 0,
        marginTop: 2,
        display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
        color: '#fff', fontSize: 11, fontWeight: 800,
      }}>
        {checked && '✓'}
      </span>

      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{
          display: 'flex', alignItems: 'center', gap: 8,
          marginBottom: 4,
        }}>
          <span style={{
            fontFamily: 'var(--f-display)', fontWeight: 700,
            fontSize: 14, color: 'var(--text)',
          }}>{word.term}</span>
          <span style={{
            fontFamily: 'var(--f-mono)', fontSize: 9.5,
            color: 'var(--text-muted)',
            textTransform: 'uppercase', letterSpacing: '0.06em',
          }}>EN → IT</span>
        </div>
        {/* Inline-editable definition */}
        <div style={{
          padding: '7px 10px',
          borderRadius: 'var(--r-sm)',
          border: '1px solid var(--border-light)',
          background: 'var(--bg)',
          fontSize: 12.5, color: 'var(--text-sec)', lineHeight: 1.45,
          fontFamily: 'var(--f-display)',
        }}>
          {word.proposedDef}
          <span style={{
            marginLeft: 8,
            fontFamily: 'var(--f-mono)', fontSize: 9.5,
            color: 'var(--text-muted)',
            textTransform: 'uppercase', letterSpacing: '0.06em',
            fontWeight: 700,
          }}>✏ tap per editare</span>
        </div>
      </div>
    </div>
  );
}

/* =====================================================================
   NUOVO sub-screen 07 — VariantExpansion
   ─────────────────────────────────────────────────────────────────────
   Glossary index term row con contexts.length > 1.
   Default collapsed. Click → expand mostra varianti per book.

   Layout: term row con chevron + term name + base_definition truncated
           click → aria-expanded="true", row espande mostrando N varianti:
             · book badge "📖 Press Start" / "📖 Rules Game"
             · definition specifica (o "(usa base)" se context.definition null)
             · first_seen link "Vedi paragrafo §X"
             · delete variant button
   ===================================================================== */

function VariantExpansionList({ desktop = false }) {
  return (
    <div style={{
      position: 'absolute', inset: 0,
      background: 'var(--bg)',
      overflowY: 'auto',
      padding: desktop ? '16px 28px' : '12px 14px',
    }}>
      {/* Page header */}
      <div style={{
        marginBottom: 14,
        display: 'flex', alignItems: 'center', justifyContent: 'space-between',
      }}>
        <div>
          <h2 style={{
            margin: '0 0 2px',
            fontFamily: 'var(--f-display)', fontWeight: 700,
            fontSize: 18, color: 'var(--text)',
          }}>Glossario · Runa di Ardenel</h2>
          <div style={{
            fontFamily: 'var(--f-mono)', fontSize: 10,
            color: 'var(--text-muted)',
            textTransform: 'uppercase', letterSpacing: '0.06em',
          }}>
            12 termini · 3 con varianti per book
          </div>
        </div>
      </div>

      {/* List of term rows — 1 collapsed, 1 expanded */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
        <TermRow
          term="ferrigni"
          baseDef="razziatori di pietra-ferro"
          contextCount={1}
          expanded={false}
        />
        <TermRow
          term="sentinel"
          baseDef="soldato di guardia"
          contextCount={2}
          expanded={true}
          variants={[
            {
              bookId: 'press-start',
              bookLabel: 'Press Start',
              definition: null,
              firstSeen: '§72',
            },
            {
              bookId: 'rules-game',
              bookLabel: 'Rules Game',
              definition: 'punto di osservazione strategica',
              firstSeen: '§147',
            },
          ]}
        />
        <TermRow
          term="runa"
          baseDef="simbolo magico inciso"
          contextCount={1}
          expanded={false}
        />
      </div>
    </div>
  );
}

function TermRow({ term, baseDef, contextCount, expanded, variants }) {
  const hasVariants = contextCount > 1;

  return (
    <div style={{
      borderRadius: 'var(--r-lg)',
      border: expanded
        ? `1.5px solid ${entityHsl('kb', 0.36)}`
        : '1px solid var(--border-light)',
      background: expanded ? entityHsl('kb', 0.04) : 'var(--bg-card)',
      overflow: 'hidden',
    }}>
      {/* Collapsible row header */}
      <button
        aria-expanded={expanded}
        aria-controls={`variants-${term}`}
        style={{
          width: '100%',
          display: 'flex', alignItems: 'center', gap: 10,
          padding: '12px 14px',
          border: 'none', background: 'transparent',
          textAlign: 'left',
          cursor: 'pointer',
          fontFamily: 'inherit',
        }}>
        {/* Chevron icon (rotates on expand) */}
        <span style={{
          display: 'inline-flex',
          width: 18, height: 18, flexShrink: 0,
          alignItems: 'center', justifyContent: 'center',
          color: 'var(--text-muted)',
          transform: expanded ? 'rotate(90deg)' : 'rotate(0deg)',
          transition: 'transform 160ms var(--ease-out, ease-out)',
          fontSize: 14, fontWeight: 800, lineHeight: 1,
        }}>›</span>

        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{
            display: 'flex', alignItems: 'baseline', gap: 8,
            marginBottom: 2, flexWrap: 'wrap',
          }}>
            <span style={{
              fontFamily: 'var(--f-display)', fontWeight: 700,
              fontSize: 15, color: 'var(--text)',
            }}>{term}</span>
            {hasVariants && (
              <span style={{
                padding: '1px 7px',
                borderRadius: 'var(--r-sm)',
                background: entityHsl('game', 0.14),
                color: entityHsl('game'),
                fontFamily: 'var(--f-mono)', fontSize: 9.5, fontWeight: 700,
                textTransform: 'uppercase', letterSpacing: '0.06em',
              }}>{contextCount} varianti</span>
            )}
          </div>
          <div style={{
            fontSize: 12.5, color: 'var(--text-sec)', lineHeight: 1.4,
            overflow: 'hidden', textOverflow: 'ellipsis',
          }}>{baseDef}</div>
        </div>
      </button>

      {/* Expanded variants list */}
      {expanded && variants && (
        <div
          id={`variants-${term}`}
          style={{
            padding: '4px 14px 14px',
            borderTop: `1px dashed ${entityHsl('kb', 0.22)}`,
            display: 'flex', flexDirection: 'column', gap: 8,
            marginTop: 4,
          }}>
          <div style={{
            fontFamily: 'var(--f-mono)', fontSize: 9.5,
            color: 'var(--text-muted)',
            textTransform: 'uppercase', letterSpacing: '0.07em',
            fontWeight: 700,
            marginTop: 10, marginBottom: 4,
          }}>
            Varianti per libro
          </div>
          {variants.map(v => (
            <VariantRow key={v.bookId} variant={v} />
          ))}
        </div>
      )}
    </div>
  );
}

function VariantRow({ variant }) {
  const usesBase = variant.definition == null;

  return (
    <div style={{
      padding: '10px 12px',
      borderRadius: 'var(--r-md)',
      border: '1px solid var(--border-light)',
      background: 'var(--bg)',
      display: 'flex', alignItems: 'flex-start', gap: 10,
    }}>
      {/* Book badge */}
      <span style={{
        display: 'inline-flex', alignItems: 'center', gap: 5,
        padding: '3px 9px',
        borderRadius: 'var(--r-pill)',
        background: entityHsl('kb', 0.12),
        color: entityHsl('kb'),
        fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: 700,
        border: `1px solid ${entityHsl('kb', 0.28)}`,
        flexShrink: 0,
        whiteSpace: 'nowrap',
      }}>
        <span>📖</span>{variant.bookLabel}
      </span>

      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{
          fontSize: 13, color: 'var(--text)', lineHeight: 1.45,
          fontFamily: 'var(--f-display)',
          fontStyle: usesBase ? 'italic' : 'normal',
          opacity: usesBase ? 0.7 : 1,
        }}>
          {usesBase ? '(usa definizione base)' : variant.definition}
        </div>
        <div style={{
          marginTop: 5,
          display: 'flex', alignItems: 'center', gap: 10,
          fontFamily: 'var(--f-mono)', fontSize: 10,
          color: 'var(--text-muted)',
        }}>
          <a href="#" style={{
            color: entityHsl('game'),
            textDecoration: 'underline',
            textDecorationStyle: 'dotted',
            fontWeight: 700,
          }}>Vedi paragrafo originale {variant.firstSeen}</a>
        </div>
      </div>

      {/* Delete variant */}
      <button
        aria-label={`Elimina variante ${variant.bookLabel}`}
        style={{
          width: 26, height: 26,
          borderRadius: 'var(--r-sm)',
          border: '1px solid var(--border-light)',
          background: 'transparent',
          color: 'var(--text-muted)',
          fontSize: 13, lineHeight: 1, padding: 0,
          cursor: 'pointer',
          flexShrink: 0,
        }}>🗑</button>
    </div>
  );
}

/* =====================================================================
   NUOVO sub-screen 08 — FilterStrip + ContextBadges
   ─────────────────────────────────────────────────────────────────────
   Header glossary index sempre visibile:
     · search input (term/definition/book)
     · filter by book chip multi-select
     · toggle "Solo con varianti"
   Ogni term row mostra context badges inline (📖 Press Start + 📖 Rules Game).
   ===================================================================== */

function FilterStripGlossaryIndex({ desktop = false }) {
  return (
    <div style={{
      position: 'absolute', inset: 0,
      background: 'var(--bg)',
      overflowY: 'auto',
      padding: desktop ? '16px 28px' : '12px 14px',
    }}>
      <div style={{
        marginBottom: 14,
      }}>
        <h2 style={{
          margin: '0 0 8px',
          fontFamily: 'var(--f-display)', fontWeight: 700,
          fontSize: 18, color: 'var(--text)',
        }}>Glossario · 12 termini</h2>
      </div>

      {/* Search input */}
      <div style={{
        position: 'relative',
        marginBottom: 10,
      }}>
        <span style={{
          position: 'absolute',
          left: 12, top: '50%',
          transform: 'translateY(-50%)',
          color: 'var(--text-muted)',
          fontSize: 14,
          pointerEvents: 'none',
        }}>🔍</span>
        <div
          role="searchbox"
          aria-label="Cerca nel glossario"
          style={{
            padding: '10px 12px 10px 38px',
            borderRadius: 'var(--r-md)',
            border: '1px solid var(--border-light)',
            background: 'var(--bg-card)',
            color: 'var(--text-muted)',
            fontFamily: 'var(--f-display)', fontSize: 13.5,
          }}>
          Cerca per termine, definizione, libro…
        </div>
      </div>

      {/* Filter chips: book multi-select */}
      <div style={{
        display: 'flex', alignItems: 'center', gap: 6, flexWrap: 'wrap',
        marginBottom: 8,
      }}>
        <span style={{
          fontFamily: 'var(--f-mono)', fontSize: 10,
          color: 'var(--text-muted)',
          textTransform: 'uppercase', letterSpacing: '0.06em',
          fontWeight: 700,
          marginRight: 4,
        }}>Libri:</span>
        <FilterChip label="📖 Press Start" active />
        <FilterChip label="📖 Rules Game" active />
        <FilterChip label="📖 Encounter Book" />
      </div>

      {/* Toggle: solo con varianti */}
      <label style={{
        display: 'inline-flex', alignItems: 'center', gap: 8,
        padding: '6px 12px',
        borderRadius: 'var(--r-pill)',
        background: entityHsl('game', 0.08),
        border: `1px solid ${entityHsl('game', 0.28)}`,
        cursor: 'pointer',
        marginBottom: 16,
      }}>
        <span
          role="switch"
          aria-checked="true"
          style={{
            width: 28, height: 16, borderRadius: 999,
            background: entityHsl('game'),
            position: 'relative',
            flexShrink: 0,
          }}>
          <span style={{
            position: 'absolute',
            right: 2, top: 2,
            width: 12, height: 12, borderRadius: '50%',
            background: '#fff',
          }} />
        </span>
        <span style={{
          fontFamily: 'var(--f-display)', fontSize: 12.5, fontWeight: 600,
          color: entityHsl('game'),
        }}>Solo con varianti</span>
      </label>

      {/* Term rows with context badges */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
        <TermRowWithBadges
          term="sentinel"
          baseDef="soldato di guardia"
          books={['Press Start', 'Rules Game']}
        />
        <TermRowWithBadges
          term="ferrigni"
          baseDef="razziatori di pietra-ferro"
          books={['Press Start', 'Rules Game', 'Encounter Book']}
        />
        <TermRowWithBadges
          term="runa"
          baseDef="simbolo magico inciso"
          books={['Press Start']}
        />
        <TermRowWithBadges
          term="threshold"
          baseDef="soglia mistica del cerchio"
          books={['Rules Game', 'Encounter Book']}
        />
      </div>
    </div>
  );
}

function FilterChip({ label, active }) {
  return (
    <button style={{
      padding: '4px 10px',
      borderRadius: 'var(--r-pill)',
      border: active
        ? `1.5px solid ${entityHsl('kb', 0.42)}`
        : '1px solid var(--border-light)',
      background: active ? entityHsl('kb', 0.12) : 'var(--bg-card)',
      color: active ? entityHsl('kb') : 'var(--text-sec)',
      fontFamily: 'var(--f-display)', fontSize: 12, fontWeight: 600,
      cursor: 'pointer',
      whiteSpace: 'nowrap',
    }}>{label}</button>
  );
}

function TermRowWithBadges({ term, baseDef, books }) {
  const hasMultiContext = books.length > 1;

  return (
    <div style={{
      padding: '11px 12px',
      borderRadius: 'var(--r-lg)',
      border: '1px solid var(--border-light)',
      background: 'var(--bg-card)',
    }}>
      <div style={{
        display: 'flex', alignItems: 'center', gap: 8,
        flexWrap: 'wrap',
        marginBottom: 4,
      }}>
        <span style={{
          fontFamily: 'var(--f-display)', fontWeight: 700,
          fontSize: 14, color: 'var(--text)',
        }}>{term}</span>
        {/* Context badges inline */}
        {books.map(book => (
          <span key={book} style={{
            display: 'inline-flex', alignItems: 'center', gap: 4,
            padding: '1px 7px',
            borderRadius: 'var(--r-sm)',
            background: entityHsl('kb', 0.10),
            color: entityHsl('kb'),
            fontFamily: 'var(--f-mono)', fontSize: 9.5, fontWeight: 700,
            border: `1px solid ${entityHsl('kb', 0.22)}`,
            textTransform: 'none', letterSpacing: 0,
          }}>📖 {book}</span>
        ))}
        {hasMultiContext && (
          <span style={{
            marginLeft: 'auto',
            fontFamily: 'var(--f-mono)', fontSize: 9.5,
            color: entityHsl('game'),
            textTransform: 'uppercase', letterSpacing: '0.06em',
            fontWeight: 700,
          }}>{books.length} varianti</span>
        )}
      </div>
      <div style={{
        fontSize: 12.5, color: 'var(--text-sec)', lineHeight: 1.4,
      }}>{baseDef}</div>
    </div>
  );
}

/* =====================================================================
   Phone + Desktop frames
   ===================================================================== */

function PhoneFrame({ children }) {
  return (
    <div className="gl-phone">
      <div className="gl-phone-sbar">
        <span>9:41</span>
        <span style={{ display: 'flex', gap: 4, fontSize: 11 }}>
          <span>•••</span><span>WiFi</span><span>87%</span>
        </span>
      </div>
      <div className="gl-phone-body">{children}</div>
      <div className="gl-phone-home" />
    </div>
  );
}

function DesktopFrame({ children, url }) {
  return (
    <div className="gl-desktop">
      <div className="gl-desktop-bar">
        <span className="traffic"></span>
        <span className="traffic"></span>
        <span className="traffic"></span>
        <span className="url">app.meepleai.com{url}</span>
      </div>
      <div className="gl-desktop-body">{children}</div>
    </div>
  );
}

/* =====================================================================
   App shell — section blocks
   ===================================================================== */

function StateBlock({ id, title, gherkin, frame, children }) {
  return (
    <section id={id} data-state={id} data-screen-label={`H ${id}`}>
      <div className="gl-section-label">
        <span>{title}</span>
        <span className="anchor">#{id}</span>
        {gherkin && <span className="gherkin">{gherkin}</span>}
        <span style={{
          fontFamily: 'var(--f-mono)', fontSize: 10, color: 'var(--text-muted)',
          textTransform: 'none', letterSpacing: 0,
          padding: '2px 8px', borderRadius: 'var(--r-sm)',
          background: 'var(--bg-muted)', border: '1px solid var(--border-light)',
        }}>{frame}</span>
      </div>
      {children}
    </section>
  );
}

function App() {
  const [state, setState] = useState('all');
  const [frame, setFrame] = useState('both');

  useEffect(() => {
    const handlers = [
      ['gl-tweak-state', e => setState(e.detail)],
      ['gl-tweak-frame', e => setFrame(e.detail)],
    ];
    handlers.forEach(([n, h]) => document.addEventListener(n, h));
    return () => handlers.forEach(([n, h]) => document.removeEventListener(n, h));
  }, []);

  const showState = key => state === 'all' || state === key;
  const showMobile = frame !== 'desktop';
  const showDesktop = frame !== 'mobile';

  return (
    <div>
      {showState('state-01') && showMobile && (
        <StateBlock
          id="state-01-edit-pristine"
          title="01 · Edit pristine — Aaron tappa pill 'Voidstone'"
          gherkin="@N3.5 entry"
          frame="mobile · 375 × 780 · fullscreen sheet"
        >
          <div className="gl-frame-caption">
            term_en read-only "Voidstone" · input editable "Pietra del Vuoto" · chip §147 · Salva <strong>disabled</strong>
            <span className="theme-tag">no dirty state</span>
          </div>
          <div className="gl-state-grid">
            <PhoneFrame>
              <FauxViewerBackdrop />
              <GlossaryEditorModal variant="pristine" />
            </PhoneFrame>
          </div>
        </StateBlock>
      )}

      {showState('state-02') && showMobile && (
        <StateBlock
          id="state-02-edited"
          title="02 · Edited — input dirty 'Pietra del Caos'"
          gherkin="@N3.5 dirty"
          frame="mobile · 375 × 780 · fullscreen sheet"
        >
          <div className="gl-frame-caption">
            Salva <strong>enabled</strong> · diff hint era→nuovo · helper "Verrà applicato a 12 termini in traduzioni future"
            <span className="theme-tag">accent game</span>
          </div>
          <div className="gl-state-grid">
            <PhoneFrame>
              <FauxViewerBackdrop />
              <GlossaryEditorModal variant="edited" />
            </PhoneFrame>
          </div>
        </StateBlock>
      )}

      {showState('state-03') && showMobile && (
        <StateBlock
          id="state-03-save-error"
          title="03 · Save error — banner amber + retry"
          gherkin="@N3.5 network failure"
          frame="mobile · 375 × 780 · fullscreen sheet"
        >
          <div className="gl-frame-caption">
            Banner amber var(--c-warning) "Errore salvataggio. Il tuo testo è ancora qui — riprova" · CTA "↻ Riprova" · value preservato
            <span className="theme-tag">tono rassicurante</span>
          </div>
          <div className="gl-state-grid">
            <PhoneFrame>
              <FauxViewerBackdrop />
              <GlossaryEditorModal variant="error" />
            </PhoneFrame>
          </div>
        </StateBlock>
      )}

      {showState('state-04') && showMobile && (
        <StateBlock
          id="state-04-collision"
          title="04 · Collision — 'Razziatore' già usato per 'Reaver'"
          gherkin="@N3.5 unique constraint"
          frame="mobile · 375 × 780 · fullscreen sheet"
        >
          <div className="gl-frame-caption">
            Banner red entity=event · 2 CTA: [Sovrascrivi] destructive + [Cambia traduzione] preferred · tono warning non aggressivo
            <span className="theme-tag">vertical CTA stack</span>
          </div>
          <div className="gl-state-grid">
            <PhoneFrame>
              <FauxViewerBackdrop />
              <GlossaryEditorModal variant="collision" />
            </PhoneFrame>
          </div>
        </StateBlock>
      )}

      {/* DESKTOP variants */}
      {showState('state-01') && showDesktop && (
        <StateBlock
          id="state-01-desktop"
          title="01 · Desktop — modal centered 480px"
          gherkin="@N3.5 desktop adaptation"
          frame="desktop · 1280 × 720 · centered overlay"
        >
          <div className="gl-frame-caption">
            Modal card 480px · scrim rgba(0,0,0,0.55) + blur · ESC + click-outside per close (preserve unsaved con confirm)
            <span className="theme-tag">light</span>
          </div>
          <DesktopFrame url="/library/games/nanolith/play/§147">
            <FauxViewerBackdrop withScrim={false} />
            <GlossaryEditorModal variant="pristine" desktop />
          </DesktopFrame>
        </StateBlock>
      )}

      {showState('state-04') && showDesktop && (
        <StateBlock
          id="state-04-desktop-conflict"
          title="04 · Desktop conflict — modal + side panel 'Reaver → Razziatore'"
          gherkin="@N3.5 desktop conflict reference"
          frame="desktop · 1280 × 720 · centered + side panel 200px"
        >
          <div className="gl-frame-caption">
            Modal centrale 480px + pannello laterale 200px var(--c-event) · mostra "Reaver → Razziatore" come reference contestuale
            <span className="theme-tag">side-panel reference</span>
          </div>
          <DesktopFrame url="/library/games/nanolith/play/§147">
            <FauxViewerBackdrop withScrim={false} />
            <GlossaryEditorModal variant="collision" desktop withConflictPanel />
          </DesktopFrame>
        </StateBlock>
      )}

      {/* =====================================================================
          NUOVI sub-screen sezione 1c — context-aware glossary
          ===================================================================== */}

      {/* 05 · Conflict dialog — context_similarity < 0.85 */}
      {showState('state-05') && showMobile && (
        <StateBlock
          id="state-05-conflict-dialog-mobile"
          title="05 · Conflict dialog mobile — 'sentinel' già nel glossario · sim 0.62 < 0.85"
          gherkin="@1c context-similarity threshold"
          frame="mobile · 375 × 780 · fullscreen sheet"
        >
          <div className="gl-frame-caption">
            Side-by-side: Press Start "soldato di guardia" vs Rules Game "punto di osservazione strategica" · 3 CTA radio · default "Mantieni esistente" (soft, no destructive)
            <span className="theme-tag">role=radiogroup</span>
            <span className="theme-tag">sim 0.62 &lt; 0.85</span>
          </div>
          <div className="gl-state-grid">
            <PhoneFrame>
              <FauxViewerBackdrop />
              <ConflictDialog />
            </PhoneFrame>
          </div>
        </StateBlock>
      )}

      {showState('state-05') && showDesktop && (
        <StateBlock
          id="state-05-conflict-dialog-desktop"
          title="05 · Conflict dialog desktop — modal 540px centered"
          gherkin="@1c context-aware"
          frame="desktop · 1280 × 720 · centered overlay"
        >
          <div className="gl-frame-caption">
            Definizioni side-by-side in grid 2-col · radio CTA visibili immediate · threshold context_similarity = 0.85 annotato in header
            <span className="theme-tag">grid 2-col</span>
          </div>
          <DesktopFrame url="/library/runa-ardenel/play/§147?glossary=conflict">
            <FauxViewerBackdrop withScrim={false} />
            <ConflictDialog desktop />
          </DesktopFrame>
        </StateBlock>
      )}

      {/* 06 · Bulk import card — ≥3 nuove parole rilevate */}
      {showState('state-06') && showMobile && (
        <StateBlock
          id="state-06-bulk-import-inline-mobile"
          title="06a · Bulk import inline card — 5 nuove parole in §147"
          gherkin="@1c bulk-import threshold ≥3"
          frame="mobile · 375 × 780 · inline card sopra viewer"
        >
          <div className="gl-frame-caption">
            Card inline bottom · 5 chip preview (ferrigni, runa, threshold, veglia cava, ardenel) · 2 CTA: "Rivedi" + "Aggiungi tutte (5)" · trigger ≥3 (NO ≥1, evita fatigue)
            <span className="theme-tag">non-blocking</span>
            <span className="theme-tag">threshold ≥3</span>
          </div>
          <div className="gl-state-grid">
            <PhoneFrame>
              <FauxViewerBackdrop withScrim={false} />
              <BulkImportCard variant="inline" />
            </PhoneFrame>
          </div>
        </StateBlock>
      )}

      {showState('state-06') && showMobile && (
        <StateBlock
          id="state-06-bulk-import-review-mobile"
          title="06b · Bulk import review modal — multi-select w/ inline edit"
          gherkin="@1c bulk-import review"
          frame="mobile · 375 × 780 · fullscreen sheet"
        >
          <div className="gl-frame-caption">
            Click "Rivedi" → modal multi-select · 5 row con checkbox + def inline-editable · listbox aria-multiselectable="true" · footer "Aggiungi 4 parole"
            <span className="theme-tag">role=listbox</span>
            <span className="theme-tag">aria-multiselectable</span>
          </div>
          <div className="gl-state-grid">
            <PhoneFrame>
              <FauxViewerBackdrop />
              <BulkImportCard variant="review" />
            </PhoneFrame>
          </div>
        </StateBlock>
      )}

      {showState('state-06') && showDesktop && (
        <StateBlock
          id="state-06-bulk-import-desktop"
          title="06 · Bulk import desktop — inline card centered + review modal"
          gherkin="@1c bulk-import desktop"
          frame="desktop · 1280 × 720"
        >
          <div className="gl-frame-caption">
            Card 560px max-width centered bottom · non-blocking · stesso pattern del mobile ma con padding maggiore
            <span className="theme-tag">light</span>
          </div>
          <DesktopFrame url="/library/runa-ardenel/play/§147?glossary=bulk">
            <FauxViewerBackdrop withScrim={false} />
            <BulkImportCard variant="inline" desktop />
          </DesktopFrame>
        </StateBlock>
      )}

      {/* 07 · Variant expansion — term row collapsed vs expanded */}
      {showState('state-07') && showMobile && (
        <StateBlock
          id="state-07-variant-expansion-mobile"
          title="07 · Variant expansion — 'sentinel' con 2 contexts per book"
          gherkin="@1c contexts.length > 1"
          frame="mobile · 375 × 780 · glossary index"
        >
          <div className="gl-frame-caption">
            3 term rows: ferrigni (collapsed, 1 context) · sentinel (expanded, 2 varianti per "Press Start" usa-base + "Rules Game" definition specifica) · runa (collapsed) · chevron ruota su expand · aria-expanded + aria-controls
            <span className="theme-tag">aria-expanded</span>
            <span className="theme-tag">chevron rotate</span>
          </div>
          <div className="gl-state-grid">
            <PhoneFrame>
              <VariantExpansionList />
            </PhoneFrame>
          </div>
        </StateBlock>
      )}

      {showState('state-07') && showDesktop && (
        <StateBlock
          id="state-07-variant-expansion-desktop"
          title="07 · Variant expansion desktop — glossary index w/ expanded row"
          gherkin="@1c variant deletion"
          frame="desktop · 1280 × 720 · glossary index"
        >
          <div className="gl-frame-caption">
            Click row → expand varianti per book · ogni variante: book badge "📖 Press Start"/"📖 Rules Game" + definition (o "(usa base)" se null) + link first_seen + delete button · last variant deletion → term torna single-context
            <span className="theme-tag">variant delete</span>
          </div>
          <DesktopFrame url="/library/runa-ardenel/glossary">
            <VariantExpansionList desktop />
          </DesktopFrame>
        </StateBlock>
      )}

      {/* 08 · Filter strip + context badges */}
      {showState('state-08') && showMobile && (
        <StateBlock
          id="state-08-filter-strip-mobile"
          title="08 · Filter strip + context badges — glossary index header"
          gherkin="@1c filter + badges"
          frame="mobile · 375 × 780 · glossary index"
        >
          <div className="gl-frame-caption">
            Search input "Cerca per termine, definizione, libro…" · filter chips multi-select per book (Press Start + Rules Game attivi, Encounter Book disattivo) · toggle "Solo con varianti" ON · ogni term row mostra context badges inline (📖 Press Start + 📖 Rules Game)
            <span className="theme-tag">multi-select</span>
            <span className="theme-tag">role=switch</span>
          </div>
          <div className="gl-state-grid">
            <PhoneFrame>
              <FilterStripGlossaryIndex />
            </PhoneFrame>
          </div>
        </StateBlock>
      )}

      {showState('state-08') && showDesktop && (
        <StateBlock
          id="state-08-filter-strip-desktop"
          title="08 · Filter strip desktop — search + chips + toggle + badges inline"
          gherkin="@1c filter-strip desktop"
          frame="desktop · 1280 × 720 · glossary index"
        >
          <div className="gl-frame-caption">
            Stesso layout mobile ma con padding orizzontale maggiore · 4 term rows demo con varianti count badge a destra "N varianti" quando books.length &gt; 1
            <span className="theme-tag">context badges inline</span>
          </div>
          <DesktopFrame url="/library/runa-ardenel/glossary?filter=multi-context">
            <FilterStripGlossaryIndex desktop />
          </DesktopFrame>
        </StateBlock>
      )}
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(<App />);
