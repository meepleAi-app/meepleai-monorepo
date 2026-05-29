/* MeepleAI SP4 — /play-records form CORE (shared by -new e -edit)
   Modello: sp7-game-night-create — wizard 3-step mobile + split-form desktop
   (form 8-col + live preview record-card 4-col).
   Entity dominante: session 🎯. game picker = game 🎲, giocatori = player 👤.

   Esporta window.PRForm.render(rootId, { mode: 'new' | 'edit' }).
   - new  : wizard vuoto / in compilazione
   - edit : precompilato da DS.playRecords[0] (Wingspan), titolo "Modifica", azione Elimina.
*/
(function () {
const { useState, useEffect } = React;
const DS = window.DS;

const entityHsl = (type, alpha) => {
  const c = DS.EC[type] || DS.EC.session;
  return alpha !== undefined ? `hsla(${c.h}, ${c.s}%, ${c.l}%, ${alpha})` : `hsl(${c.h}, ${c.s}%, ${c.l}%)`;
};

const LIBRARY = DS.games;
const PLAYERS = DS.players;

// Prefill (edit mode) — Wingspan record
const PREFILL = {
  game: 'g-wingspan', dateLabel: 'Sab 17 maggio 2026', time: '21:00', duration: '3h',
  scores: [
    { playerId:'p-marco', name:'Marco', score: 89 },
    { playerId:'p-anna',  name:'Anna',  score: 76 },
    { playerId:'p-luca',  name:'Luca',  score: 64 },
    { playerId:'p-sara',  name:'Sara',  score: 58 },
  ],
  notes: 'Partita combattuta fino all\'ultimo turno. Marco chiude con un mega-combo wetland.',
};

const winnerIndex = (scores) => {
  let best = -Infinity, idx = -1;
  scores.forEach((s, i) => { const v = Number(s.score); if (!isNaN(v) && v > best) { best = v; idx = i; } });
  return idx;
};

// ─── PRIMITIVES ────────────────────────────────────────
const Avatar = ({ player, size = 32 }) => (
  <span style={{
    width: size, height: size, borderRadius:'50%',
    background:`hsl(${player.color}, 60%, 55%)`, color:'#fff',
    fontFamily:'var(--f-display)', fontWeight: 800,
    fontSize: size <= 24 ? 9 : size <= 32 ? 11 : 13,
    display:'inline-flex', alignItems:'center', justifyContent:'center',
    border:'2px solid var(--bg-card)', flexShrink: 0,
  }}>{player.initials}</span>
);

const Label = ({ children }) => (
  <label style={{ fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 800, color:'var(--text-muted)', textTransform:'uppercase', letterSpacing:'.08em' }}>{children}</label>
);

const StepHeader = ({ title, sub }) => (
  <div style={{ display:'flex', flexDirection:'column', gap: 4, marginBottom: 4 }}>
    <h2 style={{ fontFamily:'var(--f-display)', fontSize: 22, fontWeight: 800, color:'var(--text)', margin: 0, letterSpacing:'-.01em' }}>{title}</h2>
    <p style={{ fontSize: 13, color:'var(--text-sec)', margin: 0, lineHeight: 1.5 }}>{sub}</p>
  </div>
);

const StepIndicator = ({ current, onBack, mobile }) => {
  const steps = [
    { n: 1, label: 'Gioco', icon: '🎲' },
    { n: 2, label: 'Quando', icon: '📅' },
    { n: 3, label: 'Punteggi', icon: '🏆' },
  ];
  const accent = entityHsl('session');
  return (
    <div style={{
      display:'flex', alignItems:'center', gap: 12,
      padding: mobile ? '12px 14px' : '16px 24px',
      background:'var(--bg)', borderBottom:'1px solid var(--border)',
      position:'sticky', top: 0, zIndex: 10,
    }}>
      {onBack && (
        <button type="button" aria-label="Indietro" style={{
          width: 34, height: 34, borderRadius:'var(--r-md)', background:'var(--bg-muted)',
          border:'1px solid var(--border)', color:'var(--text)', fontSize: 16, cursor:'pointer', flexShrink: 0,
        }}>←</button>
      )}
      <div style={{ display:'flex', alignItems:'center', gap: mobile ? 4 : 8, flex: 1, minWidth: 0 }}>
        {steps.map((s, i) => {
          const done = s.n < current, active = s.n === current;
          return (
            <React.Fragment key={s.n}>
              <div style={{ display:'flex', alignItems:'center', gap: 6, minWidth: 0, flexShrink: active ? 0 : 1 }}>
                <span style={{
                  width: 24, height: 24, borderRadius:'50%',
                  background: done || active ? accent : 'transparent',
                  color: done || active ? '#fff' : 'var(--text-muted)',
                  border: `2px solid ${done || active ? accent : 'var(--border)'}`,
                  display:'flex', alignItems:'center', justifyContent:'center',
                  fontFamily:'var(--f-display)', fontWeight: 800, fontSize: 11, flexShrink: 0,
                }}>{done ? '✓' : s.n}</span>
                {(!mobile || active) && (
                  <span style={{ fontFamily:'var(--f-display)', fontWeight: active ? 800 : 600, fontSize: 12, color: active ? 'var(--text)' : 'var(--text-muted)', whiteSpace:'nowrap' }}>{s.label}</span>
                )}
              </div>
              {i < steps.length - 1 && <span style={{ flex: 1, height: 2, minWidth: mobile ? 8 : 16, background: s.n < current ? accent : 'var(--border)', borderRadius: 1 }}/>}
            </React.Fragment>
          );
        })}
      </div>
      {!mobile && <span style={{ fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)', textTransform:'uppercase', letterSpacing:'.08em', fontWeight: 800, flexShrink: 0 }}>Step {current}/3</span>}
    </div>
  );
};

// ─── STEP 1 — GIOCO ────────────────────────────────────
const GamePickCard = ({ game, selected }) => (
  <div style={{
    position:'relative', borderRadius:'var(--r-lg)', background:'var(--bg-card)',
    border: selected ? `1.5px solid ${entityHsl('game', 0.55)}` : '1px solid var(--border)',
    boxShadow: selected ? `0 0 0 3px ${entityHsl('game', 0.1)}` : 'none',
    overflow:'hidden', cursor:'pointer',
  }}>
    {selected && (
      <span style={{
        position:'absolute', top: 8, right: 8, zIndex: 2, width: 22, height: 22, borderRadius:'50%',
        background: entityHsl('game'), color:'#fff', fontSize: 12, fontWeight: 800,
        display:'flex', alignItems:'center', justifyContent:'center', boxShadow:'0 2px 6px rgba(0,0,0,.25)',
      }}>✓</span>
    )}
    <div style={{ height: 80, background: game.cover, display:'flex', alignItems:'center', justifyContent:'center', fontSize: 34 }} aria-hidden="true">
      <span style={{ filter:'drop-shadow(0 4px 12px rgba(0,0,0,.4))' }}>{game.coverEmoji}</span>
    </div>
    <div style={{ padding:'9px 11px 11px', display:'flex', flexDirection:'column', gap: 4 }}>
      <div style={{ fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800, color:'var(--text)', whiteSpace:'nowrap', overflow:'hidden', textOverflow:'ellipsis' }}>{game.title}</div>
      <div style={{ display:'flex', gap: 4, flexWrap:'wrap' }}>
        <span style={{ padding:'2px 6px', borderRadius:'var(--r-sm)', background: entityHsl('game', 0.12), color: entityHsl('game'), fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800, border:`1px solid ${entityHsl('game', 0.22)}` }}>👥 {game.players}</span>
        <span style={{ padding:'2px 6px', borderRadius:'var(--r-sm)', background: entityHsl('game', 0.12), color: entityHsl('game'), fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800, border:`1px solid ${entityHsl('game', 0.22)}` }}>⏱ {game.duration}</span>
      </div>
    </div>
  </div>
);

const Step1Gioco = ({ selectedGame, empty, mobile }) => {
  const sorted = empty ? LIBRARY : [...LIBRARY].sort((a, b) => (a.id === selectedGame ? -1 : b.id === selectedGame ? 1 : 0));
  return (
    <div style={{ display:'flex', flexDirection:'column', gap: 16, padding: mobile ? '16px 14px 24px' : '20px 24px 24px' }}>
      <StepHeader title="Quale gioco avete giocato?" sub="Scegli dalla tua libreria. Il punteggio e i giocatori si adattano al gioco."/>
      <div style={{ position:'relative' }}>
        <span aria-hidden="true" style={{ position:'absolute', left: 12, top:'50%', transform:'translateY(-50%)', color:'var(--text-muted)', fontSize: 14 }}>⌕</span>
        <input type="text" placeholder="Cerca nella libreria…" readOnly style={{
          width:'100%', padding:'10px 12px 10px 34px', borderRadius:'var(--r-md)',
          border:'1px solid var(--border)', background:'var(--bg-card)', color:'var(--text)', fontFamily:'var(--f-body)', fontSize: 13,
        }}/>
      </div>
      {empty && (
        <div role="status" style={{
          padding:'10px 14px', borderRadius:'var(--r-md)', background: entityHsl('game', 0.06),
          border:`1px dashed ${entityHsl('game', 0.3)}`, fontFamily:'var(--f-mono)', fontSize: 11, fontWeight: 700, color: entityHsl('game'),
        }}>↑ Seleziona un gioco per continuare</div>
      )}
      <div style={{ display:'grid', gridTemplateColumns: mobile ? 'repeat(2, 1fr)' : 'repeat(3, 1fr)', gap: 10 }}>
        {sorted.map(g => <GamePickCard key={g.id} game={g} selected={!empty && g.id === selectedGame}/>)}
      </div>
    </div>
  );
};

// ─── STEP 2 — QUANDO ───────────────────────────────────
const MiniCalendar = ({ selectedDay = 17 }) => {
  const cells = [];
  for (let d = 27; d <= 30; d++) cells.push({ day: d, otherMonth: true });
  for (let d = 1; d <= 31; d++) cells.push({ day: d, otherMonth: false });
  const pad = 42 - cells.length;
  for (let d = 1; d <= pad; d++) cells.push({ day: d, otherMonth: true });
  const labels = ['L','M','M','G','V','S','D'];
  const accent = entityHsl('session');
  return (
    <div style={{ background:'var(--bg-card)', border:'1px solid var(--border)', borderRadius:'var(--r-lg)', padding: 14, display:'flex', flexDirection:'column', gap: 10 }}>
      <div style={{ display:'flex', alignItems:'center', justifyContent:'space-between' }}>
        <button type="button" aria-label="Mese precedente" style={{ width: 28, height: 28, borderRadius:'var(--r-sm)', background:'var(--bg-muted)', border:'1px solid var(--border)', color:'var(--text)', fontSize: 13, cursor:'pointer' }}>‹</button>
        <h3 style={{ margin: 0, fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 800, color:'var(--text)' }}>Maggio 2026</h3>
        <button type="button" aria-label="Mese successivo" style={{ width: 28, height: 28, borderRadius:'var(--r-sm)', background:'var(--bg-muted)', border:'1px solid var(--border)', color:'var(--text)', fontSize: 13, cursor:'pointer' }}>›</button>
      </div>
      <div style={{ display:'grid', gridTemplateColumns:'repeat(7, 1fr)', gap: 2 }}>
        {labels.map((d, i) => <div key={i} style={{ padding:'4px 0', textAlign:'center', fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 800, color:'var(--text-muted)', textTransform:'uppercase' }}>{d}</div>)}
        {cells.map((cell, i) => {
          const isSel = !cell.otherMonth && cell.day === selectedDay;
          return (
            <button key={i} type="button" disabled={cell.otherMonth} style={{
              aspectRatio:'1/1', minHeight: 28, padding: 0, border:'none', cursor: cell.otherMonth ? 'default' : 'pointer',
              borderRadius:'var(--r-sm)', background: isSel ? accent : 'transparent',
              color: isSel ? '#fff' : cell.otherMonth ? 'var(--text-muted)' : 'var(--text)',
              fontFamily:'var(--f-mono)', fontSize: 11, fontWeight: isSel ? 800 : 600, opacity: cell.otherMonth ? 0.35 : 1,
            }}>{cell.day}</button>
          );
        })}
      </div>
    </div>
  );
};

const DurationToggle = ({ selected = '3h' }) => {
  const options = [{ id:'1h', label:'1h' }, { id:'2h', label:'2h' }, { id:'3h', label:'3h' }, { id:'3h+', label:'3h+' }];
  return (
    <div style={{ display:'grid', gridTemplateColumns:'repeat(4, 1fr)', gap: 8 }}>
      {options.map(o => {
        const active = o.id === selected;
        return (
          <button key={o.id} type="button" aria-pressed={active} style={{
            padding:'10px 6px', borderRadius:'var(--r-md)',
            background: active ? entityHsl('session', 0.12) : 'var(--bg-card)',
            color: active ? entityHsl('session') : 'var(--text-sec)',
            border: active ? `1.5px solid ${entityHsl('session', 0.5)}` : '1px solid var(--border)',
            boxShadow: active ? `0 0 0 3px ${entityHsl('session', 0.08)}` : 'none',
            cursor:'pointer', fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 800,
          }}>{o.label}</button>
        );
      })}
    </div>
  );
};

const Step2Quando = ({ dateLabel = 'Sabato 17 maggio 2026', time = '21:00', duration = '3h', mobile }) => (
  <div style={{ display:'flex', flexDirection:'column', gap: 18, padding: mobile ? '16px 14px 24px' : '20px 24px 24px' }}>
    <StepHeader title="Quando l'avete giocata?" sub="Data, ora di inizio e durata. La durata è opzionale."/>
    <div style={{ display:'flex', flexDirection:'column', gap: 8 }}>
      <Label>Data</Label>
      <div style={{
        display:'flex', alignItems:'center', gap: 10, padding:'12px 14px', background:'var(--bg-card)',
        border:`1px solid ${entityHsl('session', 0.35)}`, borderRadius:'var(--r-lg)', boxShadow:`0 0 0 3px ${entityHsl('session', 0.1)}`,
      }}>
        <span aria-hidden="true" style={{ fontSize: 18, color: entityHsl('session') }}>📅</span>
        <span style={{ flex: 1, fontFamily:'var(--f-display)', fontSize: 15, fontWeight: 800, color:'var(--text)' }}>{dateLabel}</span>
        <button type="button" style={{ padding:'6px 10px', borderRadius:'var(--r-sm)', background:'transparent', border:'1px solid var(--border)', color:'var(--text-sec)', fontFamily:'var(--f-display)', fontSize: 11, fontWeight: 700, cursor:'pointer' }}>Cambia</button>
      </div>
      <MiniCalendar selectedDay={17}/>
    </div>
    <div style={{ display:'flex', flexDirection:'column', gap: 8 }}>
      <Label>Ora di inizio</Label>
      <div style={{ display:'flex', alignItems:'center', gap: 8, padding:'12px 14px', background:'var(--bg-card)', border:`1px solid ${entityHsl('session', 0.35)}`, borderRadius:'var(--r-lg)' }}>
        <span aria-hidden="true" style={{ fontSize: 16, color: entityHsl('session') }}>🕘</span>
        <span style={{ fontFamily:'var(--f-mono)', fontSize: 16, fontWeight: 800, color:'var(--text)', flex: 1, fontVariantNumeric:'tabular-nums' }}>{time}</span>
      </div>
    </div>
    <div style={{ display:'flex', flexDirection:'column', gap: 8 }}>
      <Label>Durata (opzionale)</Label>
      <DurationToggle selected={duration}/>
    </div>
  </div>
);

// ─── STEP 3 — GIOCATORI & PUNTEGGI ─────────────────────
const ScoreRow = ({ player, score, isWinner }) => (
  <div style={{
    display:'flex', alignItems:'center', gap: 10, padding:'10px 12px',
    background: isWinner ? entityHsl('session', 0.06) : 'var(--bg-card)',
    border: isWinner ? `1.5px solid ${entityHsl('session', 0.4)}` : '1px solid var(--border)',
    borderRadius:'var(--r-lg)',
  }}>
    <Avatar player={player} size={34}/>
    <div style={{ flex: 1, minWidth: 0 }}>
      <div style={{ fontFamily:'var(--f-display)', fontSize: 13.5, fontWeight: 800, color:'var(--text)', display:'flex', alignItems:'center', gap: 6 }}>
        {player.title.split(' ')[0]}
        {isWinner && (
          <span style={{ display:'inline-flex', alignItems:'center', gap: 3, padding:'1px 7px', borderRadius:'var(--r-pill)', background: entityHsl('session', 0.14), color: entityHsl('session'), fontFamily:'var(--f-mono)', fontSize: 8.5, fontWeight: 800, textTransform:'uppercase', letterSpacing:'.06em' }}>🏆 Vincitore</span>
        )}
      </div>
      <div style={{ fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)', fontWeight: 600 }}>{player.title}</div>
    </div>
    <div style={{
      display:'flex', alignItems:'center', gap: 4, padding:'4px 6px', borderRadius:'var(--r-md)',
      background:'var(--bg-muted)', border: isWinner ? `1px solid ${entityHsl('session', 0.3)}` : '1px solid var(--border)',
    }}>
      <button type="button" aria-label="-1" style={{ width: 24, height: 24, borderRadius:'var(--r-sm)', background:'var(--bg-card)', border:'1px solid var(--border)', color:'var(--text)', fontSize: 13, fontWeight: 800, cursor:'pointer' }}>−</button>
      <span style={{ fontFamily:'var(--f-mono)', fontSize: 18, fontWeight: 800, color: isWinner ? entityHsl('session') : 'var(--text)', minWidth: 38, textAlign:'center', fontVariantNumeric:'tabular-nums' }}>{score === null || score === undefined ? '—' : score}</span>
      <button type="button" aria-label="+1" style={{ width: 24, height: 24, borderRadius:'var(--r-sm)', background: entityHsl('session', 0.12), border:`1px solid ${entityHsl('session', 0.3)}`, color: entityHsl('session'), fontSize: 13, fontWeight: 800, cursor:'pointer' }}>+</button>
    </div>
  </div>
);

const Step3Punteggi = ({ scores, notes, withNotes, mobile }) => {
  const wIdx = winnerIndex(scores);
  return (
    <div style={{ display:'flex', flexDirection:'column', gap: 16, padding: mobile ? '16px 14px 24px' : '20px 24px 24px' }}>
      <StepHeader title="Chi ha giocato e con che punteggio?" sub="Inserisci i punteggi: il vincitore è calcolato automaticamente dal punteggio più alto."/>
      <div style={{ display:'flex', flexDirection:'column', gap: 8 }}>
        <Label>Giocatori e punteggi ({scores.length})</Label>
        <div style={{ display:'flex', flexDirection:'column', gap: 8 }}>
          {scores.map((s, i) => {
            const p = DS.byId[s.playerId] || { title: s.name, initials: s.name.slice(0,2), color: 240 };
            return <ScoreRow key={s.playerId || i} player={p} score={s.score} isWinner={i === wIdx && s.score !== null}/>;
          })}
        </div>
        <button type="button" style={{
          padding:'10px 12px', borderRadius:'var(--r-md)', background:'transparent',
          border:`1px dashed ${entityHsl('player', 0.4)}`, color: entityHsl('player'),
          fontFamily:'var(--f-display)', fontSize: 12.5, fontWeight: 800, cursor:'pointer',
          display:'inline-flex', alignItems:'center', justifyContent:'center', gap: 6,
        }}><span aria-hidden="true">+</span>Aggiungi giocatore</button>
      </div>
      <div style={{ display:'flex', flexDirection:'column', gap: 8 }}>
        <Label>Note (opzionale)</Label>
        <textarea readOnly placeholder="Aggiungi un commento sulla partita…" value={withNotes ? notes : ''} style={{
          width:'100%', minHeight: 76, padding:'10px 12px', borderRadius:'var(--r-lg)',
          border:'1px solid var(--border)', background:'var(--bg-card)', color:'var(--text)',
          fontFamily:'var(--f-body)', fontSize: 13, lineHeight: 1.5, resize:'vertical',
        }}/>
      </div>
    </div>
  );
};

// ─── LIVE PREVIEW (record card) ────────────────────────
const RecordLivePreview = ({ data, mode }) => {
  const game = DS.byId[data.game];
  const wIdx = winnerIndex(data.scores);
  return (
    <aside style={{ position:'sticky', top: 24, display:'flex', flexDirection:'column', gap: 14 }}>
      <div style={{ display:'flex', alignItems:'center', gap: 6, fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 800, color:'var(--text-muted)', textTransform:'uppercase', letterSpacing:'.08em' }}>
        <span aria-hidden="true" style={{ width: 6, height: 6, borderRadius:'50%', background: entityHsl('session'), boxShadow:`0 0 0 3px ${entityHsl('session', 0.25)}`, animation:'sp7Pulse 1.6s var(--ease-out) infinite' }}/>
        Anteprima live · Record partita
      </div>
      <article style={{ background:'var(--bg-card)', border:'1px solid var(--border)', borderRadius:'var(--r-xl)', overflow:'hidden', boxShadow:'var(--shadow-md)' }}>
        <div style={{ padding:'18px', background: game ? game.cover : entityHsl('session', 0.12), color:'#fff', position:'relative' }}>
          <div style={{ display:'flex', alignItems:'center', gap: 6, marginBottom: 10 }}>
            <span style={{ padding:'3px 8px', borderRadius:'var(--r-pill)', background:'rgba(255,255,255,.25)', backdropFilter:'blur(8px)', fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800, textTransform:'uppercase', letterSpacing:'.08em' }}>🎯 Partita</span>
            <span style={{ padding:'3px 8px', borderRadius:'var(--r-pill)', background:'rgba(255,255,255,.25)', backdropFilter:'blur(8px)', fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800, textTransform:'uppercase', letterSpacing:'.08em' }}>{mode === 'edit' ? 'Modifica' : 'Bozza'}</span>
          </div>
          <h3 style={{ margin:'0 0 6px', fontFamily:'var(--f-display)', fontSize: 20, fontWeight: 800, lineHeight: 1.1, textShadow:'0 2px 8px rgba(0,0,0,.25)' }}>{game ? game.title : 'Seleziona un gioco'}</h3>
          <div style={{ fontFamily:'var(--f-mono)', fontSize: 11, fontWeight: 700, display:'flex', flexWrap:'wrap', gap:'2px 8px' }}>
            <span>{data.dateLabel}</span><span aria-hidden="true">·</span><span>{data.time}</span><span aria-hidden="true">·</span><span>{data.duration}</span>
          </div>
        </div>
        <div style={{ padding:'14px 18px 18px', display:'flex', flexDirection:'column', gap: 12 }}>
          <div>
            <div style={{ fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800, color:'var(--text-muted)', textTransform:'uppercase', letterSpacing:'.08em', marginBottom: 8 }}>Classifica · {data.scores.length} giocatori</div>
            <div style={{ display:'flex', flexDirection:'column', gap: 5 }}>
              {[...data.scores].map((s, i) => ({ ...s, _orig: i }))
                .sort((a, b) => (Number(b.score) || -Infinity) - (Number(a.score) || -Infinity))
                .map((s, rank) => {
                  const p = DS.byId[s.playerId] || { title: s.name, initials: s.name.slice(0,2), color: 240 };
                  const isW = s._orig === wIdx && s.score !== null;
                  return (
                    <div key={s.playerId || s._orig} style={{
                      display:'flex', alignItems:'center', gap: 8, padding:'6px 8px', borderRadius:'var(--r-sm)',
                      background: isW ? entityHsl('session', 0.08) : 'var(--bg-muted)',
                      border: isW ? `1px solid ${entityHsl('session', 0.25)}` : '1px solid transparent',
                    }}>
                      <span style={{ fontFamily:'var(--f-mono)', fontSize: 11, fontWeight: 800, color: isW ? entityHsl('session') : 'var(--text-muted)', width: 16 }}>{rank + 1}</span>
                      <Avatar player={p} size={24}/>
                      <span style={{ flex: 1, fontFamily:'var(--f-display)', fontSize: 12.5, fontWeight: isW ? 800 : 700, color: isW ? entityHsl('session') : 'var(--text)' }}>{p.title.split(' ')[0]}{isW && ' 🏆'}</span>
                      <span style={{ fontFamily:'var(--f-mono)', fontSize: 14, fontWeight: 800, color: isW ? entityHsl('session') : 'var(--text-sec)', fontVariantNumeric:'tabular-nums' }}>{s.score === null ? '—' : s.score}</span>
                    </div>
                  );
                })}
            </div>
          </div>
        </div>
      </article>
      <div style={{ padding:'10px 12px', borderRadius:'var(--r-md)', background: entityHsl('session', 0.06), border:`1px dashed ${entityHsl('session', 0.3)}`, display:'flex', gap: 8 }}>
        <span aria-hidden="true" style={{ fontSize: 14, flexShrink: 0 }}>💡</span>
        <div style={{ fontSize: 11.5, color:'var(--text-sec)', lineHeight: 1.5 }}>Il vincitore è calcolato dal punteggio più alto. Si aggiorna in tempo reale mentre compili.</div>
      </div>
    </aside>
  );
};

// ─── ACTION BAR ────────────────────────────────────────
const ActionBar = ({ step, mobile, primaryLabel, mode }) => (
  <div style={{ display:'flex', gap: 10, alignItems:'center', padding: mobile ? '12px 14px' : '14px 24px', background:'var(--bg)', borderTop:'1px solid var(--border)', position:'sticky', bottom: 0, zIndex: 8 }}>
    {mode === 'edit' && step === 1 && (
      <button type="button" style={{ padding:'12px 16px', borderRadius:'var(--r-md)', background:'transparent', border:'1px solid hsl(var(--c-danger) / .4)', color:'hsl(var(--c-danger))', fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800, cursor:'pointer' }}>🗑 Elimina</button>
    )}
    {step > 1 && (
      <button type="button" style={{ padding:'12px 16px', borderRadius:'var(--r-md)', background:'transparent', border:'1px solid var(--border-strong)', color:'var(--text)', fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800, cursor:'pointer' }}>← Indietro</button>
    )}
    <div style={{ flex: 1 }}/>
    <button type="button" style={{
      padding:'12px 22px', borderRadius:'var(--r-md)',
      background:`linear-gradient(135deg, ${entityHsl('session')}, hsl(${DS.EC.session.h - 14}, ${DS.EC.session.s}%, ${DS.EC.session.l - 8}%))`,
      color:'#fff', border:'none', fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800, cursor:'pointer',
      boxShadow:`0 4px 14px ${entityHsl('session', 0.35)}`,
    }}>{primaryLabel}</button>
  </div>
);

// ─── MOBILE / DESKTOP SHELLS ───────────────────────────
const PhoneSbar = () => (
  <div className="phone-sbar" style={{ color:'var(--text)' }}>
    <span>21:42</span>
    <div className="ind"><span aria-hidden="true">●●●●</span><span aria-hidden="true">100%</span></div>
  </div>
);

const PhoneTopNav = ({ title }) => (
  <div style={{ display:'flex', alignItems:'center', gap: 9, padding:'10px 14px', background:'var(--glass-bg)', backdropFilter:'blur(12px)', borderBottom:'1px solid var(--border-light)' }}>
    <button aria-label="Chiudi" style={{ width: 32, height: 32, borderRadius:'var(--r-md)', background:'var(--bg-card)', border:'1px solid var(--border)', color:'var(--text)', fontSize: 16, cursor:'pointer' }}>×</button>
    <div style={{ flex: 1, fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 800, color:'var(--text)', textAlign:'center' }}>{title}</div>
    <button aria-label="Salva bozza" style={{ minWidth: 32, padding:'0 10px', height: 32, borderRadius:'var(--r-md)', background:'var(--bg-card)', border:'1px solid var(--border)', color:'var(--text-sec)', fontSize: 11, fontWeight: 700, fontFamily:'var(--f-display)', cursor:'pointer' }}>Bozza</button>
  </div>
);

const renderStep = (step, data, opts, mobile) => {
  if (step === 1) return <Step1Gioco selectedGame={data.game} empty={opts.emptyGame} mobile={mobile}/>;
  if (step === 2) return <Step2Quando dateLabel={data.dateLabel} time={data.time} duration={data.duration} mobile={mobile}/>;
  return <Step3Punteggi scores={data.scores} notes={data.notes} withNotes={opts.withNotes} mobile={mobile}/>;
};

// loading / error bodies
const LoadingBody = ({ mobile }) => (
  <div style={{ padding: mobile ? '16px 14px' : '20px 24px', display:'flex', flexDirection:'column', gap: 12 }}>
    <div className="mai-shimmer" style={{ height: 34, width:'60%', borderRadius:'var(--r-sm)', background:'var(--bg-muted)' }}/>
    <div className="mai-shimmer" style={{ height: 14, width:'80%', borderRadius: 4, background:'var(--bg-muted)' }}/>
    <div style={{ display:'grid', gridTemplateColumns:'repeat(2, 1fr)', gap: 10, marginTop: 6 }}>
      {[0,1,2,3].map(i => <div key={i} className="mai-shimmer" style={{ height: 130, borderRadius:'var(--r-lg)', background:'var(--bg-muted)' }}/>)}
    </div>
  </div>
);

const ErrorBody = ({ mobile }) => (
  <div style={{ padding:'40px 24px', textAlign:'center', margin: mobile ? 14 : 24, background:'hsl(var(--c-danger) / .06)', border:'1px solid hsl(var(--c-danger) / .25)', borderRadius:'var(--r-xl)', display:'flex', flexDirection:'column', alignItems:'center' }}>
    <div style={{ width: 56, height: 56, borderRadius:'50%', background:'hsl(var(--c-danger) / .15)', color:'hsl(var(--c-danger))', display:'flex', alignItems:'center', justifyContent:'center', fontSize: 26, marginBottom: 12 }} aria-hidden="true">⚠</div>
    <h3 style={{ fontFamily:'var(--f-display)', fontSize: 16, fontWeight: 800, color:'var(--text)', margin:'0 0 4px' }}>Salvataggio non riuscito</h3>
    <p style={{ fontSize: 12.5, color:'var(--text-muted)', margin:'0 0 14px', maxWidth: 300 }}>Impossibile salvare la partita. La bozza è stata conservata localmente.</p>
    <button type="button" style={{ padding:'8px 14px', borderRadius:'var(--r-md)', background:'hsl(var(--c-danger))', color:'#fff', border:'none', fontFamily:'var(--f-display)', fontSize: 12.5, fontWeight: 800, cursor:'pointer' }}>↻ Riprova salvataggio</button>
  </div>
);

const MobileScreen = ({ anchor, label, step, data, opts, mode, primaryLabel, gherkin }) => (
  <section className="phone-shell" data-screen-label={`PR-form · ${label}`}>
    <div className="state-caption">
      <span className="state-id">{anchor.replace('state-', '#')}</span>
      <span className="state-label">📱 {label}</span>
      {gherkin && <span className="gherkin">{gherkin}</span>}
    </div>
    <div className="phone">
      <PhoneSbar/>
      <div style={{ flex: 1, overflowY:'auto', display:'flex', flexDirection:'column', background:'var(--bg)' }}>
        <PhoneTopNav title={mode === 'edit' ? 'Modifica partita' : 'Registra partita'}/>
        {opts.loading ? <LoadingBody mobile/> : opts.error ? <ErrorBody mobile/> : (
          <>
            <StepIndicator current={step} onBack mobile/>
            <div style={{ flex: 1 }}>{renderStep(step, data, opts, true)}</div>
            <ActionBar step={step} mobile primaryLabel={primaryLabel} mode={mode}/>
          </>
        )}
      </div>
    </div>
  </section>
);

const StepFlowOverview = ({ anchor, label, data, mode }) => (
  <section data-screen-label={`PR-form · ${label}`}>
    <div className="state-caption">
      <span className="state-id">{anchor.replace('state-', '#')}</span>
      <span className="state-label">📱 {label}</span>
    </div>
    <div className="step-flow-row">
      {[1,2,3].map(step => (
        <div key={step} className="phone phone-mini">
          <PhoneSbar/>
          <div style={{ flex: 1, overflowY:'auto', display:'flex', flexDirection:'column', background:'var(--bg)' }}>
            <PhoneTopNav title={`Step ${step}/3`}/>
            <StepIndicator current={step} onBack mobile/>
            <div style={{ flex: 1 }}>{renderStep(step, data, { withNotes: true }, true)}</div>
          </div>
        </div>
      ))}
    </div>
  </section>
);

const DesktopNav = ({ mode }) => (
  <div style={{ display:'flex', alignItems:'center', gap: 14, padding:'10px 24px', background:'var(--glass-bg)', backdropFilter:'blur(12px)', borderBottom:'1px solid var(--border)' }}>
    <div style={{ display:'flex', alignItems:'center', gap: 9 }}>
      <div style={{ width: 26, height: 26, borderRadius: 7, background:`linear-gradient(135deg, ${entityHsl('game')}, ${entityHsl('session')})`, color:'#fff', display:'flex', alignItems:'center', justifyContent:'center', fontWeight: 800, fontSize: 13, fontFamily:'var(--f-display)' }}>M</div>
      <span style={{ fontFamily:'var(--f-display)', fontWeight: 800, fontSize: 14 }}>MeepleAI</span>
    </div>
    <div style={{ flex: 1, fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)', marginLeft: 22 }}>
      <span>Play records</span><span aria-hidden="true"> / </span><strong style={{ color:'var(--text-sec)' }}>{mode === 'edit' ? 'Modifica partita' : 'Nuova partita'}</strong>
    </div>
  </div>
);

const DesktopSplit = ({ anchor, label, step, data, opts, mode, primaryLabel }) => (
  <section data-screen-label={`PR-form · ${label}`}>
    <div className="state-caption">
      <span className="state-id">{anchor.replace('state-', '#')}</span>
      <span className="state-label">🖥️ {label}</span>
    </div>
    <div className="desktop-frame">
      <div className="desktop-bar">
        <span className="traffic"/><span className="traffic"/><span className="traffic"/>
        <span className="url">meepleai.app/play-records/{mode === 'edit' ? 'pr1/edit' : 'new'}</span>
      </div>
      <div style={{ background:'var(--bg)' }}>
        <DesktopNav mode={mode}/>
        <StepIndicator current={step} mobile={false}/>
        <div style={{ display:'grid', gridTemplateColumns:'minmax(0, 8fr) minmax(0, 4fr)', gap: 24, padding:'8px 24px 0', maxWidth: 1280, margin:'0 auto' }}>
          <div style={{ display:'flex', flexDirection:'column', paddingBottom: 24 }}>{renderStep(step, data, opts, false)}</div>
          <div style={{ paddingTop: 20, paddingBottom: 24 }}><RecordLivePreview data={data} mode={mode}/></div>
        </div>
        <ActionBar step={step} mobile={false} primaryLabel={primaryLabel} mode={mode}/>
      </div>
    </div>
  </section>
);

// ─── APP ───────────────────────────────────────────────
function App({ mode }) {
  const [theme, setTheme] = useState('light');
  useEffect(() => { document.documentElement.setAttribute('data-theme', theme); }, [theme]);

  const isEdit = mode === 'edit';
  const filled = {
    game: PREFILL.game, dateLabel: PREFILL.dateLabel, time: PREFILL.time, duration: PREFILL.duration,
    scores: PREFILL.scores, notes: PREFILL.notes,
  };
  const blank = {
    game: 'g-wingspan', dateLabel: 'Oggi · 28 mag 2026', time: '21:00', duration: '2h',
    scores: [
      { playerId:'p-marco', name:'Marco', score: 42 },
      { playerId:'p-anna',  name:'Anna',  score: 38 },
      { playerId:'p-luca',  name:'Luca',  score: 29 },
    ],
    notes: 'Bella rimonta di Marco nell\'ultimo round.',
  };
  const data = isEdit ? filled : blank;
  const primary = (step) => step === 3 ? (isEdit ? '✓ Salva modifiche' : '✓ Salva partita') : 'Avanti →';

  const accent = entityHsl('session');
  const primaryLabel3 = primary(3);

  const MOBILE = isEdit ? [
    { anchor:'state-01', step:1, opts:{}, label:'01 · Step 1 — Gioco (precompilato Wingspan)', gherkin:'EDIT' },
    { anchor:'state-02', step:2, opts:{}, label:'02 · Step 2 — Quando (precompilato)', gherkin:'EDIT' },
    { anchor:'state-03', step:3, opts:{ withNotes:true }, label:'03 · Step 3 — Punteggi + note (precompilati)', gherkin:'EDIT' },
    { anchor:'state-04', step:3, opts:{ withNotes:true, error:true }, label:'04 · Error — salvataggio fallito', gherkin:'EDIT.err' },
    { anchor:'state-05', step:1, opts:{ loading:true }, label:'05 · Loading — caricamento partita', gherkin:'EDIT.load' },
  ] : [
    { anchor:'state-01', step:1, opts:{ emptyGame:true }, label:'01 · Step 1 — Gioco · empty (nessuna selezione)', gherkin:'US.1' },
    { anchor:'state-02', step:1, opts:{}, label:'02 · Step 1 — Gioco selezionato', gherkin:'US.1' },
    { anchor:'state-03', step:2, opts:{}, label:'03 · Step 2 — Quando + durata', gherkin:'US.2' },
    { anchor:'state-04', step:3, opts:{}, label:'04 · Step 3 — Punteggi (vincitore auto)', gherkin:'US.3' },
    { anchor:'state-05', step:3, opts:{ withNotes:true }, label:'05 · Step 3 — Con note opzionali', gherkin:'US.3' },
    { anchor:'state-06', step:1, opts:{ loading:true }, label:'06 · Loading — libreria', gherkin:'US.load' },
    { anchor:'state-07', step:3, opts:{ error:true }, label:'07 · Error — salvataggio fallito', gherkin:'US.err' },
  ];

  return (
    <div style={{ minHeight:'100vh', background:'var(--bg)', color:'var(--text)', padding:'24px 24px 80px' }}>
      <header style={{ maxWidth: 1440, margin:'0 auto 32px', display:'flex', alignItems:'flex-start', gap: 16, flexWrap:'wrap' }}>
        <div style={{ display:'flex', alignItems:'center', gap: 12, flex: 1, minWidth: 0 }}>
          <div style={{ width: 40, height: 40, borderRadius: 10, background:`linear-gradient(135deg, ${entityHsl('game')}, ${accent})`, color:'#fff', display:'flex', alignItems:'center', justifyContent:'center', fontWeight: 800, fontFamily:'var(--f-display)', fontSize: 18 }}>{isEdit ? '✎' : '+'}</div>
          <div>
            <div style={{ display:'flex', alignItems:'center', gap: 8, marginBottom: 2 }}>
              <span style={{ padding:'2px 8px', borderRadius:'var(--r-pill)', background: entityHsl('session', 0.12), color: accent, fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800, textTransform:'uppercase', letterSpacing:'.08em', border:`1px solid ${entityHsl('session', 0.22)}` }}>SP4 · /play-records</span>
              <span style={{ fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)', fontWeight: 700 }}>{isEdit ? '/play-records/[id]/edit' : '/play-records/new'}</span>
            </div>
            <div style={{ fontFamily:'var(--f-display)', fontWeight: 800, fontSize: 22, color:'var(--text)', letterSpacing:'-.01em' }}>{isEdit ? 'Modifica partita' : 'Registra partita'}</div>
            <div style={{ fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)', fontWeight: 600, marginTop: 2 }}>Wizard 3-step mobile + split-form desktop con live preview</div>
          </div>
        </div>
        <button type="button" onClick={() => setTheme(t => t === 'light' ? 'dark' : 'light')} style={{ padding:'8px 14px', borderRadius:'var(--r-md)', background:'var(--bg-card)', border:'1px solid var(--border)', color:'var(--text)', fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 800, cursor:'pointer', display:'inline-flex', alignItems:'center', gap: 6 }}>
          <span aria-hidden="true">🌗</span>{theme === 'light' ? 'Light' : 'Dark'}
        </button>
      </header>

      <main style={{ maxWidth: 1440, margin:'0 auto', display:'flex', flexDirection:'column', gap: 56 }}>
        <div>
          <div className="section-header">
            <span className="section-marker" style={{ background: accent }}/>
            <h2 className="section-title">Mobile · 375 — Wizard 3 step</h2>
            <span className="section-meta">{MOBILE.length} stati · default · loading · error</span>
          </div>
          <div className="mobile-grid">
            {MOBILE.map(s => <MobileScreen key={s.anchor} {...s} data={data} mode={mode} primaryLabel={primary(s.step)}/>)}
          </div>
        </div>

        <div>
          <div className="section-header">
            <span className="section-marker" style={{ background: accent }}/>
            <h2 className="section-title">Mobile · Step-flow overview</h2>
            <span className="section-meta">i 3 step affiancati per QA</span>
          </div>
          <StepFlowOverview anchor="state-08" label="08 · Step-flow 1→3 (compact)" data={data} mode={mode}/>
        </div>

        <div>
          <div className="section-header">
            <span className="section-marker" style={{ background: accent }}/>
            <h2 className="section-title">Desktop · 1280 — Split-form</h2>
            <span className="section-meta">form 8-col + record-card live preview 4-col</span>
          </div>
          <div style={{ display:'flex', flexDirection:'column', gap: 36 }}>
            <DesktopSplit anchor="state-09" label="09 · Desktop · Step 3 Punteggi + live preview" step={3} data={data} opts={{ withNotes:true }} mode={mode} primaryLabel={primaryLabel3}/>
            <DesktopSplit anchor="state-10" label="10 · Desktop · Step 1 Gioco + live preview" step={1} data={data} opts={{}} mode={mode} primaryLabel="Avanti →"/>
          </div>
        </div>
      </main>
    </div>
  );
}

window.PRForm = {
  render(rootId, opts) {
    ReactDOM.createRoot(document.getElementById(rootId)).render(<App mode={(opts && opts.mode) || 'new'}/>);
  }
};
})();
