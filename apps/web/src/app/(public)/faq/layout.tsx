import type { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'Domande Frequenti | MeepleAI',
  description:
    'Trova risposte alle domande più comuni su MeepleAI: come funziona, formati supportati, account e sicurezza.',
  robots: { index: true, follow: true },
};

export default function FaqLayout({ children }: { children: React.ReactNode }) {
  return children;
}
