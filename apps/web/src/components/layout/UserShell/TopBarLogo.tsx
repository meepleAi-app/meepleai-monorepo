'use client';

import Link from 'next/link';

export function TopBarLogo() {
  return (
    <Link
      href="/"
      aria-label="MeepleAi home"
      className="flex items-center gap-2.5 font-quicksand font-extrabold text-[1.05rem] shrink-0"
    >
      <span
        aria-hidden
        className="flex h-8 w-8 items-center justify-center rounded-[10px] text-white font-extrabold text-sm"
        style={{
          background: 'linear-gradient(135deg, hsl(25 95% 52%), hsl(38 92% 55%))',
          boxShadow: '0 2px 8px hsla(25, 95%, 45%, 0.35)',
        }}
      >
        ◆
      </span>
      <span className="text-[var(--text-primary)]">Meeple</span>
      <span className="-ml-2.5" style={{ color: 'hsl(25 95% 42%)' }}>
        Ai
      </span>
    </Link>
  );
}
