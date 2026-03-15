'use client';

interface HoloOverlayProps {
  disabled?: boolean;
}

export function HoloOverlay({ disabled = false }: HoloOverlayProps) {
  if (disabled) return null;

  return (
    <>
      {/* Layer 1: Rainbow gradient — mix-blend-mode: screen */}
      <div
        className="holo-rainbow animate pointer-events-none absolute inset-0 z-[7] rounded-[inherit] opacity-0 transition-opacity duration-500 group-hover:opacity-100"
        style={{
          background: `linear-gradient(115deg, transparent 20%, rgba(236,110,173,0.12) 32%, rgba(52,148,230,0.12) 40%, rgba(103,232,138,0.10) 48%, rgba(233,211,98,0.12) 56%, rgba(236,110,173,0.10) 64%, transparent 80%)`,
          backgroundSize: '200% 100%',
          mixBlendMode: 'screen',
        }}
        aria-hidden="true"
      />
      {/* Layer 2: Sparkle points */}
      <div
        className="holo-sparkle pointer-events-none absolute inset-0 z-[8] rounded-[inherit] opacity-0 transition-opacity duration-500 group-hover:opacity-100"
        style={{
          background: `radial-gradient(circle at 30% 20%, rgba(255,255,255,0.06) 0%, transparent 30%), radial-gradient(circle at 70% 60%, rgba(255,255,255,0.04) 0%, transparent 25%), radial-gradient(circle at 50% 80%, rgba(255,255,255,0.03) 0%, transparent 20%)`,
        }}
        aria-hidden="true"
      />
      {/* Layer 3: Rotating rainbow border via mask-composite */}
      <div
        className="holo-border animate pointer-events-none absolute z-[9] opacity-0 transition-opacity duration-500 group-hover:opacity-100"
        style={{
          inset: '-2px',
          borderRadius: 'calc(var(--radius-card, 16px) + 2px)',
          background: `conic-gradient(from 0deg, rgba(167,139,250,0.25), rgba(96,165,250,0.2), rgba(52,211,153,0.18), rgba(251,191,36,0.22), rgba(255,107,107,0.18), rgba(236,72,153,0.2), rgba(167,139,250,0.25))`,
          WebkitMask: 'linear-gradient(#fff 0 0) content-box, linear-gradient(#fff 0 0)',
          WebkitMaskComposite: 'xor',
          maskComposite: 'exclude',
          padding: '2px',
        }}
        aria-hidden="true"
      />
    </>
  );
}
