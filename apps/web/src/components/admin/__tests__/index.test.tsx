/**
 * Barrel export tests for admin components - Issue #887
 */

import { describe, it, expect } from 'vitest';
import * as Admin from '../index';
import { AdminLayout } from '../AdminLayout';
import { StatCard } from '../StatCard';
import { MetricsGrid } from '../MetricsGrid';
import { ActivityFeed } from '../ActivityFeed';
import { AdminSidebar, defaultNavigation } from '../AdminSidebar';
import { AdminHeader } from '../AdminHeader';
import { AdminBreadcrumbs } from '../AdminBreadcrumbs';
import { AdminAuthGuard } from '../AdminAuthGuard';
import { QuickActions } from '../QuickActions';
import { SystemStatus } from '../SystemStatus';
import {
  EndpointDistributionChart,
  LatencyDistributionChart,
  RequestsTimeSeriesChart,
  FeedbackChart,
} from '../AdminCharts';
import CategoryConfigTab from '../CategoryConfigTab';
import FeatureFlagsTab from '../FeatureFlagsTab';

describe('admin index barrel exports', () => {
  it('re-exports dashboard layout and widgets', () => {
    expect(Admin.AdminLayout).toBe(AdminLayout);
    expect(Admin.StatCard).toBe(StatCard);
    expect(Admin.MetricsGrid).toBe(MetricsGrid);
    expect(Admin.ActivityFeed).toBe(ActivityFeed);
    expect(Admin.AdminHeader).toBe(AdminHeader);
    expect(Admin.AdminSidebar).toBe(AdminSidebar);
    expect(Admin.defaultNavigation).toBe(defaultNavigation);
    expect(Admin.AdminBreadcrumbs).toBe(AdminBreadcrumbs);
  });

  it('re-exports admin guard and utility widgets', () => {
    expect(Admin.AdminAuthGuard).toBe(AdminAuthGuard);
    expect(Admin.QuickActions).toBe(QuickActions);
    expect(Admin.SystemStatus).toBe(SystemStatus);
  });

  it('re-exports chart components', () => {
    expect(Admin.EndpointDistributionChart).toBe(EndpointDistributionChart);
    expect(Admin.LatencyDistributionChart).toBe(LatencyDistributionChart);
    expect(Admin.RequestsTimeSeriesChart).toBe(RequestsTimeSeriesChart);
    expect(Admin.FeedbackChart).toBe(FeedbackChart);
  });

  it('re-exports configuration tabs', () => {
    expect(Admin.CategoryConfigTab).toBe(CategoryConfigTab);
    expect(Admin.FeatureFlagsTab).toBe(FeatureFlagsTab);
  });
});
