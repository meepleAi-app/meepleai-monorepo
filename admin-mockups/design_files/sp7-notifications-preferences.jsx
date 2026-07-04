// ── sp7-notifications-preferences.jsx — MeepleAI Design System v1
// Route: /notifications/preferences · Persona: Marco (one-time setup)
// Components: ToggleSwitch, ChannelToggleRow, FrequencyRadio, TimePicker,
//             QuietHoursCard, SeverityMatrix, DeviceList, EmptyDevice,
//             SaveToast, PrefSection, PhoneTopBar, PrefScreen
// Screens 1–5 exported to window for sp7-notifications-preferences.html
//
// FREEZE: every color goes through entityHsl(entity, alpha) or var(--token).
//         No raw numeric hsl() literals anywhere. No external catalog hosts.

const { useState, useCallback } = React;

// ── Helper: entity color (HSL triplet token + optional alpha) ────────
// entityHsl('event')        -> hsl(var(--c-event))
// entityHsl('event', 0.12)  -> hsl(var(--c-event) / 0.12)
const entityHsl = (type, alpha) =>
  alpha === undefined
    ? `hsl(var(--c-${type}))`
    : `hsl(var(--c-${type}) / ${alpha})`;

// ── Severity → entity mapping (task spec) ────────────────────────────
// Critica = event · Importante = agent · Info = toolkit
const SEVERITY_ENTITY = { critica: 'event', importante: 'agent', info: 'toolkit' };

// ══════════════════════════════════════════════════════════════════
// ── PRIMITIVES ────────────────────────────────────────────────────
// ══════════════════════════════════════════════════════════════════

// ── ToggleSwitch ──────────────────────────────────────────────────
function ToggleSwitch({ checked, onChange, entity = 'game', label, size = 'md' }) {
  const W = size === 'sm' ? 38 : 48;
  const H = size === 'sm' ? 22 : 28;
  const K = size === 'sm' ? 16 : 22;
  const pad = 2;
  return (
    <button
      role="switch" aria-checked={checked} aria-label={label || ''}
      onClick={() => onChange(!checked)}
      style={{
        width: W, height: H, borderRadius: H / 2, padding: 0, flexShrink: 0,
        border: checked
          ? `1px solid ${entityHsl(entity)}`
          : '1px solid var(--border)',
        background: checked ? entityHsl(entity) : 'var(--bg-muted)',
        position: 'relative', cursor: 'pointer',
        transition: 'background var(--dur-sm) var(--ease-out), border-color var(--dur-sm) var(--ease-out)',
        outline: 'none',
      }}
    >
      <div style={{
        position: 'absolute', top: pad, left: checked ? W - K - pad : pad,
        width: K, height: K, borderRadius: '50%', background: '#fff',
        boxShadow: '0 1px 4px rgba(0,0,0,.25)',
        transition: 'left var(--dur-sm) var(--ease-out)',
      }} />
    </button>
  );
}

// ── Lbl (uppercase micro-label) ──────────────────────────────────
function Lbl({ children }) {
  return (
    <div style={{
      fontSize: 11, fontWeight: 700, fontFamily: 'var(--f-display)',
      color: 'var(--text-sec)', textTransform: 'uppercase',
      letterSpacing: '.08em', marginBottom: 6,
    }}>{children}</div>
  );
}

// ── PrefSection (auth-card style grouping) ───────────────────────
function PrefSection({ icon, entity = 'game', title, subtitle, children, footer }) {
  return (
    <div style={{
      margin: '0 14px 14px',
      border: '1px solid var(--border)', borderRadius: 'var(--r-xl)',
      background: 'var(--bg-card)', overflow: 'hidden',
    }}>
      <div style={{
        display: 'flex', alignItems: 'center', gap: 11,
        padding: '13px 15px 12px',
        borderBottom: '1px solid var(--border-light)',
      }}>
        <div style={{
          width: 34, height: 34, borderRadius: 'var(--r-md)', flexShrink: 0,
          background: entityHsl(entity, 0.12), color: entityHsl(entity),
          display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 16,
        }}>{icon}</div>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{
            fontSize: 13.5, fontWeight: 700, fontFamily: 'var(--f-display)',
            color: 'var(--text)', letterSpacing: '-.01em',
          }}>{title}</div>
          {subtitle && (
            <div style={{ fontSize: 11, color: 'var(--text-muted)', marginTop: 1, lineHeight: 1.4 }}>
              {subtitle}
            </div>
          )}
        </div>
      </div>
      <div style={{ padding: '6px 15px 12px' }}>{children}</div>
      {footer}
    </div>
  );
}

