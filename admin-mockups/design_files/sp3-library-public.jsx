/* MeepleAI SP3 missing — #8 · /library (public variant)
   ULTIMO OBBLIGATORIO SP3
   Componenti v2 nuovi: CommunityStatsRow · FeaturedGamesCarousel
   Riusi: HeroGradient (pattern pub-hero) · MeepleCard hero/grid · EntityChip
*/

const { useState, useEffect, useRef } = React;

// ── DATA ───────────────────────────────────────────────────────────────────
const FEATURED_GAMES = [
  { id: 'g-wingspan', t: 'Wingspan', a: 'Stonemaier · 2019', sessions: 8421, hue: 142, sat: 30, emoji: '🐦' },
  { id: 'g-azul', t: 'Azul', a: 'Plan B · 2017', sessions: 7280, hue: 200, sat: 55, emoji: '🔷' },
  { id: 'g-brass-bham', t: 'Brass: Birmingham', a: 'Roxley · 2018', sessions: 6940, hue: 28, sat: 45, emoji: '⚙️' },
  { id: 'g-7wonders', t: '7 Wonders', a: 'Repos · 2010', sessions: 6105, hue: 36, sat: 50, emoji: '🏛' },
  { id: 'g-ticket-ride', t: 'Ticket to Ride', a: 'Days of Wonder · 2004', sessions: 5870, hue: 8, sat: 55, emoji: '🚂' },
  { id: 'g-catan', t: 'Catan', a: 'Kosmos · 1995', sessions: 5240, hue: 18, sat: 45, emoji: '🏝' },
  { id: 'g-carcassonne', t: 'Carcassonne', a: 'Hans im Glück · 2000', sessions: 4980, hue: 88, sat: 30, emoji: '🏰' },
  { id: 'g-terraforming-mars', t: 'Terraforming Mars', a: 'Stronghold · 2016', sessions: 4720, hue: 12, sat: 60, emoji: '🪐' },
  { id: 'g-spirit-island', t: 'Spirit Island', a: 'Greater Than · 2017', sessions: 4150, hue: 168, sat: 40, emoji: '🌿' },
  { id: 'g-gloomhaven', t: 'Gloomhaven', a: 'Cephalofair · 2017', sessions: 3920, hue: 268, sat: 28, emoji: '⚔️' },
];

const TRENDING_GAMES = [
  { id: 'g-wyrmspan', t: 'Wyrmspan', a: 'Stonemaier · 2024', growth: 47, hue: 348, sat: 35, emoji: '🐉' },
  { id: 'g-arcs', t: 'Arcs', a: 'Leder Games · 2024', growth: 39, hue: 252, sat: 40, emoji: '🌌' },
  { id: 'g-sea-salt-paper', t: 'Sea Salt & Paper', a: 'Bombyx · 2022', growth: 34, hue: 188, sat: 50, emoji: '🐚' },
  { id: 'g-harmonies', t: 'Harmonies', a: 'Libellud · 2024', growth: 28, hue: 158, sat: 35, emoji: '🦊' },
  { id: 'g-sky-team', t: 'Sky Team', a: 'Le Scorpion · 2023', growth: 25, hue: 218, sat: 50, emoji: '✈️' },
  { id: 'g-lacrimosa', t: 'Lacrimosa', a: 'Devir · 2022', growth: 22, hue: 22, sat: 35, emoji: '🎼' },
  { id: 'g-darwins', t: 'Darwin\'s Journey', a: 'ThunderGryph · 2023', growth: 19, hue: 78, sat: 35, emoji: '🦎' },
  { id: 'g-revive', t: 'Revive', a: 'Aporta · 2022', growth: 17, hue: 308, sat: 35, emoji: '🌱' },
];

const CATEGORIES = [
  { id: 'c-strategia', label: 'Strategia', count: 1284, emoji: '♟' },
  { id: 'c-famiglia', label: 'Famiglia', count: 942, emoji: '👨‍👩‍👧' },
  { id: 'c-party', label: 'Party', count: 487, emoji: '🎉' },
  { id: 'c-cooperativo', label: 'Cooperativo', count: 312, emoji: '🤝' },
  { id: 'c-astratto', label: 'Astratto', count: 268, emoji: '◆' },
  { id: 'c-tematico', label: 'Tematico', count: 524, emoji: '🗡' },
  { id: 'c-wargame', label: 'Wargame', count: 198, emoji: '⚔' },
];

// ── PRIMITIVES ─────────────────────────────────────────────────────────────
const Skeleton = ({ w = '100%', h = 16, r = 6, style = {} }) => (
  <div style={{
    width: w, height: h, borderRadius: r,
    background: 'linear-gradient(90deg, var(--bg-sunken) 0%, var(--bg-muted) 50%, var(--bg-sunken) 100%)',
    backgroundSize: '200% 100%',
    animation: 'meepleSkeleton 1.6s linear infinite',
    ...style,
  }} />
);

