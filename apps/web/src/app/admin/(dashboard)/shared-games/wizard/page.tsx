/**
 * Catalog Wizard Page - Admin Dashboard
 *
 * Route: /admin/shared-games/wizard
 * Issue #118: Guided Wizard for shared game content management.
 */

import { type Metadata } from 'next';

import { CatalogWizard } from './CatalogWizard';

export const metadata: Metadata = {
  title: 'Catalog Wizard',
  description: 'Guided wizard for adding content to shared games',
};

export default function CatalogWizardPage() {
  return (
    <div className="container py-6">
      <h1 className="mb-6 text-2xl font-bold">Catalog Content Wizard</h1>
      <CatalogWizard />
    </div>
  );
}
