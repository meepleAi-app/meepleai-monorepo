/* MeepleAI SP4 · Paleo — COMPOSED PARTS  (window.PaleoParts · alias PP)
   Paleo-specific panels mounted on top of the universal skeleton.

   NEW (Paleo):
     SectionCard            — collapsible accordion container (matches skeleton UX)
     DayPhaseIndicator      — 3-phase day cycle · day counter · sun/moon (aria-current)
     TribeStatusStrip       — collective shared state band (always visible)
     TribePanel             — meeple + cave painting + skulls + resources + missions
     CardsDeckPanel         — action deck · mission deck breakdown · encounters · discard
     PlayerHandBand         — per-seat hand · chosen action (face-down) · ready status
     ActionRevealOverlay    — full-width simultaneous reveal + auto-resolve order
     TribeTab · CardsTab · SkillsTab
     RightColumnTabs        — Scoring · Tribe · Cards · Skills · Chat
     DesktopBody · MobileBody · PhoneShell

   REUSES from skeleton:
     S.TopBar · S.ChatAgentPanel (accordion) · S.ActionLogTimeline (accordion) ·
     S.StateSwitch · R.ScoringPanelRenderer (BinaryWin) ·
     R.TurnIndicatorRenderer (Simultaneous) · R.StateScaffold · M.Shimmer/StateBlock */