// ── ChannelToggleRow (one channel: in-app / email / push) ────────
function ChannelToggleRow({ icon, label, hint, entity, checked, onChange, children }) {
  return (
    <div style={{ padding: '9px 0', borderTop: '1px solid var(--border-light)' }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 11 }}>
        <span style={{ fontSize: 16, width: 22, textAlign: 'center', flexShrink: 0 }}>{icon}</span>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{
            fontSize: 13, fontWeight: 700, fontFamily: 'var(--f-display)', color: 'var(--text)',
          }}>{label}</div>
          {hint && (
            <div style={{ fontSize: 10.5, color: 'var(--text-muted)', marginTop: 1, lineHeight: 1.4 }}>
              {hint}
            </div>
          )}
        </div>
        <ToggleSwitch checked={checked} onChange={onChange} entity={entity} label={label} />
      </div>
      {checked && children && <div style={{ paddingLeft: 33, marginTop: 8 }}>{children}</div>}
    </div>
  );
}

// ── FrequencyRadio (Real-time / Daily digest / Off) ──────────────
const FREQ_OPTIONS = [
  { value: 'realtime', label: 'Real-time', desc: 'Push immediato', icon: '⚡' },
  { value: 'daily',    label: 'Daily digest', desc: 'Una mail al mattino', icon: '📬' },
  { value: 'off',      label: 'Off', desc: 'Solo notifiche critiche', icon: '🔕' },
];

function FrequencyRadio({ value, onChange, entity = 'agent', name }) {
  return (
    <div role="radiogroup" aria-label="Frequenza notifiche"
      style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
      {FREQ_OPTIONS.map(opt => {
        const active = value === opt.value;
        return (
          <label key={opt.value} style={{
            display: 'flex', alignItems: 'center', gap: 10, cursor: 'pointer',
            padding: '8px 10px', borderRadius: 'var(--r-md)',
            border: active ? `1px solid ${entityHsl(entity, 0.5)}` : '1px solid var(--border)',
            background: active ? entityHsl(entity, 0.08) : 'transparent',
            transition: 'all var(--dur-sm) var(--ease-out)',
          }}>
            <input type="radio" name={name} value={opt.value} checked={active}
              onChange={() => onChange(opt.value)}
              style={{ accentColor: entityHsl(entity), width: 15, height: 15, flexShrink: 0 }}
            />
            <span style={{ fontSize: 14, width: 18, textAlign: 'center' }}>{opt.icon}</span>
            <div style={{ flex: 1 }}>
              <div style={{
                fontSize: 12.5, fontWeight: 700, fontFamily: 'var(--f-display)',
                color: active ? entityHsl(entity) : 'var(--text)',
              }}>{opt.label}</div>
              <div style={{ fontSize: 10.5, color: 'var(--text-muted)', marginTop: 1 }}>{opt.desc}</div>
            </div>
          </label>
        );
      })}
    </div>
  );
}

// ── TimePicker (HH:MM select pair) ───────────────────────────────
const HOURS = Array.from({ length: 24 }, (_, h) => `${String(h).padStart(2, '0')}:00`);

function TimePicker({ label, value, onChange, entity = 'kb' }) {
  return (
    <div style={{ flex: 1, minWidth: 0 }}>
      <Lbl>{label}</Lbl>
      <div style={{ position: 'relative' }}>
        <select value={value} onChange={e => onChange(e.target.value)}
          aria-label={label}
          style={{
            display: 'block', width: '100%', height: 38, padding: '0 32px 0 12px',
            boxSizing: 'border-box', borderRadius: 'var(--r-md)',
            border: '1px solid var(--border)', background: 'var(--bg)',
            color: 'var(--text)', fontSize: 13, fontFamily: 'var(--f-mono)',
            outline: 'none', cursor: 'pointer', appearance: 'none',
          }}>
          {HOURS.map(h => <option key={h} value={h}>{h}</option>)}
        </select>
        <span style={{
          position: 'absolute', right: 11, top: '50%', transform: 'translateY(-50%)',
          color: entityHsl(entity), fontSize: 13, pointerEvents: 'none',
        }}>🕐</span>
      </div>
    </div>
  );
}

