/* ===================================================================
   SP6 Libro Game — Resume State (sp6-libro-game-resume-state.jsx)
   Route: /library/games/[gameId]/play (entry point shell)
   Persona: Aaron (badsworm@gmail.com), project owner MeepleAI / superadmin dogfooding.
            Estensione family Sara — tono UI identico, bypass quota, NO admin-tecnico look.

   Coverage Gherkin design doc 2026-05-07-libro-game-nanolith-demo-design.md:
     @N4.1 — resume sessione N giorni dopo (happy + dogfood)
     @N4.4 — multi-campagna parallela (edge)
     @N4.5 — campagna stale > 90 giorni (edge, soft-delete archive option)

   Stati (anchor id "state-NN-name" per scroll deep-link, pattern emerso in C/D):
     state-01-first-time         — empty, hero "Inizia la tua prima campagna"
     state-02-single-resume      — 1 campagna 7gg fa, glossary preview top-5
     state-03-multi-campaign     — 2 campagne (entityHsl game vs agent)
     state-04-stale-warning      — 1 campagna 100gg fa, warning amber rispettoso

   Desktop variants:
     state-02-desktop-split-view — 380px lista sx + dettaglio dx
     state-03-desktop-grid       — 2-col grid

   Vincolo FREEZE v2 #807/#808: solo entityHsl(entity, alpha) o token semantici.
   =================================================================== */

const { useState, useEffect, useMemo } = React;

/* ─── entityHsl helper inline (palette 9 entity, replicato dal brief master righe 73-88) ─── */
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

/* ─── Realistic dogfooding data (Aaron + Nanolith from §1.1 design doc) ─── */
const AARON = {
  name: 'Aaron',
  handle: 'badsworm',
  email: 'badsworm@gmail.com',
  role: 'superadmin',
};

// Nanolith — referenced as Aaron's primary game in design doc §1.1
// Second campaign (state-03) uses Tainted Grail as plausible parallel — both real titles, EN-only narrative.
const NANOLITH_PARTY = [
  { id: 'p-marco',   name: 'Marco',   class: 'Mago',     hp: 14, color: 'session' },
  { id: 'p-giulia',  name: 'Giulia',  class: 'Razziatrice', hp: 18, color: 'event' },
  { id: 'p-luca',    name: 'Luca',    class: 'Guaritore',hp: 12, color: 'toolkit' },
  { id: 'p-aaron',   name: 'Aaron',   class: 'Esploratore', hp: 16, color: 'player' },
];

const NANOLITH_GLOSSARY_TOP5 = [
  { en: 'Voidstone',    it: 'Pietra del Vuoto',  edited: false },
  { en: 'Reaver',       it: 'Razziatore',        edited: false },
  { en: 'Hollow Watch', it: 'Veglia Cava',       edited: true  },
  { en: 'Pale Wardens', it: 'Sentinelle Pallide',edited: false },
  { en: 'Ember Court',  it: 'Corte di Brace',    edited: false },
];
const NANOLITH_GLOSSARY_FULL_COUNT = 12;

const TG_PARTY = [
  { id: 'p-tg-niamh',   name: 'Niamh',   class: 'Cacciatrice', hp: 15, color: 'event' },
  { id: 'p-tg-arawn',   name: 'Arawn',   class: 'Druido',      hp: 13, color: 'toolkit' },
  { id: 'p-tg-aaron',   name: 'Aaron',   class: 'Bardo',       hp: 14, color: 'player' },
];

/* =====================================================================
   Atoms — riusati da pattern A/C/D (stessa visual language)
   ===================================================================== */

function PhoneFrame({ children, sepia, theme }) {
  // .lg-phone bezel (replicato 1:1 dai mockup A index, C play-session, D translation-viewer).
  // Theme override solo sepia (brief Capitolo 2 menziona phone-sp6 light/dark/sepia).
  const cls = ['lg-phone'];
  if (sepia) cls.push('theme-sepia');
  const overrideTheme = theme && !sepia ? { 'data-theme-override': theme } : {};
  return (
    <div className={cls.join(' ')} {...overrideTheme}>
      <div className="lg-phone-sbar">
        <span>9:41</span>
        <span className="ind"><span>•••</span><span>WiFi</span><span>87%</span></span>
      </div>
      <div className="lg-phone-body">{children}</div>
      <div className="lg-phone-home" />
    </div>
  );
}

function DesktopFrame({ children, url }) {
  return (
    <div className="lg-desktop">
      <div className="lg-desktop-bar">
        <span className="traffic"></span>
        <span className="traffic"></span>
        <span className="traffic"></span>
        <span className="url">app.meepleai.com{url}</span>
      </div>
      <div className="lg-desktop-body">{children}</div>
    </div>
  );
}

/* App top-bar inside phone — replicato dal pattern emerso in mockup C play-session header */
function AppTopBar({ gameTitle, subtitle, accent = 'game', avatarSuper = false }) {
  return (
    <div style={{
      display: 'flex', alignItems: 'center', gap: 10,
      padding: '12px 16px 10px', borderBottom: '1px solid var(--border-light)',
      background: 'var(--bg)', position: 'sticky', top: 0, zIndex: 5,
    }}>
      <button aria-label="Indietro" style={{
        width: 32, height: 32, borderRadius: 'var(--r-md)',
        border: '1px solid var(--border)', background: 'var(--bg-card)',
        color: 'var(--text-sec)', fontSize: 16, padding: 0,
      }}>‹</button>
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{
          fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 15,
          color: 'var(--text)', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis',
        }}>{gameTitle}</div>
        <div style={{
          fontFamily: 'var(--f-mono)', fontSize: 10, color: 'var(--text-muted)',
          textTransform: 'uppercase', letterSpacing: '0.05em', marginTop: 1,
        }}>{subtitle}</div>
      </div>
      <div style={{ position: 'relative' }}>
        <div style={{
          width: 32, height: 32, borderRadius: '50%',
          background: entityHsl('player', 0.18),
          color: entityHsl('player'),
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 13,
          border: `1.5px solid ${entityHsl('player', 0.45)}`,
        }}>A</div>
        {avatarSuper && (
          <div title="superadmin · dogfooding" style={{
            position: 'absolute', bottom: -2, right: -2,
            width: 14, height: 14, borderRadius: '50%',
            background: entityHsl('agent'),
            border: '2px solid var(--bg)',
            display: 'flex', alignItems: 'center', justifyContent: 'center',
            fontSize: 8, color: '#fff', fontWeight: 700,
          }}>★</div>
        )}
      </div>
    </div>
  );
}

/* Pill chip — riusato per glossary preview cluster (kb entity color) */
function GlossaryPill({ en, it, edited, dense }) {
  return (
    <span
      role="button"
      tabIndex={0}
      title={`${en} → ${it}${edited ? ' (modificato da te)' : ''}`}
      style={{
        display: 'inline-flex', alignItems: 'center', gap: 6,
        padding: dense ? '3px 8px' : '4px 10px',
        borderRadius: 'var(--r-pill)',
        background: entityHsl('kb', 0.10),
        border: `1px solid ${entityHsl('kb', 0.28)}`,
        color: entityHsl('kb'),
        fontFamily: 'var(--f-display)',
        fontSize: dense ? 11 : 12,
        fontWeight: 600,
        whiteSpace: 'nowrap',
        cursor: 'pointer',
      }}
    >
      <span style={{ fontWeight: 700 }}>{en}</span>
      <span style={{ opacity: 0.55 }}>→</span>
      <span>{it}</span>
      {edited && (
        <span aria-label="Modificato da te" style={{
          fontSize: 9, marginLeft: 2, color: entityHsl('agent'),
        }}>✎</span>
      )}
    </span>
  );
}

