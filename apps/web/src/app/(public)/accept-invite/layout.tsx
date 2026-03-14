import type { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'Accept Invitation — MeepleAI',
  description: 'Set your password and join MeepleAI',
};

export default function AcceptInviteLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="min-h-screen bg-gradient-to-b from-amber-50/50 to-white dark:from-slate-950 dark:to-slate-900">
      <main className="container mx-auto px-4 py-8">{children}</main>
    </div>
  );
}
