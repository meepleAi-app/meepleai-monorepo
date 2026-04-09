'use client';

import Link from 'next/link';

export interface FriendPreview {
  id: string;
  name: string;
  status: string;
  presence: 'online' | 'idle' | 'offline';
}

interface FriendsRowProps {
  friends: FriendPreview[];
}

function initials(name: string): string {
  return name
    .split(/\s+/)
    .map(part => part[0] ?? '')
    .join('')
    .slice(0, 2)
    .toUpperCase();
}

const PRESENCE_BG: Record<FriendPreview['presence'], string> = {
  online: 'hsl(140 60% 45%)',
  idle: 'hsl(38 92% 55%)',
  offline: '#cbd5e1',
};

export function FriendsRow({ friends }: FriendsRowProps) {
  if (friends.length === 0) return null;

  return (
    <section className="mb-7">
      <div className="mb-3.5 flex items-center justify-between">
        <h3 className="flex items-center gap-3 font-quicksand text-[1.1rem] font-extrabold">
          <span
            aria-hidden
            className="inline-block h-[18px] w-1 rounded-sm"
            style={{ background: 'hsl(262 83% 58%)' }}
          />
          Amici attivi
        </h3>
        <Link
          href="/players"
          className="rounded-lg px-3 py-1.5 text-[0.78rem] font-bold text-[hsl(25_95%_40%)] transition-colors hover:bg-[hsla(25,95%,45%,0.08)]"
        >
          Vedi tutto →
        </Link>
      </div>
      <div className="grid grid-cols-4 gap-3">
        {friends.slice(0, 4).map(friend => (
          <div
            key={friend.id}
            className="relative flex cursor-pointer items-center gap-2.5 rounded-xl border border-[var(--nh-border-default)] bg-[rgba(255,252,248,0.8)] px-3.5 py-2.5 transition-all duration-200 hover:translate-x-0.5 hover:bg-white hover:shadow-[var(--shadow-warm-sm)]"
          >
            <span
              aria-hidden
              className="absolute left-0 top-1/2 h-[60%] w-[3px] -translate-y-1/2 rounded-full"
              style={{ background: 'hsl(262 83% 58%)' }}
            />
            <div
              aria-hidden
              className="flex h-[42px] w-[42px] shrink-0 items-center justify-center rounded-[10px] font-quicksand text-sm font-extrabold text-white"
              style={{ background: 'linear-gradient(135deg, hsl(262 78% 75%), hsl(262 83% 55%))' }}
            >
              {initials(friend.name)}
            </div>
            <div className="min-w-0 flex-1">
              <div className="font-quicksand text-[0.82rem] font-extrabold text-[var(--nh-text-primary)]">
                {friend.name}
              </div>
              <div className="truncate text-[0.7rem] text-[var(--nh-text-muted)]">
                {friend.status}
              </div>
            </div>
            <span
              aria-label={`Presence: ${friend.presence}`}
              className="h-2 w-2 shrink-0 rounded-full"
              style={{
                background: PRESENCE_BG[friend.presence],
                boxShadow: '0 0 0 2px rgba(255,255,255,0.9)',
              }}
            />
          </div>
        ))}
      </div>
    </section>
  );
}
