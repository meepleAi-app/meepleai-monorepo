/**
 * Admin Game Import Wizard - Server Component Wrapper
 * Issue #4161: PDF Wizard Container & State Management
 *
 * Admin-only workflow for importing games from PDF:
 * 1. Upload PDF
 * 2. Review Extracted Metadata
 * 3. Match with BoardGameGeek
 * 4. Resolve Conflicts & Import
 *
 * Note: RequireRole removed — layout already enforces Admin role.
 */

import { AdminGameImportWizardClient } from './client';

export default function AdminGameImportWizardPage() {
  return <AdminGameImportWizardClient />;
}
