const ISSUE_URL = 'https://github.com/meepleAi-app/meepleai-monorepo/issues/1874';

export function FailedItemsPanel() {
  return (
    <section className="overflow-hidden rounded-xl border border-border bg-card">
      <header className="flex items-center gap-2.5 border-b border-border bg-muted/30 px-3.5 py-2.5">
        <h3 className="font-quicksand text-[13px] font-extrabold text-foreground">
          ✕ Failed items (last 30gg)
        </h3>
      </header>
      <div className="px-4 py-6 text-center">
        <p className="text-sm text-muted-foreground">
          Failed items: feature in arrivo (BE{' '}
          <a
            href={ISSUE_URL}
            target="_blank"
            rel="noopener noreferrer"
            className="text-toolkit underline"
          >
            #1874
          </a>
          ).
        </p>
      </div>
    </section>
  );
}
