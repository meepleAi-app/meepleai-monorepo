import type React from 'react';

import type { SettingsSectionDef } from '../settings-sections';

interface Props {
  readonly section: SettingsSectionDef;
}

export function SectionPlaceholder({ section }: Props): React.JSX.Element {
  const Icon = section.icon;
  return (
    <div className="text-center py-14 px-6 bg-card border border-dashed border-border rounded-lg">
      <div className="w-14 h-14 rounded-lg flex items-center justify-center mx-auto mb-3 bg-muted text-muted-foreground">
        <Icon className="h-7 w-7" aria-hidden />
      </div>
      <h3 className="font-quicksand font-bold text-lg text-foreground">{section.label}</h3>
      <p className="text-sm text-muted-foreground mt-1">Settings UI in development.</p>
    </div>
  );
}
