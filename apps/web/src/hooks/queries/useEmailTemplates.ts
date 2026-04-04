/**
 * Email Templates Query Hooks (Issue #52-#56)
 *
 * TanStack Query hooks for admin email template CRUD,
 * preview, publish, and version history.
 */

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { CreateEmailTemplateInput, UpdateEmailTemplateInput } from '@/lib/api';

// ─── Query Keys ──────────────────────────────────────────────────────────────

export const emailTemplateKeys = {
  all: ['email-templates'] as const,
  list: (params?: { type?: string; locale?: string }) =>
    [...emailTemplateKeys.all, 'list', params] as const,
  detail: (id: string) => [...emailTemplateKeys.all, 'detail', id] as const,
  versions: (name: string, locale?: string) =>
    [...emailTemplateKeys.all, 'versions', name, locale] as const,
};

// ─── Queries ─────────────────────────────────────────────────────────────────

export function useEmailTemplates(params?: { type?: string; locale?: string }) {
  return useQuery({
    queryKey: emailTemplateKeys.list(params),
    queryFn: () => api.admin.getEmailTemplates(params),
  });
}

export function useEmailTemplate(id: string) {
  return useQuery({
    queryKey: emailTemplateKeys.detail(id),
    queryFn: () => api.admin.getEmailTemplate(id),
    enabled: !!id,
  });
}

export function useEmailTemplateVersions(name: string, locale?: string) {
  return useQuery({
    queryKey: emailTemplateKeys.versions(name, locale),
    queryFn: () => api.admin.getEmailTemplateVersions(name, locale),
    enabled: !!name,
  });
}

// ─── Mutations ───────────────────────────────────────────────────────────────

export function useCreateEmailTemplate() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateEmailTemplateInput) => api.admin.createEmailTemplate(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: emailTemplateKeys.all });
    },
  });
}

export function useUpdateEmailTemplate() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdateEmailTemplateInput }) =>
      api.admin.updateEmailTemplate(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: emailTemplateKeys.all });
    },
  });
}

export function usePublishEmailTemplate() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.admin.publishEmailTemplate(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: emailTemplateKeys.all });
    },
  });
}

export function usePreviewEmailTemplate() {
  return useMutation({
    mutationFn: ({ id, testData }: { id: string; testData?: Record<string, string> }) =>
      api.admin.previewEmailTemplate(id, testData),
  });
}
