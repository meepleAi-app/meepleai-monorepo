import { DiffSummary } from "./DiffSummary";
import { ChangeItem } from "./ChangeItem";

// Tipi per il diff viewer
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

type DiffSummaryData = {
  totalChanges: number;
  added: number;
  modified: number;
  deleted: number;
  unchanged: number;
};

type RuleSpecDiff = {
  gameId: string;
  fromVersion: string;
  toVersion: string;
  fromCreatedAt: string;
  toCreatedAt: string;
  summary: DiffSummaryData;
  changes: RuleAtomChange[];
};

type DiffViewerProps = {
  diff: RuleSpecDiff;
  showOnlyChanges: boolean;
};

export function DiffViewer({ diff, showOnlyChanges }: DiffViewerProps) {
  const changesToShow = showOnlyChanges
    ? diff.changes.filter((c) => c.type !== "Unchanged")
    : diff.changes;

  return (
    <div>
      <DiffSummary summary={diff.summary} />

      <h3>Modifiche ({changesToShow.length})</h3>
      <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
        {changesToShow.length === 0 ? (
          <div style={{ padding: 16, background: "#f5f5f5", borderRadius: 4, textAlign: "center", color: "#666" }}>
            Nessuna modifica da visualizzare
          </div>
        ) : (
          changesToShow.map((change, index) => (
            <ChangeItem key={index} change={change} />
          ))
        )}
      </div>
    </div>
  );
}