// ── PhoneTopBar ──────────────────────────────────────────────────
function PhoneTopBar({ title, onBack, onSave, saving }) {
  return (
    <div style={{
      display: 'flex', alignItems: 'center', gap: 10,
      padding: '10px 14px 8px',
      borderBottom: '1px solid var(--border)',
      background: 'var(--bg)', flexShrink: 0,
    }}>
      <button onClick={onBack} aria-label="Indietro" style={{
        width: 32, height: 32, borderRadius: '50%',
        background: 'var(--bg-muted)', border: 'none', cursor: 'pointer',
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        color: 'var(--text-sec)', fontSize: 16,
      }}>←</button>
      <span style={{
        fontFamily: 'var(--f-display)', fontWeight: 800,
        fontSize: 16, flex: 1, letterSpacing: '-.01em',
      }}>{title}</span>
      <button onClick={onSave} style={{
        padding: '6px 13px', borderRadius: 'var(--r-pill)',
        background: saving ? entityHsl('toolkit') : entityHsl('event'),
        color: '#fff', border: 'none', cursor: 'pointer',
        fontSize: 11.5, fontWeight: 800, fontFamily: 'var(--f-display)',
        transition: 'background var(--dur-sm) var(--ease-out)',
      }}>{saving ? '✓ Salvato' : 'Salva'}</button>
    </div>
  );
}

// ── SeverityMatrix (severity rows × channel columns) ─────────────
const SEVERITY_ROWS = [
  { key: 'critica',    label: 'Critica',    desc: 'Inviti, cancellazioni, errori' },
  { key: 'importante', label: 'Importante', desc: 'RSVP, training completato' },
  { key: 'info',       label: 'Info',       desc: 'Digest, riepiloghi, novità' },
];
const SEVERITY_CHANNELS = [
  { key: 'inapp', icon: '🔔', label: 'In-app' },
  { key: 'email', icon: '✉️', label: 'Email' },
  { key: 'push',  icon: '📱', label: 'Push' },
];

function SeverityMatrix({ matrix, onToggle, pushDisabled }) {
  return (
    <div>
      {/* Column header */}
      <div style={{
        display: 'flex', alignItems: 'center', gap: 8,
        padding: '4px 0 8px',
      }}>
        <div style={{ flex: 1 }} />
        {SEVERITY_CHANNELS.map(c => (
          <div key={c.key} style={{
            width: 48, textAlign: 'center',
            fontSize: 9.5, fontWeight: 700, fontFamily: 'var(--f-mono)',
            color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '.04em',
          }}>
            <div style={{ fontSize: 14, marginBottom: 2 }}>{c.icon}</div>
            {c.label}
          </div>
        ))}
      </div>
      {SEVERITY_ROWS.map(row => {
        const ent = SEVERITY_ENTITY[row.key];
        return (
          <div key={row.key} style={{
            display: 'flex', alignItems: 'center', gap: 8,
            padding: '10px 0', borderTop: '1px solid var(--border-light)',
          }}>
            <div style={{
              flex: 1, minWidth: 0, display: 'flex', alignItems: 'flex-start', gap: 8,
              borderLeft: `3px solid ${entityHsl(ent)}`, paddingLeft: 9,
            }}>
              <div>
                <div style={{
                  fontSize: 12.5, fontWeight: 700, fontFamily: 'var(--f-display)',
                  color: entityHsl(ent),
                }}>{row.label}</div>
                <div style={{ fontSize: 10, color: 'var(--text-muted)', marginTop: 1, lineHeight: 1.35 }}>
                  {row.desc}
                </div>
              </div>
            </div>
            {SEVERITY_CHANNELS.map(c => {
              const isPush = c.key === 'push';
              const disabled = isPush && pushDisabled;
              return (
                <div key={c.key} style={{ width: 48, display: 'flex', justifyContent: 'center', opacity: disabled ? 0.35 : 1 }}>
                  <ToggleSwitch
                    size="sm"
                    entity={ent}
                    checked={!disabled && matrix[row.key][c.key]}
                    onChange={() => !disabled && onToggle(row.key, c.key)}
                    label={`${row.label} ${c.label}`}
                  />
                </div>
              );
            })}
          </div>
        );
      })}
    </div>
  );
}

