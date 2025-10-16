// Componente per visualizzare una singola modifica in una regola
type RuleAtom = {
  id: string;
  text: string;
  section?: string | null;
  page?: string | null;
  line?: string | null;
};

type FieldChange = {
  fieldName: string;
  oldValue?: string | null;
  newValue?: string | null;
};

type ChangeType = "Added" | "Modified" | "Deleted" | "Unchanged";

type RuleAtomChange = {
  type: ChangeType;
  oldAtom?: string | null;
  newAtom?: string | null;
  oldValue?: RuleAtom | null;
  newValue?: RuleAtom | null;
  fieldChanges?: FieldChange[] | null;
};

type ChangeItemProps = {
  change: RuleAtomChange;
};

export function ChangeItem({ change }: ChangeItemProps) {
  const getBgColor = () => {
    switch (change.type) {
      case "Added":
        return "#e7f5e7";
      case "Modified":
        return "#fff3e0";
      case "Deleted":
        return "#fce4e4";
      case "Unchanged":
        return "#f9f9f9";
      default:
        return "#f9f9f9";
    }
  };

  const getBorderColor = () => {
    switch (change.type) {
      case "Added":
        return "#4caf50";
      case "Modified":
        return "#ff9800";
      case "Deleted":
        return "#d93025";
      case "Unchanged":
        return "#ccc";
      default:
        return "#ccc";
    }
  };

  const getIcon = () => {
    switch (change.type) {
      case "Added":
        return "+ ";
      case "Modified":
        return "~ ";
      case "Deleted":
        return "- ";
      case "Unchanged":
        return "= ";
      default:
        return "";
    }
  };

  return (
    <div
      style={{
        padding: 16,
        background: getBgColor(),
        border: `2px solid ${getBorderColor()}`,
        borderRadius: 4
      }}
      data-testid={`change-item-${change.type.toLowerCase()}`}
    >
      <div style={{ marginBottom: 12 }}>
        <strong style={{ fontSize: 16 }}>
          {getIcon()}
          {change.type}: {change.newAtom || change.oldAtom}
        </strong>
      </div>

      {change.type === "Added" && change.newValue && (
        <div data-testid="change-added-content">
          <div style={{ fontSize: 14, marginBottom: 8 }}>
            <strong>Testo:</strong> {change.newValue.text}
          </div>
          {change.newValue.section && (
            <div style={{ fontSize: 12, color: "#666" }}>
              Sezione: {change.newValue.section}
            </div>
          )}
          {change.newValue.page && (
            <div style={{ fontSize: 12, color: "#666" }}>
              Pagina: {change.newValue.page}
            </div>
          )}
        </div>
      )}

      {change.type === "Deleted" && change.oldValue && (
        <div data-testid="change-deleted-content">
          <div style={{ fontSize: 14, marginBottom: 8, textDecoration: "line-through" }}>
            <strong>Testo:</strong> {change.oldValue.text}
          </div>
          {change.oldValue.section && (
            <div style={{ fontSize: 12, color: "#666", textDecoration: "line-through" }}>
              Sezione: {change.oldValue.section}
            </div>
          )}
          {change.oldValue.page && (
            <div style={{ fontSize: 12, color: "#666", textDecoration: "line-through" }}>
              Pagina: {change.oldValue.page}
            </div>
          )}
        </div>
      )}

      {change.type === "Modified" && change.fieldChanges && (
        <div data-testid="change-modified-content">
          {change.fieldChanges.map((fieldChange, idx) => (
            <div key={idx} style={{ marginBottom: 12 }}>
              <div style={{ fontSize: 14, fontWeight: "bold", marginBottom: 4 }}>
                {fieldChange.fieldName}:
              </div>
              <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12 }}>
                <div style={{ padding: 8, background: "#fce4e4", borderRadius: 4 }}>
                  <div style={{ fontSize: 12, color: "#666", marginBottom: 4 }}>Prima:</div>
                  <div style={{ fontSize: 14 }}>{fieldChange.oldValue || "(vuoto)"}</div>
                </div>
                <div style={{ padding: 8, background: "#e7f5e7", borderRadius: 4 }}>
                  <div style={{ fontSize: 12, color: "#666", marginBottom: 4 }}>Dopo:</div>
                  <div style={{ fontSize: 14 }}>{fieldChange.newValue || "(vuoto)"}</div>
                </div>
              </div>
            </div>
          ))}
        </div>
      )}

      {change.type === "Unchanged" && change.oldValue && (
        <div data-testid="change-unchanged-content">
          <div style={{ fontSize: 14, color: "#666" }}>
            {change.oldValue.text}
          </div>
        </div>
      )}
    </div>
  );
}
