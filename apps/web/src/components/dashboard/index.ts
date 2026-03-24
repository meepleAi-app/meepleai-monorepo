/**
 * Dashboard Components
 *
 * Legacy barrel — only DashboardEngine + useDashboardMode are still used
 * externally (by UserShellClient, AdminShell, ExtraMeepleCardDrawer).
 * All new dashboard UI lives in dashboard/v2/.
 */

export { dashboardMachine } from './DashboardEngine';
export type { DashboardEvent, DashboardEngineContext } from './DashboardEngine';
export { DashboardEngineProvider } from './DashboardEngineProvider';
export { DashboardRenderer } from './DashboardRenderer';
export { useDashboardMode } from './useDashboardMode';
