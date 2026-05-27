// PHASE-A STUB — replaced in later phases
import type React from 'react';

import { SectionPlaceholder } from './SectionPlaceholder';
import { SETTINGS_SECTIONS } from '../settings-sections';

export function ProfileSection(): React.JSX.Element {
  const def = SETTINGS_SECTIONS.find(s => s.id === 'profile')!;
  return <SectionPlaceholder section={def} />;
}
