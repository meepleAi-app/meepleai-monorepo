/**
 * SP5 — Profile · Settings tab + 2FA enrollment wizard
 * Route target: /profile?tab=settings(&section=*)
 * Sblocca #1608 (SP5 S3 strict cutover).
 *
 * Sub-components (exported):
 *   ProfileTabBar         — TabBar 4-tab del Profile page
 *   SettingsTab           — Container con sidebar sub-nav + content
 *   SettingsSubNav        — Sidebar desktop 240px + mobile list
 *   TwoFactorStatusCard   — Card "2FA" stati OFF / pending / ON
 *   TwoFactorSetupModal   — Wizard modal 3-step (desktop)
 *   OTPInput6Slot         — Input 6 cifre auto-advance + paste + 5fail/900s lockout
 *   BackupCodesGrid       — Grid 2×5 codes + Copy/Download/Print + ack checkbox
 *   TwoFactorBottomSheet  — Bottom-sheet wizard mobile (V2 Peek&Expand)
 *
 * Default export `SettingsTabMockup` monta tutti gli stati per dev preview
 * (8 frames matching sp5-profile-settings.html).
 *
 * Tailwind utility classes mappate ai token MeepleAI (bg-card, text-foreground,
 * border-border, bg-kb, bg-warning, bg-success, text-danger). Vedi tailwind.config.ts.
 *
 * @prop-types-only — TypeScript flavored, no actual .ts. Inline JSDoc.
 * Nessuna import da @/lib/api: usa MOCK_* inline.
 */

import React, { useState, useRef, useEffect, useCallback } from 'react';

/* ─────────────────────────────────────────────────────────────────────────
 * Inline lucide-flavored icons (NO dipendenza da 'lucide-react')
 * ───────────────────────────────────────────────────────────────────────── */
const Svg = ({ children, size = 18, className = '' }) => (
  <svg width={size} height={size} viewBox="0 0 24 24" fill="none"
    stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round"
    className={className}>{children}</svg>
);
const Shield  = (p) => <Svg {...p}><path d="M12 2 4 5v6c0 5 3.5 9 8 11 4.5-2 8-6 8-11V5l-8-3z"/></Svg>;
const ShieldCheck = (p) => <Svg {...p}><path d="M12 2 4 5v6c0 5 3.5 9 8 11 4.5-2 8-6 8-11V5l-8-3z"/><polyline points="9 12 11 14 15 10"/></Svg>;
const Key     = (p) => <Svg {...p}><circle cx="7.5" cy="15.5" r="3.5"/><path d="M10 13l8-8"/><path d="m16 7 3 3"/><path d="m14 9 3 3"/></Svg>;
const Bell    = (p) => <Svg {...p}><path d="M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9z"/><path d="M10 21a2 2 0 0 0 4 0"/></Svg>;
const Cog     = (p) => <Svg {...p}><circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.7 1.7 0 0 0 .3 1.8 2 2 0 1 1-2.8 2.8 1.7 1.7 0 0 0-1.8-.3 1.7 1.7 0 0 0-1 1.5 2 2 0 1 1-4 0 1.7 1.7 0 0 0-1-1.5 1.7 1.7 0 0 0-1.8.3 2 2 0 1 1-2.8-2.8 1.7 1.7 0 0 0 .3-1.8 1.7 1.7 0 0 0-1.5-1 2 2 0 1 1 0-4 1.7 1.7 0 0 0 1.5-1 1.7 1.7 0 0 0-.3-1.8 2 2 0 1 1 2.8-2.8 1.7 1.7 0 0 0 1.8.3 1.7 1.7 0 0 0 1-1.5 2 2 0 1 1 4 0 1.7 1.7 0 0 0 1 1.5 1.7 1.7 0 0 0 1.8-.3 2 2 0 1 1 2.8 2.8 1.7 1.7 0 0 0-.3 1.8 1.7 1.7 0 0 0 1.5 1 2 2 0 1 1 0 4 1.7 1.7 0 0 0-1.5 1z"/></Svg>;
const UserIcon= (p) => <Svg {...p}><circle cx="12" cy="8" r="4"/><path d="M5 21a7 7 0 0 1 14 0"/></Svg>;
const LinkIc  = (p) => <Svg {...p}><path d="M10 13a5 5 0 0 0 7 0l3-3a5 5 0 0 0-7-7l-1 1"/><path d="M14 11a5 5 0 0 0-7 0l-3 3a5 5 0 0 0 7 7l1-1"/></Svg>;
const Chev    = (p) => <Svg {...p}><polyline points="9 18 15 12 9 6"/></Svg>;
const ArrowL  = (p) => <Svg {...p}><polyline points="15 18 9 12 15 6"/></Svg>;
const Copy    = (p) => <Svg {...p}><rect x="9" y="9" width="13" height="13" rx="2"/><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"/></Svg>;
const Download= (p) => <Svg {...p}><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" y1="15" x2="12" y2="3"/></Svg>;
const Printer = (p) => <Svg {...p}><polyline points="6 9 6 2 18 2 18 9"/><path d="M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2"/><rect x="6" y="14" width="12" height="8"/></Svg>;
const X       = (p) => <Svg {...p}><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></Svg>;
const Check   = (p) => <Svg {...p}><polyline points="20 6 9 17 4 12"/></Svg>;
const Alert   = (p) => <Svg {...p}><path d="M10.3 3.86 1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"/><line x1="12" y1="9" x2="12" y2="13"/><line x1="12" y1="17" x2="12.01" y2="17"/></Svg>;
const RefreshCw = (p) => <Svg {...p}><polyline points="23 4 23 10 17 10"/><polyline points="1 20 1 14 7 14"/><path d="M3.51 9a9 9 0 0 1 14.85-3.36L23 10M1 14l4.64 4.36A9 9 0 0 0 20.49 15"/></Svg>;

/* ─────────────────────────────────────────────────────────────────────────
 * Mock data (inline — NO @/lib/api)
 * ───────────────────────────────────────────────────────────────────────── */
const MOCK_USER = {
  displayName: 'Marco Rossi',
  email: 'marco.rossi@meepleai.app',
  username: '@marcorossi',
  initials: 'MR',
  joinedAt: 'Gen 2025',
  language: 'Italiano (IT)',
  timezone: 'Europe/Rome (UTC+1)',
};
const MOCK_SESSIONS = [
  { id: 's1', device: 'Chrome 124 · macOS Sonoma', meta: '2 min ago · 192.168.1.42 · Milano, IT', current: true },
  { id: 's2', device: 'Safari · iPhone 15 · iOS 17.4', meta: '1 day ago · 5.91.32.118 · Milano, IT', current: false },
  { id: 's3', device: 'API client · mai-cli/0.4.2',   meta: '3 days ago · Token tk_live_X3…',     current: false },
];
const MOCK_BACKUP_CODES = [
  'A7K3-Z9PQ', 'M4N1-X8BD', 'R2W6-LJ5T', 'F8C9-V3HK', 'D1Y4-Q6MP',
  'G3E7-N2SX', 'B5T8-W9CL', 'H6U2-K4FR', 'P9J1-Z7VN', 'S4A5-D2EQ',
];
const MOCK_TOTP_SECRET = 'JBSWY3DPEHPK3PXP';
const MOCK_VALID_OTP   = '123456'; // demo accept code

