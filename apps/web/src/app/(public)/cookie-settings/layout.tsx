import type { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'Preferenze cookie | MeepleAI',
  description:
    'Gestisci il tuo consenso ai cookie su MeepleAI. Modifica le preferenze in qualsiasi momento.',
  robots: { index: false, follow: true },
};

export default function CookieSettingsLayout({ children }: { children: React.ReactNode }) {
  return children;
}
