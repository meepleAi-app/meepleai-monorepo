import { BarChart3, Users, Gamepad2, BookOpen } from 'lucide-react';
import { type Metadata } from 'next';

export const metadata: Metadata = {
  title: 'Overview',
  description: 'Admin dashboard overview with platform stats and quick actions',
};

function StatCard({
  label,
  icon: Icon,
  description,
}: {
  label: string;
  icon: React.ComponentType<{ className?: string }>;
  description: string;
}) {
  return (
    <div className="rounded-2xl border border-slate-200/60 dark:border-zinc-700/40 bg-white/70 dark:bg-zinc-800/50 backdrop-blur-sm p-5">
      <div className="flex items-center gap-3">
        <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-amber-100/80 dark:bg-amber-900/30">
          <Icon className="h-5 w-5 text-amber-700 dark:text-amber-400" />
        </div>
        <div>
          <p className="font-quicksand text-sm font-semibold text-foreground">{label}</p>
          <p className="text-xs text-muted-foreground">{description}</p>
        </div>
      </div>
    </div>
  );
}

export default function OverviewPage() {
  return (
    <div className="space-y-8">
      <div>
        <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
          Dashboard Overview
        </h1>
        <p className="text-sm text-muted-foreground mt-1">
          Platform stats, approvals, and user management at a glance
        </p>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
        <StatCard icon={BarChart3} label="Analytics" description="View usage statistics" />
        <StatCard icon={Users} label="Users" description="Manage user accounts" />
        <StatCard icon={Gamepad2} label="Games" description="Shared game catalog" />
        <StatCard icon={BookOpen} label="Knowledge Base" description="Documents and vectors" />
      </div>

      <div className="rounded-2xl border border-dashed border-border/50 bg-white/30 dark:bg-zinc-800/20 backdrop-blur-sm p-8 text-center">
        <p className="font-quicksand text-sm font-semibold text-foreground">
          Dashboard widgets will be connected to live data
        </p>
        <p className="mt-1.5 text-xs text-muted-foreground">
          Use the sidebar navigation to access specific admin sections.
        </p>
      </div>
    </div>
  );
}
