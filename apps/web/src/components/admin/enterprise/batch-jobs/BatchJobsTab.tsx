/**
 * BatchJobsTab Component
 * Issue #3693 - Batch Job System
 *
 * Main container for batch job management.
 * Features:
 * - Fetches jobs from API with status filtering
 * - Displays BatchJobQueueViewer table
 * - Create job modal
 * - Auto-refresh
 */

'use client';

import { useCallback, useEffect, useState } from 'react';

import { PlusIcon, RefreshCwIcon } from 'lucide-react';
import { toast } from 'sonner';

import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/overlays/select';
import { Button } from '@/components/ui/primitives/button';
import { api } from '@/lib/api';
import type { BatchJobDto } from '@/lib/api/schemas';

import { BatchJobQueueViewer } from './BatchJobQueueViewer';
import { CreateJobModal } from './CreateJobModal';
import { JobDetailModal } from './JobDetailModal';

const REFRESH_INTERVAL = 5000; // 5s for running jobs

export function BatchJobsTab() {
  const [jobs, setJobs] = useState<BatchJobDto[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [statusFilter, setStatusFilter] = useState<string>('all');
  const [page, setPage] = useState(1);
  const [selectedJob, setSelectedJob] = useState<BatchJobDto | null>(null);
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [isRefreshing, setIsRefreshing] = useState(false);

  const fetchJobs = useCallback(async () => {
    try {
      const result = await api.admin.getAllBatchJobs({
        status: statusFilter,
        page,
        pageSize: 20,
      });
      setJobs(result.jobs);
      setTotal(result.total);
    } catch (error) {
      toast.error('Failed to fetch batch jobs');
      console.error('Batch jobs fetch error:', error);
    } finally {
      setLoading(false);
      setIsRefreshing(false);
    }
  }, [statusFilter, page]);

  const handleRefresh = async () => {
    setIsRefreshing(true);
    await fetchJobs();
  };

  const handleJobClick = (job: BatchJobDto) => {
    setSelectedJob(job);
  };

  const handleJobAction = async () => {
    await fetchJobs();
    if (selectedJob) {
      try {
        const updatedJob = await api.admin.getBatchJob(selectedJob.id);
        setSelectedJob(updatedJob);
      } catch {
        setSelectedJob(null);
      }
    }
  };

  const handleCreateJob = async () => {
    await fetchJobs();
    setShowCreateModal(false);
  };

  useEffect(() => {
    fetchJobs();
  }, [fetchJobs]);

  // Auto-refresh for running jobs
  useEffect(() => {
    const hasRunningJobs = jobs.some((job) => job.status === 'Running' || job.status === 'Queued');
    if (!hasRunningJobs) return;

    const interval = setInterval(fetchJobs, REFRESH_INTERVAL);
    return () => clearInterval(interval);
  }, [jobs, fetchJobs]);

  return (
    <div className="p-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-2xl font-bold text-zinc-900 dark:text-zinc-100">Batch Jobs</h2>
          <p className="text-sm text-zinc-600 dark:text-zinc-400 mt-1">
            Manage background processing jobs and tasks
          </p>
        </div>
        <div className="flex items-center gap-3">
          <Select value={statusFilter} onValueChange={setStatusFilter}>
            <SelectTrigger className="w-[150px]">
              <SelectValue placeholder="Filter status" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All Status</SelectItem>
              <SelectItem value="Queued">Queued</SelectItem>
              <SelectItem value="Running">Running</SelectItem>
              <SelectItem value="Completed">Completed</SelectItem>
              <SelectItem value="Failed">Failed</SelectItem>
              <SelectItem value="Cancelled">Cancelled</SelectItem>
            </SelectContent>
          </Select>
          <Button
            variant="outline"
            size="sm"
            onClick={handleRefresh}
            disabled={isRefreshing}
          >
            <RefreshCwIcon className={`h-4 w-4 mr-2 ${isRefreshing ? 'animate-spin' : ''}`} />
            Refresh
          </Button>
          <Button onClick={() => setShowCreateModal(true)}>
            <PlusIcon className="h-4 w-4 mr-2" />
            Create Job
          </Button>
        </div>
      </div>

      {/* Job Queue Table */}
      <BatchJobQueueViewer
        jobs={jobs}
        loading={loading}
        onJobClick={handleJobClick}
      />

      {/* Pagination */}
      {total > 20 && (
        <div className="flex items-center justify-between">
          <p className="text-sm text-zinc-600 dark:text-zinc-400">
            Showing {jobs.length} of {total} jobs
          </p>
          <div className="flex gap-2">
            <Button
              variant="outline"
              size="sm"
              onClick={() => setPage(p => Math.max(1, p - 1))}
              disabled={page === 1}
            >
              Previous
            </Button>
            <Button
              variant="outline"
              size="sm"
              onClick={() => setPage(p => p + 1)}
              disabled={page * 20 >= total}
            >
              Next
            </Button>
          </div>
        </div>
      )}

      {/* Modals */}
      {selectedJob && (
        <JobDetailModal
          job={selectedJob}
          open={!!selectedJob}
          onClose={() => setSelectedJob(null)}
          onAction={handleJobAction}
        />
      )}

      {showCreateModal && (
        <CreateJobModal
          open={showCreateModal}
          onClose={() => setShowCreateModal(false)}
          onCreate={handleCreateJob}
        />
      )}
    </div>
  );
}
