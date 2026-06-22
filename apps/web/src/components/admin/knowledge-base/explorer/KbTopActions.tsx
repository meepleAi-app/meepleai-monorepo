import Link from 'next/link';

export function KbTopActions() {
  return (
    <div className="flex items-center gap-2">
      <div className="hidden md:flex items-center gap-2 rounded-lg border border-border/60 bg-card px-3 py-1.5 text-sm text-muted-foreground w-[280px]">
        <span aria-hidden>🔍</span>
        <input
          className="flex-1 bg-transparent outline-none text-foreground placeholder:text-muted-foreground"
          placeholder="Search docs, chunks, games…"
          aria-label="Search knowledge base"
          readOnly
          tabIndex={-1}
        />
        <kbd className="rounded border border-border/60 bg-muted px-1.5 font-mono text-[10px]">
          ⌘K
        </kbd>
      </div>
      <Link
        href="/admin/knowledge-base/upload"
        className="inline-flex items-center gap-1.5 rounded-md bg-entity-kb px-3 py-1.5 text-sm font-semibold font-quicksand text-white shadow-sm hover:brightness-110"
      >
        + Upload PDF
      </Link>
    </div>
  );
}