// 2FA wire constants — matched to TotpService Redis bucket
const LOCKOUT_THRESHOLD   = 5;       // fail count trigger
const LOCKOUT_DURATION_S  = 900;     // 15 min

// Sub-nav definition: section → entity color + lucide icon
const SECTIONS = [
  { id: 'profile',       label: 'Profile',            sub: 'Avatar, display name, lingua', entity: 'player',  Ic: UserIcon },
  { id: 'security',      label: 'Security',           sub: 'Manage 2FA, sessioni, codes',  entity: 'kb',      Ic: Shield   },
  { id: 'notifications', label: 'Notifications',      sub: 'Email, push, digest',          entity: 'chat',    Ic: Bell     },
  { id: 'preferences',   label: 'Preferences',        sub: 'Theme, densità, gesture',      entity: 'tool',    Ic: Cog      },
  { id: 'api-keys',      label: 'API keys',           sub: 'Token per integrazioni',       entity: 'agent',   Ic: Key      },
  { id: 'services',      label: 'Connected services', sub: 'BGG, Discord, Slack',          entity: 'toolkit', Ic: LinkIc   },
];

/* ─────────────────────────────────────────────────────────────────────────
 * Utilities
 * ───────────────────────────────────────────────────────────────────────── */
/** @param {string} txt */
const copyToClipboard = (txt) => {
  if (navigator.clipboard) return navigator.clipboard.writeText(txt);
  return Promise.resolve();
};
/** @param {string} s */
const downloadTxt = (filename, body) => {
  const blob = new Blob([body], { type: 'text/plain' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url; a.download = filename; a.click();
  URL.revokeObjectURL(url);
};
const fmtMmSs = (s) => `${String(Math.floor(s / 60)).padStart(2, '0')}:${String(s % 60).padStart(2, '0')}`;

/** Deterministic QR placeholder pattern (NOT a real QR code) */
const QRPlaceholder = ({ size = 240, seed = 17.3 }) => {
  const cells = [];
  const corners = [[0, 0], [18, 0], [0, 18]];
  corners.forEach(([cx, cy]) => {
    for (let y = 0; y < 7; y++) for (let x = 0; x < 7; x++) {
      if (x === 0 || x === 6 || y === 0 || y === 6 || (x >= 2 && x <= 4 && y >= 2 && y <= 4)) {
        cells.push([cx + x, cy + y]);
      }
    }
  });
  for (let y = 0; y < 5; y++) for (let x = 0; x < 5; x++) {
    if (x === 0 || x === 4 || y === 0 || y === 4 || (x === 2 && y === 2)) cells.push([18 + x, 18 + y]);
  }
  for (let i = 8; i < 17; i++) {
    if (i % 2 === 0) { cells.push([i, 6]); cells.push([6, i]); }
  }
  for (let y = 0; y < 25; y++) for (let x = 0; x < 25; x++) {
    if ((x < 8 && y < 8) || (x > 16 && y < 8) || (x < 8 && y > 16)) continue;
    if (x > 16 && y > 16 && x < 23 && y < 23) continue;
    if (y === 6 || x === 6) continue;
    const v = Math.sin((x + 1) * (y + 1) * seed) * 10000;
    if (v - Math.floor(v) > 0.52) cells.push([x, y]);
  }
  return (
    <div className="rounded-md border border-border bg-white p-4 shadow-xs" style={{ width: size, height: size }}>
      <svg viewBox="0 0 25 25" shapeRendering="crispEdges" className="w-full h-full" data-qr-placeholder="true">
        <rect width="25" height="25" fill="#ffffff" />
        <g fill="#14100a">
          {cells.map(([x, y], i) => <rect key={i} x={x} y={y} width="1" height="1" />)}
        </g>
      </svg>
    </div>
  );
};

/* ─────────────────────────────────────────────────────────────────────────
 * 1. ProfileTabBar
 * @prop tab {'overview'|'achievements'|'activity'|'settings'}
 * @prop onChange (next) => void
 * ───────────────────────────────────────────────────────────────────────── */
export function ProfileTabBar({ tab = 'settings', onChange = () => {} }) {
  const tabs = [
    { id: 'overview',     label: 'Overview' },
    { id: 'achievements', label: 'Achievements', count: 12 },
    { id: 'activity',     label: 'Activity' },
    { id: 'settings',     label: 'Settings' },
  ];
  return (
    <div role="tablist" className="flex gap-1 border-b border-transparent">
      {tabs.map(t => (
        <button key={t.id} role="tab" aria-selected={tab === t.id} onClick={() => onChange(t.id)}
          className={`relative px-[18px] py-3 pb-[14px] font-display font-bold text-[13px] flex items-center gap-1.5 border-b-2 transition-colors
            ${tab === t.id ? 'text-player border-player' : 'text-muted-foreground border-transparent hover:text-secondary-foreground'}`}>
          {t.label}
          {t.count != null && (
            <span className={`font-mono text-[10px] font-semibold px-1.5 py-0.5 rounded-sm
              ${tab === t.id ? 'bg-player/10 text-player' : 'bg-muted text-muted-foreground'}`}>{t.count}</span>
          )}
        </button>
      ))}
    </div>
  );
}

/* ─────────────────────────────────────────────────────────────────────────
 * 2. SettingsSubNav (desktop sidebar + mobile list, controlled by `mode`)
 * @prop activeSection string
 * @prop onSelect (sectionId) => void
 * @prop twoFactorEnabled bool
 * @prop apiKeyCount number
 * @prop mode 'desktop'|'mobile'
 * ───────────────────────────────────────────────────────────────────────── */
export function SettingsSubNav({ activeSection, onSelect = () => {}, twoFactorEnabled = false, apiKeyCount = 3, mode = 'desktop' }) {
  const badgeFor = (sec) => {
    if (sec.id === 'security') return twoFactorEnabled
      ? <span className="font-mono text-[9px] font-bold px-1.5 py-0.5 rounded-sm bg-success/15 text-success">✓ ON</span>
      : <span className="font-mono text-[9px] font-bold px-1.5 py-0.5 rounded-sm bg-warning/15 text-warning">⚠ 2FA off</span>;
    if (sec.id === 'api-keys' && apiKeyCount) return (
      <span className="font-mono text-[9px] font-bold px-1.5 py-0.5 rounded-sm bg-agent/15 text-agent">{apiKeyCount}</span>
    );
    return null;
  };

  if (mode === 'mobile') {
    return (
      <div role="navigation" aria-label="Settings sub-sections" className="flex flex-col bg-background">
        {SECTIONS.map(sec => (
          <button key={sec.id} onClick={() => onSelect(sec.id)}
            className="flex items-center gap-3.5 px-5 py-3.5 border-b border-border-light bg-card hover:bg-hover text-left">
            <span className={`flex items-center justify-center w-9 h-9 rounded-md bg-${sec.entity}/10 text-${sec.entity} shrink-0`}>
              <sec.Ic size={16} />
            </span>
            <span className="flex-1 min-w-0">
              <span className="font-display font-bold text-[14px] flex items-center gap-2 text-foreground">
                {sec.label} {badgeFor(sec)}
              </span>
              <span className="block text-[11px] text-muted-foreground mt-0.5 truncate">{sec.sub}</span>
            </span>
            <Chev className="text-muted-foreground shrink-0" size={18} />
          </button>
        ))}
      </div>
    );
  }

  // desktop
  return (
    <aside className="bg-card border-r border-border-light p-3 pt-6 sticky top-0 self-stretch w-[240px]">
      <div className="font-mono text-[10px] text-muted-foreground uppercase tracking-[0.1em] font-bold px-3 pb-3">Sub-section</div>
      <nav className="flex flex-col gap-0.5">
        {SECTIONS.map(sec => {
          const active = activeSection === sec.id;
          return (
            <button key={sec.id} onClick={() => onSelect(sec.id)}
              className={`relative flex items-center gap-3 px-3 py-2.5 rounded-md text-left font-display font-semibold text-[13px] transition-colors
                ${active
                  ? `bg-${sec.entity}/10 text-${sec.entity} font-bold before:content-[''] before:absolute before:left-0 before:top-2 before:bottom-2 before:w-[3px] before:rounded-r before:bg-${sec.entity}`
                  : 'text-secondary-foreground hover:bg-hover hover:text-foreground'}`}>
              <sec.Ic size={18} className="shrink-0" />
              <span className="flex-1">{sec.label}</span>
              {badgeFor(sec)}
            </button>
          );
        })}
      </nav>
    </aside>
  );
}

/* ─────────────────────────────────────────────────────────────────────────
 * 3. SettingsTab — container
 * @prop activeSection 'profile'|'security'|... default 'profile'
 * @prop twoFactorEnabled bool
 * @prop pending2faSetup bool
 * @prop onOpenWizard () => void
 * ───────────────────────────────────────────────────────────────────────── */
export function SettingsTab({
  activeSection = 'profile',
  twoFactorEnabled = false,
  pending2faSetup = false,
  onOpenWizard = () => {},
  onChangeSection = () => {},
}) {
  return (
    <div className="grid grid-cols-[240px_1fr] bg-background min-h-[720px]">
      <SettingsSubNav activeSection={activeSection} onSelect={onChangeSection}
        twoFactorEnabled={twoFactorEnabled} mode="desktop" />
      <div className="p-8 px-10 pb-12 overflow-hidden">
        {activeSection === 'profile' && <ProfileSection />}
        {activeSection === 'security' && (
          <SecuritySection twoFactorEnabled={twoFactorEnabled} pending2faSetup={pending2faSetup} onOpenWizard={onOpenWizard} />
        )}
        {!['profile', 'security'].includes(activeSection) && (
          <SectionPlaceholder section={SECTIONS.find(s => s.id === activeSection)} />
        )}
      </div>
    </div>
  );
}

/* ─── Profile section (D1 default content) ─────────────────────────────── */
function ProfileSection() {
  return (
    <div>
      <SectionHead kicker="PROFILE · DEFAULT" title="Profile" sub="Le tue informazioni pubbliche e le preferenze base dell'account." />
      <Card>
        <CardHead entity="player" Ic={UserIcon} title="Identità pubblica" />
        <div className="flex gap-4 items-center">
          <div className="w-[84px] h-[84px] rounded-full flex items-center justify-center text-white font-display font-extrabold text-[30px] bg-gradient-to-br from-player to-player/60 border-[3px] border-card outline outline-1 outline-border-light">{MOCK_USER.initials}</div>
          <div>
            <div className="font-mono text-[10px] font-bold uppercase tracking-[0.08em] text-muted-foreground">Display name</div>
            <div className="font-display font-bold text-[15px] text-foreground">{MOCK_USER.displayName}</div>
          </div>
          <div className="ml-auto flex gap-2">
            <Btn variant="ghost" size="sm">Carica avatar</Btn>
            <Btn size="sm">Modifica</Btn>
          </div>
        </div>
      </Card>
      <Card>
        <CardHead entity="player" Ic={Bell} title="Contatto e localizzazione" />
        <div className="grid grid-cols-2 gap-4">
          <Field label="Email" value={MOCK_USER.email} mono />
          <Field label="Username" value={MOCK_USER.username} mono />
          <Field label="Lingua" select options={['Italiano (IT)', 'English (EN)', 'Français (FR)']} />
          <Field label="Fuso orario" select options={['Europe/Rome (UTC+1)', 'Europe/London (UTC+0)']} />
        </div>
      </Card>
      <div className="flex gap-2.5 justify-end mt-2">
        <Btn variant="ghost">Annulla</Btn>
        <Btn className="bg-player text-white">Salva modifiche</Btn>
      </div>
    </div>
  );
}

/* ─── Security section ─────────────────────────────────────────────────── */
function SecuritySection({ twoFactorEnabled, pending2faSetup, onOpenWizard }) {
  return (
    <div>
      <SectionHead
        kicker={twoFactorEnabled ? 'SECURITY · 2FA ENABLED' : 'SECURITY · 2FA OFF'}
        kickerColor={twoFactorEnabled ? 'success' : 'kb'}
        title="Security"
        sub="Proteggi il tuo account con verifica a due fattori. Gestisci sessioni e recovery codes." />
      {pending2faSetup && !twoFactorEnabled && (
        <div className="bg-warning/10 border border-warning/30 rounded-md px-3.5 py-3 flex items-center gap-3 mb-4">
          <div className="w-7 h-7 rounded-full bg-warning/20 text-warning flex items-center justify-center shrink-0">◐</div>
          <div className="flex-1 text-[12px] text-foreground">
            <strong className="font-display font-bold block text-[13px]">Setup started · Continue from where you left off</strong>
            Hai aperto il wizard 2FA ma non hai confermato il TOTP. Riprendi.
          </div>
          <Btn variant="primary-kb" size="sm" onClick={onOpenWizard}>Resume</Btn>
        </div>
      )}
      <TwoFactorStatusCard state={twoFactorEnabled ? 'on' : (pending2faSetup ? 'pending' : 'off')} onSetup={onOpenWizard} />
      <Card>
        <CardHead entity="kb" Ic={() => <Svg><rect x="3" y="5" width="18" height="14" rx="2"/><path d="M8 21h8M12 17v4"/></Svg>} title="Active sessions"
          right={<span className="font-mono text-[10px] text-muted-foreground">{MOCK_SESSIONS.length} sessioni</span>} />
        {MOCK_SESSIONS.map(s => (
          <div key={s.id} className="grid grid-cols-[32px_1fr_auto] items-center gap-3.5 py-3 border-b border-border-light last:border-b-0">
            <div className="w-8 h-8 rounded-md bg-muted flex items-center justify-center text-secondary-foreground">
              <Svg size={16}><rect x="3" y="4" width="18" height="13" rx="2"/><path d="M8 21h8M12 17v4"/></Svg>
            </div>
            <div>
              <div className="font-display font-bold text-[13px] flex items-center gap-2 text-foreground">
                {s.device}
                {s.current && <span className="font-mono text-[9px] font-bold px-1.5 py-px rounded-sm bg-success/15 text-success">CURRENT</span>}
              </div>
              <div className="text-[11px] text-muted-foreground font-mono mt-0.5">{s.meta}</div>
            </div>
            {s.current
              ? <Btn variant="ghost" size="sm" disabled>This session</Btn>
              : <Btn variant="danger" size="sm">Revoke</Btn>}
          </div>
        ))}
        <div className="flex justify-end mt-3.5"><Btn variant="ghost">Sign out all other sessions</Btn></div>
      </Card>
      {twoFactorEnabled ? (
        <Card>
          <CardHead entity="kb" Ic={Key} title="Recovery codes"
            right={<Badge variant="ok">✓ 10 codes remaining</Badge>} />
          <p className="text-[13px] text-secondary-foreground m-0">I codici sono monouso. Genera un nuovo set per invalidare quelli attuali — utile se sospetti che siano stati esposti.</p>
          <Btn className="mt-3.5"><RefreshCw size={13}/>Regenerate codes</Btn>
        </Card>
      ) : (
        <Card disabled>
          <CardHead entity="kb" Ic={Key} title="Recovery codes" right={<Badge variant="dim">Disabled</Badge>} />
          <p className="text-[13px] text-secondary-foreground m-0">Disponibili dopo aver abilitato la 2FA. Servono come fallback se perdi accesso all'authenticator.</p>
          <Btn className="mt-3.5" disabled>Generate codes</Btn>
        </Card>
      )}
    </div>
  );
}

/* ─── Placeholder section (Notifications / Preferences / API keys / Services) ─── */
function SectionPlaceholder({ section }) {
  if (!section) return null;
  const { label, entity, Ic } = section;
  return (
    <div>
      <SectionHead kicker={`${label.toUpperCase()} · PLACEHOLDER`} title={label} sub="Settings UI in development." />
      <div className={`text-center py-14 px-6 bg-card border border-dashed border-border-strong rounded-lg`}>
        <div className={`w-14 h-14 rounded-lg flex items-center justify-center mx-auto mb-3.5 bg-${entity}/10 text-${entity}`}>
          <Ic size={28}/>
        </div>
        <h3 className="font-display font-bold text-[18px] m-0 text-foreground">{label}</h3>
        <p className="text-[13px] text-secondary-foreground mx-auto mt-1 max-w-[380px]">Settings UI in development. Tornerà presto con configurazione completa.</p>
        <div className="mt-6 flex flex-col gap-2.5 max-w-[480px] mx-auto">
          {[0, 1, 2].map(i => (
            <div key={i} className="bg-muted rounded-md h-14 opacity-50 flex items-center gap-3 px-3.5">
              <div className="w-7 h-7 rounded-full bg-border-strong"/>
              <div className="flex-1 flex flex-col gap-1">
                <div className="h-1.5 bg-border-strong rounded-sm"/>
                <div className="h-1.5 bg-border-strong rounded-sm w-2/5"/>
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

/* ─────────────────────────────────────────────────────────────────────────
 * 4. TwoFactorStatusCard
 * @prop state 'off' | 'pending' | 'on'
 * @prop onSetup () => void
 * @prop onRegenerate () => void
 * @prop onDisable () => void
 * ───────────────────────────────────────────────────────────────────────── */
export function TwoFactorStatusCard({ state = 'off', onSetup = () => {}, onRegenerate = () => {}, onDisable = () => {} }) {
  if (state === 'on') {
    return (
      <Card accent="success">
        <CardHead entity="success" Ic={ShieldCheck} title="Two-factor authentication"
          right={<Badge variant="ok">✓ Enabled · 2 min ago</Badge>} />
        <p className="text-[13px] text-secondary-foreground m-0">Verifica via authenticator app · Ultima verifica 5 min fa · Metodo: TOTP RFC 6238.</p>
        <div className="flex gap-2.5 mt-3.5 flex-wrap">
          <Btn variant="ghost" onClick={onRegenerate}><RefreshCw size={13}/>Regenerate recovery codes</Btn>
          <Btn variant="danger" onClick={onDisable}><X size={13}/>Disable 2FA</Btn>
        </div>
      </Card>
    );
  }
  const isPending = state === 'pending';
  return (
    <Card accent="warning">
      <CardHead entity="kb" Ic={Shield} title="Two-factor authentication"
        right={<Badge variant={isPending ? 'pending' : 'warn'}>{isPending ? '◐ Pending' : '⚠ Not enabled'}</Badge>} />
      <ul className="my-2 mb-4 p-0 list-none flex flex-col gap-1.5">
        {[
          'Aggiungi un secondo livello: se la password viene compromessa, l\'account resta al sicuro.',
          'Verifica via authenticator app (Google Authenticator, Authy, 1Password, Bitwarden).',
          'Riceverai 10 codici di recupero monouso da conservare offline.',
        ].map((t, i) => (
          <li key={i} className="text-[13px] text-secondary-foreground pl-5 relative leading-snug
            before:content-[''] before:absolute before:left-1 before:top-2 before:w-1.5 before:h-1.5 before:rounded-full before:bg-kb/55">{t}</li>
        ))}
      </ul>
      <Btn variant="primary-kb" onClick={onSetup}>
        <Shield size={14}/>{isPending ? 'Continue setup' : 'Set up two-factor authentication'}
      </Btn>
    </Card>
  );
}

/* ─────────────────────────────────────────────────────────────────────────
 * 5. OTPInput6Slot — auto-advance, paste, backspace, 5-fail/900s lockout
 * @prop onSubmit (code) => Promise<{ ok: boolean, locked?: boolean, retryAfterSeconds?: number }>
 * @prop disabled bool
 * @prop initialDigits string[] (optional, demo)
 * @prop initialFailCount number (optional, demo)
 * @prop initialLockedSeconds number|null (optional, demo)
 * ───────────────────────────────────────────────────────────────────────── */
export function OTPInput6Slot({
  onSubmit,
  disabled = false,
  initialDigits = ['', '', '', '', '', ''],
  initialFailCount = 0,
  initialLockedSeconds = null,
}) {
  const [digits, setDigits]   = useState(initialDigits);
  const [error, setError]     = useState(false);
  const [failCount, setFails] = useState(initialFailCount);
  const [lockedFor, setLock]  = useState(initialLockedSeconds);
  const refs = useRef([]);

  // lockout countdown
  useEffect(() => {
    if (lockedFor == null || lockedFor <= 0) return;
    const t = setInterval(() => setLock(s => (s != null && s > 0 ? s - 1 : 0)), 1000);
    return () => clearInterval(t);
  }, [lockedFor != null]);

  const isLocked = lockedFor != null && lockedFor > 0;
  const remaining = LOCKOUT_THRESHOLD - failCount;

  const setAt = (i, v) => {
    const next = [...digits]; next[i] = v.slice(-1); setDigits(next);
    setError(false);
    if (v && i < 5) refs.current[i + 1]?.focus();
    // auto-submit when last filled
    if (i === 5 && v && next.every(Boolean)) tryVerify(next.join(''));
  };
  const onKey = (e, i) => {
    if (e.key === 'Backspace' && !digits[i] && i > 0) refs.current[i - 1]?.focus();
  };
  const onPaste = (e) => {
    e.preventDefault();
    const txt = (e.clipboardData.getData('text') || '').replace(/\D/g, '').slice(0, 6);
    const next = txt.padEnd(6, '').split('').slice(0, 6);
    setDigits(next);
    const last = Math.min(txt.length, 6) - 1;
    if (last >= 0) refs.current[last]?.focus();
    if (txt.length === 6) tryVerify(txt);
  };
  const tryVerify = useCallback(async (code) => {
    if (isLocked || disabled) return;
    const ok = onSubmit ? await onSubmit(code) : (code === MOCK_VALID_OTP);
    if (ok && (ok === true || ok.ok)) { setError(false); setFails(0); return; }
    // failure path
    const nextFail = failCount + 1;
    setError(true);
    setFails(nextFail);
    if (nextFail >= LOCKOUT_THRESHOLD) {
      setLock(LOCKOUT_DURATION_S);
    }
    // shake animation duration (matches HTML 280ms)
    setTimeout(() => setError(false), 320);
  }, [failCount, isLocked, disabled, onSubmit]);

  return (
    <div className="flex flex-col items-center gap-2">
      <div role="group" aria-label="6-digit verification code"
        className={`flex gap-2 ${error ? 'animate-shake' : ''}`}>
        {digits.map((d, i) => (
          <input key={i} ref={el => (refs.current[i] = el)}
            value={d} onChange={e => setAt(i, e.target.value.replace(/\D/g, ''))}
            onKeyDown={e => onKey(e, i)} onPaste={onPaste}
            disabled={isLocked || disabled} maxLength={1} inputMode="numeric" pattern="[0-9]*"
            aria-label={`Digit ${i + 1}`}
            className={`w-12 h-14 text-center font-mono text-[22px] font-bold rounded-md bg-card text-foreground transition
              ${error ? 'border-[1.5px] border-danger bg-danger/5'
                      : 'border-[1.5px] border-border-strong focus:border-kb focus:outline-none focus:ring-[3px] focus:ring-kb/15 focus:scale-[1.03]'}
              ${(isLocked || disabled) ? 'opacity-50 pointer-events-none' : ''}`} />
        ))}
      </div>
      {isLocked ? (
        <div role="alert" className="mt-3 px-3.5 py-2.5 rounded-md bg-danger/8 border border-danger/25 text-danger text-[12px] flex items-center gap-2.5 justify-center">
          <Alert size={14}/>
          Too many failed attempts. Try again in <span className="font-mono font-bold">{fmtMmSs(lockedFor)}</span>.
        </div>
      ) : failCount > 0 ? (
        <div className="text-[12px] text-danger font-semibold mt-1">
          ⚠ Invalid code. Try again. ({remaining} tentativi rimasti)
        </div>
      ) : (
        <div className="text-[12px] text-muted-foreground mt-1">Il codice si aggiorna ogni 30 secondi</div>
      )}
    </div>
  );
}

/* ─────────────────────────────────────────────────────────────────────────
 * 6. BackupCodesGrid
 * @prop codes string[10]
 * @prop onCopyAll () => void
 * @prop onDownload () => void
 * @prop onAck (checked) => void
 * @prop ackChecked bool
 * ───────────────────────────────────────────────────────────────────────── */
export function BackupCodesGrid({ codes = MOCK_BACKUP_CODES, onCopyAll = () => {}, onDownload = () => {}, onAck = () => {}, ackChecked = false }) {
  const [copiedAt, setCopiedAt] = useState(null);
  const handleCopy = async () => {
    await copyToClipboard(codes.join('\n'));
    setCopiedAt(Date.now());
    onCopyAll();
    setTimeout(() => setCopiedAt(null), 1800);
  };
  const handleDownload = () => {
    downloadTxt('meepleai-recovery-codes.txt',
      `MeepleAI · Recovery codes\n${new Date().toISOString()}\n\n${codes.join('\n')}\n`);
    onDownload();
  };
  return (
    <div>
      <div className="grid grid-cols-2 gap-x-3.5 gap-y-2 p-3.5 rounded-md bg-sunken border border-border">
        {codes.map((c, i) => (
          <div key={i} className="font-mono text-[14px] font-semibold py-1.5 px-1 rounded-sm text-foreground tracking-wide text-center bg-card border border-border-light">{c}</div>
        ))}
      </div>
      <div className="flex gap-2 mt-3">
        <Btn size="sm" onClick={handleCopy}><Copy size={12}/>{copiedAt ? 'Copied!' : 'Copy all'}</Btn>
        <Btn size="sm" onClick={handleDownload}><Download size={12}/>Download .txt</Btn>
        <Btn size="sm" onClick={() => window.print()}><Printer size={12}/>Print</Btn>
      </div>
      <label className="flex items-center gap-2.5 mt-4 p-3 rounded-md bg-muted cursor-pointer select-none">
        <input type="checkbox" checked={ackChecked} onChange={e => onAck(e.target.checked)}
          className="w-[18px] h-[18px] accent-success cursor-pointer shrink-0"/>
        <span className="text-[13px] text-foreground">Ho salvato i recovery codes in un posto sicuro</span>
      </label>
    </div>
  );
}

/* ─────────────────────────────────────────────────────────────────────────
 * 7. TwoFactorSetupModal (desktop wizard, 560px)
 *    State machine: 'setup' → 'verify' → 'codes'
 *    Step 2-3 chiedono confirm su close.
 * @prop open bool
 * @prop onClose () => void
 * @prop onEnabled () => void
 * @prop initialStep optional (per dev preview)
 * ───────────────────────────────────────────────────────────────────────── */
export function TwoFactorSetupModal({ open, onClose = () => {}, onEnabled = () => {}, initialStep = 'setup' }) {
  const [step, setStep] = useState(initialStep);    // 'setup' | 'verify' | 'codes'
  const [ack, setAck]   = useState(false);

  if (!open) return null;

  const confirmClose = () => {
    if (step === 'setup') return onClose();
    if (window.confirm('Discard 2FA setup?')) onClose();
  };

  return (
    <div role="dialog" aria-modal="true" aria-label="Two-factor authentication setup"
      className="absolute inset-0 bg-[rgba(20,14,8,0.42)] backdrop-blur-[3px] z-10 flex items-start justify-center pt-20 dark:bg-black/60">
      <div className="w-[560px] max-w-[90%] bg-card rounded-xl border border-border shadow-[0_24px_64px_rgba(0,0,0,0.25)] overflow-hidden flex flex-col">
        <header className="px-6 pt-5 pb-4 flex items-start gap-3 border-b border-border-light">
          <h2 className="font-display font-bold text-[17px] flex-1 leading-tight text-foreground m-0">
            {step === 'setup' && 'Set up two-factor authentication'}
            {step === 'verify' && 'Verify your authenticator'}
            {step === 'codes'  && 'Save your recovery codes'}
          </h2>
          <span className={`font-mono text-[10px] font-bold px-2.5 py-1 rounded-full uppercase tracking-wide
            ${step === 'codes' ? 'bg-success/12 text-success' : 'bg-kb/12 text-kb'}`}>
            Step {step === 'setup' ? 1 : step === 'verify' ? 2 : 3} of 3
          </span>
          {step !== 'codes' && (
            <button onClick={confirmClose} aria-label="Close"
              className="w-[30px] h-[30px] rounded-full bg-muted text-secondary-foreground flex items-center justify-center hover:bg-hover shrink-0">
              <X size={14}/>
            </button>
          )}
        </header>
        <ProgressBar step={step} />
        <div className="p-6 flex-1">
          {step === 'setup' && <SetupBody />}
          {step === 'verify' && (
            <div className="flex flex-col items-center gap-3.5 text-center py-2">
              <div className="w-14 h-14 rounded-lg bg-kb/12 text-kb flex items-center justify-center"><Shield size={28}/></div>
              <h3 className="font-display font-bold text-[18px] m-0 text-foreground">Enter the code from your app</h3>
              <p className="text-[13px] text-secondary-foreground max-w-[360px] m-0">Apri l'authenticator e digita il codice a 6 cifre per MeepleAI.</p>
              <OTPInput6Slot onSubmit={async (code) => {
                if (code === MOCK_VALID_OTP) { setStep('codes'); return true; }
                return false;
              }} />
            </div>
          )}
          {step === 'codes' && (
            <>
              <div className="flex items-center gap-3 px-3.5 py-3 rounded-md bg-success/10 border border-success/25 mb-4">
                <div className="w-7 h-7 rounded-full bg-success text-white flex items-center justify-center shrink-0"><Check size={16}/></div>
                <div>
                  <strong className="font-display font-bold text-[13px] block text-foreground">2FA enabled · Authenticator linked</strong>
                  <span className="text-[11px] text-secondary-foreground">Conserva i 10 codici qui sotto: ti permetteranno di accedere se perdi il telefono. Sono monouso.</span>
                </div>
              </div>
              <BackupCodesGrid ackChecked={ack} onAck={setAck} />
            </>
          )}
        </div>
        <footer className="px-6 py-4 border-t border-border-light bg-background flex items-center justify-end gap-2.5">
          {step === 'setup' && (<>
            <Btn variant="ghost" className="mr-auto" onClick={onClose}>Cancel</Btn>
            <Btn variant="primary-kb" onClick={() => setStep('verify')}>Continue →</Btn>
          </>)}
          {step === 'verify' && (<>
            <Btn variant="ghost" className="mr-auto" onClick={() => setStep('setup')}>← Back</Btn>
            <Btn variant="primary-kb" onClick={() => setStep('codes')}>Verify and enable</Btn>
          </>)}
          {step === 'codes' && (
            <Btn variant="primary-toolkit" disabled={!ack} onClick={() => { onEnabled(); onClose(); }}>Done</Btn>
          )}
        </footer>
      </div>
    </div>
  );
}
function ProgressBar({ step }) {
  const order = ['setup', 'verify', 'codes'];
  const idx = order.indexOf(step);
  return (
    <div className="flex gap-1 px-6 pt-3.5 bg-card">
      {order.map((k, i) => (
        <div key={k} className={`flex-1 h-1 rounded-sm relative overflow-hidden
          ${i < idx ? 'bg-kb' : i === idx ? 'bg-kb/25' : 'bg-muted'}
          ${step === 'codes' && i === 2 ? '!bg-success' : ''}`}>
          {i === idx && <span className="absolute inset-y-0 left-0 w-3/5 bg-kb rounded-sm animate-[slide_1.4s_ease-in-out_infinite]"/>}
        </div>
      ))}
    </div>
  );
}
function SetupBody() {
  const [copied, setCopied] = useState(false);
  const handleCopy = async () => {
    await copyToClipboard(MOCK_TOTP_SECRET);
    setCopied(true); setTimeout(() => setCopied(false), 1500);
  };
  const formatted = MOCK_TOTP_SECRET.replace(/(.{4})/g, '$1 ').trim();
  return (
    <div className="grid grid-cols-[240px_1fr] gap-6 max-md:grid-cols-1">
      <div className="flex flex-col gap-2.5 items-center">
        <QRPlaceholder size={240} />
        <div className="font-mono text-[11px] text-muted-foreground text-center">Scan with your authenticator app</div>
      </div>
      <div>
        <h4 className="font-display font-bold text-[13px] m-0 mb-1 text-foreground">Can't scan?</h4>
        <p className="text-[12px] text-secondary-foreground m-0 mb-2.5">Enter this code manually in your app:</p>
        <div className="bg-sunken border border-border rounded-md px-3 py-2.5 flex items-center gap-2.5 font-mono text-[13px] tracking-wide mb-3.5 text-foreground">
          <span className="flex-1 select-all">{formatted}</span>
          <button onClick={handleCopy} aria-label="Copy secret"
            className="w-7 h-7 rounded-sm bg-card border border-border flex items-center justify-center text-secondary-foreground hover:text-kb hover:border-kb/40 shrink-0">
            {copied ? <Check size={12}/> : <Copy size={14}/>}
          </button>
        </div>
        <h4 className="font-display font-bold text-[13px] m-0 mb-1.5 text-foreground">App compatibili</h4>
        <div className="flex flex-wrap gap-1.5">
          {['Google Authenticator', 'Authy', '1Password', 'Bitwarden'].map(app => (
            <span key={app} className="font-mono text-[10px] px-2.5 py-1 rounded-full bg-muted text-secondary-foreground font-semibold">{app}</span>
          ))}
        </div>
      </div>
    </div>
  );
}

/* ─────────────────────────────────────────────────────────────────────────
 * 8. TwoFactorBottomSheet (mobile, V2 Peek & Expand)
 *    snap ∈ {'expanded'|'peek'|'closed'} — drag-down su step 2/3 chiede confirm
 * @prop open bool
 * @prop step 'setup'|'verify'|'codes'
 * @prop onStepChange
 * @prop onClose
 * ───────────────────────────────────────────────────────────────────────── */
export function TwoFactorBottomSheet({ open, step = 'setup', onStepChange = () => {}, onClose = () => {} }) {
  const [snap, setSnap] = useState('expanded');
  const [ack, setAck]   = useState(false);
  if (!open) return null;

  const guardedSnap = (next) => {
    if (next === 'peek' && step !== 'setup') {
      if (!window.confirm('Discard 2FA setup?')) return;
      onClose(); return;
    }
    setSnap(next);
  };
  const guardedClose = () => {
    if (step === 'setup') return onClose();
    if (window.confirm('Discard 2FA setup?')) onClose();
  };

  return (
    <>
      <div className="absolute inset-0 bg-[rgba(20,14,8,0.35)] backdrop-blur-[2px] z-30 dark:bg-black/55" onClick={guardedClose}/>
      <div role="dialog" aria-modal="true"
        className={`absolute inset-x-0 bottom-0 bg-card rounded-t-[20px] shadow-[0_-10px_30px_rgba(0,0,0,0.25)] z-[31] flex flex-col transition-[height] duration-300
          ${snap === 'peek' ? 'h-[38%]' : 'h-[88%]'}`}>
        <div className="w-10 h-1 rounded-sm bg-border-strong mx-auto mt-2 mb-1" aria-hidden="true"/>
        <header className="flex items-center gap-2.5 px-4 pt-2 pb-3 border-b border-border-light">
          <span className="font-mono text-[10px] font-bold px-2 py-0.5 rounded-full bg-kb/12 text-kb uppercase">
            Step {step === 'setup' ? 1 : step === 'verify' ? 2 : 3} of 3
          </span>
          <div className="flex-1 font-display font-bold text-[14px] text-foreground">Set up 2FA</div>
          <button className="w-7 h-7 rounded-full bg-muted text-secondary-foreground flex items-center justify-center" onClick={guardedClose} aria-label="Close">
            <X size={12}/>
          </button>
        </header>
        <ProgressBar step={step} />
        <div className="flex-1 p-4 overflow-y-auto">
          {step === 'setup' && (
            <div className="flex flex-col gap-4">
              <div className="flex flex-col items-center gap-2.5">
                <QRPlaceholder size={180} />
                <div className="font-mono text-[11px] text-muted-foreground text-center">Scan con la tua authenticator app</div>
              </div>
              <div>
                <div className="font-display font-bold text-[12px] mb-1.5 text-foreground">Can't scan? Inserisci manualmente</div>
                <div className="bg-sunken border border-border rounded-md px-3 py-2.5 flex items-center gap-2.5 font-mono text-[12px] text-foreground">
                  <span className="flex-1 select-all">{MOCK_TOTP_SECRET.replace(/(.{4})/g, '$1 ').trim()}</span>
                  <button onClick={() => copyToClipboard(MOCK_TOTP_SECRET)} className="w-6 h-6 rounded-sm bg-card border border-border flex items-center justify-center text-secondary-foreground"><Copy size={12}/></button>
                </div>
              </div>
            </div>
          )}
          {step === 'verify' && (
            <div className="flex flex-col items-center gap-3 pt-2">
              <div className="w-12 h-12 rounded-lg bg-kb/12 text-kb flex items-center justify-center"><Shield size={24}/></div>
              <h3 className="font-display font-bold text-[16px] m-0 text-foreground">Enter the code</h3>
              <OTPInput6Slot onSubmit={async (code) => code === MOCK_VALID_OTP ? (onStepChange('codes'), true) : false} />
            </div>
          )}
          {step === 'codes' && <BackupCodesGrid ackChecked={ack} onAck={setAck} />}
        </div>
        <footer className="px-4 py-3 border-t border-border-light bg-background flex gap-2">
          {step === 'setup' && (<>
            <Btn variant="ghost" onClick={() => guardedSnap(snap === 'peek' ? 'expanded' : 'peek')}>{snap === 'peek' ? '⌃ Expand' : '⌄ Peek'}</Btn>
            <Btn variant="primary-kb" className="flex-1 justify-center" onClick={() => onStepChange('verify')}>Continue →</Btn>
          </>)}
          {step === 'verify' && (
            <Btn variant="ghost" className="flex-1 justify-center" onClick={() => onStepChange('setup')}>← Back</Btn>
          )}
          {step === 'codes' && (
            <Btn variant="primary-toolkit" className="flex-1 justify-center" disabled={!ack} onClick={onClose}>Done</Btn>
          )}
        </footer>
      </div>
    </>
  );
}

/* ─────────────────────────────────────────────────────────────────────────
 * Atomic helpers
 * ───────────────────────────────────────────────────────────────────────── */
function Card({ children, accent, disabled }) {
  const accentCls = accent === 'kb'      ? 'border-l-[3px] border-l-kb'
                  : accent === 'warning' ? 'border-l-[3px] border-l-warning'
                  : accent === 'success' ? 'border-l-[3px] border-l-success bg-gradient-to-b from-success/3 to-card'
                  : '';
  return (
    <div className={`bg-card border border-border-light rounded-lg px-6 py-5 mb-4 relative ${accentCls} ${disabled ? 'opacity-60' : ''}`}>
      {children}
    </div>
  );
}
function CardHead({ entity = 'kb', Ic, title, right }) {
  return (
    <header className="flex items-center gap-3 mb-3">
      <div className={`w-9 h-9 rounded-md flex items-center justify-center bg-${entity}/12 text-${entity} shrink-0`}><Ic size={18}/></div>
      <h3 className="font-display font-bold text-[16px] m-0 text-foreground">{title}</h3>
      {right && <div className="ml-auto">{right}</div>}
    </header>
  );
}
function Badge({ variant = 'warn', children }) {
  const m = {
    warn:    'bg-warning/15 text-warning',
    ok:      'bg-success/15 text-success',
    pending: 'bg-warning/15 text-warning',
    dim:     'bg-muted text-muted-foreground',
  };
  return (
    <span className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full font-mono text-[10px] font-bold uppercase tracking-wide ${m[variant]}`}>
      {children}
    </span>
  );
}
function Btn({ variant = 'default', size = 'md', children, className = '', ...rest }) {
  const v = {
    default:         'bg-muted text-foreground',
    ghost:           'bg-transparent border border-border text-secondary-foreground',
    'primary-kb':    'bg-kb text-white hover:shadow-[0_4px_14px_rgba(0,136,128,0.35)]',
    'primary-toolkit':'bg-success text-white',
    danger:          'bg-transparent text-danger hover:bg-danger/8',
  }[variant];
  const s = size === 'sm' ? 'px-2.5 py-1.5 text-[11px]' : 'px-4 py-2.5 text-[13px]';
  return (
    <button {...rest}
      className={`inline-flex items-center gap-1.5 rounded-md font-display font-bold border border-transparent transition disabled:opacity-50 disabled:pointer-events-none ${v} ${s} ${className}`}>
      {children}
    </button>
  );
}
function SectionHead({ kicker, kickerColor = 'kb', title, sub }) {
  return (
    <div className="mb-6">
      <div className={`font-mono text-[10px] uppercase tracking-[0.12em] font-bold mb-1.5 text-${kickerColor}`}>{kicker}</div>
      <h1 className="font-display font-bold text-[26px] leading-tight m-0 text-foreground">{title}</h1>
      {sub && <p className="text-secondary-foreground text-[13px] mt-1.5 max-w-[620px] m-0">{sub}</p>}
    </div>
  );
}
function Field({ label, value, mono, select, options }) {
  return (
    <div className="flex flex-col gap-1.5">
      <label className="font-mono text-[10px] font-bold uppercase tracking-[0.08em] text-muted-foreground">{label}</label>
      {select
        ? <select className="px-3 py-2.5 rounded-md border border-border bg-background text-[13px] text-foreground focus:outline-none focus:border-player/60 focus:ring-[3px] focus:ring-player/12">
            {options?.map(o => <option key={o}>{o}</option>)}
          </select>
        : <div className={`px-3 py-2.5 rounded-md border border-border bg-background text-[13px] ${mono ? 'font-mono text-secondary-foreground' : 'text-foreground'}`}>{value}</div>}
    </div>
  );
}

/* ─────────────────────────────────────────────────────────────────────────
 * Default export: SettingsTabMockup — dev preview con tutti gli stati
 * Replica gli 8 frame di sp5-profile-settings.html (D1-D6 + M1-M2)
 * ───────────────────────────────────────────────────────────────────────── */
export default function SettingsTabMockup() {
  // Each frame is an isolated, scrollable preview block
  const Frame = ({ label, route, children, mobile, withModal }) => (
    <section className={`mb-12 relative ${withModal ? 'min-h-[820px]' : ''}`}>
      <div className="font-mono text-[10px] text-muted-foreground uppercase tracking-[0.08em] font-bold mb-2 pb-2 border-b border-border">
        {label}
      </div>
      <div className={`rounded-2xl border border-border bg-background shadow-lg overflow-hidden relative
        ${mobile ? 'mx-auto w-[380px] h-[780px] rounded-[48px] border-[10px] border-black' : ''}`}>
        <div className="h-9 bg-muted border-b border-border flex items-center gap-2.5 px-3.5 text-[11px] font-mono text-muted-foreground">
          <span className="w-2.5 h-2.5 rounded-full bg-border-strong"/><span className="w-2.5 h-2.5 rounded-full bg-border-strong"/><span className="w-2.5 h-2.5 rounded-full bg-border-strong"/>
          <span className="flex-1 bg-card rounded-full px-3.5 py-1 truncate">meepleai.app{route}</span>
        </div>
        {children}
      </div>
    </section>
  );

  const HeaderBlock = ({ enabled }) => (
    <div className="bg-card border-b border-border-light px-10 pt-7">
      <div className="flex items-center gap-5 pb-6">
        <div className="w-[76px] h-[76px] rounded-full flex items-center justify-center text-white font-display font-extrabold text-[26px] bg-gradient-to-br from-player to-player/60 shadow-[0_6px_18px_rgba(124,58,237,0.25)] border-[3px] border-card outline outline-1 outline-border-light">{MOCK_USER.initials}</div>
        <div>
          <div className="font-display text-[22px] font-bold leading-tight text-foreground">{MOCK_USER.displayName}</div>
          <div className="font-mono text-[12px] text-muted-foreground mt-0.5">{MOCK_USER.email}</div>
          {enabled && (
            <div className="flex gap-2 mt-2 items-center text-[11px] text-secondary-foreground">
              <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-sm bg-success/12 text-success font-display font-bold uppercase tracking-wide text-[9px]">✓ Protected</span>
              <span>2FA · authenticator app</span>
            </div>
          )}
        </div>
        <div className="ml-auto"><Btn>✎ Modifica profilo</Btn></div>
      </div>
      <ProfileTabBar tab="settings" />
    </div>
  );

  // D3-D5 wizard demo state
  return (
    <div className="bg-background text-foreground p-6 pt-20 min-h-screen">
      <h1 className="font-display font-bold text-[32px] mb-2">SP5 · Profile Settings + 2FA · dev preview</h1>
      <p className="text-secondary-foreground text-[14px] mb-8 max-w-[720px]">Tutti gli stati del tab Settings + wizard 2FA. Clona <code className="font-mono">SettingsTab</code> in <code className="font-mono">apps/web/src/app/(authenticated)/profile/_components/</code>.</p>

      <Frame label="D1 · Profile landing — tab Settings (Profile section default)" route="?tab=settings">
        <HeaderBlock />
        <SettingsTab activeSection="profile" />
      </Frame>

      <Frame label="D2 · Section Security — 2FA OFF" route="?tab=settings&section=security">
        <HeaderBlock />
        <SettingsTab activeSection="security" twoFactorEnabled={false} />
      </Frame>

      <Frame label="D3 · Wizard 2FA — Step 1/3 (QR)" route="?tab=settings&section=security · modal:setup" withModal>
        <HeaderBlock />
        <div className="opacity-40 pointer-events-none"><SettingsTab activeSection="security" /></div>
        <TwoFactorSetupModal open initialStep="setup" />
      </Frame>

      <Frame label="D4 · Wizard 2FA — Step 2/3 (Verify)" route="?tab=settings&section=security · modal:verify" withModal>
        <HeaderBlock />
        <div className="opacity-40 pointer-events-none"><SettingsTab activeSection="security" /></div>
        <TwoFactorSetupModal open initialStep="verify" />
      </Frame>

      <Frame label="D5 · Wizard 2FA — Step 3/3 (Backup codes)" route="?tab=settings&section=security · modal:codes" withModal>
        <HeaderBlock />
        <div className="opacity-40 pointer-events-none"><SettingsTab activeSection="security" /></div>
        <TwoFactorSetupModal open initialStep="codes" />
      </Frame>

      <Frame label="D6 · Section Security — 2FA ON" route="?tab=settings&section=security">
        <HeaderBlock enabled />
        <SettingsTab activeSection="security" twoFactorEnabled />
      </Frame>

      <div className="grid grid-cols-[380px_380px] gap-7 justify-center mt-12">
        <Frame label="M1 · Mobile · Settings tab list" route="?tab=settings" mobile>
          <div className="bg-card px-5 pt-3 pb-0 border-b border-border-light">
            <div className="flex items-center gap-3 py-1.5 pb-3.5">
              <div className="w-12 h-12 rounded-full bg-gradient-to-br from-player to-player/60 text-white font-display font-extrabold text-[18px] flex items-center justify-center">{MOCK_USER.initials}</div>
              <div>
                <div className="font-display font-bold text-[15px] text-foreground">{MOCK_USER.displayName}</div>
                <div className="font-mono text-[10px] text-muted-foreground">{MOCK_USER.email}</div>
              </div>
            </div>
            <div className="flex gap-0.5 overflow-x-auto">
              {['Overview', 'Achievements', 'Activity', 'Settings'].map(t => (
                <div key={t} className={`px-3.5 py-2.5 font-display font-bold text-[12px] border-b-2 whitespace-nowrap ${t === 'Settings' ? 'text-player border-player' : 'text-muted-foreground border-transparent'}`}>{t}</div>
              ))}
            </div>
          </div>
          <SettingsSubNav activeSection="settings" mode="mobile" />
        </Frame>

        <Frame label="M2 · Mobile · Security + bottom-sheet" route="?tab=settings&section=security" mobile>
          <div className="relative h-full flex flex-col bg-background">
            <div className="bg-card border-b border-border-light px-3.5 py-2.5 flex items-center gap-2">
              <button className="w-9 h-9 rounded-full text-foreground flex items-center justify-center"><ArrowL size={18}/></button>
              <div className="font-display font-bold text-[16px] text-foreground">Security</div>
            </div>
            <div className="p-3.5 flex flex-col gap-3 flex-1">
              <TwoFactorStatusCard state="off" />
            </div>
            <TwoFactorBottomSheet open step="setup" />
          </div>
        </Frame>
      </div>

      <style>{`
        @keyframes slide { 0% { transform: translateX(-100%); } 100% { transform: translateX(166%); } }
        @keyframes shake {
          0%,100% { transform: translateX(0); }
          25% { transform: translateX(-6px); }
          75% { transform: translateX(6px); }
        }
        .animate-shake { animation: shake .28s var(--ease-out, ease-out); }
      `}</style>
    </div>
  );
}
