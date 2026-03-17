'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';

import {
  DASHBOARD_SECTIONS,
  getActiveSection,
  isSidebarItemActive,
} from '@/config/admin-dashboard-navigation';
import { cn } from '@/lib/utils';

export function AdminTabSidebar() {
  const pathname = usePathname();
  const activeSection = getActiveSection(pathname);

  return (
    <div
      className={cn(
        'flex flex-col h-full w-[200px] border-r border-border/30',
        'bg-background/50 backdrop-blur-sm'
      )}
      data-testid="admin-tab-sidebar"
    >
      {/* Section tabs */}
      <div className="flex flex-col">
        {DASHBOARD_SECTIONS.map(section => {
          const isActive = activeSection?.id === section.id;
          const Icon = section.icon;

          return (
            <div key={section.id}>
              <Link
                href={section.baseRoute}
                className={cn(
                  'flex items-center gap-2 px-3 py-2.5 text-sm font-medium',
                  'transition-colors border-l-2',
                  isActive
                    ? 'bg-primary/5 border-primary text-primary'
                    : 'border-transparent text-muted-foreground hover:bg-muted/50 hover:text-foreground'
                )}
                data-testid={`admin-tab-${section.id}`}
                data-active={isActive ? 'true' : 'false'}
              >
                <Icon className="w-4 h-4 shrink-0" />
                <span className="font-quicksand">{section.label}</span>
              </Link>

              {/* Sub-items for active section */}
              {isActive && section.sidebarItems.length > 0 && (
                <div className="ml-4 border-l border-border/20 py-1">
                  {section.sidebarItems.map(item => {
                    const ItemIcon = item.icon;
                    const isItemActive = isSidebarItemActive(item, pathname);

                    return (
                      <Link
                        key={item.href}
                        href={item.href}
                        className={cn(
                          'flex items-center gap-2 px-3 py-1.5 text-xs',
                          'transition-colors',
                          isItemActive
                            ? 'text-primary font-medium'
                            : 'text-muted-foreground hover:text-foreground'
                        )}
                      >
                        <ItemIcon className="w-3.5 h-3.5 shrink-0" />
                        <span className="font-nunito">{item.label}</span>
                      </Link>
                    );
                  })}
                </div>
              )}
            </div>
          );
        })}
      </div>
    </div>
  );
}
