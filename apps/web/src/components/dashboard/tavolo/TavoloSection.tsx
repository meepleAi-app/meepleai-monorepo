'use client';
import { type ReactNode } from 'react';

interface TavoloSectionProps {
  icon: string;
  title: string;
  children: ReactNode;
}

export function TavoloSection({ icon, title, children }: TavoloSectionProps) {
  return (
    <section>
      <div className="mb-3 flex items-center gap-2">
        <span className="text-sm" aria-hidden="true">
          {icon}
        </span>
        <h2 className="text-sm font-semibold uppercase tracking-wider text-[#8b949e]">{title}</h2>
        <div className="h-px flex-1 bg-[#30363d]" />
      </div>
      {children}
    </section>
  );
}
