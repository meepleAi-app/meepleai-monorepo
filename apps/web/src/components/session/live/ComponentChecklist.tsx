/**
 * ComponentChecklist
 *
 * Setup Wizard — Task 8
 *
 * Checkable list of game components. Each item shows name, quantity, and a checkbox.
 */

'use client';

// ─── Types ────────────────────────────────────────────────────────────────────

export interface SetupComponent {
  name: string;
  quantity: number;
  checked: boolean;
}

interface ComponentChecklistProps {
  components: SetupComponent[];
  onToggle: (index: number) => void;
}

// ─── Component ────────────────────────────────────────────────────────────────

export function ComponentChecklist({ components, onToggle }: ComponentChecklistProps) {
  return (
    <div className="space-y-2">
      {components.map((component, index) => (
        <label
          key={`${component.name}-${index}`}
          className={[
            'flex items-center gap-3 rounded-xl border p-3 cursor-pointer transition-shadow',
            component.checked
              ? 'bg-amber-50 border-amber-300 shadow-sm'
              : 'bg-white/70 backdrop-blur-md border-white/40 shadow-sm hover:shadow-md',
          ].join(' ')}
        >
          <input
            type="checkbox"
            checked={component.checked}
            onChange={() => onToggle(index)}
            className="h-4 w-4 rounded border-gray-300 text-amber-500 focus:ring-amber-400"
          />
          <span
            className={[
              'text-sm font-nunito',
              component.checked ? 'text-gray-500 line-through' : 'text-gray-900',
            ].join(' ')}
          >
            {component.name} (x{component.quantity})
          </span>
        </label>
      ))}
    </div>
  );
}
