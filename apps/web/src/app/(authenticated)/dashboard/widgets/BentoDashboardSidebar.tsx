'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';

import { cn } from '@/lib/utils';

const SIDEBAR_NAV = [
  { icon: '🏠', label: 'Dashboard', href: '/dashboard' },
  { icon: '📚', label: 'Libreria', href: '/library?tab=collection' },
  { icon: '🎲', label: 'Sessioni', href: '/sessions' },
  { icon: '💬', label: 'Chat AI', href: '/chat' },
  { icon: '📄', label: 'Regole KB', href: '/library?tab=private' },
  { icon: '👥', label: 'Giocatori', href: '/players' },
];

const SIDEBAR_MANAGE = [
  { icon: '📊', label: 'Analytics', href: '/play-records' },
  { icon: '⚙️', label: 'Impostazioni', href: '/settings' },
];

export function BentoDashboardSidebar() {
  const pathname = usePathname();
  const isActive = (href: string) => {
    const path = href.split('?')[0] ?? '';
    return path === '/dashboard' ? pathname === '/dashboard' : pathname.startsWith(path);
  };

  return (
    <aside className="hidden lg:flex w-[200px] min-w-[200px] h-full bg-card border-r border-border/40 flex-col py-3 px-2 overflow-y-auto shrink-0">
      <p className="font-mono text-[9px] uppercase tracking-widest text-muted-foreground/40 px-2 pb-1 pt-1">
        Navigazione
      </p>
      {SIDEBAR_NAV.map(item => (
        <Link
          key={item.href}
          href={item.href}
          className={cn(
            'flex items-center gap-2 px-2 py-1.5 rounded-lg font-quicksand text-[12px] font-semibold transition-colors',
            isActive(item.href)
              ? 'text-[hsl(25,95%,45%)] bg-[rgba(245,130,31,0.1)]'
              : 'text-muted-foreground hover:text-foreground hover:bg-muted/40'
          )}
        >
          <span className="text-sm w-5 text-center shrink-0">{item.icon}</span>
          <span>{item.label}</span>
        </Link>
      ))}
      <div className="h-px bg-border/40 my-2 mx-2" />
      <p className="font-mono text-[9px] uppercase tracking-widest text-muted-foreground/40 px-2 pb-1">
        Gestione
      </p>
      {SIDEBAR_MANAGE.map(item => (
        <Link
          key={item.href}
          href={item.href}
          className={cn(
            'flex items-center gap-2 px-2 py-1.5 rounded-lg font-quicksand text-[12px] font-semibold transition-colors',
            isActive(item.href)
              ? 'text-[hsl(25,95%,45%)] bg-[rgba(245,130,31,0.1)]'
              : 'text-muted-foreground hover:text-foreground hover:bg-muted/40'
          )}
        >
          <span className="text-sm w-5 text-center shrink-0">{item.icon}</span>
          <span>{item.label}</span>
        </Link>
      ))}
    </aside>
  );
}
