import type { Metadata } from 'next';

const CANONICAL_URL = 'https://meepleai.com/how-it-works/game-comprehension';

export const metadata: Metadata = {
  title: 'Come MeepleAI comprende un gioco | MeepleAI',
  description:
    'Dal PDF del regolamento alla scheda con citazioni: scopri la catena di fiducia che rende ogni risposta di MeepleAI verificabile, pagina per pagina.',
  robots: { index: true, follow: true },
  alternates: { canonical: CANONICAL_URL },
  openGraph: {
    type: 'article',
    title: 'Come MeepleAI comprende un gioco',
    description:
      'La catena di fiducia dietro ogni risposta: PDF ufficiale, riformulazione AI, revisione umana per-affermazione, scheda con citazione della pagina esatta.',
    url: CANONICAL_URL,
    siteName: 'MeepleAI',
  },
};

export default function GameComprehensionLayout({ children }: { children: React.ReactNode }) {
  return children;
}
