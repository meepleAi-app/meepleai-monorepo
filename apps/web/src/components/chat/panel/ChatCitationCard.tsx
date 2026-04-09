'use client';

export interface ChatCitation {
  documentName: string;
  pages: number[];
  excerpt: string;
  openUrl?: string;
}

interface ChatCitationCardProps {
  citation: ChatCitation;
}

export function ChatCitationCard({ citation }: ChatCitationCardProps) {
  return (
    <div
      className="flex max-w-full gap-3 rounded-2xl border p-3.5"
      style={{
        background: 'hsla(210, 40%, 55%, 0.06)',
        borderColor: 'hsla(210, 40%, 55%, 0.2)',
      }}
    >
      <div
        className="flex h-8 w-8 flex-shrink-0 items-center justify-center rounded-lg text-white"
        style={{ background: 'linear-gradient(135deg, hsl(210 40% 65%), hsl(210 40% 45%))' }}
        aria-hidden
      >
        📄
      </div>
      <div className="min-w-0 flex-1">
        <div className="mb-1.5 flex flex-wrap items-center gap-2">
          <span
            className="font-quicksand text-[0.78rem] font-extrabold"
            style={{ color: 'hsl(210 40% 32%)' }}
          >
            {citation.documentName}
          </span>
          {citation.pages.map(page => (
            <span
              key={page}
              className="rounded px-2 py-0.5 text-[0.68rem] font-extrabold"
              style={{ background: 'hsla(210, 40%, 55%, 0.12)', color: 'hsl(210 40% 30%)' }}
            >
              pag. {page}
            </span>
          ))}
        </div>
        <p
          className="border-l-2 pl-2.5 text-[0.78rem] italic leading-relaxed text-[var(--nh-text-secondary)]"
          style={{ borderLeftColor: 'hsla(210, 40%, 55%, 0.3)' }}
        >
          &ldquo;{citation.excerpt}&rdquo;
        </p>
        {citation.openUrl && (
          <a
            href={citation.openUrl}
            className="mt-1.5 inline-flex items-center gap-1 font-nunito text-[0.72rem] font-extrabold"
            style={{ color: 'hsl(210 40% 40%)' }}
          >
            Apri regolamento →
          </a>
        )}
      </div>
    </div>
  );
}
