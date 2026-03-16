import { Download, HeartPulse, RefreshCw, Trash2 } from 'lucide-react';
import Link from 'next/link';

// ============================================================================
// Types
// ============================================================================

interface TechActionConfig {
  id: string;
  label: string;
  icon: React.ElementType;
  href: string;
}

// ============================================================================
// Constants
// ============================================================================

const TECH_ACTIONS: TechActionConfig[] = [
  {
    id: 'clear-cache',
    label: 'Clear Cache',
    icon: Trash2,
    href: '/admin/config',
  },
  {
    id: 'reindex-all',
    label: 'Reindex All',
    icon: RefreshCw,
    href: '/admin/ai/knowledge-base',
  },
  {
    id: 'system-health',
    label: 'System Health',
    icon: HeartPulse,
    href: '/admin/monitor',
  },
  {
    id: 'export-users',
    label: 'Export Users',
    icon: Download,
    href: '/admin/users',
  },
];

const LINK_CLASS =
  'inline-flex items-center gap-1.5 text-sm text-muted-foreground hover:text-foreground hover:bg-slate-100 dark:hover:bg-zinc-800 rounded-md px-3 py-1.5 transition-colors';

const SEPARATOR_CLASS = 'text-slate-300 dark:text-zinc-600';

// ============================================================================
// Component
// ============================================================================

export function TechActionsBar() {
  return (
    <div
      className="flex flex-wrap items-center gap-1 pt-2 border-t border-slate-200/60 dark:border-zinc-700/40"
      data-testid="tech-actions-bar"
    >
      {TECH_ACTIONS.map((action, index) => {
        const Icon = action.icon;

        return (
          <span key={action.id} className="inline-flex items-center gap-1">
            {index > 0 && <span className={SEPARATOR_CLASS}>·</span>}
            <Link
              href={action.href}
              className={LINK_CLASS}
              data-testid={`tech-action-${action.id}`}
            >
              <Icon className="h-4 w-4" />
              {action.label}
            </Link>
          </span>
        );
      })}
    </div>
  );
}

export default TechActionsBar;
