/**
 * Add Game to Collection Page
 * Issue #3477: Multi-step wizard for adding games to user's personal collection
 *
 * Route: /dashboard/collection/add-game
 */

import { AddGameWizard } from '@/components/collection/wizard/AddGameWizard';

export default function AddGamePage() {
  return <AddGameWizard />;
}
