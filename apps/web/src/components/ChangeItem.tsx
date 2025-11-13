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
      className="p-4 rounded border-2"
      style={{
        background: getBgColor(),
        borderColor: getBorderColor()
      }}
      data-testid={`change-item-${change.type.toLowerCase()}`}
    >
      <div className="mb-3">
        <strong className="text-base">
          {getIcon()}
          {change.type}: {change.newAtom || change.oldAtom}
        </strong>
      </div>

      {change.type === "Added" && change.newValue && (
        <div data-testid="change-added-content">
          <div className="text-sm mb-2">
            <strong>Testo:</strong> {change.newValue.text}
          </div>
          {change.newValue.section && (
            <div className="text-xs text-gray-600">
              Sezione: {change.newValue.section}
            </div>
          )}
          {change.newValue.page && (
            <div className="text-xs text-gray-600">
              Pagina: {change.newValue.page}
            </div>
          )}
        </div>
      )}

      {change.type === "Deleted" && change.oldValue && (
        <div data-testid="change-deleted-content">
          <div className="text-sm mb-2 line-through">
            <strong>Testo:</strong> {change.oldValue.text}
          </div>
          {change.oldValue.section && (
            <div className="text-xs text-gray-600 line-through">
              Sezione: {change.oldValue.section}
            </div>
          )}
          {change.oldValue.page && (
            <div className="text-xs text-gray-600 line-through">
              Pagina: {change.oldValue.page}
            </div>
          )}
        </div>
      )}

      {change.type === "Modified" && change.fieldChanges && (
        <div data-testid="change-modified-content">
          {change.fieldChanges.map((fieldChange, idx) => (
            <div key={idx} className="mb-3">
              <div className="text-sm font-bold mb-1">
                {fieldChange.fieldName}:
              </div>
              <div className="grid grid-cols-2 gap-3">
                <div className="p-2 bg-red-50 rounded">
                  <div className="text-xs text-gray-600 mb-1">Prima:</div>
                  <div className="text-sm">{fieldChange.oldValue || "(vuoto)"}</div>
                </div>
                <div className="p-2 bg-green-50 rounded">
                  <div className="text-xs text-gray-600 mb-1">Dopo:</div>
                  <div className="text-sm">{fieldChange.newValue || "(vuoto)"}</div>
                </div>
              </div>
            </div>
          ))}
        </div>
      )}

      {change.type === "Unchanged" && change.oldValue && (
        <div data-testid="change-unchanged-content">
          <div className="text-sm text-gray-600">
            {change.oldValue.text}
          </div>
        </div>
      )}
    </div>
  );
}