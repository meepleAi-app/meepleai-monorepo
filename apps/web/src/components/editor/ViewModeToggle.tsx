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
    <div
      style={{
        display: "inline-flex",
        background: "#f0f0f0",
        borderRadius: 4,
        padding: 2
      }}
    >
      <button
        onClick={() => onModeChange("rich")}
        style={{
          padding: "6px 16px",
          background: mode === "rich" ? "white" : "transparent",
          color: mode === "rich" ? "#0070f3" : "#666",
          border: "none",
          borderRadius: 3,
          cursor: "pointer",
          fontSize: 14,
          fontWeight: mode === "rich" ? "bold" : "normal",
          transition: "all 0.15s ease",
          boxShadow: mode === "rich" ? "0 1px 3px rgba(0,0,0,0.1)" : "none"
        }}
        title="Editor visuale con formattazione"
      >
        📝 Editor Visuale
      </button>
      <button
        onClick={() => onModeChange("json")}
        style={{
          padding: "6px 16px",
          background: mode === "json" ? "white" : "transparent",
          color: mode === "json" ? "#0070f3" : "#666",
          border: "none",
          borderRadius: 3,
          cursor: "pointer",
          fontSize: 14,
          fontWeight: mode === "json" ? "bold" : "normal",
          transition: "all 0.15s ease",
          boxShadow: mode === "json" ? "0 1px 3px rgba(0,0,0,0.1)" : "none"
        }}
        title="Visualizza e modifica JSON direttamente"
      >
        {"{ }"} Codice JSON
      </button>
    </div>
  );
}
