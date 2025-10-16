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
    <div
      style={{
        padding: 16,
        background: "#f5f5f5",
        border: "1px solid #ddd",
        borderRadius: 4,
        marginBottom: 16
      }}
    >
      <h3 style={{ marginTop: 0, marginBottom: 12 }}>Riepilogo Modifiche</h3>
      <div style={{ display: "grid", gridTemplateColumns: "repeat(4, 1fr)", gap: 12, fontSize: 14 }}>
        <div
          style={{ padding: 8, background: "#e7f5e7", borderRadius: 4, textAlign: "center" }}
          data-testid="diff-summary-added"
        >
          <div style={{ fontWeight: "bold", color: "#4caf50" }}>+{summary.added}</div>
          <div style={{ fontSize: 12, color: "#666" }}>Aggiunte</div>
        </div>
        <div
          style={{ padding: 8, background: "#fff3e0", borderRadius: 4, textAlign: "center" }}
          data-testid="diff-summary-modified"
        >
          <div style={{ fontWeight: "bold", color: "#ff9800" }}>~{summary.modified}</div>
          <div style={{ fontSize: 12, color: "#666" }}>Modificate</div>
        </div>
        <div
          style={{ padding: 8, background: "#fce4e4", borderRadius: 4, textAlign: "center" }}
          data-testid="diff-summary-deleted"
        >
          <div style={{ fontWeight: "bold", color: "#d93025" }}>-{summary.deleted}</div>
          <div style={{ fontSize: 12, color: "#666" }}>Eliminate</div>
        </div>
        <div
          style={{ padding: 8, background: "#f0f0f0", borderRadius: 4, textAlign: "center" }}
          data-testid="diff-summary-unchanged"
        >
          <div style={{ fontWeight: "bold", color: "#666" }}>{summary.unchanged}</div>
          <div style={{ fontSize: 12, color: "#666" }}>Non modificate</div>
        </div>
      </div>
    </div>
  );
}
