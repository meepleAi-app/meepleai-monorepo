'use client';

import {
  Dice5,
  Bot,
  MessageSquare,
  BookOpen,
  Trophy,
  CalendarDays,
  Bell,
  Settings,
} from 'lucide-react';
import Link from 'next/link';

const DOMAINS = [
  { label: 'Games', icon: Dice5, href: '/discover', color: '25 95% 45%' },
  { label: 'Agents', icon: Bot, href: '/agents', color: '38 92% 50%' },
  { label: 'Chat', icon: MessageSquare, href: '/ask', color: '220 80% 55%' },
  { label: 'Library', icon: BookOpen, href: '/library', color: '168 76% 42%' },
  { label: 'Leaderboards', icon: Trophy, href: '/badges', color: '262 83% 58%' },
  { label: 'Sessions', icon: CalendarDays, href: '/sessions', color: '240 60% 55%' },
  { label: 'Notifications', icon: Bell, href: '/notifications', color: '350 89% 60%' },
  { label: 'Settings', icon: Settings, href: '/settings', color: '220 70% 50%' },
] as const;

interface DomainHubProps {
  userName?: string;
}

export function DomainHub({ userName }: DomainHubProps) {
  return (
    <div className="flex flex-col gap-6 p-6">
      {userName && <h1 className="text-xl font-heading font-bold">Welcome back, {userName}!</h1>}
      <div className="grid grid-cols-2 gap-4">
        {DOMAINS.map(({ label, icon: Icon, href, color }) => (
          <Link
            key={label}
            href={href}
            className="aspect-square rounded-2xl flex flex-col items-center justify-center gap-2 transition-transform active:scale-95"
            style={{ backgroundColor: `hsl(${color} / 0.1)` }}
          >
            <Icon size={32} style={{ color: `hsl(${color})` }} />
            <span className="text-sm font-medium">{label}</span>
          </Link>
        ))}
      </div>
    </div>
  );
}