// ── QuietHoursCard ───────────────────────────────────────────────
function QuietHoursCard({ enabled, from, to, onToggle, onFromChange, onToChange }) {
  return (
    <PrefSection
      icon="⏰" entity="kb"
      title="Quiet hours"
      subtitle="Silenzia le notifiche in una fascia oraria"
    >
      <div style={{
        display: 'flex', alignItems: 'center', justifyContent: 'space-between',
        gap: 12, padding: '4px 0 2px',
      }}>
        <div style={{ fontSize: 12.5, color: 'var(--text-sec)', fontFamily: 'var(--f-display)', fontWeight: 600 }}>
          {enabled ? `Attivo · ${from} → ${to}` : 'Disattivato'}
        </div>
        <ToggleSwitch checked={enabled} onChange={onToggle} entity="kb" label="Quiet hours" />
      </div>
      {enabled && (
        <div style={{ display: 'flex', gap: 12, marginTop: 12, alignItems: 'flex-end' }}>
          <TimePicker label="Da" value={from} onChange={onFromChange} entity="kb" />
          <div style={{ paddingBottom: 9, color: 'var(--text-muted)', fontSize: 16 }}>→</div>
          <TimePicker label="A" value={to} onChange={onToChange} entity="kb" />
        </div>
      )}
    </PrefSection>
  );
}

// ── DeviceList (registered push devices) ─────────────────────────
function DeviceList({ devices }) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
      {devices.map(d => (
        <div key={d.id} style={{
          display: 'flex', alignItems: 'center', gap: 11,
          padding: '10px 12px', borderRadius: 'var(--r-md)',
          background: 'var(--bg-muted)', border: '1px solid var(--border-light)',
        }}>
          <span style={{ fontSize: 18, flexShrink: 0 }}>{d.icon}</span>
          <div style={{ flex: 1, minWidth: 0 }}>
            <div style={{
              fontSize: 12.5, fontWeight: 700, fontFamily: 'var(--f-display)', color: 'var(--text)',
            }}>{d.name}</div>
            <div style={{ fontSize: 10, color: 'var(--text-muted)', fontFamily: 'var(--f-mono)' }}>
              {d.lastSeen}
            </div>
          </div>
          {d.current && (
            <span style={{
              padding: '2px 8px', borderRadius: 'var(--r-pill)', whiteSpace: 'nowrap',
              background: entityHsl('toolkit', 0.14), color: entityHsl('toolkit'),
              fontSize: 9.5, fontWeight: 800, fontFamily: 'var(--f-display)',
            }}>● Questo device</span>
          )}
        </div>
      ))}
    </div>
  );
}

// ── EmptyDevice (no mobile registered) ───────────────────────────
function EmptyDevice() {
  return (
    <div style={{
      display: 'flex', flexDirection: 'column', alignItems: 'center',
      textAlign: 'center', padding: '18px 16px 6px', gap: 10,
    }}>
      <div style={{
        width: 60, height: 60, borderRadius: '50%',
        background: entityHsl('session', 0.1), color: entityHsl('session'),
        display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 28,
      }}>📭</div>
      <div style={{
        fontSize: 13.5, fontWeight: 700, fontFamily: 'var(--f-display)', color: 'var(--text)',
      }}>Nessun device mobile registrato</div>
      <div style={{ fontSize: 11.5, color: 'var(--text-muted)', lineHeight: 1.5, maxWidth: 240 }}>
        Installa l'app MeepleAI sul telefono per ricevere le notifiche push.
      </div>
      <button style={{
        marginTop: 4, padding: '9px 18px',
        background: entityHsl('session'), color: '#fff',
        border: 'none', borderRadius: 'var(--r-lg)', cursor: 'pointer',
        fontSize: 12, fontWeight: 700, fontFamily: 'var(--f-display)',
        display: 'inline-flex', alignItems: 'center', gap: 7,
      }} onClick={() => { setTimeout(() => { window.location.href = 'notifications.html'; }, 0); /* DEMO-NAV */ }}>
        <span>⬇️</span> Installa app
      </button>
    </div>
  );
}

