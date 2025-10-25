import { useState, useMemo, useEffect } from 'react';
import { DiffSummary } from "./DiffSummary";
import { ChangeItem } from "./ChangeItem";
import { DiffViewModeToggle } from "./diff/DiffViewModeToggle";
import { DiffToolbar } from "./diff/DiffToolbar";
import { SideBySideDiffView } from "./diff/SideBySideDiffView";
import {
  processDiff,
  identifyCollapsibleSections,
  filterChangesByQuery,
  CollapsibleSection
} from "../lib/diffProcessor";

// Existing types from original DiffViewer
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

type DiffViewerEnhancedProps = {
  diff: RuleSpecDiff;
  showOnlyChanges: boolean;
  defaultViewMode?: 'list' | 'side-by-side';
};

/**
 * Enhanced DiffViewer with side-by-side view, syntax highlighting, and navigation
 * Maintains backward compatibility with existing list view
 */
export function DiffViewerEnhanced({
  diff,
  showOnlyChanges,
  defaultViewMode = 'list'
}: DiffViewerEnhancedProps) {
  const [viewMode, setViewMode] = useState<'list' | 'side-by-side'>(defaultViewMode);
  const [searchQuery, setSearchQuery] = useState('');
  const [currentChangeIndex, setCurrentChangeIndex] = useState(0);
  const [collapsibleSections, setCollapsibleSections] = useState<Map<string, CollapsibleSection>>(new Map());

  // Convert RuleSpecDiff to JSON strings for diff.js processing
  const { oldJson, newJson } = useMemo(() => {
    // Build JSON representations from RuleAtomChanges
    const oldRules = diff.changes
      .filter(c => c.oldValue || c.type === 'Deleted')
      .map(c => c.oldValue)
      .filter(Boolean);

    const newRules = diff.changes
      .filter(c => c.newValue || c.type === 'Added')
      .map(c => c.newValue)
      .filter(Boolean);

    return {
      oldJson: JSON.stringify(oldRules, null, 2),
      newJson: JSON.stringify(newRules, null, 2)
    };
  }, [diff.changes]);

  // Process diff for side-by-side view
  const processedDiff = useMemo(() => {
    if (viewMode !== 'side-by-side') return null;
    return processDiff(oldJson, newJson);
  }, [oldJson, newJson, viewMode]);

  // Identify collapsible sections
  useEffect(() => {
    if (!processedDiff) return;

    const oldSections = identifyCollapsibleSections(processedDiff.oldLines);
    const newSections = identifyCollapsibleSections(processedDiff.newLines);

    const sectionsMap = new Map<string, CollapsibleSection>();
    oldSections.forEach(s => sectionsMap.set(`old-${s.startLine}`, s));
    newSections.forEach(s => sectionsMap.set(`new-${s.startLine}`, s));

    setCollapsibleSections(sectionsMap);
  }, [processedDiff]);

  // Filter changes by search query
  const filteredChanges = useMemo(() => {
    if (viewMode === 'list') {
      return showOnlyChanges
        ? diff.changes.filter((c) => c.type !== "Unchanged")
        : diff.changes;
    }

    if (!processedDiff) return [];

    return filterChangesByQuery(
      processedDiff.changes,
      processedDiff.oldLines,
      processedDiff.newLines,
      searchQuery
    );
  }, [viewMode, diff.changes, showOnlyChanges, processedDiff, searchQuery]);

  const handleNavigatePrev = () => {
    setCurrentChangeIndex(prev => Math.max(0, prev - 1));
  };

  const handleNavigateNext = () => {
    setCurrentChangeIndex(prev => Math.min(filteredChanges.length - 1, prev + 1));
  };

  const handleToggleSection = (sectionKey: string) => {
    setCollapsibleSections(prev => {
      const newMap = new Map(prev);
      const section = newMap.get(sectionKey);
      if (section) {
        newMap.set(sectionKey, { ...section, isCollapsed: !section.isCollapsed });
      }
      return newMap;
    });
  };

  // Render list view (original implementation)
  if (viewMode === 'list') {
    const changesToShow = filteredChanges as RuleAtomChange[];

    return (
      <div>
        <DiffViewModeToggle currentMode={viewMode} onModeChange={setViewMode} />
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

  // Render side-by-side view
  if (!processedDiff) return null;

  return (
    <div className="diff-viewer-enhanced">
      <DiffViewModeToggle currentMode={viewMode} onModeChange={setViewMode} />

      <DiffToolbar
        statistics={processedDiff.statistics}
        searchQuery={searchQuery}
        onSearchChange={setSearchQuery}
        currentChangeIndex={currentChangeIndex}
        totalChanges={filteredChanges.length}
        onNavigatePrev={handleNavigatePrev}
        onNavigateNext={handleNavigateNext}
        showNavigation={true}
      />

      <SideBySideDiffView
        processedDiff={processedDiff}
        searchQuery={searchQuery}
        currentChangeIndex={currentChangeIndex}
        collapsibleSections={collapsibleSections}
        onToggleSection={handleToggleSection}
        maxHeight="600px"
      />
    </div>
  );
}