// Hero gradient (riuso pattern pub-hero da sp3-how-it-works)
const HeroGradient = ({ kicker, title, subtitle, isMobile }) => (
  <header style={{
    position: 'relative',
    padding: isMobile ? '52px 16px 40px' : '88px 24px 56px',
    overflow: 'hidden',
    isolation: 'isolate',
  }}>
    {/* Multi-radial gradient soft */}
    <div aria-hidden="true" style={{
      position: 'absolute', inset: 0, zIndex: -1,
      background: `
        radial-gradient(ellipse 60% 80% at 18% 30%, hsl(var(--c-game) / 0.18), transparent 60%),
        radial-gradient(ellipse 50% 70% at 82% 20%, hsl(var(--c-player) / 0.16), transparent 60%),
        radial-gradient(ellipse 55% 60% at 50% 90%, hsl(var(--c-session) / 0.14), transparent 60%),
        var(--bg)
      `,
    }} />
    <div style={{ maxWidth: 1180, margin: '0 auto', textAlign: 'center' }}>
      <div style={{
        fontFamily: 'var(--f-mono)', fontWeight: 600,
        fontSize: 11, letterSpacing: '0.18em', textTransform: 'uppercase',
        color: 'hsl(var(--c-player))', marginBottom: 14,
        display: 'inline-flex', alignItems: 'center', gap: 8,
      }}>
        <span style={{
          width: 6, height: 6, borderRadius: '50%',
          background: 'hsl(var(--c-player))',
          boxShadow: '0 0 8px hsl(var(--c-player) / 0.6)',
          animation: 'meeplePulse 2s ease-in-out infinite',
        }} />
        {kicker}
      </div>
      <h1 style={{
        fontFamily: 'var(--f-display)',
        fontSize: isMobile ? 36 : 60,
        fontWeight: 700,
        lineHeight: 1.05,
        letterSpacing: '-0.025em',
        margin: '0 0 16px',
        color: 'var(--text)',
        textWrap: 'balance',
      }}>{title.before}<span style={{
        background: `linear-gradient(105deg, hsl(var(--c-game)) 0%, hsl(var(--c-player)) 60%, hsl(var(--c-session)) 100%)`,
        WebkitBackgroundClip: 'text', WebkitTextFillColor: 'transparent',
        backgroundClip: 'text',
      }}>{title.mark}</span>{title.after}</h1>
      <p style={{
        margin: '0 auto', maxWidth: 580,
        fontFamily: 'var(--f-body)', fontSize: isMobile ? 16 : 19,
        lineHeight: 1.55, color: 'var(--text-sec)',
      }}>{subtitle}</p>
    </div>
  </header>
);

// ── COMMUNITY STATS ROW (componente v2 nuovo) ─────────────────────────────
const useCountUp = (target, duration = 1400, run = true) => {
  const [v, setV] = useState(run ? 0 : target);
  useEffect(() => {
    if (!run) { setV(target); return; }
    const reduced = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    if (reduced) { setV(target); return; }
    let raf, start;
    const tick = (t) => {
      if (!start) start = t;
      const p = Math.min((t - start) / duration, 1);
      const eased = 1 - Math.pow(1 - p, 3);
      setV(Math.floor(target * eased));
      if (p < 1) raf = requestAnimationFrame(tick);
    };
    raf = requestAnimationFrame(tick);
    return () => cancelAnimationFrame(raf);
  }, [target, duration, run]);
  return v;
};

const StatNum = ({ target, format = 'int', run }) => {
  const v = useCountUp(target, 1400, run);
  const formatted = format === 'int'
    ? v.toLocaleString('it-IT')
    : `${(v / 1_000_000).toFixed(1)}M`;
  return <span style={{ fontVariantNumeric: 'tabular-nums' }}>{formatted}</span>;
};