// ── SaveToast ────────────────────────────────────────────────────
function SaveToast({ message }) {
  return (
    <div role="status" aria-live="polite" style={{
      position: 'absolute', bottom: 24, left: '50%',
      transform: 'translateX(-50%)',
      display: 'flex', alignItems: 'center', gap: 9,
      padding: '11px 18px', borderRadius: 'var(--r-pill)',
      background: entityHsl('toolkit'), color: '#fff',
      boxShadow: 'var(--shadow-lg)', zIndex: 20, whiteSpace: 'nowrap',
      fontSize: 13, fontWeight: 700, fontFamily: 'var(--f-display)',
      animation: 'prefToastIn var(--dur-md) var(--ease-out)',
    }}>
      <span style={{
        width: 20, height: 20, borderRadius: '50%',
        background: 'rgba(255,255,255,0.24)',
        display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 12,
      }}>✓</span>
      {message}
    </div>
  );
}

// ── ChannelsSection (in-app, email + freq, push) ─────────────────
function ChannelsSection({ channels, onToggle, freq, onFreqChange, pushVariant, devices, freqName }) {
  return (
    <PrefSection
      icon="🎚️" entity="agent"
      title="Canali"
      subtitle="Dove vuoi ricevere le notifiche"
    >
      {/* In-app */}
      <ChannelToggleRow
        icon="🔔" label="In-app" hint="Centro notifiche dentro MeepleAI"
        entity="event"
        checked={channels.inapp} onChange={() => onToggle('inapp')}
      />
      {/* Email + frequency */}
      <ChannelToggleRow
        icon="✉️" label="Email" hint="marco@example.com"
        entity="agent"
        checked={channels.email} onChange={() => onToggle('email')}
      >
        <FrequencyRadio value={freq} onChange={onFreqChange} entity="agent" name={freqName} />
      </ChannelToggleRow>
      {/* Mobile push — toggle OR empty-state */}
      {pushVariant === 'empty' ? (
        <div style={{ paddingTop: 12, borderTop: '1px solid var(--border-light)', marginTop: 2 }}>
          <EmptyDevice />
        </div>
      ) : (
        <ChannelToggleRow
          icon="📱" label="Push mobile" hint="Notifiche sul telefono"
          entity="session"
          checked={channels.push} onChange={() => onToggle('push')}
        >
          {devices && devices.length > 0 && <DeviceList devices={devices} />}
        </ChannelToggleRow>
      )}
    </PrefSection>
  );
}

// ── PrefScreen (shared shell + scroll body) ──────────────────────
function PrefScreen({ children, onSave, saving, toast }) {
  return (
    <div style={{ flex: 1, display: 'flex', flexDirection: 'column', overflow: 'hidden', position: 'relative' }}>
      <PhoneTopBar
        title="Preferenze notifiche"
        onBack={() => { setTimeout(() => { window.location.href = 'notifications.html'; }, 0); /* DEMO-NAV */ }}
        onSave={onSave}
        saving={saving}
      />
      <div style={{ flex: 1, overflowY: 'auto', padding: '14px 0 28px' }}>
        {children}
      </div>
      {toast && <SaveToast message={toast} />}
    </div>
  );
}

// ── Default datasets ─────────────────────────────────────────────
const ALL_ON_MATRIX = {
  critica:    { inapp: true, email: true, push: true },
  importante: { inapp: true, email: true, push: true },
  info:       { inapp: true, email: true, push: true },
};
const CRITICAL_ONLY_MATRIX = {
  critica:    { inapp: true,  email: true,  push: true  },
  importante: { inapp: true,  email: true,  push: false },
  info:       { inapp: true,  email: false, push: false },
};
const DEVICES = [
  { id: 1, icon: '📱', name: 'iPhone 15 di Marco', lastSeen: 'Attivo ora', current: true },
  { id: 2, icon: '💻', name: 'MacBook Pro · Chrome', lastSeen: 'Visto 2h fa', current: false },
];

