import type { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'Sei offline | MeepleAI',
  description: 'Non sei connesso a internet. Puoi ancora accedere ai contenuti salvati in cache.',
  robots: { index: false, follow: false },
};

export default function OfflineLayout({ children }: { children: React.ReactNode }) {
  return children;
}
