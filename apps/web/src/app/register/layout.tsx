import type { Metadata } from 'next';

// Metadata for SEO
export const metadata: Metadata = {
  title: 'Registrati | MeepleAI',
  description:
    "Crea il tuo account MeepleAI per accedere all'assistente AI per giochi da tavolo. Risposte immediate alle regole con citazioni dal manuale.",
  robots: {
    index: true,
    follow: true,
  },
};

export default function RegisterLayout({ children }: { children: React.ReactNode }) {
  return children;
}