// ══════════════════════════════════════════════════════════════════
// ── SCREEN 1 — Default · all toggles ON ───────────────────────────
// ══════════════════════════════════════════════════════════════════
function Screen1DefaultAllOn() {
  const [channels, setChannels] = useState({ inapp: true, email: true, push: true });
  const [matrix, setMatrix] = useState(ALL_ON_MATRIX);
  const [freq, setFreq] = useState('realtime');
  const [quiet, setQuiet] = useState(false);
  const [saving, setSaving] = useState(false);

  const toggleChannel = k => setChannels(c => ({ ...c, [k]: !c[k] }));
  const toggleMatrix = (r, c) => setMatrix(m => ({ ...m, [r]: { ...m[r], [c]: !m[r][c] } }));
  const save = useCallback(() => { setSaving(true); setTimeout(() => setSaving(false), 2000); }, []);

  return (
    <PrefScreen onSave={save} saving={saving}>
      <ChannelsSection
        channels={channels} onToggle={toggleChannel}
        freq={freq} onFreqChange={setFreq} freqName="freq-s1"
        pushVariant="toggle" devices={DEVICES}
      />
      <PrefSection icon="🗂️" entity="event" title="Per severità"
        subtitle="Scegli i canali per ogni livello">
        <SeverityMatrix matrix={matrix} onToggle={toggleMatrix} />
      </PrefSection>
      <QuietHoursCard
        enabled={quiet} from="22:00" to="08:00"
        onToggle={setQuiet} onFromChange={() => {}} onToChange={() => {}}
      />
    </PrefScreen>
  );
}

// ══════════════════════════════════════════════════════════════════
// ── SCREEN 2 — Customized · real-time only critical ───────────────
// ══════════════════════════════════════════════════════════════════
function Screen2Customized() {
  const [channels, setChannels] = useState({ inapp: true, email: true, push: true });
  const [matrix, setMatrix] = useState(CRITICAL_ONLY_MATRIX);
  const [freq, setFreq] = useState('daily');
  const [quiet, setQuiet] = useState(false);
  const [saving, setSaving] = useState(false);

  const toggleChannel = k => setChannels(c => ({ ...c, [k]: !c[k] }));
  const toggleMatrix = (r, c) => setMatrix(m => ({ ...m, [r]: { ...m[r], [c]: !m[r][c] } }));
  const save = useCallback(() => { setSaving(true); setTimeout(() => setSaving(false), 2000); }, []);

  return (
    <PrefScreen onSave={save} saving={saving}>
      <ChannelsSection
        channels={channels} onToggle={toggleChannel}
        freq={freq} onFreqChange={setFreq} freqName="freq-s2"
        pushVariant="toggle" devices={DEVICES}
      />
      <PrefSection icon="🗂️" entity="event" title="Per severità"
        subtitle="Real-time solo per le critiche · digest per il resto">
        <SeverityMatrix matrix={matrix} onToggle={toggleMatrix} />
      </PrefSection>
      <QuietHoursCard
        enabled={quiet} from="22:00" to="08:00"
        onToggle={setQuiet} onFromChange={() => {}} onToChange={() => {}}
      />
    </PrefScreen>
  );
}

