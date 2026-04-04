import { FileQuestion, ArrowLeft } from 'lucide-react';
import Link from 'next/link';

import { Button } from '@/components/ui/primitives/button';

export default function AdminNotFound() {
  return (
    <div className="flex flex-col items-center justify-center py-16 px-4">
      <div
        className="w-full max-w-md overflow-hidden rounded-2xl border border-[rgba(45,42,38,0.08)] bg-[#FFFDF9] p-8 text-center"
        style={{ boxShadow: '0 8px 32px rgba(45, 42, 38, 0.12)' }}
      >
        <div className="mx-auto mb-6 flex h-14 w-14 items-center justify-center rounded-xl bg-[hsla(25,95%,38%,0.1)]">
          <FileQuestion className="h-7 w-7 text-[hsl(25,95%,38%)]" />
        </div>
        <h2 className="mb-2 font-quicksand text-xl font-bold text-[#2D2A26]">Pagina non trovata</h2>
        <p className="mb-6 font-nunito text-sm text-[#6B665C]">
          La pagina che cerchi non esiste o è stata spostata.
        </p>
        <Button
          asChild
          className="bg-[hsl(25,95%,38%)] font-quicksand font-semibold text-white hover:bg-[hsl(25,95%,45%)]"
        >
          <Link href="/admin/overview">
            <ArrowLeft className="mr-2 h-4 w-4" />
            Torna alla Dashboard
          </Link>
        </Button>
      </div>
    </div>
  );
}