(function () {
  const M = window.MAI;
  const R = window.SkeletonRenderers;
  const S = window.SkeletonParts;
  const PD = window.PaleoData;
  const PF = window.PaleoFlavor;
  const eHsl = M.entityHsl;
  const { useState } = React;
  const { mono, disp } = PF;
  const STATES = window.SkeletonData.STATES;

  // ─── SectionCard — titled, optionally collapsible (accordion) ─────────────
  const SectionCard = ({ title, entity = 'session', accent, right, children, pad = 12, flush, collapsed, onHeaderClick }) => {
    const collapsible = !!onHeaderClick;
    const headStyle = { width: '100%', textAlign: 'left', display: 'flex', alignItems: 'center', gap: 8, padding: '8px 12px', background: accent ? eHsl(entity, collapsed ? 0.04 : 0.06) : 'var(--bg-muted)', border: 'none', borderBottom: collapsed ? 'none' : `1px solid ${accent ? eHsl(entity, 0.18) : 'var(--border-light)'}`, cursor: collapsible ? 'pointer' : 'default' };
    const headInner = (
      <React.Fragment>
        <span style={{ ...mono(10, 800, accent ? eHsl(entity) : 'var(--text-sec)'), textTransform: 'uppercase', letterSpacing: '.07em' }}>{title}</span>
        <div style={{ flex: 1 }} />{right}
        {collapsible && <span aria-hidden="true" style={{ ...mono(11, 800, eHsl(entity)), flexShrink: 0, transform: collapsed ? 'rotate(-90deg)' : 'none', transition: 'transform var(--dur-sm) var(--ease-out)' }}>▾</span>}
      </React.Fragment>
    );
    return (
      <section style={{ background: 'var(--bg-card)', border: `1px solid ${accent || collapsible ? eHsl(entity, collapsed ? 0.2 : 0.3) : 'var(--border)'}`, borderRadius: 'var(--r-lg)', overflow: 'hidden', boxShadow: 'var(--shadow-sm)', flexShrink: 0 }}>
        {title && (collapsible
          ? <button type="button" onClick={onHeaderClick} aria-expanded={!collapsed} style={headStyle}>{headInner}</button>
          : <div style={headStyle}>{headInner}</div>)}
        {!collapsed && <div style={{ padding: flush ? 0 : pad }}>{children}</div>}
      </section>
    );
  };

  const PanelFrame = ({ label, entity = 'session', dark, children, w = 312, h = 470 }) => (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }} data-theme={dark ? 'dark' : undefined}>
      <div style={{ ...mono(10, 800, 'var(--text-sec)'), textTransform: 'uppercase', letterSpacing: '.07em' }}>{label}</div>
      <div style={{ width: w, height: h, borderRadius: 'var(--r-lg)', border: `1px solid ${eHsl(entity, 0.3)}`, background: 'var(--bg-card)', overflow: 'hidden', display: 'flex', flexDirection: 'column', boxShadow: 'var(--shadow-sm)' }}>{children}</div>
    </div>
  );

  // ══════════════════════════════════════════════════════════════════════════
  // DayPhaseIndicator — Mattina · Giorno · Notte + day counter + sun/moon
  // ══════════════════════════════════════════════════════════════════════════
  const DayPhaseIndicator = ({ compact, sticky }) => {
    const { day, totalDays, phaseIndex, phases } = PD.day;
    const active = phases[phaseIndex];
    return (
      <div role="group" aria-label="Fase del giorno" style={{
        display: 'flex', alignItems: 'center', gap: compact ? 9 : 14, padding: compact ? '8px 12px' : '10px 16px',
        background: 'var(--glass-bg)', backdropFilter: 'blur(14px)', borderBottom: '1px solid var(--border)',
        ...(sticky ? { position: 'sticky', top: 0, zIndex: 10 } : {}), flexShrink: 0, flexWrap: 'wrap', rowGap: 7,
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: compact ? 8 : 10, flexShrink: 0 }}>
          <PF.DayMoon phaseId={active.id} size={compact ? 30 : 36} />
          <div style={{ display: 'flex', flexDirection: 'column', lineHeight: 1 }}>
            <span style={{ ...disp(compact ? 17 : 21, 800, 'var(--text)'), fontVariantNumeric: 'tabular-nums' }}>Giorno {day}</span>
            <span style={{ ...mono(8, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.06em' }}>di {totalDays} · {active.short}</span>
          </div>
        </div>
        <span aria-hidden="true" style={{ width: 1, alignSelf: 'stretch', background: 'var(--border)', flexShrink: 0 }} />
        <ol style={{ listStyle: 'none', margin: 0, padding: 0, display: 'flex', alignItems: 'center', flexWrap: 'wrap', rowGap: 6, gap: compact ? 5 : 7, flex: 1, minWidth: 0 }}>
          {phases.map((ph, i) => {
            const on = i === phaseIndex, past = i < phaseIndex;
            return (
              <li key={ph.id} aria-current={on ? 'step' : undefined} style={{ display: 'flex', alignItems: 'center', gap: compact ? 4 : 7, flexShrink: 0 }}>
                <div title={ph.desc} style={{
                  display: 'flex', alignItems: 'center', gap: 6, padding: compact ? '5px 8px' : '6px 11px', borderRadius: 'var(--r-pill)',
                  background: on ? eHsl('session', 0.14) : past ? eHsl('toolkit', 0.07) : 'var(--bg-muted)',
                  border: `1px solid ${on ? eHsl('session', 0.45) : past ? eHsl('toolkit', 0.25) : 'var(--border-light)'}`,
                  boxShadow: on ? `0 3px 10px ${eHsl('session', 0.22)}` : 'none',
                }}>
                  <span aria-hidden="true" style={{
                    width: compact ? 18 : 20, height: compact ? 18 : 20, borderRadius: '50%', flexShrink: 0,
                    background: on ? eHsl('session') : past ? eHsl('toolkit', 0.25) : 'transparent',
                    color: on ? '#fff' : past ? eHsl('toolkit') : 'var(--text-muted)',
                    border: on || past ? 'none' : '1px solid var(--border-strong)',
                    display: 'inline-flex', alignItems: 'center', justifyContent: 'center', ...mono(compact ? 9 : 10, 800),
                  }}>{past ? '✓' : ph.n}</span>
                  {!compact && <span style={{ ...disp(11.5, on ? 800 : 600, on ? eHsl('session') : past ? 'var(--text-sec)' : 'var(--text-muted)'), whiteSpace: 'nowrap' }}>{ph.short}</span>}
                </div>
                {i < phases.length - 1 && <span aria-hidden="true" style={{ ...mono(10, 800, past ? eHsl('toolkit') : 'var(--text-muted)') }}>{compact ? '·' : '→'}</span>}
              </li>
            );
          })}
        </ol>
        {!compact && <span style={{ ...mono(8.5, 700, eHsl('toolkit')), flexShrink: 0, whiteSpace: 'nowrap', display: 'inline-flex', alignItems: 'center', gap: 5 }}><span aria-hidden="true">⚡</span>tutti agiscono insieme</span>}
      </div>
    );
  };

  // ══════════════════════════════════════════════════════════════════════════
  // TribeStatusStrip — collective shared state (always-visible band)
  // ══════════════════════════════════════════════════════════════════════════
  const Chip = ({ children, e, danger, title }) => (
    <span title={title} style={{ display: 'inline-flex', alignItems: 'center', gap: 5, padding: '4px 9px', borderRadius: 'var(--r-pill)', flexShrink: 0, background: e ? eHsl(e, danger ? 0.1 : 0.1) : 'var(--bg-muted)', border: `1px solid ${e ? eHsl(e, danger ? 0.4 : 0.28) : 'var(--border-light)'}` }}>{children}</span>
  );
  const TribeStatusStrip = ({ compact }) => {
    const t = PD.tribe;
    return (
      <div className="mai-cb-scroll" aria-label="Stato della tribù" style={{
        display: 'flex', alignItems: 'center', gap: compact ? 6 : 8, padding: compact ? '7px 12px' : '8px 16px',
        background: 'var(--bg-card)', borderBottom: '1px solid var(--border)', overflowX: 'auto', flexShrink: 0,
      }}>
        <span style={{ ...mono(8.5, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.06em', flexShrink: 0 }}>Tribù</span>
        <Chip e="player" title="membri vivi"><PF.Meeple member={t.members.find(m => m.status !== 'dead')} size={16} /><span style={{ ...disp(12, 800, eHsl('player')), fontVariantNumeric: 'tabular-nums' }}>{t.membersAlive}/{t.membersMax}</span></Chip>
        <span aria-hidden="true" style={{ width: 1, height: 16, background: 'var(--border)', flexShrink: 0 }} />
        <Chip e="toolkit" title="legno"><PF.ResourceToken kind="wood" size={15} /><span style={{ ...disp(12, 800, eHsl('toolkit')) }}>{t.wood}</span></Chip>
        <Chip title="pietra"><PF.ResourceToken kind="stone" size={15} /><span style={{ ...disp(12, 800, 'var(--text)') }}>{t.stone}</span></Chip>
        <Chip e="game" title="cibo"><PF.ResourceToken kind="food" size={15} /><span style={{ ...disp(12, 800, eHsl('game')) }}>{t.food}</span></Chip>
        <Chip e="kb" title="carte sapere"><span aria-hidden="true">📜</span><span style={{ ...disp(12, 800, eHsl('kb')) }}>{t.knowledge}</span></Chip>
        <span aria-hidden="true" style={{ width: 1, height: 16, background: 'var(--border)', flexShrink: 0 }} />
        <Chip title="pittura rupestre" style={{}}><span aria-hidden="true" style={{ fontSize: 13 }}>✋</span><span style={{ ...disp(12, 800, PF.OCHRE) }}>{t.painting}/{t.paintingMax}</span></Chip>
        <Chip e="event" danger={t.skulls >= t.skullsMax - 1} title="teschi"><span aria-hidden="true">💀</span><span style={{ ...disp(12, 800, eHsl('event')) }}>{t.skulls}/{t.skullsMax}</span></Chip>
      </div>
    );
  };

  // ══════════════════════════════════════════════════════════════════════════
  // TribePanel — meeple + cave painting + skulls + resources + missions
  // ══════════════════════════════════════════════════════════════════════════
  const MemberCard = ({ member }) => {
    const owner = PD.byPlayer(member.owner);
    const statusMeta = { home: { lb: 'all\u2019accampamento', e: 'toolkit' }, away: { lb: member.place, e: 'session' }, wounded: { lb: 'ferito', e: 'event' }, dead: { lb: 'caduto', e: null } }[member.status];
    return (
      <div style={{ display: 'flex', alignItems: 'center', gap: 9, padding: '8px 10px', borderRadius: 'var(--r-md)', background: 'var(--bg-muted)', border: `1px solid ${member.status === 'wounded' ? eHsl('event', 0.3) : 'var(--border-light)'}` }}>
        <PF.Meeple member={member} size={32} />
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
            <span style={{ ...disp(12.5, 800, 'var(--text)') }}>{member.name}</span>
            <PF.MiniAvatar p={owner} size={16} />
          </div>
          <div style={{ ...mono(9, 700, statusMeta.e ? eHsl(statusMeta.e) : 'var(--text-muted)'), whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{statusMeta.lb}</div>
        </div>
        <PF.SkillGlyph skill={member.skill} size={22} />
      </div>
    );
  };
  const TribePanelInner = ({ compact }) => {
    const t = PD.tribe;
    return (
      <div style={{ display: 'flex', flexDirection: 'column', gap: 12, padding: 12 }}>
        {/* win / lose tracks — prominent */}
        <div style={{ display: 'grid', gridTemplateColumns: compact ? '1fr' : '1.2fr 1fr', gap: 10 }}>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
            <div style={{ display: 'flex', alignItems: 'baseline', gap: 6 }}>
              <span style={{ ...mono(9, 800, PF.OCHRE_DK), textTransform: 'uppercase', letterSpacing: '.06em' }}>Pittura rupestre</span>
              <span style={{ ...mono(9, 800, PF.OCHRE) }}>{t.painting}/{t.paintingMax}</span>
              <div style={{ flex: 1 }} />
              <span style={{ ...mono(8, 700, 'var(--text-muted)') }}>5 = vittoria</span>
            </div>
            <PF.CavePainting value={t.painting} max={t.paintingMax} size={compact ? 30 : 38} />
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
            <div style={{ display: 'flex', alignItems: 'baseline', gap: 6 }}>
              <span style={{ ...mono(9, 800, eHsl('event')), textTransform: 'uppercase', letterSpacing: '.06em' }}>Teschi</span>
              <span style={{ ...mono(9, 800, eHsl('event')) }}>{t.skulls}/{t.skullsMax}</span>
              <div style={{ flex: 1 }} />
              <span style={{ ...mono(8, 700, 'var(--text-muted)') }}>5 = estinzione</span>
            </div>
            <PF.SkullCluster value={t.skulls} max={t.skullsMax} size={compact ? 24 : 28} />
          </div>
        </div>
        {/* resources */}
        <div style={{ display: 'flex', gap: 7, flexWrap: 'wrap' }}>
          <PF.ResourceCounter kind="wood" value={t.wood} big />
          <PF.ResourceCounter kind="stone" value={t.stone} big />
          <PF.ResourceCounter kind="food" value={t.food} big />
        </div>
        <div><PF.KnowledgeBadge count={t.knowledge} big /></div>
        {/* members */}
        <div>
          <div style={{ display: 'flex', alignItems: 'baseline', gap: 6, marginBottom: 6 }}>
            <span style={{ ...mono(9, 800, eHsl('player')), textTransform: 'uppercase', letterSpacing: '.07em' }}>Membri</span>
            <span style={{ ...mono(9, 800, 'var(--text-muted)') }}>{t.membersAlive} vivi · {t.members.filter(m => m.status === 'dead').length} caduti</span>
          </div>
          <div style={{ display: 'grid', gridTemplateColumns: compact ? '1fr' : '1fr 1fr', gap: 7 }}>
            {t.members.map(m => <MemberCard key={m.id} member={m} />)}
          </div>
        </div>
        {/* missions */}
        <div>
          <div style={{ ...mono(9, 800, PF.OCHRE_DK), textTransform: 'uppercase', letterSpacing: '.07em', marginBottom: 6 }}>Missioni in corso</div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
            {PD.cards.missions.map(ms => <PF.MissionRow key={ms.id} mission={ms} />)}
          </div>
        </div>
      </div>
    );
  };
  const TribePanel = ({ state = 'default', compact, collapsed, onHeaderClick }) => (
    <SectionCard title="Tribù · stato condiviso" entity="session" accent flush collapsed={collapsed} onHeaderClick={onHeaderClick}
      right={<span style={{ ...mono(8.5, 800, eHsl('player')), padding: '1px 7px', borderRadius: 'var(--r-pill)', background: eHsl('player', 0.12), border: `1px solid ${eHsl('player', 0.3)}` }}>{PD.tribe.membersAlive} membri</span>}>
      <R.StateScaffold state={state} sseWhere="tribù"
        empty={{ icon: '🦣', title: 'Tribù non avviata', body: 'Lo stato della tribù comparirà all\u2019avvio della partita.' }}
        error={{ title: 'Tribù non disponibile', body: 'Impossibile caricare lo stato condiviso della tribù.' }}
        loading={<div style={{ padding: 14, display: 'flex', flexDirection: 'column', gap: 10 }}>{Array.from({ length: 5 }).map((_, i) => <M.Shimmer key={i} h={i === 0 ? 70 : 40} />)}</div>}>
        <TribePanelInner compact={compact} />
      </R.StateScaffold>
    </SectionCard>
  );

  // ══════════════════════════════════════════════════════════════════════════
  // CardsDeckPanel — action deck · mission deck · encounters · discard
  // ══════════════════════════════════════════════════════════════════════════
  const DeckStat = ({ icon, lb, value, e, cardBack }) => (
    <div style={{ flex: '1 1 120px', display: 'flex', alignItems: 'center', gap: 9, padding: '9px 11px', borderRadius: 'var(--r-md)', background: eHsl(e, 0.06), border: `1px solid ${eHsl(e, 0.25)}` }}>
      {cardBack ? <PF.CardBack w={32} h={44} e={e} label={icon} /> : <span aria-hidden="true" style={{ fontSize: 20, flexShrink: 0 }}>{icon}</span>}
      <div style={{ minWidth: 0 }}>
        <div style={{ ...disp(20, 800, eHsl(e)), fontVariantNumeric: 'tabular-nums', lineHeight: 1 }}>{value}</div>
        <div style={{ ...mono(8.5, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.05em', marginTop: 2 }}>{lb}</div>
      </div>
    </div>
  );
  const CardsDeckInner = () => {
    const c = PD.cards;
    return (
      <div style={{ display: 'flex', flexDirection: 'column', gap: 12, padding: 12 }}>
        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
          <DeckStat icon="⚒" lb="mazzo azioni" value={c.actionDeck} e="session" cardBack />
          <DeckStat icon="🗑" lb="scarti" value={c.discard} e="event" />
        </div>
        {/* mission deck + breakdown */}
        <div style={{ borderRadius: 'var(--r-md)', background: 'var(--bg-muted)', border: '1px solid var(--border-light)', padding: 11 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 9 }}>
            <span aria-hidden="true" style={{ fontSize: 17 }}>🎯</span>
            <span style={{ ...disp(12.5, 800, 'var(--text)') }}>Mazzo missioni</span>
            <div style={{ flex: 1 }} />
            <span style={{ ...disp(18, 800, eHsl('toolkit')), fontVariantNumeric: 'tabular-nums' }}>{c.missionDeck}</span>
          </div>
          <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
            {c.missionBreakdown.map(b => (
              <span key={b.lb} style={{ display: 'inline-flex', alignItems: 'center', gap: 5, padding: '3px 9px', borderRadius: 'var(--r-pill)', background: eHsl(b.e, 0.1), border: `1px solid ${eHsl(b.e, 0.28)}` }}>
                <span style={{ ...disp(12, 800, eHsl(b.e)) }}>{b.n}</span>
                <span style={{ ...mono(9, 700, 'var(--text-sec)') }}>{b.lb}</span>
              </span>
            ))}
          </div>
        </div>
        {/* mysterious encounter piles */}
        <div>
          <div style={{ ...mono(9, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.07em', marginBottom: 6 }}>Incontri misteriosi · {c.encounters.length}</div>
          <div style={{ display: 'flex', gap: 10 }}>
            {c.encounters.map(en => (
              <div key={en.id} style={{ flex: 1, display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 6 }}>
                <PF.CardBack w={52} h={70} e="agent" label="?" />
                <span style={{ ...mono(9, 700, 'var(--text-sec)'), display: 'inline-flex', alignItems: 'center', gap: 4 }}><span aria-hidden="true">{en.icon}</span>{en.lb}</span>
              </div>
            ))}
          </div>
        </div>
      </div>
    );
  };
  const CardsDeckPanel = ({ state = 'default', collapsed, onHeaderClick }) => (
    <SectionCard title="Carte & mazzi" entity="game" accent flush collapsed={collapsed} onHeaderClick={onHeaderClick}
      right={<span style={{ ...mono(8.5, 700, 'var(--text-muted)') }}>azioni · missioni · incontri</span>}>
      <R.StateScaffold state={state} sseWhere="mazzi"
        empty={{ icon: '🃏', title: 'Mazzi vuoti', body: 'Le carte compariranno all\u2019avvio della Mattina.' }}
        error={{ title: 'Mazzi non disponibili', body: 'Impossibile caricare i mazzi della partita.' }}
        loading={<div style={{ padding: 14, display: 'flex', gap: 8, flexWrap: 'wrap' }}>{Array.from({ length: 4 }).map((_, i) => <M.Shimmer key={i} h={56} style={{ flex: '1 1 120px' }} />)}</div>}>
        <CardsDeckInner />
      </R.StateScaffold>
    </SectionCard>
  );

  // ══════════════════════════════════════════════════════════════════════════
  // PlayerHandBand — per-seat hand · chosen action · ready status (fascia)
  // ══════════════════════════════════════════════════════════════════════════
  const SeatCard = ({ seat, isDay, compact }) => {
    const p = PD.byPlayer(seat.playerId);
    const you = seat.playerId === PD.hands.youId;
    return (
      <div style={{ flex: compact ? '1 1 auto' : '1 1 220px', width: compact ? '100%' : 'auto', minWidth: 0, display: 'flex', flexDirection: 'column', gap: 8, padding: 10, borderRadius: 'var(--r-md)', background: you ? eHsl('session', 0.05) : 'var(--bg-card)', border: `1px solid ${you ? eHsl('session', 0.3) : 'var(--border)'}` }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 7 }}>
          <PF.MiniAvatar p={p} size={24} ring={you} />
          <span style={{ ...disp(12.5, 800, 'var(--text)') }}>{p.name}</span>
          {you && <span style={{ ...mono(8, 800, eHsl('session')), padding: '1px 6px', borderRadius: 'var(--r-pill)', background: eHsl('session', 0.12), border: `1px solid ${eHsl('session', 0.3)}` }}>tu</span>}
          <div style={{ flex: 1 }} />
          <PF.ReadyBadge ready={seat.ready} />
        </div>
        {/* hand */}
        {you && seat.hand ? (
          <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
            {seat.hand.map(card => {
              const chosen = card.action === seat.chosen;
              return (
                <div key={card.id} title={`${card.title} · ${card.cost}`} style={{ width: 58, display: 'flex', flexDirection: 'column', gap: 3, padding: 6, borderRadius: 'var(--r-sm)', background: eHsl(card.e, chosen ? 0.14 : 0.07), border: `1.5px solid ${chosen ? eHsl(card.e, 0.6) : eHsl(card.e, 0.25)}`, boxShadow: chosen ? `0 3px 9px ${eHsl(card.e, 0.25)}` : 'none' }}>
                  <span aria-hidden="true" style={{ fontSize: 15, textAlign: 'center' }}>{PD.ACTIONS[card.action].icon}</span>
                  <span style={{ ...disp(8.5, 800, eHsl(card.e)), textAlign: 'center', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{card.title}</span>
                </div>
              );
            })}
          </div>
        ) : (
          <div style={{ display: 'flex', alignItems: 'center', gap: 5 }}>
            {Array.from({ length: Math.min(seat.count, 5) }).map((_, i) => <PF.CardBack key={i} w={26} h={36} e="player" label="" />)}
            <span style={{ ...mono(9, 700, 'var(--text-muted)'), marginLeft: 4 }}>{seat.count} carte · segrete</span>
          </div>
        )}
        {/* action chosen this turn (face-down until reveal) */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 7, padding: '6px 8px', borderRadius: 'var(--r-sm)', background: 'var(--bg-muted)', border: '1px solid var(--border-light)' }}>
          <span style={{ ...mono(8, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.05em', flexShrink: 0 }}>Azione</span>
          {seat.chosen
            ? <span style={{ display: 'inline-flex', alignItems: 'center', gap: 5 }}><PF.CardBack w={20} h={28} e="session" label="" /><span style={{ ...mono(9, 700, 'var(--text-sec)') }}>scelta · coperta</span></span>
            : <span style={{ ...mono(9, 700, eHsl('agent')) }} className="mai-pulse-dot-host">in attesa…</span>}
          <div style={{ flex: 1 }} />
        </div>
      </div>
    );
  };
  const PlayerHandBand = ({ compact, onReveal }) => {
    const isDay = PD.day.phases[PD.day.phaseIndex].id === 'day';
    const waiting = PD.hands.seats.filter(s => !s.ready).map(s => PD.byPlayer(s.playerId).name);
    return (
      <div aria-label="Mani dei giocatori" style={{ display: 'flex', flexDirection: 'column', gap: 8, padding: compact ? '10px 12px' : '11px 16px', background: 'var(--glass-bg)', backdropFilter: 'blur(14px)', borderTop: '1px solid var(--border)', flexShrink: 0 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap', rowGap: 6 }}>
          <span style={{ ...mono(9, 800, 'var(--text-sec)'), textTransform: 'uppercase', letterSpacing: '.06em' }}>Mani & azioni segrete</span>
          <span style={{ ...mono(8.5, 700, 'var(--text-muted)') }}>vista di Marco · le altre mani sono coperte</span>
          <div style={{ flex: 1 }} />
          {isDay && (<>
            {waiting.length > 0 && <span style={{ ...mono(8.5, 700, eHsl('agent')), display: 'inline-flex', alignItems: 'center', gap: 4, flexShrink: 0 }}><span aria-hidden="true" className="mai-pulse-dot">◐</span>{waiting.join(', ')} in pensiero</span>}
            <button type="button" onClick={onReveal} style={{
              display: 'inline-flex', alignItems: 'center', gap: 6, padding: '7px 14px', borderRadius: 'var(--r-md)', flexShrink: 0,
              background: eHsl('session'), color: '#fff', border: 'none', cursor: 'pointer',
              ...disp(12, 800, '#fff'), boxShadow: `0 3px 10px ${eHsl('session', 0.35)}`,
            }}><span aria-hidden="true">⚡</span>Rivela azioni</button>
          </>)}
        </div>
        <div style={{ display: 'flex', gap: 9, flexDirection: compact ? 'column' : 'row', flexWrap: compact ? 'nowrap' : 'wrap' }}>
          {PD.hands.seats.map(s => <SeatCard key={s.playerId} seat={s} isDay={isDay} compact={compact} />)}
        </div>
      </div>
    );
  };

  // ══════════════════════════════════════════════════════════════════════════
  // ActionRevealOverlay — full-width simultaneous reveal + auto-resolve order
  // ══════════════════════════════════════════════════════════════════════════
  const OUTCOME_BG = { gain: 'game', paint: 'kb', skull: 'event', kill: 'event' };
  const ActionRevealOverlay = ({ open, onClose }) => {
    const cards = [...PD.reveal.cards].sort((a, b) => a.priority - b.priority);
    return (
      <div aria-hidden={!open} style={{ position: 'absolute', inset: 0, zIndex: 60, pointerEvents: open ? 'auto' : 'none' }}>
        <div onClick={onClose} style={{ position: 'absolute', inset: 0, background: 'rgba(15,12,8,.62)', backdropFilter: 'blur(4px)', opacity: open ? 1 : 0, transition: 'opacity var(--dur-md) var(--ease-out)' }} />
        <div role="dialog" aria-modal="true" aria-label="Rivelazione simultanea delle azioni" style={{
          position: 'absolute', left: '50%', top: '50%', transform: open ? 'translate(-50%,-50%) scale(1)' : 'translate(-50%,-50%) scale(.96)',
          width: 'min(880px, 94%)', maxHeight: '88%', overflowY: 'auto', background: 'var(--bg-card)', borderRadius: 'var(--r-2xl)',
          border: `2px solid ${eHsl('session', 0.45)}`, boxShadow: 'var(--shadow-lg)', opacity: open ? 1 : 0,
          transition: 'opacity var(--dur-md) var(--ease-out), transform var(--dur-md) var(--ease-spring)',
        }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 9, padding: '14px 18px', borderBottom: `1px solid ${eHsl('session', 0.25)}`, background: eHsl('session', 0.07), position: 'sticky', top: 0, backdropFilter: 'blur(8px)' }}>
            <span aria-hidden="true" className="mai-pulse-dot" style={{ color: eHsl('session'), fontSize: 11 }}>●</span>
            <div style={{ minWidth: 0 }}>
              <div style={{ ...disp(16, 800, 'var(--text)') }}>Rivelazione simultanea</div>
              <div style={{ ...mono(9.5, 700, 'var(--text-muted)') }}>Giorno {PD.day.day} · fase {PD.reveal.phase} · risoluzione automatica per priorità</div>
            </div>
            <div style={{ flex: 1 }} />
            <button type="button" onClick={onClose} aria-label="Chiudi" style={{ width: 32, height: 32, borderRadius: 'var(--r-md)', background: 'var(--bg-muted)', border: '1px solid var(--border)', color: 'var(--text-sec)', cursor: 'pointer', fontSize: 14, flexShrink: 0 }}>✕</button>
          </div>
          <div style={{ padding: 18, display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: 12 }}>
            {cards.map((c, i) => {
              const p = PD.byPlayer(c.playerId);
              const oe = OUTCOME_BG[c.outcome.kind] || 'session';
              return (
                <div key={i} style={{ display: 'flex', flexDirection: 'column', gap: 9, padding: 12, borderRadius: 'var(--r-lg)', background: 'var(--bg-muted)', border: `1px solid ${eHsl(c.outcome.e, 0.28)}`, position: 'relative' }}>
                  <span style={{ position: 'absolute', top: 10, right: 10, width: 22, height: 22, borderRadius: '50%', background: eHsl('session', 0.14), border: `1px solid ${eHsl('session', 0.35)}`, color: eHsl('session'), display: 'flex', alignItems: 'center', justifyContent: 'center', ...mono(10, 800) }} title={`risolve ${i + 1}°`}>{i + 1}</span>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 7 }}>
                    <PF.MiniAvatar p={p} size={26} />
                    <span style={{ ...disp(13, 800, 'var(--text)') }}>{p.name}</span>
                  </div>
                  <PF.ActionPill action={c.action} />
                  <div style={{ ...mono(9.5, 700, 'var(--text-sec)'), lineHeight: 1.4 }}>{c.title}</div>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '8px 10px', borderRadius: 'var(--r-md)', background: eHsl(c.outcome.e, 0.1), border: `1px solid ${eHsl(c.outcome.e, 0.3)}`, marginTop: 'auto' }}>
                    <span aria-hidden="true" style={{ fontSize: 18, flexShrink: 0 }}>{c.outcome.icon}</span>
                    <span style={{ ...disp(11, 800, eHsl(c.outcome.e)), lineHeight: 1.3 }}>{c.outcome.text}</span>
                  </div>
                </div>
              );
            })}
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '12px 18px 16px', flexWrap: 'wrap' }}>
            <span style={{ ...mono(9.5, 700, 'var(--text-muted)'), flex: 1, minWidth: 180, lineHeight: 1.5 }}>Le carte con priorità più alta (numero più basso) si risolvono per prime. Risultati applicati allo stato condiviso della tribù.</span>
            <button type="button" onClick={onClose} style={{ padding: '8px 18px', borderRadius: 'var(--r-md)', background: eHsl('session'), color: '#fff', border: 'none', ...disp(12.5, 800, '#fff'), cursor: 'pointer', boxShadow: `0 3px 10px ${eHsl('session', 0.35)}` }}>Applica & chiudi alla Notte</button>
          </div>
        </div>
      </div>
    );
  };

  // ══════════════════════════════════════════════════════════════════════════
  // Right-column tab bodies — Tribe · Cards · Skills
  // ══════════════════════════════════════════════════════════════════════════
  const TribeTab = ({ state = 'default' }) => (
    <div style={{ flex: 1, minHeight: 0, overflowY: 'auto', display: 'flex', flexDirection: 'column' }}>
      {state === 'default' && <R.TurnIndicatorRenderer data={PD.ds} state="default" />}
      {state !== 'default'
        ? <TribePanel state={state} compact />
        : <div style={{ padding: '0 12px 12px' }}><TribePanelInner compact /></div>}
    </div>
  );
  const CardsTab = ({ state = 'default' }) => (
    <div style={{ flex: 1, minHeight: 0, overflowY: 'auto', padding: 12 }}>
      <CardsDeckPanel state={state} />
    </div>
  );
  const SkillsTab = ({ state = 'default' }) => (
    <R.StateScaffold state={state} sseWhere="abilità"
      empty={{ icon: '🌀', title: 'Nessuna abilità', body: 'Le abilità dei membri compariranno con lo sviluppo della tribù.' }}
      error={{ title: 'Abilità non disponibili', body: 'Impossibile caricare l\u2019albero delle abilità.' }}>
      <R.Panel gap={10}>
        <R.Label>Abilità per membro · asimmetriche</R.Label>
        {PD.skillTree.map(({ member, nodes }) => {
          const owner = PD.byPlayer(member.owner);
          const s = PD.SKILLS[member.skill];
          return (
            <div key={member.id} style={{ borderRadius: 'var(--r-md)', background: 'var(--bg-card)', border: '1px solid var(--border)', padding: 10 }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 8 }}>
                <PF.Meeple member={member} size={26} />
                <span style={{ ...disp(12.5, 800, 'var(--text)') }}>{member.name}</span>
                <PF.MiniAvatar p={owner} size={16} />
                <div style={{ flex: 1 }} />
                <PF.SkillBadge skill={member.skill} />
              </div>
              <div style={{ display: 'flex', alignItems: 'center', gap: 0 }}>
                {nodes.map((n, i) => (
                  <React.Fragment key={i}>
                    <div style={{ flex: 1, display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 4 }}>
                      <span style={{ width: 30, height: 30, borderRadius: '50%', display: 'flex', alignItems: 'center', justifyContent: 'center', flexShrink: 0,
                        background: n.done ? eHsl(s.e, 0.16) : 'var(--bg-sunken)', border: `1.5px solid ${n.done ? eHsl(s.e, 0.5) : 'var(--border-strong)'}`,
                        ...mono(11, 800, n.done ? eHsl(s.e) : 'var(--text-muted)') }}>{n.done ? '✓' : '🔒'}</span>
                      <span style={{ ...mono(8.5, 700, n.done ? 'var(--text-sec)' : 'var(--text-muted)'), textAlign: 'center' }}>{n.lb}</span>
                    </div>
                    {i < nodes.length - 1 && <span aria-hidden="true" style={{ width: 16, height: 2, background: nodes[i + 1].done ? eHsl(s.e, 0.4) : 'var(--border)', flexShrink: 0, marginTop: -16 }} />}
                  </React.Fragment>
                ))}
              </div>
            </div>
          );
        })}
      </R.Panel>
    </R.StateScaffold>
  );

  // ─── RightColumnTabs — Scoring · Tribe · Cards · Skills · Chat ─────────────
  const RC_TABS = [
    { id: 'scoring', icon: '🎯', label: 'Scoring', entity: 'session', render: (st) => <R.ScoringPanelRenderer data={PD.ds} state={st} /> },
    { id: 'tribe',   icon: '🦣', label: 'Tribù',   entity: 'player',  render: (st) => <TribeTab state={st} /> },
    { id: 'cards',   icon: '🃏', label: 'Carte',   entity: 'game',    render: (st) => <CardsTab state={st} /> },
    { id: 'skills',  icon: '🌀', label: 'Abilità', entity: 'kb',      render: (st) => <SkillsTab state={st} /> },
    { id: 'chat',    icon: '💬', label: 'Chat',    entity: 'agent',   render: () => <div style={{ flex: 1, minHeight: 0, padding: 12, display: 'flex' }}><S.ChatAgentPanel ds={PD.ds} /></div> },
  ];
  const RightColumnTabs = ({ initial = 'scoring', initialState = 'default', embedded }) => {
    const [tab, setTab] = useState(initial);
    const [st, setSt] = useState(initialState);
    const active = RC_TABS.find(t => t.id === tab) || RC_TABS[0];
    return (
      <aside aria-label="Pannello sessione" style={{ width: embedded ? '100%' : 360, minWidth: embedded ? 0 : 300, flexShrink: 0, height: '100%', background: 'var(--bg-card)', borderLeft: embedded ? 'none' : '1px solid var(--border)', display: 'flex', flexDirection: 'column', minHeight: 0 }}>
        <div role="tablist" aria-label="Sezioni" style={{ display: 'flex', borderBottom: '1px solid var(--border-light)', flexShrink: 0, overflow: 'hidden' }}>
          {RC_TABS.map(t => {
            const on = tab === t.id;
            return (
              <button key={t.id} type="button" role="tab" aria-selected={on} aria-label={t.label} onClick={() => setTab(t.id)} style={{ flex: '1 1 0', minWidth: 0, padding: '10px 3px', background: on ? eHsl(t.entity, 0.06) : 'transparent', border: 'none', borderBottom: on ? `2px solid ${eHsl(t.entity)}` : '2px solid transparent', color: on ? eHsl(t.entity) : 'var(--text-sec)', ...disp(10.5, 800), cursor: 'pointer', display: 'inline-flex', alignItems: 'center', justifyContent: 'center', gap: 4, whiteSpace: 'nowrap' }}>
                <span aria-hidden="true" style={{ fontSize: 12, flexShrink: 0 }}>{t.icon}</span><span style={{ minWidth: 0, overflow: 'hidden', textOverflow: 'ellipsis' }}>{t.label}</span>
              </button>
            );
          })}
        </div>
        {active.id !== 'chat' && <S.StateSwitch value={st} onChange={setSt} />}
        <div role="tabpanel" style={{ flex: 1, minHeight: 0, display: 'flex', flexDirection: 'column' }}>{active.render(st)}</div>
      </aside>
    );
  };

  // ══════════════════════════════════════════════════════════════════════════
  // DESKTOP body — phase banner · tribe strip · [accordion main | tabs] · hand band
  // ══════════════════════════════════════════════════════════════════════════
  const DesktopBody = ({ initialTab, initialState }) => {
    const [expanded, setExpanded] = useState('tribe'); // tribe | cards | chat | log
    const [reveal, setReveal] = useState(false);
    const sec = (id) => ({ collapsed: expanded !== id, onHeaderClick: () => setExpanded(id) });
    return (
      <div style={{ display: 'flex', flexDirection: 'column', flex: 1, minHeight: 0, position: 'relative' }}>
        <DayPhaseIndicator />
        <TribeStatusStrip />
        <div style={{ display: 'flex', flex: 1, minHeight: 0 }}>
          {/* MAIN — single-expanded accordion (tribe · cards · chat · log) */}
          <main className="mai-cb-scroll" style={{ flex: 1, minWidth: 0, overflowY: 'auto', padding: 12, display: 'flex', flexDirection: 'column', gap: 10, background: 'var(--bg)' }}>
            <TribePanel {...sec('tribe')} />
            <CardsDeckPanel {...sec('cards')} />
            <S.ChatAgentPanel ds={PD.ds} {...sec('chat')} />
            <S.ActionLogTimeline ds={PD.ds} {...sec('log')} />
          </main>
          <RightColumnTabs initial={initialTab} initialState={initialState} />
        </div>
        <PlayerHandBand onReveal={() => setReveal(true)} />
        <ActionRevealOverlay open={reveal} onClose={() => setReveal(false)} />
      </div>
    );
  };

  // ══════════════════════════════════════════════════════════════════════════
  // MOBILE body — phase sticky · scroll accordion · hand band · tab→sheet
  // ══════════════════════════════════════════════════════════════════════════
  const MobileSheet = ({ open, tab, st, setSt, onClose }) => {
    const active = RC_TABS.find(t => t.id === tab) || RC_TABS[0];
    return (
      <div aria-hidden={!open} style={{ position: 'absolute', inset: 0, zIndex: 50, pointerEvents: open ? 'auto' : 'none' }}>
        <div onClick={onClose} style={{ position: 'absolute', inset: 0, background: 'rgba(15,12,8,.5)', backdropFilter: 'blur(3px)', opacity: open ? 1 : 0, transition: 'opacity var(--dur-md) var(--ease-out)' }} />
        <div role="dialog" aria-modal="true" aria-label={active.label} style={{ position: 'absolute', left: 0, right: 0, bottom: 0, height: '86%', background: 'var(--bg-card)', borderTopLeftRadius: 'var(--r-2xl)', borderTopRightRadius: 'var(--r-2xl)', borderTop: `3px solid ${eHsl(active.entity)}`, boxShadow: 'var(--shadow-drawer)', display: 'flex', flexDirection: 'column', minHeight: 0, transform: open ? 'translateY(0)' : 'translateY(101%)', transition: 'transform var(--dur-lg) var(--ease-spring)' }}>
          <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', paddingTop: 8, flexShrink: 0 }}>
            <div style={{ width: 38, height: 4, borderRadius: 999, background: 'var(--border-strong)' }} />
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '8px 14px 10px', flexShrink: 0 }}>
            <span aria-hidden="true" style={{ fontSize: 16 }}>{active.icon}</span>
            <span style={{ ...disp(15, 800, eHsl(active.entity)) }}>{active.label}</span>
            <div style={{ flex: 1 }} />
            <button type="button" onClick={onClose} aria-label="Chiudi" style={{ width: 28, height: 28, borderRadius: 'var(--r-md)', background: 'var(--bg-muted)', border: '1px solid var(--border)', color: 'var(--text-sec)', cursor: 'pointer', fontSize: 13 }}>✕</button>
          </div>
          {active.id !== 'chat' && <S.StateSwitch value={st} onChange={setSt} />}
          <div style={{ flex: 1, minHeight: 0, display: 'flex', flexDirection: 'column' }}>{active.render(st)}</div>
        </div>
      </div>
    );
  };
  const MobileBody = ({ initialTab }) => {
    const [sheet, setSheet] = useState(initialTab || null);
    const [st, setSt] = useState('default');
    const [expanded, setExpanded] = useState('tribe');
    const [reveal, setReveal] = useState(false);
    const sec = (id) => ({ collapsed: expanded !== id, onHeaderClick: () => setExpanded(id) });
    return (
      <>
        <DayPhaseIndicator compact sticky />
        <div className="mai-cb-scroll" style={{ flex: 1, overflowY: 'auto', minHeight: 0, background: 'var(--bg)' }}>
          <TribeStatusStrip compact />
          <div style={{ padding: 12, display: 'flex', flexDirection: 'column', gap: 10 }}>
            <TribePanel compact {...sec('tribe')} />
            <CardsDeckPanel {...sec('cards')} />
            <S.ChatAgentPanel ds={PD.ds} compact {...sec('chat')} />
            <S.ActionLogTimeline ds={PD.ds} compact {...sec('log')} />
          </div>
          <PlayerHandBand compact onReveal={() => setReveal(true)} />
        </div>
        <nav aria-label="Sezioni sessione" style={{ display: 'flex', gap: 5, flexShrink: 0, padding: '8px 8px', background: 'var(--glass-bg)', backdropFilter: 'blur(14px)', borderTop: '1px solid var(--border)' }}>
          {RC_TABS.map(t => (
            <button key={t.id} type="button" onClick={() => { setSt('default'); setSheet(t.id); }} style={{ flex: '1 1 0', minWidth: 0, display: 'inline-flex', alignItems: 'center', justifyContent: 'center', gap: 4, padding: '7px 4px', borderRadius: 'var(--r-pill)', background: eHsl(t.entity, 0.1), border: `1px solid ${eHsl(t.entity, 0.28)}`, color: eHsl(t.entity), ...disp(10, 800), cursor: 'pointer', whiteSpace: 'nowrap' }}>
              <span aria-hidden="true" style={{ flexShrink: 0 }}>{t.icon}</span><span style={{ minWidth: 0, overflow: 'hidden', textOverflow: 'ellipsis' }}>{t.label}</span>
            </button>
          ))}
        </nav>
        <MobileSheet open={!!sheet} tab={sheet} st={st} setSt={setSt} onClose={() => setSheet(null)} />
        <ActionRevealOverlay open={reveal} onClose={() => setReveal(false)} />
      </>
    );
  };

  // ─── PhoneShell ───────────────────────────────────────────────────────────
  const PhoneSbar = () => (
    <div className="phone-sbar" style={{ color: 'var(--text)' }}>
      <span style={{ fontFamily: 'var(--f-mono)' }}>17:31</span>
      <div className="ind"><span aria-hidden="true">●●●●</span><span aria-hidden="true">100%</span></div>
    </div>
  );
  const PhoneShell = ({ label, desc, dark, initialTab }) => (
    <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 10 }}>
      <div style={{ ...mono(11, 700, 'var(--text-sec)'), textTransform: 'uppercase', letterSpacing: '.08em' }}>{label}{dark && <span style={{ color: eHsl('session'), marginLeft: 6 }}>· dark</span>}</div>
      <div className="phone" data-theme={dark ? 'dark' : undefined}>
        <PhoneSbar />
        <div style={{ flex: 1, display: 'flex', flexDirection: 'column', background: 'var(--bg)', position: 'relative', overflow: 'hidden', minHeight: 0 }}>
          <S.TopBar ds={PD.ds} compact />
          <MobileBody initialTab={initialTab} />
        </div>
      </div>
      {desc && <div style={{ fontSize: 11, color: 'var(--text-muted)', maxWidth: 340, textAlign: 'center', lineHeight: 1.55 }}>{desc}</div>}
    </div>
  );

  window.PaleoParts = {
    STATES, SectionCard, PanelFrame,
    DayPhaseIndicator, TribeStatusStrip, TribePanel, TribePanelInner, CardsDeckPanel,
    PlayerHandBand, ActionRevealOverlay, TribeTab, CardsTab, SkillsTab,
    RightColumnTabs, DesktopBody, MobileBody, PhoneShell,
  };
})();