// ══════════════════════════════════════════════════════════════════
// ── SCREEN 3 — Quiet hours 22:00 → 08:00 configured ───────────────
// ══════════════════════════════════════════════════════════════════
function Screen3QuietHours() {
  const [channels, setChannels] = useState({ inapp: true, email: true, push: true });
  const [matrix, setMatrix] = useState(ALL_ON_MATRIX);
  const [freq, setFreq] = useState('realtime');
  const [quiet, setQuiet] = useState(true);
  const [from, setFrom] = useState('22:00');
  const [to, setTo] = useState('08:00');
  const [saving, setSaving] = useState(false);

  const toggleChannel = k => setChannels(c => ({ ...c, [k]: !c[k] }));
  const toggleMatrix = (r, c) => setMatrix(m => ({ ...m, [r]: { ...m[r], [c]: !m[r][c] } }));
  const save = useCallback(() => { setSaving(true); setTimeout(() => setSaving(false), 2000); }, []);

  return (
    <PrefScreen onSave={save} saving={saving}>
      <QuietHoursCard
        enabled={quiet} from={from} to={to}
        onToggle={setQuiet} onFromChange={setFrom} onToChange={setTo}
      />
      <ChannelsSection
        channels={channels} onToggle={toggleChannel}
        freq={freq} onFreqChange={setFreq} freqName="freq-s3"
        pushVariant="toggle" devices={DEVICES}
      />
      <PrefSection icon="🗂️" entity="event" title="Per severità"
        subtitle="Le critiche superano sempre il silenzio">
        <SeverityMatrix matrix={matrix} onToggle={toggleMatrix} />
      </PrefSection>
    </PrefScreen>
  );
}

// ══════════════════════════════════════════════════════════════════
// ── SCREEN 4 — No mobile device registered ────────────────────────
// ══════════════════════════════════════════════════════════════════
function Screen4NoDevice() {
  const [channels, setChannels] = useState({ inapp: true, email: true, push: false });
  const [matrix, setMatrix] = useState(ALL_ON_MATRIX);
  const [freq, setFreq] = useState('realtime');
  const [quiet, setQuiet] = useState(false);
  const [saving, setSaving] = useState(false);

  const toggleChannel = k => setChannels(c => ({ ...c, [k]: !c[k] }));
  // push column disabled in matrix because no device is registered
  const toggleMatrix = (r, c) => setMatrix(m => ({ ...m, [r]: { ...m[r], [c]: !m[r][c] } }));
  const save = useCallback(() => { setSaving(true); setTimeout(() => setSaving(false), 2000); }, []);

  return (
    <PrefScreen onSave={save} saving={saving}>
      <ChannelsSection
        channels={channels} onToggle={toggleChannel}
        freq={freq} onFreqChange={setFreq} freqName="freq-s4"
        pushVariant="empty"
      />
      <PrefSection icon="🗂️" entity="event" title="Per severità"
        subtitle="Push disabilitato finché non registri un device">
        <SeverityMatrix matrix={matrix} onToggle={toggleMatrix} pushDisabled />
      </PrefSection>
      <QuietHoursCard
        enabled={quiet} from="22:00" to="08:00"
        onToggle={setQuiet} onFromChange={() => {}} onToChange={() => {}}
      />
    </PrefScreen>
  );
}

// ══════════════════════════════════════════════════════════════════
// ── SCREEN 5 — Save success toast ─────────────────────────────────
// ══════════════════════════════════════════════════════════════════
function Screen5SaveToast() {
  const [channels, setChannels] = useState({ inapp: true, email: true, push: true });
  const [matrix, setMatrix] = useState(CRITICAL_ONLY_MATRIX);
  const [freq, setFreq] = useState('realtime');
  const [quiet, setQuiet] = useState(true);

  const toggleChannel = k => setChannels(c => ({ ...c, [k]: !c[k] }));
  const toggleMatrix = (r, c) => setMatrix(m => ({ ...m, [r]: { ...m[r], [c]: !m[r][c] } }));

  return (
    <PrefScreen onSave={() => {}} saving toast="Preferenze aggiornate">
      <ChannelsSection
        channels={channels} onToggle={toggleChannel}
        freq={freq} onFreqChange={setFreq} freqName="freq-s5"
        pushVariant="toggle" devices={DEVICES}
      />
      <PrefSection icon="🗂️" entity="event" title="Per severità"
        subtitle="Configurazione salvata">
        <SeverityMatrix matrix={matrix} onToggle={toggleMatrix} />
      </PrefSection>
      <QuietHoursCard
        enabled={quiet} from="22:00" to="08:00"
        onToggle={() => {}} onFromChange={() => {}} onToChange={() => {}}
      />
    </PrefScreen>
  );
}

// ── Export to window ─────────────────────────────────────────────
Object.assign(window, {
  Screen1DefaultAllOn, Screen2Customized, Screen3QuietHours,
  Screen4NoDevice, Screen5SaveToast,
});
