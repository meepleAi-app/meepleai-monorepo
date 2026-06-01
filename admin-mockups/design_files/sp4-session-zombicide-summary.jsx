/* MeepleAI SP4 · Zombicide: Green Horde — SESSION SUMMARY  (App)
   /sessions/[id]/summary — riepilogo post-partita di Zombicide GH.

   Hero: banner VITTORIA / SCONFITTA large.
     · VITTORIA → nome missione + survivor vivi + XP totali guadagnati
     · SCONFITTA → causa (tutti morti / civile ucciso / Necromancer fuggito / timeout)
   Tab: Scoreboard (BinaryWin · obiettivi check/cross + survivor vivi + XP per survivor) ·
        Survivors (skill tree finale · livello · kill per tipo · ferite · armi) ·
        Board (snapshot tessere finale + zombie uccisi per tipo · istogramma) ·
        Stats (turni, XP/turno, kill ratio, tiri medi, armi più usate, Danger per round).

   Riusa: OutcomeBadge · ComputationBadge · ScoringPanelRenderer atoms · MapTilesGrid ·
          SurvivorAvatar · SkillTree · WoundPips · LevelChip · atomi ZOM.
   Carica gli stessi deps del live + QUESTO file.
   DEMO-NAV-HINTS: sp4-session-zombicide-live.html
*/

const M = window.MAI;
const R = window.SkeletonRenderers;
const S = window.SkeletonParts;
const Z = window.ZOM;
const F = window.ZOMFlavor;
const P = window.ZOMParts;
const eHsl = M.entityHsl;
const { useState, useEffect } = React;
const { mono, disp } = F;
const SUM = Z.summary;

function ThemeToggle() {
  const [dark, setDark] = useState(true);
  useEffect(() => { document.documentElement.setAttribute('data-theme', dark ? 'dark' : 'light'); }, [dark]);
  return (
    <button onClick={() => setDark(d => !d)} className="theme-toggle" aria-label={dark ? 'Tema chiaro' : 'Tema scuro'}>
      <span aria-hidden="true">{dark ? '🌙' : '☀️'}</span><span>{dark ? 'Dark' : 'Light'}</span>
    </button>
  );
}

const totalXp = (sum) => sum.survivors.reduce((a, s) => a + s.xp, 0);