/* Party pip — riusato dal pattern ConnectionPip emerso in C */
function PartyPip({ member }) {
  const initials = member.name.slice(0, 1).toUpperCase();
  return (
    <span
      title={`${member.name} · ${member.class} · HP ${member.hp}`}
      style={{
        display: 'inline-flex', alignItems: 'center', gap: 6,
        padding: '4px 10px 4px 4px',
        borderRadius: 'var(--r-pill)',
        background: 'var(--bg-card)',
        border: `1px solid ${entityHsl(member.color, 0.30)}`,
        fontFamily: 'var(--f-display)', fontSize: 12, fontWeight: 600,
        color: 'var(--text)',
      }}
    >
      <span style={{
        width: 22, height: 22, borderRadius: '50%',
        background: entityHsl(member.color, 0.18),
        color: entityHsl(member.color),
        display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
        fontWeight: 700, fontSize: 11,
      }}>{initials}</span>
      <span>{member.name}</span>
    </span>
  );
}

/* Session pulse dot — entity=session pulsing only when active.
   prefers-reduced-motion: disabled (replicato da brief master deviazione "session pulsing reduced-motion") */
function SessionPulse({ active }) {
  if (!active) return null;
  return (
    <span style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}>
      <span style={{
        width: 8, height: 8, borderRadius: '50%',
        background: entityHsl('session'),
        boxShadow: `0 0 0 0 ${entityHsl('session', 0.5)}`,
        animation: 'sp6-pulse 1.6s var(--ease-out) infinite',
      }} />
      <span style={{
        fontFamily: 'var(--f-mono)', fontSize: 10, color: entityHsl('session'),
        textTransform: 'uppercase', letterSpacing: '0.06em', fontWeight: 700,
      }}>Attiva</span>
    </span>
  );
}

/* Buttons — replicato dal pattern .btn .primary/.ghost del design system */
function PrimaryBtn({ children, accent = 'game', size = 'md', onClick, disabled, full }) {
  const padY = size === 'lg' ? 14 : size === 'sm' ? 8 : 11;
  const padX = size === 'lg' ? 22 : size === 'sm' ? 14 : 18;
  const fz = size === 'lg' ? 15 : size === 'sm' ? 13 : 14;
  return (
    <button
      onClick={onClick}
      disabled={disabled}
      style={{
        display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
        gap: 8,
        padding: `${padY}px ${padX}px`,
        borderRadius: 'var(--r-md)',
        border: 'none',
        background: disabled ? 'var(--bg-muted)' : entityHsl(accent),
        color: disabled ? 'var(--text-muted)' : '#fff',
        fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: fz,
        boxShadow: disabled ? 'none' : `0 4px 14px ${entityHsl(accent, 0.30)}`,
        cursor: disabled ? 'not-allowed' : 'pointer',
        width: full ? '100%' : 'auto',
        letterSpacing: '-0.005em',
      }}
    >{children}</button>
  );
}
function GhostBtn({ children, onClick, full, size = 'md', danger }) {
  const padY = size === 'lg' ? 13 : size === 'sm' ? 7 : 10;
  const padX = size === 'lg' ? 20 : size === 'sm' ? 12 : 16;
  return (
    <button
      onClick={onClick}
      style={{
        display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
        gap: 8,
        padding: `${padY}px ${padX}px`,
        borderRadius: 'var(--r-md)',
        border: `1px solid ${danger ? entityHsl('event', 0.45) : 'var(--border-strong)'}`,
        background: 'transparent',
        color: danger ? entityHsl('event') : 'var(--text-sec)',
        fontFamily: 'var(--f-display)', fontWeight: 600, fontSize: 13,
        cursor: 'pointer',
        width: full ? '100%' : 'auto',
      }}
    >{children}</button>
  );
}

/* Cost indicator footer — dogfooding superadmin (visible but never blocking, N3.6) */
function CostFooter({ amount = '€0,00', sessionCount = 0, hidden }) {
  if (hidden) return null;
  return (
    <div style={{
      display: 'flex', alignItems: 'center', justifyContent: 'space-between',
      gap: 10,
      padding: '10px 16px',
      borderTop: '1px solid var(--border-light)',
      background: 'var(--bg-muted)',
      fontFamily: 'var(--f-mono)', fontSize: 10,
      color: 'var(--text-muted)',
      textTransform: 'uppercase', letterSpacing: '0.05em',
    }}>
      <span>● dogfood · superadmin · bypass quota</span>
      <span>{sessionCount > 0 ? `${amount} / ${sessionCount} sess` : amount}</span>
    </div>
  );
}

/* =====================================================================
   STATE 01 — first-time (Aaron mai giocato Nanolith)
   anchor: state-01-first-time
   ===================================================================== */

