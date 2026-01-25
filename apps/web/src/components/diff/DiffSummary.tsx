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
    <div className="p-4 bg-gray-100 border border-gray-300 rounded mb-4">
      <h3 className="mt-0 mb-3">Riepilogo Modifiche</h3>
      <div className="grid grid-cols-4 gap-3 text-sm">
        <div
          className="p-2 bg-green-100 rounded text-center"
          data-testid="diff-summary-added"
        >
          <div className="font-bold text-green-600">+{summary.added}</div>
          <div className="text-xs text-gray-600">Aggiunte</div>
        </div>
        <div
          className="p-2 bg-orange-100 rounded text-center"
          data-testid="diff-summary-modified"
        >
          <div className="font-bold text-orange-500">~{summary.modified}</div>
          <div className="text-xs text-gray-600">Modificate</div>
        </div>
        <div
          className="p-2 bg-red-100 rounded text-center"
          data-testid="diff-summary-deleted"
        >
          <div className="font-bold text-red-600">-{summary.deleted}</div>
          <div className="text-xs text-gray-600">Eliminate</div>
        </div>
        <div
          className="p-2 bg-gray-200 rounded text-center"
          data-testid="diff-summary-unchanged"
        >
          <div className="font-bold text-gray-600">{summary.unchanged}</div>
          <div className="text-xs text-gray-600">Non modificate</div>
        </div>
      </div>
    </div>
  );
}