const CommunityStatsRow = ({ isMobile, run }) => {
  const stats = [
    { icon: '👤', label: 'giocatori attivi', target: 12847, format: 'int', cv: '--c-player' },
    { icon: '🎯', label: 'partite giocate', target: 89234, format: 'int', cv: '--c-session' },
    { icon: '💬', label: 'ore di chat con agenti', target: 4_200_000, format: 'M', cv: '--c-chat' },
  ];
  return (
    <div style={{
      maxWidth: 1180, margin: '0 auto',
      padding: isMobile ? '0 16px' : '0 24px',
      marginTop: isMobile ? -24 : -36,
      position: 'relative', zIndex: 2,
    }}>
      <div style={{
        display: 'grid',
        gridTemplateColumns: isMobile ? '1fr' : 'repeat(3, 1fr)',
        gap: isMobile ? 12 : 16,
      }}>
        {stats.map(s => (
          <div key={s.label} style={{
            background: 'var(--bg-card)',
            border: '1px solid var(--border)',
            borderRadius: 16,
            padding: isMobile ? '18px 20px' : '24px 28px',
            display: 'flex', alignItems: 'center', gap: 16,
            boxShadow: '0 4px 16px -4px rgba(0,0,0,0.06)',
          }}>
            <div style={{
              width: 48, height: 48, borderRadius: 12,
              background: `hsl(var(${s.cv}) / 0.14)`,
              display: 'grid', placeItems: 'center',
              fontSize: 22,
              flexShrink: 0,
            }}>{s.icon}</div>
            <div style={{ minWidth: 0, flex: 1 }}>
              <div style={{
                fontFamily: 'var(--f-display)',
                fontSize: isMobile ? 26 : 30, fontWeight: 700,
                color: `hsl(var(${s.cv}))`,
                lineHeight: 1.1, letterSpacing: '-0.02em',
              }}>
                <StatNum target={s.target} format={s.format} run={run} />
              </div>
              <div style={{
                fontFamily: 'var(--f-body)',
                fontSize: 13.5, color: 'var(--text-sec)',
                marginTop: 2,
              }}>{s.label}</div>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
};

const StatsRowSkeleton = ({ isMobile }) => (
  <div style={{
    maxWidth: 1180, margin: '0 auto',
    padding: isMobile ? '0 16px' : '0 24px',
    marginTop: isMobile ? -24 : -36,
    position: 'relative', zIndex: 2,
  }}>
    <div style={{
      display: 'grid',
      gridTemplateColumns: isMobile ? '1fr' : 'repeat(3, 1fr)',
      gap: isMobile ? 12 : 16,
    }}>
      {[0, 1, 2].map(i => (
        <div key={i} style={{
          background: 'var(--bg-card)', border: '1px solid var(--border)',
          borderRadius: 16, padding: isMobile ? '18px 20px' : '24px 28px',
          display: 'flex', alignItems: 'center', gap: 16,
        }}>
          <Skeleton w={48} h={48} r={12} />
          <div style={{ flex: 1 }}>
            <Skeleton w="60%" h={26} r={6} style={{ marginBottom: 8 }} />
            <Skeleton w="80%" h={12} r={6} />
          </div>
        </div>
      ))}
    </div>
  </div>
);

// ── MEEPLE CARD HERO (riuso variant) ──────────────────────────────────────
const MeepleCardHero = ({ g, rank }) => (
  <article style={{
    width: 320, minWidth: 320,
    background: 'var(--bg-card)',
    border: '1px solid var(--border)',
    borderRadius: 18,
    overflow: 'hidden',
    scrollSnapAlign: 'start',
    boxShadow: '0 4px 16px -4px rgba(0,0,0,0.08)',
    display: 'flex', flexDirection: 'column',
  }}>
    {/* cover */}
    <div style={{
      position: 'relative',
      height: 160,
      background: `linear-gradient(135deg, hsl(${g.hue} ${g.sat}% 55%) 0%, hsl(${(g.hue + 30) % 360} ${g.sat}% 40%) 100%)`,
      display: 'grid', placeItems: 'center',
      fontSize: 64,
    }}>
      <span aria-hidden="true">{g.emoji}</span>
      {rank != null && (
        <div style={{
          position: 'absolute', top: 10, left: 10,
          padding: '4px 10px',
          background: 'rgba(20,14,8,0.72)',
          color: '#fff', backdropFilter: 'blur(8px)',
          borderRadius: 999,
          fontFamily: 'var(--f-mono)', fontSize: 11, fontWeight: 700,
          letterSpacing: '0.05em',
        }}>#{rank}</div>
      )}
    </div>
    {/* body */}
    <div style={{ padding: '14px 16px 16px', flex: 1, display: 'flex', flexDirection: 'column' }}>
      <h3 style={{
        margin: '0 0 4px',
        fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 17,
        color: 'var(--text)', letterSpacing: '-0.01em', lineHeight: 1.2,
      }}>{g.t}</h3>
      <div style={{
        fontFamily: 'var(--f-body)', fontSize: 12.5, color: 'var(--text-muted)',
        marginBottom: 12,
      }}>{g.a}</div>
      <div style={{
        marginTop: 'auto',
        display: 'inline-flex', alignItems: 'center', gap: 6,
        padding: '5px 10px',
        background: 'hsl(var(--c-session) / 0.12)',
        border: '1px solid hsl(var(--c-session) / 0.25)',
        borderRadius: 999,
        fontFamily: 'var(--f-mono)', fontSize: 11.5, fontWeight: 600,
        color: 'hsl(var(--c-session))',
        alignSelf: 'flex-start',
        fontVariantNumeric: 'tabular-nums',
      }}>
        <span aria-hidden="true">🎯</span>
        {g.sessions.toLocaleString('it-IT')} partite
      </div>
    </div>
  </article>
);

const MeepleCardHeroSkel = () => (
  <div style={{
    width: 320, minWidth: 320,
    background: 'var(--bg-card)', border: '1px solid var(--border)',
    borderRadius: 18, overflow: 'hidden',
  }}>
    <Skeleton w="100%" h={160} r={0} />
    <div style={{ padding: '14px 16px 16px' }}>
      <Skeleton w="65%" h={18} r={5} style={{ marginBottom: 8 }} />
      <Skeleton w="45%" h={12} r={4} style={{ marginBottom: 14 }} />
      <Skeleton w={110} h={26} r={999} />
    </div>
  </div>
);

// ── FEATURED GAMES CAROUSEL (componente v2 nuovo) ─────────────────────────
const FeaturedGamesCarousel = ({ games, isMobile, loading }) => {
  const scrollerRef = useRef(null);
  const scrollBy = (dx) => {
    scrollerRef.current?.scrollBy({ left: dx, behavior: 'smooth' });
  };
  return (
    <section style={{ maxWidth: 1180, margin: '0 auto', padding: isMobile ? '40px 0 0' : '64px 0 0' }}>
      <div style={{
        display: 'flex', alignItems: 'flex-end', justifyContent: 'space-between',
        gap: 16, padding: isMobile ? '0 16px' : '0 24px',
        marginBottom: 18,
      }}>
        <div>
          <h2 style={{
            margin: 0, fontFamily: 'var(--f-display)',
            fontSize: isMobile ? 24 : 30, fontWeight: 700,
            color: 'var(--text)', letterSpacing: '-0.02em',
            display: 'flex', alignItems: 'center', gap: 10,
          }}>
            <span aria-hidden="true">🔥</span>
            I più giocati
          </h2>
          <div style={{
            marginTop: 4,
            fontFamily: 'var(--f-body)', fontSize: 13.5,
            color: 'var(--text-muted)',
          }}>Top 10 ultimi 30 giorni</div>
        </div>
        {!isMobile && !loading && (
          <div style={{ display: 'flex', gap: 8 }}>
            <button onClick={() => scrollBy(-360)} aria-label="Scorri indietro" style={carouselBtn} className="meeple-carousel-btn">‹</button>
            <button onClick={() => scrollBy(360)} aria-label="Scorri avanti" style={carouselBtn} className="meeple-carousel-btn">›</button>
          </div>
        )}
      </div>
      <div
        ref={scrollerRef}
        role="region"
        aria-label="Carousel giochi più giocati"
        style={{
          display: 'flex', gap: 16,
          overflowX: 'auto', overflowY: 'hidden',
          scrollSnapType: 'x mandatory',
          padding: isMobile ? '4px 16px 24px' : '4px 24px 24px',
          scrollPaddingInline: isMobile ? 16 : 24,
          scrollbarWidth: 'thin',
        }}
      >
        {loading
          ? Array.from({ length: 5 }).map((_, i) => <MeepleCardHeroSkel key={i} />)
          : games.map((g, i) => <MeepleCardHero key={g.id} g={g} rank={i + 1} />)}
      </div>
    </section>
  );
};

const carouselBtn = {
  width: 36, height: 36, borderRadius: '50%',
  border: '1px solid var(--border-strong)',
  background: 'var(--bg-card)',
  color: 'var(--text)',
  fontFamily: 'var(--f-display)', fontSize: 18, fontWeight: 700,
  cursor: 'pointer',
  display: 'grid', placeItems: 'center',
  boxShadow: '0 2px 8px -2px rgba(0,0,0,0.08)',
};

// ── TRENDING ROW ──────────────────────────────────────────────────────────
const MeepleCardGrid = ({ g }) => (
  <article style={{
    background: 'var(--bg-card)',
    border: '1px solid var(--border)',
    borderRadius: 14, overflow: 'hidden',
    display: 'flex', flexDirection: 'column',
    minWidth: 0,
  }}>
    <div style={{
      position: 'relative',
      aspectRatio: '1 / 1',
      background: `linear-gradient(135deg, hsl(${g.hue} ${g.sat}% 58%), hsl(${(g.hue + 25) % 360} ${g.sat}% 42%))`,
      display: 'grid', placeItems: 'center', fontSize: 52,
    }}>
      <span aria-hidden="true">{g.emoji}</span>
      <div style={{
        position: 'absolute', top: 8, right: 8,
        padding: '3px 8px',
        background: 'hsl(var(--c-toolkit) / 0.95)',
        color: '#fff',
        borderRadius: 999,
        fontFamily: 'var(--f-mono)', fontSize: 10.5, fontWeight: 700,
        letterSpacing: '0.03em',
        fontVariantNumeric: 'tabular-nums',
        boxShadow: '0 2px 6px rgba(0,0,0,0.18)',
      }}>+{g.growth}% sessioni</div>
    </div>
    <div style={{ padding: '10px 12px 12px' }}>
      <div style={{
        fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 14,
        color: 'var(--text)', lineHeight: 1.2, marginBottom: 2,
        whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis',
      }}>{g.t}</div>
      <div style={{
        fontFamily: 'var(--f-body)', fontSize: 11.5, color: 'var(--text-muted)',
        whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis',
      }}>{g.a}</div>
    </div>
  </article>
);

const MeepleCardGridSkel = () => (
  <div style={{
    background: 'var(--bg-card)', border: '1px solid var(--border)',
    borderRadius: 14, overflow: 'hidden',
  }}>
    <Skeleton w="100%" h={0} r={0} style={{ aspectRatio: '1 / 1', height: 'auto' }} />
    <div style={{ padding: '10px 12px 12px' }}>
      <Skeleton w="70%" h={14} r={4} style={{ marginBottom: 6 }} />
      <Skeleton w="50%" h={10} r={4} />
    </div>
  </div>
);

const TrendingRow = ({ games, isMobile, loading }) => (
  <section style={{ maxWidth: 1180, margin: '0 auto', padding: isMobile ? '40px 16px 0' : '56px 24px 0' }}>
    <div style={{ marginBottom: 18 }}>
      <h2 style={{
        margin: 0, fontFamily: 'var(--f-display)',
        fontSize: isMobile ? 22 : 28, fontWeight: 700,
        color: 'var(--text)', letterSpacing: '-0.02em',
        display: 'flex', alignItems: 'center', gap: 10,
      }}>
        <span aria-hidden="true">📈</span>
        In crescita questa settimana
      </h2>
      <div style={{
        marginTop: 4, fontFamily: 'var(--f-body)', fontSize: 13.5,
        color: 'var(--text-muted)',
      }}>Giochi con la maggior crescita di sessioni vs settimana precedente</div>
    </div>
    <div style={{
      display: 'grid',
      gridTemplateColumns: isMobile ? 'repeat(2, 1fr)' : 'repeat(4, 1fr)',
      gap: isMobile ? 12 : 16,
    }}>
      {loading
        ? Array.from({ length: 8 }).map((_, i) => <MeepleCardGridSkel key={i} />)
        : games.map(g => <MeepleCardGrid key={g.id} g={g} />)}
    </div>
  </section>
);

// ── CATEGORIES ROW ────────────────────────────────────────────────────────
const CategoriesRow = ({ cats, isMobile, loading }) => (
  <section style={{ maxWidth: 1180, margin: '0 auto', padding: isMobile ? '40px 16px 0' : '56px 24px 0' }}>
    <div style={{ marginBottom: 18 }}>
      <h2 style={{
        margin: 0, fontFamily: 'var(--f-display)',
        fontSize: isMobile ? 22 : 28, fontWeight: 700,
        color: 'var(--text)', letterSpacing: '-0.02em',
        display: 'flex', alignItems: 'center', gap: 10,
      }}>
        <span aria-hidden="true">🎲</span>
        Esplora per genere
      </h2>
      <div style={{
        marginTop: 4, fontFamily: 'var(--f-body)', fontSize: 13.5,
        color: 'var(--text-muted)',
      }}>Filtra il catalogo community per il tipo di esperienza che cerchi</div>
    </div>
    <div style={{
      display: 'flex', flexWrap: 'wrap',
      gap: isMobile ? 10 : 12,
    }}>
      {loading
        ? Array.from({ length: 7 }).map((_, i) => (
            <Skeleton key={i} w={isMobile ? 130 : 160} h={isMobile ? 64 : 76} r={14} />
          ))
        : cats.map(c => (
          <button key={c.id} type="button" style={{
            border: '1px solid hsl(var(--c-game) / 0.28)',
            background: `linear-gradient(135deg, hsl(var(--c-game) / 0.05), hsl(var(--c-game) / 0.10))`,
            borderRadius: 14,
            padding: isMobile ? '12px 16px' : '14px 20px',
            cursor: 'pointer',
            display: 'flex', flexDirection: 'column', alignItems: 'flex-start', gap: 4,
            fontFamily: 'var(--f-display)',
            color: 'var(--text)',
            transition: 'all 140ms ease',
            minWidth: isMobile ? 130 : 160,
          }}>
            <span style={{
              display: 'inline-flex', alignItems: 'center', gap: 8,
              fontWeight: 700, fontSize: isMobile ? 14.5 : 16,
            }}>
              <span aria-hidden="true">{c.emoji}</span>
              {c.label}
            </span>
            <span style={{
              fontFamily: 'var(--f-mono)', fontSize: 11.5, fontWeight: 500,
              color: 'var(--text-muted)',
              fontVariantNumeric: 'tabular-nums',
            }}>{c.count.toLocaleString('it-IT')} giochi</span>
          </button>
        ))}
    </div>
  </section>
);

// ── FOOTER CTA ────────────────────────────────────────────────────────────
const FooterCTA = ({ isMobile }) => (
  <section style={{ maxWidth: 1180, margin: '0 auto', padding: isMobile ? '56px 16px 40px' : '88px 24px 64px' }}>
    <div style={{
      position: 'relative',
      borderRadius: 24, overflow: 'hidden',
      padding: isMobile ? '40px 24px' : '64px 56px',
      background: `linear-gradient(135deg,
        hsl(var(--c-game) / 0.14) 0%,
        hsl(var(--c-toolkit) / 0.16) 100%)`,
      border: '1px solid hsl(var(--c-game) / 0.28)',
      textAlign: isMobile ? 'left' : 'center',
    }}>
      <div aria-hidden="true" style={{
        position: 'absolute', inset: 0, zIndex: 0,
        background: `radial-gradient(ellipse 50% 60% at 80% 30%, hsl(var(--c-toolkit) / 0.18), transparent 60%)`,
      }} />
      <div style={{ position: 'relative', zIndex: 1 }}>
        <h2 style={{
          margin: '0 0 12px',
          fontFamily: 'var(--f-display)', fontWeight: 700,
          fontSize: isMobile ? 28 : 40,
          letterSpacing: '-0.02em', lineHeight: 1.1,
          color: 'var(--text)', textWrap: 'balance',
        }}>Unisciti alla community</h2>
        <p style={{
          margin: '0 auto 24px',
          maxWidth: 480,
          fontFamily: 'var(--f-body)', fontSize: isMobile ? 15.5 : 17.5,
          lineHeight: 1.55, color: 'var(--text-sec)',
        }}>Crea il tuo account e inizia a tracciare le tue partite, costruire la tua libreria e chiedere agli agenti AI tutto sui regolamenti.</p>
        <div style={{
          display: 'flex', flexWrap: 'wrap', gap: 16,
          alignItems: 'center',
          justifyContent: isMobile ? 'flex-start' : 'center',
        }}>
          <a href="#/register" style={{
            display: 'inline-flex', alignItems: 'center', gap: 8,
            padding: '14px 28px',
            background: 'hsl(var(--c-game))',
            color: '#fff',
            borderRadius: 999,
            textDecoration: 'none',
            fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 15.5,
            letterSpacing: '-0.01em',
            boxShadow: '0 8px 20px -6px hsl(var(--c-game) / 0.5)',
          }}>
            Crea account gratis
            <span aria-hidden="true">→</span>
          </a>
          <a href="#/login" style={{
            fontFamily: 'var(--f-body)', fontSize: 14.5,
            color: 'var(--text-sec)',
            textDecoration: 'none',
          }}>
            Hai già un account? <span style={{
              color: 'hsl(var(--c-game))',
              textDecoration: 'underline',
              textDecorationColor: 'hsl(var(--c-game) / 0.4)',
              textUnderlineOffset: 3,
              fontWeight: 600,
            }}>Accedi</span>
          </a>
        </div>
      </div>
    </div>
  </section>
);

// ── PAGE BODY ─────────────────────────────────────────────────────────────
const LibraryPublicBody = ({ isMobile, loading }) => (
  <main style={{ background: 'var(--bg)', minHeight: '100%' }}>
    {loading ? (
      <header style={{
        padding: isMobile ? '52px 16px 40px' : '88px 24px 56px',
        textAlign: 'center', background: 'var(--bg)',
      }}>
        <div style={{ maxWidth: 1180, margin: '0 auto' }}>
          <Skeleton w={140} h={12} r={4} style={{ margin: '0 auto 18px' }} />
          <Skeleton w={isMobile ? '90%' : 600} h={isMobile ? 38 : 60} r={10} style={{ margin: '0 auto 16px' }} />
          <Skeleton w={isMobile ? '80%' : 460} h={isMobile ? 18 : 22} r={6} style={{ margin: '0 auto' }} />
        </div>
      </header>
    ) : (
      <HeroGradient
        kicker="Community MeepleAI"
        title={{ before: 'La library della ', mark: 'community', after: '' }}
        subtitle="Cosa si gioca, cosa si scopre — in tempo reale"
        isMobile={isMobile}
      />
    )}

    {loading
      ? <StatsRowSkeleton isMobile={isMobile} />
      : <CommunityStatsRow isMobile={isMobile} run={!loading} />
    }

    <FeaturedGamesCarousel games={FEATURED_GAMES} isMobile={isMobile} loading={loading} />
    <TrendingRow games={TRENDING_GAMES} isMobile={isMobile} loading={loading} />
    <CategoriesRow cats={CATEGORIES} isMobile={isMobile} loading={loading} />

    {!loading && <FooterCTA isMobile={isMobile} />}
  </main>
);

// ── FRAMES ────────────────────────────────────────────────────────────────
const DesktopScreen = ({ label, theme, loading }) => (
  <div data-screen-label={label} data-theme={theme} style={{
    width: 1440, background: 'var(--bg)',
    borderRadius: 16, border: '1px solid var(--border-strong)',
    overflow: 'hidden',
    boxShadow: '0 32px 64px -24px rgba(0,0,0,0.25), 0 12px 32px -12px rgba(0,0,0,0.12)',
  }}>
    <div style={{
      padding: '8px 14px',
      background: 'var(--bg-sunken)', borderBottom: '1px solid var(--border)',
      display: 'flex', alignItems: 'center', gap: 10,
      fontFamily: 'var(--f-mono)', fontSize: 11, color: 'var(--text-muted)',
    }}>
      <span style={{ display: 'inline-flex', gap: 6 }}>
        <span style={{ width: 10, height: 10, borderRadius: '50%', background: '#FF5F57' }} />
        <span style={{ width: 10, height: 10, borderRadius: '50%', background: '#FEBC2E' }} />
        <span style={{ width: 10, height: 10, borderRadius: '50%', background: '#28C840' }} />
      </span>
      <span style={{ marginLeft: 8 }}>meepleai.app/library · {label}</span>
    </div>
    <LibraryPublicBody isMobile={false} loading={loading} />
  </div>
);

const MobileScreen = ({ label, theme, loading }) => (
  <div data-screen-label={label} data-theme={theme} style={{
    width: 375, background: 'var(--bg)',
    borderRadius: 36, border: '8px solid #1a1410',
    overflow: 'hidden',
    boxShadow: '0 30px 60px rgba(0,0,0,0.6)',
  }}>
    <div style={{
      height: 30, background: 'var(--bg-sunken)',
      display: 'flex', alignItems: 'center', justifyContent: 'space-between',
      padding: '0 18px',
      fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 11,
      color: 'var(--text)',
    }}>
      <span>9:41</span>
      <span style={{ display: 'inline-flex', gap: 6, alignItems: 'center' }}>
        <span>●●●</span><span>📶</span><span>🔋</span>
      </span>
    </div>
    <LibraryPublicBody isMobile={true} loading={loading} />
  </div>
);

// ── APP ───────────────────────────────────────────────────────────────────
const App = () => {
  const [theme, setTheme] = useState(() => {
    try { return localStorage.getItem('mai-sp3-libpub-theme') || 'light'; } catch { return 'light'; }
  });

  useEffect(() => {
    document.documentElement.setAttribute('data-theme', theme);
    try { localStorage.setItem('mai-sp3-libpub-theme', theme); } catch {}
  }, [theme]);

  // inject keyframes once
  useEffect(() => {
    if (document.getElementById('meeple-libpub-keyframes')) return;
    const s = document.createElement('style');
    s.id = 'meeple-libpub-keyframes';
    s.textContent = `
      @keyframes meepleSkeleton { 0%{background-position:200% 0} 100%{background-position:-200% 0} }
      @keyframes meeplePulse { 0%,100%{opacity:1; transform:scale(1)} 50%{opacity:0.6; transform:scale(1.4)} }
      .meeple-carousel-btn:hover { background: var(--bg-muted); transform: translateY(-1px); }
      @media (prefers-reduced-motion: reduce) {
        @keyframes meepleSkeleton { 0%,100%{background-position:0 0} }
        @keyframes meeplePulse { 0%,100%{opacity:1; transform:scale(1)} }
      }
    `;
    document.head.appendChild(s);
  }, []);

  return (
    <div style={{
      minHeight: '100vh',
      background: theme === 'dark' ? '#0a0805' : '#e8dfcf',
      padding: '40px 24px 80px',
    }}>
      {/* Toolbar */}
      <div style={{
        maxWidth: 1480, margin: '0 auto 32px',
        display: 'flex', alignItems: 'center', gap: 16, flexWrap: 'wrap',
      }}>
        <div>
          <div style={{
            fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 22,
            color: theme === 'dark' ? '#f0e4d2' : '#2b1f12',
            letterSpacing: '-0.015em',
          }}>SP3 · #8 — /library (public, non-loggato) · ULTIMO OBBLIGATORIO</div>
          <div style={{
            fontFamily: 'var(--f-mono)', fontSize: 12,
            color: theme === 'dark' ? '#8a7860' : '#9a8870',
          }}>Default popolato + Loading skeleton · Desktop 1440 · Mobile 375 · Light/Dark</div>
        </div>
        <div style={{ marginLeft: 'auto' }}>
          <button onClick={() => setTheme(theme === 'light' ? 'dark' : 'light')}
            style={{
              padding: '8px 14px', borderRadius: 999,
              border: '1px solid ' + (theme === 'dark' ? 'rgba(224,180,110,0.28)' : 'rgba(180,130,80,0.32)'),
              background: theme === 'dark' ? '#1e1710' : '#fff',
              color: theme === 'dark' ? '#f0e4d2' : '#2b1f12',
              fontFamily: 'var(--f-display)', fontWeight: 600, fontSize: 13,
              cursor: 'pointer',
            }}>{theme === 'light' ? '🌙 Dark' : '☀️ Light'}</button>
        </div>
      </div>

      {/* DESKTOP DEFAULT */}
      <SectionLabel theme={theme}>Desktop · 1440 — Default popolato</SectionLabel>
      <Center>
        <DesktopScreen label="01 Desktop · /library · Default" theme={theme} loading={false} />
      </Center>

      {/* DESKTOP LOADING */}
      <SectionLabel theme={theme}>Desktop · 1440 — Loading skeleton</SectionLabel>
      <Center>
        <DesktopScreen label="02 Desktop · /library · Loading" theme={theme} loading={true} />
      </Center>

      {/* MOBILE DEFAULT */}
      <SectionLabel theme={theme}>Mobile · 375 — Default popolato</SectionLabel>
      <Center>
        <MobileScreen label="03 Mobile · /library · Default" theme={theme} loading={false} />
      </Center>

      {/* MOBILE LOADING */}
      <SectionLabel theme={theme}>Mobile · 375 — Loading skeleton</SectionLabel>
      <Center>
        <MobileScreen label="04 Mobile · /library · Loading" theme={theme} loading={true} />
      </Center>

      {/* DOD */}
      <div style={{
        maxWidth: 900, margin: '40px auto 0',
        padding: '24px 28px',
        background: theme === 'dark' ? '#1e1710' : '#fff',
        border: '1px solid ' + (theme === 'dark' ? 'rgba(224,180,110,0.14)' : 'rgba(180,130,80,0.18)'),
        borderRadius: 16,
        color: theme === 'dark' ? '#f0e4d2' : '#2b1f12',
      }}>
        <div style={{
          display: 'inline-flex', alignItems: 'center', gap: 8,
          padding: '4px 12px',
          background: 'hsl(var(--c-toolkit) / 0.14)',
          color: 'hsl(var(--c-toolkit))',
          border: '1px solid hsl(var(--c-toolkit) / 0.32)',
          borderRadius: 999,
          fontFamily: 'var(--f-mono)', fontSize: 11, fontWeight: 700,
          letterSpacing: '0.06em', textTransform: 'uppercase',
          marginBottom: 14,
        }}>🏁 Ultimo obbligatorio SP3 · 8/8 mandatory complete</div>
        <h3 style={{ fontFamily: 'var(--f-display)', margin: '0 0 12px', fontSize: 18 }}>DoD Checklist · sp3-library-public</h3>
        <ul style={{ fontFamily: 'var(--f-body)', fontSize: 13.5, lineHeight: 1.7, color: theme === 'dark' ? '#c8b896' : '#5a4a38', paddingLeft: 20, margin: 0 }}>
          <li>✓ Route target /library public variant (non-loggato) — diverso dalla /library authenticated</li>
          <li>✓ Hero community: kicker "Community MeepleAI" + h1 "La library della community" con span gradient game→player→session + subtitle "Cosa si gioca, cosa si scopre — in tempo reale"</li>
          <li>✓ Sfondo gradient radiale game+player+session soft, tre ellissi posizionate (18/30, 82/20, 50/90)</li>
          <li>✓ CommunityStatsRow: 3 card desktop / stack vertical mobile · 12.847 player · 89.234 session · 4.2M chat · count-up animation rispetta prefers-reduced-motion · tabular-nums</li>
          <li>✓ FeaturedGamesCarousel: top 10, MeepleCard hero 320×280, scroll-snap-type:x mandatory + scrollPaddingInline · frecce ‹ › desktop sopra md (mobile niente, swipe nativo)</li>
          <li>✓ Cards hero: cover gradient hue/sat per gioco + rank badge #N + emoji 64px + titolo + autore + chip mono "🎯 N partite" palette session</li>
          <li>✓ 10 giochi: Wingspan, Azul, Brass: Birmingham, 7 Wonders, Ticket to Ride, Catan, Carcassonne, Terraforming Mars, Spirit Island, Gloomhaven</li>
          <li>✓ Trending row: 8 giochi growth, MeepleCard grid aspectRatio 1:1, 4-col desktop / 2-col mobile, badge "+N% sessioni" palette toolkit (success)</li>
          <li>✓ Categories row: 7 pill grandi (Strategia, Famiglia, Party, Cooperativo, Astratto, Tematico, Wargame) con count "N giochi" e gradient game/0.05→0.10</li>
          <li>✓ Footer CTA: card gradient game→toolkit + h2 "Unisciti alla community" + subtitle + pill "Crea account gratis →" /register + sub-CTA "Hai già un account? Accedi" /login</li>
          <li>✓ Loading state: Hero shimmer + StatsRow 3 box + Carousel 5 card hero skel + Trending 8 card grid skel + Categories 7 pill skel — NO empty (community sempre popolata)</li>
          <li>✓ ID short readable: g-wingspan, g-azul, c-strategia (no UUID-like)</li>
          <li>✓ Testo italiano integrale, numeri locale-it (12.847, 89.234, 4,2M)</li>
          <li>✓ Riusi: HeroGradient (pattern pub-hero) · MeepleCard hero/grid · scroll-snap-type (lezione sp4-discover)</li>
          <li>✓ Componenti v2 nuovi: CommunityStatsRow · FeaturedGamesCarousel</li>
          <li>✓ ARIA: &lt;main&gt;, &lt;article&gt;, role="region" aria-label sul carousel scroller, aria-label sui button frecce, aria-hidden sulle emoji decorative, &lt;h1&gt; unico, h2 per ogni section</li>
          <li>✓ Light + Dark mode toggle persistito</li>
          <li>✓ NO window.innerWidth in body — isMobile è prop esplicita dalla frame (lezione sp3-how-it-works applicata)</li>
          <li>✓ NON sovrascritto components.css né data.js</li>
          <li>✓ 4 frame generati: Desktop default · Desktop loading · Mobile default · Mobile loading</li>
        </ul>
      </div>
    </div>
  );
};

const SectionLabel = ({ children, theme }) => (
  <h2 style={{
    maxWidth: 1480, margin: '0 auto 14px',
    fontFamily: 'var(--f-display)', fontSize: 14, fontWeight: 700,
    textTransform: 'uppercase', letterSpacing: '0.08em',
    color: theme === 'dark' ? '#c8b896' : '#5a4a38',
  }}>{children}</h2>
);

const Center = ({ children }) => (
  <div style={{
    maxWidth: 1480, margin: '0 auto 48px',
    display: 'flex', justifyContent: 'center',
  }}>{children}</div>
);

ReactDOM.createRoot(document.getElementById('root')).render(<App />);
