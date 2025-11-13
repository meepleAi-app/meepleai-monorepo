type ViewMode = "rich" | "json";

type ViewModeToggleProps = {
  mode: ViewMode;
  onModeChange: (mode: ViewMode) => void;
};

/**
 * ViewModeToggle Component
 *
 * Toggle button to switch between rich text editor and JSON code view.
 * Provides visual feedback for the current mode.
 *
 * @param mode - Current view mode ("rich" or "json")
 * @param onModeChange - Callback when mode changes
 */
export default function ViewModeToggle({ mode, onModeChange }: ViewModeToggleProps) {
  return (
    <div className="inline-flex bg-gray-100 rounded p-0.5 gap-0.5">
      <button
        onClick={() => onModeChange("rich")}
        className={`px-4 py-1.5 rounded text-sm transition-all duration-150 ${
          mode === "rich"
            ? "bg-white text-blue-600 font-bold shadow-sm"
            : "bg-transparent text-gray-600 font-normal"
        }`}
        title="Editor visuale con formattazione"
      >
        📝 Editor Visuale
      </button>
      <button
        onClick={() => onModeChange("json")}
        className={`px-4 py-1.5 rounded text-sm transition-all duration-150 ${
          mode === "json"
            ? "bg-white text-blue-600 font-bold shadow-sm"
            : "bg-transparent text-gray-600 font-normal"
        }`}
        title="Visualizza e modifica JSON direttamente"
      >
        {"{ }"} Codice JSON
      </button>
    </div>
  );
}