function State01Mobile() {
  return (
    <PhoneFrame>
      <AppTopBar gameTitle="Nanolith" subtitle="EN · Awaken Realms" avatarSuper={true} />
      <div style={{ padding: '24px 18px 32px', display: 'flex', flexDirection: 'column', gap: 20 }}>
        {/* Hero illustration — gradient placeholder no SVG complesso */}
        <div style={{
          aspectRatio: '4/3',
          borderRadius: 'var(--r-2xl)',
          background: `linear-gradient(135deg, ${entityHsl('game', 0.18)}, ${entityHsl('event', 0.14)})`,
          border: `1px solid ${entityHsl('game', 0.22)}`,
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          position: 'relative', overflow: 'hidden',
        }}>
          <div style={{
            position: 'absolute', inset: 0,
            background: `radial-gradient(circle at 30% 30%, ${entityHsl('game', 0.18)}, transparent 60%)`,
          }} />
          <div style={{
            fontFamily: 'var(--f-display)', fontSize: 56, fontWeight: 800,
            color: entityHsl('game', 0.9),
            position: 'relative', letterSpacing: '-0.02em',
          }}>§</div>
          <div style={{
            position: 'absolute', bottom: 12, left: 14,
            fontFamily: 'var(--f-mono)', fontSize: 10,
            color: 'var(--text-muted)',
            textTransform: 'uppercase', letterSpacing: '0.08em',
          }}>nessuna campagna</div>
        </div>

        <div>
          <h2 style={{
            fontFamily: 'var(--f-display)', fontWeight: 700,
            fontSize: 26, lineHeight: 1.2, margin: '0 0 8px',
            color: 'var(--text)', letterSpacing: '-0.01em',
          }}>Inizia la tua prima campagna</h2>
          <p style={{
            margin: 0, color: 'var(--text-sec)', fontSize: 14, lineHeight: 1.55,
          }}>
            Nanolith è una campagna mista lunga.
            Crea un party, fotografa le pagine narrative man mano e l'app
            terrà il segno paragrafo per paragrafo, sera dopo sera.
          </p>
        </div>

        {/* What you'll get — 3 mini reassurance cards (no filler, copia ridotta) */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
          {[
            { ic: '§', label: 'Riprendi all\'ultimo paragrafo', sub: 'Anche dopo settimane', e: 'kb' },
            { ic: '⊜', label: 'Glossario coerente', sub: 'Voidstone resta sempre "Pietra del Vuoto"', e: 'agent' },
            { ic: '◑', label: 'Più campagne in parallelo', sub: 'Un party per gruppo di amici', e: 'session' },
          ].map((row, i) => (
            <div key={i} style={{
              display: 'flex', alignItems: 'center', gap: 12,
              padding: '12px 14px',
              borderRadius: 'var(--r-lg)',
              background: 'var(--bg-card)',
              border: '1px solid var(--border)',
            }}>
              <span style={{
                width: 32, height: 32, borderRadius: 'var(--r-md)',
                background: entityHsl(row.e, 0.14),
                color: entityHsl(row.e),
                display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
                fontFamily: 'var(--f-display)', fontWeight: 800, fontSize: 17,
                flexShrink: 0,
              }}>{row.ic}</span>
              <div style={{ minWidth: 0 }}>
                <div style={{
                  fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 13,
                  color: 'var(--text)',
                }}>{row.label}</div>
                <div style={{
                  fontSize: 12, color: 'var(--text-muted)', marginTop: 1,
                }}>{row.sub}</div>
              </div>
            </div>
          ))}
        </div>

        <PrimaryBtn accent="game" size="lg" full>
          + Crea nuova campagna
        </PrimaryBtn>
        <button style={{
          background: 'transparent', border: 'none',
          color: 'var(--text-muted)', fontFamily: 'var(--f-display)',
          fontSize: 12, fontWeight: 600, padding: '6px 0', cursor: 'pointer',
          textDecoration: 'underline', textUnderlineOffset: 3,
        }}>Come funziona la prima sessione →</button>
      </div>
      <CostFooter amount="€0,00" sessionCount={0} />
    </PhoneFrame>
  );
}

/* =====================================================================
   STATE 02 — single-resume (1 campagna, ultima 7gg fa)
   anchor: state-02-single-resume — coverage @N4.1 happy
   ===================================================================== */

function ResumeHero({ campaign, accent = 'game', daysAgo, stale, sepia }) {
  const isStale = stale === true;
  return (
    <div style={{
      borderRadius: 'var(--r-2xl)',
      background: 'var(--bg-card)',
      border: `1.5px solid ${isStale ? entityHsl('event', 0.45) : entityHsl(accent, 0.36)}`,
      boxShadow: `0 12px 32px ${isStale ? entityHsl('event', 0.10) : entityHsl(accent, 0.14)}`,
      overflow: 'hidden',
      position: 'relative',
    }}>
      {/* Accent strip top (game color, replicato pattern A index card) */}
      <div style={{
        height: 4,
        background: `linear-gradient(90deg, ${entityHsl(accent)}, ${entityHsl(accent, 0.4)})`,
      }} />

      {/* Stale warning banner (state-04 only) — amber, rispettoso */}
      {isStale && (
        <div style={{
          padding: '10px 16px',
          background: entityHsl('event', 0.10),
          borderBottom: `1px solid ${entityHsl('event', 0.30)}`,
          display: 'flex', alignItems: 'flex-start', gap: 10,
        }}>
          <span style={{ fontSize: 18, lineHeight: '20px', flexShrink: 0 }}>⚠️</span>
          <div style={{ minWidth: 0 }}>
            <div style={{
              fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 13,
              color: entityHsl('event'), marginBottom: 2,
            }}>Ultima sessione {daysAgo} giorni fa</div>
            <div style={{ fontSize: 12, color: 'var(--text-sec)', lineHeight: 1.45 }}>
              Potrebbe volerci qualche minuto per ricordare la trama.
              Nessuna fretta — il glossario e il party sono ancora qui.
            </div>
          </div>
        </div>
      )}

      <div style={{ padding: '18px 18px 14px' }}>
        {/* Top row: game title + session pulse */}
        <div style={{
          display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between',
          gap: 12, marginBottom: 6,
        }}>
          <div style={{ minWidth: 0, flex: 1 }}>
            <div style={{
              fontFamily: 'var(--f-mono)', fontSize: 10,
              color: entityHsl(accent),
              textTransform: 'uppercase', letterSpacing: '0.08em',
              fontWeight: 700, marginBottom: 4,
            }}>{campaign.gameTitle}</div>
            <h2 style={{
              fontFamily: 'var(--f-display)', fontWeight: 700,
              fontSize: 22, lineHeight: 1.18, margin: 0,
              color: 'var(--text)', letterSpacing: '-0.01em',
              wordBreak: 'break-word',
            }}>{campaign.title}</h2>
          </div>
          {!isStale && <SessionPulse active />}
        </div>

        {/* Last paragraph big number */}
        <div style={{
          display: 'flex', alignItems: 'baseline', gap: 8,
          marginTop: 16, marginBottom: 10,
        }}>
          <span style={{
            fontFamily: 'var(--f-mono)', fontSize: 11,
            color: 'var(--text-muted)', textTransform: 'uppercase',
            letterSpacing: '0.06em', fontWeight: 700,
          }}>Ultimo paragrafo</span>
        </div>
        <div style={{
          display: 'flex', alignItems: 'baseline', gap: 12, marginBottom: 16,
        }}>
          <span style={{
            fontFamily: 'var(--f-display)', fontWeight: 800,
            fontSize: 44, lineHeight: 1, color: entityHsl(accent),
            letterSpacing: '-0.02em', fontVariantNumeric: 'tabular-nums',
          }}>§{campaign.lastParagraph}</span>
          <span style={{
            fontFamily: 'var(--f-mono)', fontSize: 11,
            color: 'var(--text-muted)',
            fontVariantNumeric: 'tabular-nums',
          }}>letto {daysAgo === 1 ? 'ieri' : `${daysAgo} giorni fa`}</span>
        </div>

        {/* Stats triplet */}
        <div style={{
          display: 'grid', gridTemplateColumns: '1fr 1fr 1fr',
          gap: 8,
          padding: '12px 0',
          borderTop: '1px solid var(--border-light)',
          borderBottom: '1px solid var(--border-light)',
          marginBottom: 14,
        }}>
          {[
            { v: campaign.glossaryCount, l: 'termini', e: 'kb' },
            { v: campaign.partyCount,    l: 'party',   e: 'player' },
            { v: campaign.sessionsCount, l: 'sessioni',e: 'session' },
          ].map((s, i) => (
            <div key={i} style={{ textAlign: 'center' }}>
              <div style={{
                fontFamily: 'var(--f-display)', fontWeight: 800,
                fontSize: 22, color: entityHsl(s.e),
                fontVariantNumeric: 'tabular-nums', lineHeight: 1,
              }}>{s.v}</div>
              <div style={{
                fontFamily: 'var(--f-mono)', fontSize: 10,
                color: 'var(--text-muted)',
                textTransform: 'uppercase', letterSpacing: '0.05em',
                marginTop: 4, fontWeight: 700,
              }}>{s.l}</div>
            </div>
          ))}
        </div>

        {/* Party pips strip */}
        <div style={{ marginBottom: 14 }}>
          <div style={{
            fontFamily: 'var(--f-mono)', fontSize: 10,
            color: 'var(--text-muted)',
            textTransform: 'uppercase', letterSpacing: '0.06em',
            fontWeight: 700, marginBottom: 8,
          }}>Party al tavolo</div>
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
            {campaign.party.map(m => <PartyPip key={m.id} member={m} />)}
          </div>
        </div>

        {/* Glossary preview cluster top-5 */}
        <div style={{ marginBottom: 16 }}>
          <div style={{
            display: 'flex', alignItems: 'center', justifyContent: 'space-between',
            gap: 8, marginBottom: 8,
          }}>
            <span style={{
              fontFamily: 'var(--f-mono)', fontSize: 10,
              color: 'var(--text-muted)',
              textTransform: 'uppercase', letterSpacing: '0.06em', fontWeight: 700,
            }}>Glossario · top 5 di {campaign.glossaryCount}</span>
            <button style={{
              background: 'transparent', border: 'none',
              color: entityHsl('kb'),
              fontFamily: 'var(--f-display)', fontWeight: 600, fontSize: 11,
              cursor: 'pointer', padding: 0,
            }}>Vedi tutti →</button>
          </div>
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
            {campaign.glossary.map(g => (
              <GlossaryPill key={g.en} en={g.en} it={g.it} edited={g.edited} dense />
            ))}
          </div>
        </div>

        {/* CTA stack — primary "Riprendi" + secondary "Nuova" */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
          {isStale ? (
            <>
              <PrimaryBtn accent="game" size="lg" full>
                Riprendi
                <span style={{
                  fontFamily: 'var(--f-mono)', fontWeight: 600, fontSize: 11,
                  opacity: 0.85, marginLeft: 4,
                }}>(potrebbe essere disorientante)</span>
              </PrimaryBtn>
              <GhostBtn full danger>Archivia e ricomincia</GhostBtn>
              <p style={{
                margin: '4px 0 0', fontSize: 11, color: 'var(--text-muted)',
                textAlign: 'center', lineHeight: 1.45,
              }}>
                Archiviare non cancella — la campagna resta consultabile in cronologia.
              </p>
            </>
          ) : (
            <>
              <PrimaryBtn accent="game" size="lg" full>
                Riprendi → §{campaign.lastParagraph + 1}
              </PrimaryBtn>
              <GhostBtn full>+ Nuova campagna</GhostBtn>
            </>
          )}
        </div>
      </div>
    </div>
  );
}

const NANOLITH_CAMPAIGN_FRESH = {
  id: 'cs-nano-1',
  gameTitle: 'Nanolith · Awaken Realms',
  title: 'Veglia di Brace',
  lastParagraph: 289,
  partyCount: 4,
  glossaryCount: 12,
  sessionsCount: 7,
  party: NANOLITH_PARTY,
  glossary: NANOLITH_GLOSSARY_TOP5,
};

function State02Mobile({ sepia }) {
  return (
    <PhoneFrame sepia={sepia}>
      <AppTopBar gameTitle="Nanolith" subtitle="EN · in corso · sessione 7" avatarSuper={true} />
      <div style={{ padding: '18px 14px 24px' }}>
        <div style={{
          fontFamily: 'var(--f-mono)', fontSize: 10,
          color: 'var(--text-muted)',
          textTransform: 'uppercase', letterSpacing: '0.07em',
          fontWeight: 700, marginBottom: 10, padding: '0 4px',
        }}>Bentornato, Aaron</div>
        <ResumeHero
          campaign={NANOLITH_CAMPAIGN_FRESH}
          accent="game"
          daysAgo={7}
        />
      </div>
      <CostFooter amount="€1,42" sessionCount={6} />
    </PhoneFrame>
  );
}

/* =====================================================================
   STATE 03 — multi-campaign (2 campagne parallele, accent diversi)
   anchor: state-03-multi-campaign — coverage @N4.4 edge
   ===================================================================== */

function CampaignCard({ campaign, accent, daysAgo, lastNote }) {
  return (
    <div style={{
      borderRadius: 'var(--r-xl)',
      background: 'var(--bg-card)',
      border: `1.5px solid ${entityHsl(accent, 0.32)}`,
      boxShadow: `0 8px 22px ${entityHsl(accent, 0.10)}`,
      overflow: 'hidden',
    }}>
      {/* Accent strip */}
      <div style={{ height: 3, background: entityHsl(accent) }} />

      <div style={{ padding: '14px 16px' }}>
        <div style={{
          display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between',
          gap: 10, marginBottom: 8,
        }}>
          <div style={{ minWidth: 0, flex: 1 }}>
            <div style={{
              fontFamily: 'var(--f-mono)', fontSize: 9.5,
              color: entityHsl(accent),
              textTransform: 'uppercase', letterSpacing: '0.08em',
              fontWeight: 700, marginBottom: 3,
            }}>{campaign.gameTitle}</div>
            <div style={{
              fontFamily: 'var(--f-display)', fontWeight: 700,
              fontSize: 17, lineHeight: 1.2, color: 'var(--text)',
              letterSpacing: '-0.005em',
            }}>{campaign.title}</div>
          </div>
          <div style={{
            padding: '3px 8px', borderRadius: 'var(--r-pill)',
            background: entityHsl(accent, 0.12),
            color: entityHsl(accent),
            fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: 700,
            textTransform: 'uppercase', letterSpacing: '0.05em',
            whiteSpace: 'nowrap', flexShrink: 0,
          }}>§{campaign.lastParagraph}</div>
        </div>

        {/* Last note (lastNote slot) */}
        <div style={{
          fontSize: 12, color: 'var(--text-muted)',
          fontFamily: 'var(--f-mono)', marginBottom: 10,
          fontVariantNumeric: 'tabular-nums',
        }}>
          {daysAgo === 1 ? 'ieri' : `${daysAgo}gg fa`} · {campaign.glossaryCount} termini · {campaign.partyCount} personaggi
        </div>

        {/* Mini party row (avatars only) */}
        <div style={{
          display: 'flex', gap: -6, marginBottom: 12,
          alignItems: 'center',
        }}>
          {campaign.party.slice(0, 4).map((m, i) => (
            <span key={m.id} title={`${m.name} · ${m.class}`} style={{
              width: 28, height: 28, borderRadius: '50%',
              background: entityHsl(m.color, 0.18),
              color: entityHsl(m.color),
              display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
              fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 11,
              border: '2px solid var(--bg-card)',
              marginLeft: i === 0 ? 0 : -6,
              position: 'relative', zIndex: 4 - i,
            }}>{m.name.slice(0, 1)}</span>
          ))}
          {campaign.partyCount > 4 && (
            <span style={{
              marginLeft: -6,
              padding: '0 8px', height: 28,
              borderRadius: 'var(--r-pill)',
              background: 'var(--bg-muted)',
              color: 'var(--text-sec)',
              fontFamily: 'var(--f-mono)', fontSize: 11, fontWeight: 700,
              display: 'inline-flex', alignItems: 'center',
              border: '2px solid var(--bg-card)',
            }}>+{campaign.partyCount - 4}</span>
          )}
        </div>

        {/* Mini glossary row top-3 */}
        <div style={{ display: 'flex', flexWrap: 'wrap', gap: 5, marginBottom: 14 }}>
          {campaign.glossary.slice(0, 3).map(g => (
            <GlossaryPill key={g.en} en={g.en} it={g.it} edited={g.edited} dense />
          ))}
          {campaign.glossaryCount > 3 && (
            <span style={{
              padding: '3px 8px', borderRadius: 'var(--r-pill)',
              background: 'var(--bg-muted)', color: 'var(--text-muted)',
              fontFamily: 'var(--f-mono)', fontSize: 11, fontWeight: 700,
              border: '1px solid var(--border-light)',
            }}>+{campaign.glossaryCount - 3}</span>
          )}
        </div>

        <PrimaryBtn accent={accent} size="md" full>
          Riprendi → §{campaign.lastParagraph + 1}
        </PrimaryBtn>
      </div>
    </div>
  );
}

const TG_CAMPAIGN = {
  id: 'cs-tg-1',
  gameTitle: 'Tainted Grail · Awaken Realms',
  title: 'La Caduta di Avalon',
  lastParagraph: 412,
  partyCount: 3,
  glossaryCount: 18,
  sessionsCount: 4,
  party: TG_PARTY,
  glossary: [
    { en: 'Wyrdness',     it: 'Stranezza',         edited: false },
    { en: 'Wraithstone',  it: 'Pietra Spettrale',  edited: false },
    { en: 'Sword of Avalon', it: 'Spada di Avalon',edited: true  },
  ],
};

function State03Mobile() {
  return (
    <PhoneFrame>
      <AppTopBar gameTitle="Le tue campagne" subtitle="2 attive" avatarSuper={true} />
      <div style={{ padding: '18px 14px 24px', display: 'flex', flexDirection: 'column', gap: 14 }}>
        <div>
          <div style={{
            fontFamily: 'var(--f-mono)', fontSize: 10,
            color: 'var(--text-muted)',
            textTransform: 'uppercase', letterSpacing: '0.07em',
            fontWeight: 700, marginBottom: 6, padding: '0 4px',
          }}>Bentornato, Aaron</div>
          <h2 style={{
            fontFamily: 'var(--f-display)', fontWeight: 700,
            fontSize: 22, color: 'var(--text)', margin: '0 0 4px',
            padding: '0 4px',
            letterSpacing: '-0.01em',
          }}>Le tue campagne attive (2)</h2>
          <p style={{
            margin: '0 0 6px', padding: '0 4px',
            fontSize: 12.5, color: 'var(--text-sec)', lineHeight: 1.45,
          }}>
            Ogni campagna mantiene il suo glossario e il suo party. Niente si mescola.
          </p>
        </div>

        <CampaignCard campaign={NANOLITH_CAMPAIGN_FRESH} accent="game" daysAgo={7} />
        <CampaignCard campaign={TG_CAMPAIGN} accent="agent" daysAgo={2} />

        <button style={{
          marginTop: 4,
          padding: '12px 14px',
          borderRadius: 'var(--r-md)',
          border: '1.5px dashed var(--border-strong)',
          background: 'transparent',
          color: 'var(--text-sec)',
          fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 13,
          cursor: 'pointer',
          display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 8,
        }}>
          <span style={{ fontSize: 16 }}>+</span> Nuova campagna
        </button>
      </div>
      <CostFooter amount="€2,18" sessionCount={11} />
    </PhoneFrame>
  );
}

/* =====================================================================
   STATE 04 — stale > 90 giorni (1 campagna 100gg fa)
   anchor: state-04-stale-warning — coverage @N4.5 edge
   ===================================================================== */

const NANOLITH_CAMPAIGN_STALE = {
  ...NANOLITH_CAMPAIGN_FRESH,
  title: 'Veglia di Brace',
  // Campagna lasciata indietro — context-recovery mood
};

function State04Mobile() {
  return (
    <PhoneFrame>
      <AppTopBar gameTitle="Nanolith" subtitle="EN · in pausa · sessione 7" avatarSuper={true} />
      <div style={{ padding: '18px 14px 24px' }}>
        <div style={{
          fontFamily: 'var(--f-mono)', fontSize: 10,
          color: 'var(--text-muted)',
          textTransform: 'uppercase', letterSpacing: '0.07em',
          fontWeight: 700, marginBottom: 10, padding: '0 4px',
        }}>Bentornato, Aaron</div>
        <ResumeHero
          campaign={NANOLITH_CAMPAIGN_STALE}
          accent="game"
          daysAgo={100}
          stale={true}
        />
      </div>
      <CostFooter amount="€1,42" sessionCount={6} />
    </PhoneFrame>
  );
}

/* =====================================================================
   DESKTOP variant — state-02 split-view (380px lista + dettaglio dx)
   ===================================================================== */

function State02Desktop() {
  const [selected] = useState(NANOLITH_CAMPAIGN_FRESH);

  return (
    <DesktopFrame url="/library/games/nanolith/play">
      <div style={{ display: 'flex', height: '100%', minHeight: 0 }}>
        {/* Sidebar 380px — campaigns list */}
        <aside style={{
          width: 380, flexShrink: 0,
          borderRight: '1px solid var(--border)',
          background: 'var(--bg-muted)',
          overflowY: 'auto',
        }}>
          <div style={{ padding: '20px 20px 12px' }}>
            <div style={{
              fontFamily: 'var(--f-mono)', fontSize: 10,
              color: 'var(--text-muted)',
              textTransform: 'uppercase', letterSpacing: '0.08em',
              fontWeight: 700, marginBottom: 6,
            }}>Bentornato, Aaron</div>
            <h2 style={{
              fontFamily: 'var(--f-display)', fontWeight: 700,
              fontSize: 20, color: 'var(--text)', margin: '0 0 4px',
            }}>Campagne attive</h2>
          </div>

          <div style={{ padding: '0 12px 20px', display: 'flex', flexDirection: 'column', gap: 8 }}>
            {/* Selected card */}
            <div style={{
              padding: '14px 14px',
              borderRadius: 'var(--r-lg)',
              background: 'var(--bg-card)',
              border: `2px solid ${entityHsl('game', 0.55)}`,
              boxShadow: `0 4px 14px ${entityHsl('game', 0.15)}`,
              cursor: 'pointer',
            }}>
              <div style={{
                display: 'flex', alignItems: 'center', justifyContent: 'space-between',
                gap: 10, marginBottom: 8,
              }}>
                <div style={{
                  fontFamily: 'var(--f-mono)', fontSize: 10,
                  color: entityHsl('game'),
                  textTransform: 'uppercase', letterSpacing: '0.08em', fontWeight: 700,
                }}>Nanolith</div>
                <SessionPulse active />
              </div>
              <div style={{
                fontFamily: 'var(--f-display)', fontWeight: 700,
                fontSize: 17, color: 'var(--text)', marginBottom: 6,
              }}>Veglia di Brace</div>
              <div style={{
                fontFamily: 'var(--f-mono)', fontSize: 11,
                color: 'var(--text-muted)', fontVariantNumeric: 'tabular-nums',
              }}>§289 · 7gg fa · 12 termini · 4 personaggi</div>
            </div>

            {/* Other campaign placeholder (state-03 preview) */}
            <div style={{
              padding: '12px 14px',
              borderRadius: 'var(--r-lg)',
              background: 'var(--bg-card)',
              border: '1px solid var(--border)',
              cursor: 'pointer', opacity: 0.85,
            }}>
              <div style={{
                fontFamily: 'var(--f-mono)', fontSize: 10,
                color: entityHsl('agent'),
                textTransform: 'uppercase', letterSpacing: '0.08em', fontWeight: 700,
                marginBottom: 6,
              }}>Tainted Grail</div>
              <div style={{
                fontFamily: 'var(--f-display)', fontWeight: 700,
                fontSize: 15, color: 'var(--text)', marginBottom: 4,
              }}>La Caduta di Avalon</div>
              <div style={{
                fontFamily: 'var(--f-mono)', fontSize: 11,
                color: 'var(--text-muted)', fontVariantNumeric: 'tabular-nums',
              }}>§412 · 2gg fa · 18 termini</div>
            </div>

            {/* New campaign CTA */}
            <button style={{
              marginTop: 4,
              padding: '14px',
              borderRadius: 'var(--r-md)',
              border: '1.5px dashed var(--border-strong)',
              background: 'transparent',
              color: 'var(--text-sec)',
              fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 13,
              cursor: 'pointer',
            }}>+ Nuova campagna</button>
          </div>

          <div style={{
            padding: '12px 20px', borderTop: '1px solid var(--border-light)',
            fontFamily: 'var(--f-mono)', fontSize: 10,
            color: 'var(--text-muted)',
            textTransform: 'uppercase', letterSpacing: '0.06em',
          }}>● dogfood · superadmin · €2,18 totali</div>
        </aside>

        {/* Detail pane */}
        <main style={{ flex: 1, overflowY: 'auto', minWidth: 0, padding: '32px 36px 48px' }}>
          <div style={{ maxWidth: 720 }}>
            {/* Breadcrumb */}
            <div style={{
              fontFamily: 'var(--f-mono)', fontSize: 11,
              color: 'var(--text-muted)', marginBottom: 14,
              textTransform: 'uppercase', letterSpacing: '0.06em',
            }}>Library / Games / Nanolith / Play</div>

            {/* Hero header */}
            <div style={{
              display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between',
              gap: 18, marginBottom: 24, flexWrap: 'wrap',
            }}>
              <div>
                <div style={{
                  fontFamily: 'var(--f-mono)', fontSize: 11,
                  color: entityHsl('game'),
                  textTransform: 'uppercase', letterSpacing: '0.08em', fontWeight: 700,
                  marginBottom: 6,
                }}>Nanolith · Awaken Realms · EN</div>
                <h1 style={{
                  fontFamily: 'var(--f-display)', fontWeight: 700,
                  fontSize: 36, color: 'var(--text)', margin: '0 0 6px',
                  letterSpacing: '-0.015em',
                }}>Veglia di Brace</h1>
                <div style={{
                  fontSize: 14, color: 'var(--text-sec)',
                  fontVariantNumeric: 'tabular-nums',
                }}>
                  Sessione 7 · ultima lettura mercoledì 30 aprile · 7 giorni fa
                </div>
              </div>
              <div style={{ display: 'flex', gap: 10 }}>
                <GhostBtn>+ Nuova campagna</GhostBtn>
                <PrimaryBtn accent="game" size="lg">Riprendi → §290</PrimaryBtn>
              </div>
            </div>

            {/* Big paragraph anchor */}
            <div style={{
              padding: '24px 28px',
              borderRadius: 'var(--r-2xl)',
              background: `linear-gradient(135deg, ${entityHsl('game', 0.10)}, ${entityHsl('game', 0.04)})`,
              border: `1.5px solid ${entityHsl('game', 0.32)}`,
              marginBottom: 28,
              display: 'flex', alignItems: 'center', justifyContent: 'space-between',
              gap: 24, flexWrap: 'wrap',
            }}>
              <div>
                <div style={{
                  fontFamily: 'var(--f-mono)', fontSize: 11,
                  color: 'var(--text-muted)', textTransform: 'uppercase',
                  letterSpacing: '0.07em', fontWeight: 700, marginBottom: 6,
                }}>Eri qui</div>
                <div style={{
                  fontFamily: 'var(--f-display)', fontWeight: 800,
                  fontSize: 64, lineHeight: 1, color: entityHsl('game'),
                  letterSpacing: '-0.02em', fontVariantNumeric: 'tabular-nums',
                }}>§289</div>
              </div>
              <div style={{
                fontSize: 14, color: 'var(--text-sec)',
                lineHeight: 1.55, maxWidth: 380, fontStyle: 'italic',
              }}>
                "Le luci della Veglia Cava si spegnevano una a una mentre Marco
                accendeva la Pietra del Vuoto…"
                <div style={{
                  fontFamily: 'var(--f-mono)', fontSize: 11, fontStyle: 'normal',
                  color: 'var(--text-muted)', marginTop: 8,
                  textTransform: 'uppercase', letterSpacing: '0.06em',
                }}>ultime righe lette</div>
              </div>
            </div>

            {/* Two-column: party + glossary */}
            <div style={{
              display: 'grid', gridTemplateColumns: '1fr 1fr',
              gap: 20, marginBottom: 24,
            }}>
              <div style={{
                padding: '18px 20px',
                borderRadius: 'var(--r-xl)',
                background: 'var(--bg-card)',
                border: '1px solid var(--border)',
              }}>
                <div style={{
                  display: 'flex', alignItems: 'center', justifyContent: 'space-between',
                  marginBottom: 14,
                }}>
                  <h3 style={{
                    fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 14,
                    color: 'var(--text-sec)', margin: 0,
                    textTransform: 'uppercase', letterSpacing: '0.06em',
                  }}>Party (4)</h3>
                  <button style={{
                    background: 'transparent', border: 'none',
                    color: entityHsl('player'),
                    fontFamily: 'var(--f-display)', fontWeight: 600, fontSize: 12,
                    cursor: 'pointer',
                  }}>Modifica →</button>
                </div>
                <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                  {NANOLITH_PARTY.map(m => (
                    <div key={m.id} style={{
                      display: 'flex', alignItems: 'center', gap: 12,
                      padding: '8px 10px',
                      borderRadius: 'var(--r-md)',
                      background: 'var(--bg-muted)',
                    }}>
                      <span style={{
                        width: 30, height: 30, borderRadius: '50%',
                        background: entityHsl(m.color, 0.18),
                        color: entityHsl(m.color),
                        display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
                        fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 12,
                      }}>{m.name.slice(0, 1)}</span>
                      <div style={{ flex: 1, minWidth: 0 }}>
                        <div style={{
                          fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 13,
                          color: 'var(--text)',
                        }}>{m.name}</div>
                        <div style={{ fontSize: 11, color: 'var(--text-muted)' }}>{m.class}</div>
                      </div>
                      <div style={{
                        fontFamily: 'var(--f-mono)', fontSize: 11,
                        color: 'var(--text-sec)', fontVariantNumeric: 'tabular-nums',
                      }}>HP {m.hp}</div>
                    </div>
                  ))}
                </div>
              </div>

              <div style={{
                padding: '18px 20px',
                borderRadius: 'var(--r-xl)',
                background: 'var(--bg-card)',
                border: '1px solid var(--border)',
              }}>
                <div style={{
                  display: 'flex', alignItems: 'center', justifyContent: 'space-between',
                  marginBottom: 14,
                }}>
                  <h3 style={{
                    fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 14,
                    color: 'var(--text-sec)', margin: 0,
                    textTransform: 'uppercase', letterSpacing: '0.06em',
                  }}>Glossario (12)</h3>
                  <button style={{
                    background: 'transparent', border: 'none',
                    color: entityHsl('kb'),
                    fontFamily: 'var(--f-display)', fontWeight: 600, fontSize: 12,
                    cursor: 'pointer',
                  }}>Vedi tutti →</button>
                </div>
                <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
                  {NANOLITH_GLOSSARY_TOP5.map(g => (
                    <GlossaryPill key={g.en} en={g.en} it={g.it} edited={g.edited} dense />
                  ))}
                  <span style={{
                    padding: '3px 8px', borderRadius: 'var(--r-pill)',
                    background: 'var(--bg-muted)', color: 'var(--text-muted)',
                    fontFamily: 'var(--f-mono)', fontSize: 11, fontWeight: 700,
                    border: '1px solid var(--border-light)',
                  }}>+7</span>
                </div>
                <p style={{
                  margin: '12px 0 0', fontSize: 11.5,
                  color: 'var(--text-muted)', lineHeight: 1.5,
                }}>
                  Ogni nuova traduzione di §290 userà questi termini per coerenza.
                  Tap per modificare una traduzione che non ti convince.
                </p>
              </div>
            </div>
          </div>
        </main>
      </div>
    </DesktopFrame>
  );
}

/* =====================================================================
   DESKTOP variant — state-03 grid (2-col)
   ===================================================================== */

function State03Desktop() {
  return (
    <DesktopFrame url="/library/games/nanolith/play">
      <div style={{ padding: '32px 40px 48px', minHeight: '100%' }}>
        <div style={{ maxWidth: 1100, margin: '0 auto' }}>
          {/* Header */}
          <div style={{ marginBottom: 28 }}>
            <div style={{
              fontFamily: 'var(--f-mono)', fontSize: 11,
              color: 'var(--text-muted)',
              textTransform: 'uppercase', letterSpacing: '0.08em', fontWeight: 700,
              marginBottom: 8,
            }}>Bentornato, Aaron</div>
            <div style={{
              display: 'flex', alignItems: 'flex-end', justifyContent: 'space-between',
              gap: 18, flexWrap: 'wrap',
            }}>
              <div>
                <h1 style={{
                  fontFamily: 'var(--f-display)', fontWeight: 700,
                  fontSize: 36, color: 'var(--text)', margin: '0 0 6px',
                  letterSpacing: '-0.015em',
                }}>Le tue campagne attive (2)</h1>
                <p style={{
                  margin: 0, color: 'var(--text-sec)', fontSize: 14, maxWidth: 580,
                }}>
                  Ogni campagna mantiene il suo glossario e il suo party.
                  Riprendi quella che vuoi — niente si mescola.
                </p>
              </div>
              <PrimaryBtn accent="game">+ Nuova campagna</PrimaryBtn>
            </div>
          </div>

          {/* 2-col grid */}
          <div style={{
            display: 'grid', gridTemplateColumns: '1fr 1fr',
            gap: 20,
          }}>
            <DesktopCampaignCard campaign={NANOLITH_CAMPAIGN_FRESH} accent="game" daysAgo={7} active />
            <DesktopCampaignCard campaign={TG_CAMPAIGN} accent="agent" daysAgo={2} />
          </div>

          {/* Footer cost */}
          <div style={{
            marginTop: 32,
            padding: '14px 18px',
            borderRadius: 'var(--r-md)',
            background: 'var(--bg-muted)',
            border: '1px solid var(--border-light)',
            display: 'flex', alignItems: 'center', justifyContent: 'space-between',
            gap: 12, flexWrap: 'wrap',
            fontFamily: 'var(--f-mono)', fontSize: 11,
            color: 'var(--text-muted)',
          }}>
            <span style={{ textTransform: 'uppercase', letterSpacing: '0.06em' }}>
              ● dogfood · superadmin · bypass quota
            </span>
            <span>
              <strong style={{ color: 'var(--text-sec)' }}>€2,18</strong> totali · 11 sessioni · 0,3% del tuo budget mensile
            </span>
          </div>
        </div>
      </div>
    </DesktopFrame>
  );
}

function DesktopCampaignCard({ campaign, accent, daysAgo, active }) {
  return (
    <div style={{
      borderRadius: 'var(--r-2xl)',
      background: 'var(--bg-card)',
      border: `1.5px solid ${entityHsl(accent, 0.36)}`,
      boxShadow: `0 12px 28px ${entityHsl(accent, 0.10)}`,
      overflow: 'hidden',
    }}>
      <div style={{ height: 5, background: entityHsl(accent) }} />
      <div style={{ padding: '24px 26px' }}>
        <div style={{
          display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between',
          gap: 14, marginBottom: 16,
        }}>
          <div style={{ minWidth: 0, flex: 1 }}>
            <div style={{
              fontFamily: 'var(--f-mono)', fontSize: 11,
              color: entityHsl(accent),
              textTransform: 'uppercase', letterSpacing: '0.08em', fontWeight: 700,
              marginBottom: 6,
            }}>{campaign.gameTitle}</div>
            <h2 style={{
              fontFamily: 'var(--f-display)', fontWeight: 700,
              fontSize: 24, color: 'var(--text)', margin: '0 0 6px',
              letterSpacing: '-0.01em',
            }}>{campaign.title}</h2>
            <div style={{
              fontFamily: 'var(--f-mono)', fontSize: 12,
              color: 'var(--text-muted)', fontVariantNumeric: 'tabular-nums',
            }}>
              ultima lettura {daysAgo === 1 ? 'ieri' : `${daysAgo} giorni fa`} · {campaign.sessionsCount} sessioni
            </div>
          </div>
          {active && <SessionPulse active />}
        </div>

        {/* Last paragraph + stats */}
        <div style={{
          display: 'grid', gridTemplateColumns: 'auto 1fr 1fr 1fr',
          gap: 14, alignItems: 'center',
          padding: '14px 0',
          borderTop: '1px solid var(--border-light)',
          borderBottom: '1px solid var(--border-light)',
          marginBottom: 18,
        }}>
          <div>
            <div style={{
              fontFamily: 'var(--f-mono)', fontSize: 10,
              color: 'var(--text-muted)',
              textTransform: 'uppercase', letterSpacing: '0.06em',
              fontWeight: 700, marginBottom: 2,
            }}>Ultimo</div>
            <div style={{
              fontFamily: 'var(--f-display)', fontWeight: 800,
              fontSize: 32, lineHeight: 1, color: entityHsl(accent),
              letterSpacing: '-0.02em', fontVariantNumeric: 'tabular-nums',
            }}>§{campaign.lastParagraph}</div>
          </div>
          {[
            { v: campaign.glossaryCount, l: 'termini', e: 'kb' },
            { v: campaign.partyCount,    l: 'party',   e: 'player' },
            { v: campaign.sessionsCount, l: 'sessioni',e: 'session' },
          ].map((s, i) => (
            <div key={i} style={{ textAlign: 'center' }}>
              <div style={{
                fontFamily: 'var(--f-display)', fontWeight: 700,
                fontSize: 18, color: entityHsl(s.e),
                fontVariantNumeric: 'tabular-nums', lineHeight: 1,
              }}>{s.v}</div>
              <div style={{
                fontFamily: 'var(--f-mono)', fontSize: 10,
                color: 'var(--text-muted)',
                textTransform: 'uppercase', letterSpacing: '0.05em',
                marginTop: 4, fontWeight: 700,
              }}>{s.l}</div>
            </div>
          ))}
        </div>

        {/* Party row */}
        <div style={{ marginBottom: 14 }}>
          <div style={{
            fontFamily: 'var(--f-mono)', fontSize: 10,
            color: 'var(--text-muted)',
            textTransform: 'uppercase', letterSpacing: '0.06em',
            fontWeight: 700, marginBottom: 8,
          }}>Party</div>
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
            {campaign.party.map(m => <PartyPip key={m.id} member={m} />)}
          </div>
        </div>

        {/* Glossary cluster */}
        <div style={{ marginBottom: 18 }}>
          <div style={{
            fontFamily: 'var(--f-mono)', fontSize: 10,
            color: 'var(--text-muted)',
            textTransform: 'uppercase', letterSpacing: '0.06em',
            fontWeight: 700, marginBottom: 8,
          }}>Glossario · top {campaign.glossary.length} di {campaign.glossaryCount}</div>
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
            {campaign.glossary.map(g => (
              <GlossaryPill key={g.en} en={g.en} it={g.it} edited={g.edited} dense />
            ))}
            {campaign.glossaryCount > campaign.glossary.length && (
              <span style={{
                padding: '3px 8px', borderRadius: 'var(--r-pill)',
                background: 'var(--bg-muted)', color: 'var(--text-muted)',
                fontFamily: 'var(--f-mono)', fontSize: 11, fontWeight: 700,
                border: '1px solid var(--border-light)',
              }}>+{campaign.glossaryCount - campaign.glossary.length}</span>
            )}
          </div>
        </div>

        <div style={{ display: 'flex', gap: 10 }}>
          <PrimaryBtn accent={accent}>Riprendi → §{campaign.lastParagraph + 1}</PrimaryBtn>
          <GhostBtn>Dettagli</GhostBtn>
        </div>
      </div>
    </div>
  );
}

/* =====================================================================
   App shell — state grid + filtering
   ===================================================================== */

function StateBlock({ id, title, gherkin, frame, children }) {
  return (
    <section id={id} data-state={id} data-screen-label={`G ${id}`}>
      <div className="lg-section-label">
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
  const [sepia, setSepia] = useState('off');

  useEffect(() => {
    const handlers = [
      ['lg-tweak-state', e => setState(e.detail)],
      ['lg-tweak-frame', e => setFrame(e.detail)],
      ['lg-tweak-sepia', e => setSepia(e.detail)],
    ];
    handlers.forEach(([n, h]) => document.addEventListener(n, h));
    return () => handlers.forEach(([n, h]) => document.removeEventListener(n, h));
  }, []);

  const showState = key => state === 'all' || state === key;
  const showMobile = frame !== 'desktop';
  const showDesktop = frame !== 'mobile';
  const sepiaOn = sepia === 'on';

  return (
    <div>
      {showState('state-01') && showMobile && (
        <StateBlock
          id="state-01-first-time"
          title="01 · First time — Aaron mai giocato Nanolith"
          gherkin="empty / N4-bootstrap"
          frame="mobile · 375 × 780"
        >
          <div className="lg-frame-caption">
            Hero "Inizia la tua prima campagna" · 3 reassurance row · CTA primary game · cost €0,00
            <span className="theme-tag">light</span>
          </div>
          <div className="lg-state-grid"><State01Mobile /></div>
        </StateBlock>
      )}

      {showState('state-02') && showMobile && (
        <StateBlock
          id="state-02-single-resume"
          title="02 · Single resume — 1 campagna · ultima 7gg fa"
          gherkin="@N4.1 happy + dogfood"
          frame="mobile · 375 × 780"
        >
          <div className="lg-frame-caption">
            Hero card "Riprendi: §289 · 12 termini · 4 personaggi · 7gg fa" · glossary preview top-5 · CTA primary "Riprendi" + secondary "Nuova"
            {sepiaOn && <span className="theme-tag">sepia preview</span>}
            {!sepiaOn && <span className="theme-tag">light</span>}
          </div>
          <div className="lg-state-grid">
            <State02Mobile sepia={sepiaOn} />
          </div>
        </StateBlock>
      )}

      {showState('state-03') && showMobile && (
        <StateBlock
          id="state-03-multi-campaign"
          title="03 · Multi-campagna — 2 campagne parallele"
          gherkin="@N4.4 edge"
          frame="mobile · 375 × 780"
        >
          <div className="lg-frame-caption">
            Header "Le tue campagne attive (2)" · Nanolith (entity=game) + Tainted Grail (entity=agent) accent diversi
            <span className="theme-tag">light</span>
          </div>
          <div className="lg-state-grid"><State03Mobile /></div>
        </StateBlock>
      )}

      {showState('state-04') && showMobile && (
        <StateBlock
          id="state-04-stale-warning"
          title="04 · Stale warning — campagna 100gg fa"
          gherkin="@N4.5 edge — soft-delete archive"
          frame="mobile · 375 × 780"
        >
          <div className="lg-frame-caption">
            Warning amber rispettoso · CTA primary "Riprendi (potrebbe essere disorientante)" · CTA secondary "Archivia e ricomincia" (soft-delete)
            <span className="theme-tag">light</span>
          </div>
          <div className="lg-state-grid"><State04Mobile /></div>
        </StateBlock>
      )}

      {/* DESKTOP variants */}
      {showState('state-02') && showDesktop && (
        <StateBlock
          id="state-02-desktop-split-view"
          title="02 · Desktop split-view — lista 380px sx + dettaglio dx"
          gherkin="@N4.1 desktop adaptation"
          frame="desktop · 1280 × 820"
        >
          <div className="lg-frame-caption">
            Sidebar campaigns list · main pane: hero §289 + party 4 + glossary 12 due colonne
            <span className="theme-tag">light</span>
          </div>
          <State02Desktop />
        </StateBlock>
      )}

      {showState('state-03') && showDesktop && (
        <StateBlock
          id="state-03-desktop-grid"
          title="03 · Desktop grid — 2-col"
          gherkin="@N4.4 desktop adaptation"
          frame="desktop · 1280 × 820"
        >
          <div className="lg-frame-caption">
            2-col grid invece di stack · ogni card mostra party row + glossary cluster + CTA pair
            <span className="theme-tag">light</span>
          </div>
          <State03Desktop />
        </StateBlock>
      )}

      {/* keyframes inline */}
      <style>{`
        @keyframes sp6-pulse {
          0%   { box-shadow: 0 0 0 0 ${entityHsl('session', 0.55)}; }
          70%  { box-shadow: 0 0 0 10px ${entityHsl('session', 0)}; }
          100% { box-shadow: 0 0 0 0 ${entityHsl('session', 0)}; }
        }
        @media (prefers-reduced-motion: reduce) {
          [style*="sp6-pulse"] { animation: none !important; }
        }
      `}</style>
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(<App />);
