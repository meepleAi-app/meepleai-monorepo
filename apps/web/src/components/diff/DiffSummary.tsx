// Componente per visualizzare il riepilogo delle modifiche tra due versioni
type DiffSummaryProps = {
  summary: {
    added: number;
    modified: number;
    deleted: number;
    unchanged: number;
  };
};

export function DiffSummary({ summary }: DiffSummaryProps) {
  return (
    <div className="p-4 bg-muted border border-border rounded mb-4">
      <h3 className="mt-0 mb-3" data-testid="diff-summary-title">Riepilogo Modifiche</h3>
      <div className="grid grid-cols-4 gap-3 text-sm">
        <div
          className="p-2 bg-green-100 rounded text-center"
          data-testid="diff-summary-added"
        >
          <div className="font-bold text-green-600">+{summary.added}</div>
          <div className="text-xs text-muted-foreground" data-testid="added-label">Aggiunte</div>
        </div>
        <div
          className="p-2 bg-orange-100 rounded text-center"
          data-testid="diff-summary-modified"
        >
          <div className="font-bold text-orange-500">~{summary.modified}</div>
          <div className="text-xs text-muted-foreground" data-testid="modified-label">Modificate</div>
        </div>
        <div
          className="p-2 bg-red-100 rounded text-center"
          data-testid="diff-summary-deleted"
        >
          <div className="font-bold text-red-600">-{summary.deleted}</div>
          <div className="text-xs text-muted-foreground" data-testid="deleted-label">Eliminate</div>
        </div>
        <div
          className="p-2 bg-muted rounded text-center"
          data-testid="diff-summary-unchanged"
        >
          <div className="font-bold text-muted-foreground">{summary.unchanged}</div>
          <div className="text-xs text-muted-foreground" data-testid="unchanged-label">Non modificate</div>
        </div>
      </div>
    </div>
  );
}
