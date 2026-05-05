import type { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'Come Funziona | MeepleAI',
  description:
    'Scopri come MeepleAI ti aiuta a comprendere le regole dei giochi da tavolo in tre semplici passaggi.',
  robots: { index: true, follow: true },
};

export default function HowItWorksLayout({ children }: { children: React.ReactNode }) {
  return children;
}