// ─── Hero — banner VITTORIA / SCONFITTA ───────────────────────────────────
const SummaryHero = ({ sum = SUM, compact }) => {
  const win = sum.result === 'victory';
  const e = win ? 'toolkit' : 'danger';
  const xp = totalXp(sum);
  return (
    <div role="status" style={{ position: 'relative', borderRadius: 'var(--r-xl)', overflow: 'hidden', border: `1px solid ${eHsl(e, 0.4)}`, background: `linear-gradient(135deg, ${eHsl(e, 0.14)}, ${eHsl('session', 0.06)})`, boxShadow: 'var(--shadow-md)' }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: compact ? 12 : 18, padding: compact ? 14 : 22, flexWrap: 'wrap' }}>
        <div style={{ width: compact ? 54 : 70, height: compact ? 66 : 88, borderRadius: 'var(--r-md)', background: Z.game.cover, flexShrink: 0, display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: compact ? 26 : 36, boxShadow: 'var(--shadow-sm)' }} aria-hidden="true">{Z.game.emoji}</div>
        <div style={{ flex: 1, minWidth: 200 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 5 }}>
            <span style={{ display: 'inline-flex', alignItems: 'center', gap: 6, padding: '3px 11px', borderRadius: 'var(--r-pill)', background: eHsl(e, 0.16), color: eHsl(e), border: `1px solid ${eHsl(e, 0.4)}`, ...mono(10, 800), textTransform: 'uppercase', letterSpacing: '.08em' }}>
              <span aria-hidden="true">{win ? '🏆' : '☠'}</span>{win ? 'Vittoria' : 'Sconfitta'}
            </span>
            <span style={{ ...mono(9.5, 700, 'var(--text-muted)') }}>{Z.game.title} · {sum.rounds} round · {sum.turns} turni</span>
          </div>
          <h2 style={{ ...disp(compact ? 24 : 34, 800, 'var(--text)'), margin: '2px 0 4px' }}>
            {win ? <>Missione completata <span aria-hidden="true">🏆</span></> : <>Gruppo annientato <span aria-hidden="true">☠</span></>}
          </h2>
          <div style={{ ...mono(11, 700, 'var(--text-sec)'), lineHeight: 1.5 }}>
            {win
              ? <>Scenario <strong style={{ color: 'var(--text)' }}>"{sum.mission}"</strong> · obiettivi raggiunti con {sum.survivorsAlive}/{sum.survivorsTotal} survivor vivi.</>
              : <>Scenario <strong style={{ color: 'var(--text)' }}>"{sum.mission}"</strong> · causa: <strong style={{ color: eHsl('danger') }}>{sum.defeatCause}</strong>.</>}
          </div>
        </div>
        {/* metriche primarie */}
        <div style={{ display: 'flex', gap: 10, flexShrink: 0 }}>
          <div style={{ textAlign: 'center', padding: '10px 16px', borderRadius: 'var(--r-lg)', background: 'var(--bg-card)', border: `1px solid ${eHsl(win ? 'toolkit' : 'danger', 0.3)}` }}>
            <div style={{ ...disp(compact ? 28 : 38, 800, eHsl(win ? 'toolkit' : 'danger')), fontVariantNumeric: 'tabular-nums', lineHeight: 1 }}>{sum.survivorsAlive}<span style={{ ...disp(compact ? 16 : 20, 800, 'var(--text-muted)') }}>/{sum.survivorsTotal}</span></div>
            <div style={{ ...mono(8.5, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.06em', marginTop: 3 }}>survivor vivi</div>
          </div>
          <div style={{ textAlign: 'center', padding: '10px 16px', borderRadius: 'var(--r-lg)', background: 'var(--bg-card)', border: '1px solid var(--border)' }}>
            <div style={{ ...disp(compact ? 26 : 34, 800, eHsl('game')), fontVariantNumeric: 'tabular-nums', lineHeight: 1, marginTop: compact ? 2 : 4 }}>{xp}</div>
            <div style={{ ...mono(8.5, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.06em', marginTop: 3 }}>XP totali</div>
          </div>
        </div>
      </div>
      <div style={{ ...mono(9, 700, 'var(--text-muted)'), padding: '6px 16px', borderTop: '1px solid var(--border-light)', background: 'var(--glass-bg)' }}>
        BinaryWin · co-op · {sum.objectives.filter(o => o.done).length}/{sum.objectives.length} obiettivi completati · Danger Level finale <strong style={{ color: eHsl(Z.levelMeta(sum.dangerByRound[sum.dangerByRound.length - 1].level).e) }}>{Z.levelMeta(sum.dangerByRound[sum.dangerByRound.length - 1].level).lb}</strong>
      </div>
    </div>
  );
};

// ─── Scoreboard (BinaryWin) — obiettivi + survivor vivi + XP per survivor ──
const ScoreboardPanel = ({ state = 'default' }) => (
  <R.StateScaffold state={state} sseWhere="esito"
    empty={{ icon: '🏆', title: 'Nessun esito', body: 'L\u2019esito comparirà a partita conclusa.' }}
    error={{ title: 'Esito non disponibile', body: 'Impossibile caricare il riepilogo della missione.' }}
    loading={<div style={{ padding: 14, display: 'flex', flexDirection: 'column', gap: 8 }}>{Array.from({ length: 5 }).map((_, i) => <M.Shimmer key={i} h={44} />)}</div>}>
    <div style={{ padding: 14, display: 'flex', flexDirection: 'column', gap: 16 }}>
      {/* condizioni BinaryWin */}
      <div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 8 }}>
          <span style={{ ...mono(10, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.07em' }}>Condizioni di vittoria</span>
          <span style={{ ...mono(9, 800, eHsl('session')), padding: '2px 8px', borderRadius: 'var(--r-pill)', background: eHsl('session', 0.12), border: `1px solid ${eHsl('session', 0.3)}`, textTransform: 'uppercase' }}>scoreType · BinaryWin</span>
        </div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 5 }}>
          {Z.ds.scoring.categories.map(cat => (
            <div key={cat.id} title={cat.description} style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '8px 10px', borderRadius: 'var(--r-md)', background: 'var(--bg-muted)', border: '1px solid var(--border-light)' }}>
              <R.ComputationBadge c={cat.computation} />
              <span style={{ flex: 1, minWidth: 0, ...disp(12, 700, 'var(--text)') }}>{cat.label}</span>
              <span style={{ ...mono(9.5, 800, cat.weight > 0 ? eHsl('toolkit') : 'var(--text-muted)') }}>{cat.weight > 0 ? 'vince' : 'info'}</span>
            </div>
          ))}
        </div>
      </div>
      {/* obiettivi missione */}
      <div>
        <div style={{ ...mono(10, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.07em', marginBottom: 6 }}>Obiettivi missione</div>
        <div role="list" style={{ display: 'flex', flexDirection: 'column', gap: 5 }}>
          {SUM.objectives.map(o => (
            <div key={o.id} role="listitem" style={{ display: 'flex', alignItems: 'center', gap: 9, padding: '8px 10px', borderRadius: 'var(--r-md)', background: o.done ? eHsl('toolkit', 0.07) : eHsl('danger', 0.06), border: `1px solid ${o.done ? eHsl('toolkit', 0.28) : eHsl('danger', 0.25)}` }}>
              <span aria-hidden="true" style={{ width: 20, height: 20, borderRadius: '50%', flexShrink: 0, background: o.done ? eHsl('toolkit') : eHsl('danger'), color: '#fff', display: 'flex', alignItems: 'center', justifyContent: 'center', ...mono(11, 800) }}>{o.done ? '✓' : '✕'}</span>
              <span style={{ flex: 1, ...disp(12.5, 700, 'var(--text)') }}>{o.label}</span>
              <span style={{ ...mono(9, 800, o.done ? eHsl('toolkit') : eHsl('danger')), textTransform: 'uppercase' }}>{o.done ? 'fatto' : 'fallito'}</span>
            </div>
          ))}
        </div>
      </div>
      {/* survivor vivi + XP */}
      <div>
        <div style={{ ...mono(10, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.07em', marginBottom: 6 }}>Survivor · stato finale + XP</div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
          {SUM.survivors.map(s => (
            <div key={s.id} style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '8px 10px', borderRadius: 'var(--r-md)', background: s.dead ? eHsl('danger', 0.05) : 'var(--bg-card)', border: `1px solid ${s.dead ? eHsl('danger', 0.25) : 'var(--border)'}` }}>
              <F.SurvivorAvatar s={s} size={30} dead={s.dead} />
              <div style={{ flex: 1, minWidth: 0 }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                  <span style={{ ...disp(13.5, 800, 'var(--text)') }}>{s.name}</span>
                  <F.LevelChip level={s.level} compact />
                </div>
                <div style={{ ...mono(8.5, 700, 'var(--text-muted)') }}>{s.klass} · {Object.values(s.kills).reduce((a, b) => a + b, 0)} kill</div>
              </div>
              <F.WoundPips wounds={s.wounds} dead={s.dead} size={14} />
              <div style={{ textAlign: 'right', flexShrink: 0 }}>
                <div style={{ ...disp(18, 800, eHsl('game')), fontVariantNumeric: 'tabular-nums', lineHeight: 1 }}>{s.xp}</div>
                <div style={{ ...mono(7.5, 800, 'var(--text-muted)'), textTransform: 'uppercase' }}>XP</div>
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  </R.StateScaffold>
);

// ─── Survivors tab — skill tree finale · kill per tipo · armi ──────────────
const SurvivorsTab = ({ state = 'default' }) => (
  <R.StateScaffold state={state} sseWhere="survivor"
    empty={{ icon: '🧍', title: 'Nessun survivor', body: 'I dettagli compariranno a fine partita.' }}
    error={{ title: 'Survivor non disponibili', body: 'Impossibile caricare le schede finali.' }}>
    <div className="mai-cb-scroll" style={{ padding: 14, display: 'flex', flexDirection: 'column', gap: 14, overflowY: 'auto', flex: 1, minHeight: 0 }}>
      {SUM.survivors.map(s => {
        const survForTree = Z.byId(s.id) || s;
        return (
          <div key={s.id} style={{ borderRadius: 'var(--r-lg)', border: `1px solid ${s.dead ? eHsl('danger', 0.3) : 'var(--border)'}`, background: 'var(--bg-card)', overflow: 'hidden' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 9, padding: '9px 11px', background: s.dead ? eHsl('danger', 0.06) : 'var(--bg-muted)', borderBottom: '1px solid var(--border-light)', flexWrap: 'wrap', rowGap: 6 }}>
              <F.SurvivorAvatar s={s} size={30} dead={s.dead} />
              <span style={{ ...disp(14, 800, 'var(--text)') }}>{s.name}</span>
              <span style={{ ...mono(9, 700, 'var(--text-muted)') }}>{s.klass}</span>
              <F.LevelChip level={s.level} compact />
              <div style={{ flex: 1 }} />
              <F.WoundPips wounds={s.wounds} dead={s.dead} size={14} />
              <span style={{ ...disp(16, 800, eHsl('game')), fontVariantNumeric: 'tabular-nums' }}>{s.xp} XP</span>
            </div>
            <div style={{ padding: 11, display: 'flex', flexDirection: 'column', gap: 11 }}>
              <F.SkillTree survivor={survForTree} layout="rows" />
              {/* kill per tipo */}
              <div>
                <div style={{ ...mono(8.5, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.06em', marginBottom: 5 }}>Eliminazioni per tipo</div>
                <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                  {Z.ZORDER.map(t => (
                    <span key={t} style={{ display: 'inline-flex', alignItems: 'center', gap: 4, padding: '3px 8px 3px 3px', borderRadius: 'var(--r-pill)', background: 'var(--bg-muted)', border: '1px solid var(--border-light)', opacity: s.kills[t] ? 1 : 0.45 }}>
                      <F.ZombieToken type={t} size={15} />
                      <span style={{ ...mono(10, 800, s.kills[t] ? 'var(--text)' : 'var(--text-muted)'), fontVariantNumeric: 'tabular-nums' }}>{s.kills[t]}</span>
                    </span>
                  ))}
                </div>
              </div>
              {/* armi usate */}
              <div style={{ display: 'flex', alignItems: 'center', gap: 6, flexWrap: 'wrap' }}>
                <span style={{ ...mono(8.5, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.06em' }}>Armi</span>
                {s.weapons.map(w => <span key={w} style={{ ...mono(9.5, 700, 'var(--text-sec)'), padding: '2px 9px', borderRadius: 'var(--r-pill)', background: eHsl('kb', 0.08), border: `1px solid ${eHsl('kb', 0.25)}`, color: eHsl('kb') }}>{w}</span>)}
              </div>
            </div>
          </div>
        );
      })}
    </div>
  </R.StateScaffold>
);

// ─── Board final tab — snapshot tessere + zombie uccisi per tipo ───────────
const BoardFinalTab = ({ state = 'default' }) => {
  const kills = SUM.zombieKills;
  const max = Math.max(...Object.values(kills));
  const totalKills = Object.values(kills).reduce((a, b) => a + b, 0);
  return (
    <R.StateScaffold state={state} sseWhere="plancia"
      empty={{ icon: '🗺', title: 'Nessuna plancia', body: 'Lo snapshot finale comparirà a partita conclusa.' }}
      error={{ title: 'Plancia non disponibile', body: 'Impossibile caricare lo snapshot finale.' }}
      loading={<div style={{ padding: 14 }}><M.Shimmer h={200} /></div>}>
      <div className="mai-cb-scroll" style={{ padding: 14, display: 'flex', flexDirection: 'column', gap: 16, overflowY: 'auto', flex: 1, minHeight: 0 }}>
        <div>
          <div style={{ ...mono(10, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.07em', marginBottom: 8 }}>Snapshot finale · {Z.mapTiles.scenario}</div>
          <F.MapTilesGrid />
        </div>
        <div>
          <div style={{ display: 'flex', alignItems: 'baseline', gap: 8, marginBottom: 9 }}>
            <span style={{ ...mono(10, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.07em' }}>Zombie eliminati per tipo</span>
            <span style={{ ...mono(9, 800, eHsl('event')) }}>{totalKills} totali</span>
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            {Z.ZORDER.map(t => {
              const z = Z.ZTYPES[t];
              return (
                <div key={t} style={{ display: 'flex', alignItems: 'center', gap: 9 }}>
                  <span style={{ display: 'inline-flex', alignItems: 'center', gap: 5, width: 122, flexShrink: 0 }}>
                    <F.ZombieToken type={t} size={18} />
                    <span style={{ ...disp(11, 800, 'var(--text)'), whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{z.lb}</span>
                  </span>
                  <div style={{ flex: 1, height: 18, borderRadius: 'var(--r-pill)', background: 'var(--bg-sunken)', overflow: 'hidden' }}>
                    <div style={{ width: `${(kills[t] / max) * 100}%`, height: '100%', borderRadius: 'var(--r-pill)', background: z.color, minWidth: 3 }} />
                  </div>
                  <span style={{ ...mono(11, 800, 'var(--text)'), width: 30, textAlign: 'right', fontVariantNumeric: 'tabular-nums' }}>{kills[t]}</span>
                </div>
              );
            })}
          </div>
        </div>
      </div>
    </R.StateScaffold>
  );
};

// ─── Stats tab — turni · XP/turno · kill ratio · armi · danger per round ───
const StatsTab = () => {
  const st = SUM.stats;
  const maxKill = Math.max(...SUM.survivors.map(s => Object.values(s.kills).reduce((a, b) => a + b, 0)));
  const maxUse = Math.max(...st.mostUsedWeapons.map(w => w.uses));
  return (
    <div className="mai-cb-scroll" style={{ padding: 14, display: 'flex', flexDirection: 'column', gap: 16, overflowY: 'auto', flex: 1, minHeight: 0 }}>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(120px, 1fr))', gap: 8 }}>
        {st.facts.map(f => (
          <div key={f.lb} style={{ padding: '9px 11px', borderRadius: 'var(--r-md)', background: 'var(--bg-muted)', border: '1px solid var(--border-light)' }}>
            <div style={{ ...disp(20, 800, 'var(--text)'), fontVariantNumeric: 'tabular-nums' }}>{f.v}</div>
            <div style={{ ...mono(8.5, 700, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.05em' }}>{f.lb}</div>
          </div>
        ))}
        <div style={{ padding: '9px 11px', borderRadius: 'var(--r-md)', background: eHsl('game', 0.08), border: `1px solid ${eHsl('game', 0.28)}` }}>
          <div style={{ ...disp(20, 800, eHsl('game')), fontVariantNumeric: 'tabular-nums' }}>{st.avgXpPerTurn}</div>
          <div style={{ ...mono(8.5, 700, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.05em' }}>XP / turno medio</div>
        </div>
      </div>
      {/* kill ratio per survivor */}
      <div>
        <div style={{ ...mono(9.5, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.07em', marginBottom: 9 }}>Kill ratio per survivor</div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
          {SUM.survivors.map(s => {
            const k = Object.values(s.kills).reduce((a, b) => a + b, 0);
            return (
              <div key={s.id} style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                <F.SurvivorAvatar s={s} size={22} dead={s.dead} />
                <span style={{ ...disp(11.5, 800, 'var(--text)'), width: 52, flexShrink: 0 }}>{s.name}</span>
                <div style={{ flex: 1, height: 16, borderRadius: 'var(--r-pill)', background: 'var(--bg-sunken)', overflow: 'hidden' }}>
                  <div style={{ width: `${(k / maxKill) * 100}%`, height: '100%', borderRadius: 'var(--r-pill)', background: `hsl(${s.hue},58%,52%)`, minWidth: 3 }} />
                </div>
                <span style={{ ...mono(10, 800, 'var(--text)'), width: 28, textAlign: 'right', fontVariantNumeric: 'tabular-nums' }}>{k}</span>
              </div>
            );
          })}
        </div>
      </div>
      {/* armi più usate */}
      <div>
        <div style={{ ...mono(9.5, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.07em', marginBottom: 9 }}>Armi più usate</div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 7 }}>
          {st.mostUsedWeapons.map(w => (
            <div key={w.name} style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <span style={{ ...mono(9.5, 700, 'var(--text-sec)'), width: 96, flexShrink: 0, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{w.name}</span>
              <div style={{ flex: 1, height: 16, borderRadius: 'var(--r-pill)', background: 'var(--bg-sunken)', overflow: 'hidden' }}>
                <div style={{ width: `${(w.uses / maxUse) * 100}%`, height: '100%', borderRadius: 'var(--r-pill)', background: eHsl(w.e) }} />
              </div>
              <span style={{ ...mono(10, 800, 'var(--text)'), width: 28, textAlign: 'right', fontVariantNumeric: 'tabular-nums' }}>{w.uses}</span>
            </div>
          ))}
        </div>
      </div>
      {/* danger level per round */}
      <div>
        <div style={{ ...mono(9.5, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.07em', marginBottom: 9 }}>Danger Level per round</div>
        <div style={{ display: 'flex', alignItems: 'flex-end', gap: 4 }}>
          {SUM.dangerByRound.map(d => {
            const m = Z.levelMeta(d.level);
            const hgt = { blue: 22, yellow: 38, orange: 56, red: 74 }[d.level];
            return (
              <div key={d.round} style={{ flex: 1, display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 3 }}>
                <div title={`Round ${d.round} · ${m.lb}`} style={{ width: '100%', height: hgt, borderRadius: 'var(--r-sm)', background: eHsl(m.e), border: `1px solid ${eHsl(m.e)}` }} />
                <span style={{ ...mono(7.5, 700, 'var(--text-muted)') }}>R{d.round}</span>
              </div>
            );
          })}
        </div>
        <div style={{ display: 'flex', gap: 10, marginTop: 8, flexWrap: 'wrap' }}>
          {Z.LEVELS.map(l => <span key={l.id} style={{ display: 'inline-flex', alignItems: 'center', gap: 4, ...mono(8.5, 700, 'var(--text-sec)') }}><span style={{ width: 9, height: 9, borderRadius: 2, background: eHsl(l.e) }} />{l.lb}</span>)}
        </div>
      </div>
    </div>
  );
};

// ─── Summary body (tabs) ──────────────────────────────────────────────────
const SUM_TABS = [
  { id: 'scoreboard', icon: '🏆', label: 'Scoreboard', entity: 'toolkit', stateful: true,  render: (st) => <ScoreboardPanel state={st} /> },
  { id: 'survivors',  icon: '🧍', label: 'Survivors',  entity: 'session', stateful: false, render: (st) => <SurvivorsTab state={st} /> },
  { id: 'board',      icon: '🗺', label: 'Board',      entity: 'event',   stateful: true,  render: (st) => <BoardFinalTab state={st} /> },
  { id: 'stats',      icon: '📊', label: 'Stats',      entity: 'player',  stateful: false, render: () => <StatsTab /> },
];
const SummaryBody = ({ initial = 'scoreboard', states, embedded }) => {
  const [tab, setTab] = useState(initial);
  const [st, setSt] = useState('default');
  const active = SUM_TABS.find(t => t.id === tab) || SUM_TABS[0];
  return (
    <div style={{ display: 'flex', flexDirection: 'column', flex: 1, minHeight: 0, background: 'var(--bg-card)' }}>
      <div role="tablist" aria-label="Sezioni riepilogo" className="mai-cb-scroll" style={{ display: 'flex', borderBottom: '1px solid var(--border-light)', flexShrink: 0, overflowX: 'auto', background: 'var(--bg-card)' }}>
        {SUM_TABS.map(t => {
          const on = tab === t.id;
          return (
            <button key={t.id} type="button" role="tab" aria-selected={on} onClick={() => { setTab(t.id); setSt('default'); }} style={{ flex: '1 0 auto', padding: '11px 14px', background: on ? eHsl(t.entity, 0.06) : 'transparent', border: 'none', borderBottom: on ? `2px solid ${eHsl(t.entity)}` : '2px solid transparent', color: on ? eHsl(t.entity) : 'var(--text-sec)', ...disp(12.5, 800), cursor: 'pointer', display: 'inline-flex', alignItems: 'center', justifyContent: 'center', gap: 6, whiteSpace: 'nowrap' }}>
              <span aria-hidden="true" style={{ fontSize: 14 }}>{t.icon}</span>{t.label}
            </button>
          );
        })}
      </div>
      {states && active.stateful && <S.StateSwitch value={st} onChange={setSt} />}
      <div role="tabpanel" className="mai-cb-scroll" style={{ flex: 1, minHeight: 0, overflowY: 'auto', display: 'flex', flexDirection: 'column' }}>{active.render(st)}</div>
    </div>
  );
};

// ─── App ──────────────────────────────────────────────────────────────────
const StatesRow = ({ title, sub, entity, render }) => (
  <div style={{ marginBottom: 26 }}>
    <div style={{ display: 'flex', alignItems: 'baseline', gap: 8, marginBottom: 10, flexWrap: 'wrap' }}>
      <span style={{ ...disp(15, 800, eHsl(entity)) }}>{title}</span>
      {sub && <span style={{ ...mono(10, 700, 'var(--text-muted)') }}>{sub}</span>}
    </div>
    <div className="mai-cb-scroll" style={{ display: 'flex', gap: 16, overflowX: 'auto', paddingBottom: 10 }}>
      {P.STATES.map(s => <P.PanelFrame key={s.id} label={s.lb} entity={entity} dark={s.id === 'sse'} w={340}>{render(s.id)}</P.PanelFrame>)}
    </div>
  </div>
);

// scheda DEFEAT alternativa (showcase)
const DEFEAT = { ...SUM, result: 'defeat', survivorsAlive: 0, defeatCause: 'Tutti i survivor a terra',
  objectives: SUM.objectives.map((o, i) => ({ ...o, done: i < 1 })) };

function App() {
  return (
    <div className="stage">
      <ThemeToggle />
      <div className="stage-wrap">
        <div style={{ ...mono(11, 600, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.08em', marginBottom: 8 }}>SP4 · Zombicide: Green Horde · Summary 🏁 · estensione skeleton</div>
        <h1>Session summary — Zombicide: Green Horde</h1>
        <p className="lead">
          Riepilogo post-partita per <code>/sessions/[id]/summary</code>. Hero con esito <strong>VITTORIA / SCONFITTA</strong>
          (missione, survivor vivi, XP totali · oppure causa della sconfitta), poi quattro tab:
          <strong> Scoreboard</strong> (BinaryWin · obiettivi + survivor + XP),
          <strong> Survivors</strong> (skill tree finale, kill per tipo, armi),
          <strong> Board</strong> (snapshot tessere + zombie uccisi per tipo) e
          <strong> Stats</strong> (turni, XP/turno, kill ratio, armi, Danger per round). Dark = primaria.
        </p>

        <div className="section-label">Hero · esito + metriche primarie (vittoria)</div>
        <SummaryHero />

        <div className="section-label">Hero · variante sconfitta (causa esplicita)</div>
        <SummaryHero sum={DEFEAT} />

        <div className="section-label">Interattivo · Desktop 1280 — tab Scoreboard / Survivors / Board / Stats</div>
        <S.DesktopFrame ds={{ ...Z.ds, session: { elapsed: '1h 38min' } }} dark label="Desktop · summary · Zombicide GH" url="meepleai.app/sessions/zom-4/summary" height={600}
          desc="Tab Scoreboard: condizioni BinaryWin + obiettivi check/cross + survivor vivi e XP. Tab Survivors: skill tree finale, kill per tipo, armi usate. Tab Board: snapshot tessere + istogramma zombie uccisi. Tab Stats: kill ratio, armi più usate, Danger Level per round.">
          <S.TopBar ds={{ ...Z.ds, session: { elapsed: '1h 38min' } }} connection="offline" />
          <SummaryBody states />
        </S.DesktopFrame>

        <div className="section-label">Mobile 375 — hero + tab in colonna</div>
        <div className="phones-grid">
          <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 10 }}>
            <div style={{ ...mono(11, 700, 'var(--text-sec)'), textTransform: 'uppercase', letterSpacing: '.08em' }}>01 · Scoreboard</div>
            <div className="phone">
              <div className="phone-sbar" style={{ color: 'var(--text)' }}><span style={{ fontFamily: 'var(--f-mono)' }}>22:51</span><div className="ind"><span aria-hidden="true">●●●●</span><span aria-hidden="true">100%</span></div></div>
              <div style={{ flex: 1, display: 'flex', flexDirection: 'column', background: 'var(--bg)', overflow: 'hidden', minHeight: 0 }}>
                <div style={{ padding: 10 }}><SummaryHero compact /></div>
                <SummaryBody initial="scoreboard" />
              </div>
            </div>
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 10 }} data-theme="dark">
            <div style={{ ...mono(11, 700, 'var(--text-sec)'), textTransform: 'uppercase', letterSpacing: '.08em' }}>02 · Survivors · dark</div>
            <div className="phone" data-theme="dark">
              <div className="phone-sbar" style={{ color: 'var(--text)' }}><span style={{ fontFamily: 'var(--f-mono)' }}>22:52</span><div className="ind"><span aria-hidden="true">●●●●</span><span aria-hidden="true">100%</span></div></div>
              <div style={{ flex: 1, display: 'flex', flexDirection: 'column', background: 'var(--bg)', overflow: 'hidden', minHeight: 0 }}>
                <div style={{ padding: 10 }}><SummaryHero compact /></div>
                <SummaryBody initial="survivors" />
              </div>
            </div>
          </div>
        </div>

        <div className="section-label">Gallery stati · Scoreboard × 5 stati canonici</div>
        <StatesRow title="ScoreboardPanel" sub="default · empty · loading · error · sse-disconnect" entity="toolkit"
          render={(st) => <div style={{ flex: 1, overflowY: 'auto' }}><ScoreboardPanel state={st} /></div>} />
        <StatesRow title="BoardFinalTab" sub="snapshot tessere + zombie uccisi per tipo" entity="event"
          render={(st) => <div style={{ flex: 1, overflowY: 'auto', display: 'flex', flexDirection: 'column' }}><BoardFinalTab state={st} /></div>} />
      </div>

      <style>{`
        @keyframes mai-shimmer-anim { 0% { background-position: -200% 0; } 100% { background-position: 200% 0; } }
        @keyframes mai-pulse-dot-anim { 0%,100% { transform: scale(1); opacity: 1; } 50% { transform: scale(1.4); opacity: .6; } }
        .mai-shimmer { background: linear-gradient(90deg, var(--bg-muted) 0%, var(--bg-hover) 50%, var(--bg-muted) 100%) !important; background-size: 200% 100% !important; animation: mai-shimmer-anim 1.4s linear infinite; }
        .mai-pulse-dot { animation: mai-pulse-dot-anim 1.5s ease-in-out infinite; transform-origin: center; display: inline-block; }
        .mai-cb-scroll::-webkit-scrollbar { height: 7px; width: 7px; }
        .mai-cb-scroll::-webkit-scrollbar-thumb { background: var(--border-strong); border-radius: 999px; }
        .phones-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(380px, 1fr)); gap: var(--s-7); align-items: start; }
        .stage h1 { font-size: var(--fs-3xl); }
        .stage code { background: var(--bg-muted); padding: 1px 6px; border-radius: var(--r-sm); font-size: .85em; font-family: var(--f-mono); }
        @media (prefers-reduced-motion: reduce) { .mai-pulse-dot, .mai-shimmer { animation: none !important; } }
        .phone > div::-webkit-scrollbar { display: none; }
        button:focus-visible, a:focus-visible { outline: 2px solid hsl(var(--c-session)); outline-offset: 2px; }
      `}</style>
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(<App />);
