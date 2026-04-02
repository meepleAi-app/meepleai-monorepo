import { Database, ListOrdered, ShieldAlert, ClipboardList, ExternalLink } from 'lucide-react';
import Link from 'next/link';

const OPERATIONS_SECTIONS = [
  { id: 'resources', label: 'Resources', icon: Database, description: 'DB, storage, memory usage' },
  { id: 'queue', label: 'Queue', icon: ListOrdered, description: 'PDF processing jobs e status' },
  { id: 'emergency', label: 'Emergency', icon: ShieldAlert, description: 'LLM override controls' },
  { id: 'audit', label: 'Audit', icon: ClipboardList, description: 'Admin action trail completo' },
] as const;

export function OperationsLinkTab() {
  return (
    <div className="space-y-6">
      <div>
        <h2 className="font-quicksand text-lg font-semibold tracking-tight text-foreground">
          Operations Console
        </h2>
        <p className="mt-1 text-sm text-muted-foreground">
          Gestione risorse, coda PDF, controlli di emergenza e audit trail.
        </p>
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        {OPERATIONS_SECTIONS.map(section => {
          const Icon = section.icon;
          return (
            <Link
              key={section.id}
              href={`/admin/monitor/operations?tab=${section.id}`}
              className="group rounded-2xl border border-slate-200/60 dark:border-zinc-700/40 bg-white/70 dark:bg-zinc-800/50 backdrop-blur-sm p-5 hover:border-amber-300 dark:hover:border-amber-700 transition-colors"
            >
              <div className="flex items-start gap-3">
                <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-amber-100/80 dark:bg-amber-900/30 shrink-0">
                  <Icon className="h-5 w-5 text-amber-700 dark:text-amber-400" />
                </div>
                <div className="flex-1 min-w-0">
                  <p className="font-quicksand text-sm font-semibold text-foreground group-hover:text-amber-700 dark:group-hover:text-amber-400 transition-colors">
                    {section.label}
                  </p>
                  <p className="text-xs text-muted-foreground mt-0.5">{section.description}</p>
                </div>
              </div>
            </Link>
          );
        })}
      </div>

      <div className="flex">
        <Link
          href="/admin/monitor/operations"
          className="inline-flex items-center gap-2 rounded-lg bg-amber-100/80 dark:bg-amber-900/30 px-4 py-2 text-sm font-semibold text-amber-900 dark:text-amber-300 hover:bg-amber-200/80 dark:hover:bg-amber-800/40 transition-colors"
        >
          <ExternalLink className="h-4 w-4" />
          Apri Operations Console
        </Link>
      </div>
    </div>
  );
}
