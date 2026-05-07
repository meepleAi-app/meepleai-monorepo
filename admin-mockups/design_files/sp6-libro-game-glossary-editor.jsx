/* ===================================================================
   SP6 Libro Game — Glossary Editor (sp6-libro-game-glossary-editor.jsx)
   Coverage: scenario N3.5 inline pill edit (design doc 2026-05-07).
   Modal sopra il viewer D translation-viewer quando Aaron tappa una
   glossary pill cliccabile inline al testo del paragrafo §147.

   Stati:
     state-01-edit-pristine     — Voidstone → Pietra del Vuoto, Salva disabled
     state-02-edited            — input dirty → "Pietra del Caos", Salva enabled
     state-03-save-error        — banner amber + Retry, value preservato
     state-04-collision         — banner red, [Sovrascrivi] + [Cambia traduzione]
     state-01-desktop           — modal centered 480px
     state-04-desktop-conflict  — modal centered + side panel 200px "Reaver → Razziatore"

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
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(<App />);
