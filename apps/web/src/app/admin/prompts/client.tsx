'use client';

import React, { useState, useEffect, useCallback } from 'react';
import { useRouter } from 'next/navigation';
import { api } from '@/lib/api';
import { cn } from '@/lib/utils';
import { ErrorDisplay } from '@/components/errors';
import { categorizeError } from '@/lib/errorUtils';
import { getErrorMessage } from '@/lib/utils/errorHandler';
import { useAuthUser } from '@/components/auth/AuthProvider';
import { AdminAuthGuard } from '@/components/admin/AdminAuthGuard';

type PromptTemplate = {
  id: string;
  name: string;
  description: string;
  category: string;
  createdAt: string;
  updatedAt: string;
  activeVersionId?: string | null;
};

type PagedResult = {
  templates: PromptTemplate[];
  totalPages: number;
  page: number;
  total: number;
};

type ModalState = {
  isOpen: boolean;
  mode: 'create' | 'edit';
  template?: PromptTemplate;
};

type ToastState = {
  show: boolean;
  message: string;
  type: 'success' | 'error';
};

type DeleteDialogState = {
  isOpen: boolean;
  template?: PromptTemplate;
};

const CATEGORIES = [
  'qa-system-prompt',
  'chess-system-prompt',
  'setup-guide-system-prompt',
  'streaming-qa-prompt',
];

export function AdminPageClient() {
  const router = useRouter();
  const { user, loading: authLoading } = useAuthUser();

  if (!user) return null;

  // Data state - ALL hooks must be called unconditionally
  const [templates, setTemplates] = useState<PromptTemplate[]>([]);
  const [dataLoading, setDataLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Filter/pagination state
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [search, setSearch] = useState('');
  const [categoryFilter, setCategoryFilter] = useState('');
  const [sortBy, setSortBy] = useState('createdAt');
  const [sortOrder, setSortOrder] = useState<'asc' | 'desc'>('desc');

  // Modal/dialog state
  const [modalState, setModalState] = useState<ModalState>({
    isOpen: false,
    mode: 'create',
  });
  const [deleteDialog, setDeleteDialog] = useState<DeleteDialogState>({
    isOpen: false,
  });
  const [toast, setToast] = useState<ToastState>({
    show: false,
    message: '',
    type: 'success',
  });

  // Form state
  const [formData, setFormData] = useState({
    name: '',
    description: '',
    category: '',
    initialContent: '',
  });
  const [formLoading, setFormLoading] = useState(false);

  const showToast = useCallback((message: string, type: 'success' | 'error') => {
    setToast({ show: true, message, type });
    setTimeout(() => setToast({ show: false, message: '', type: 'success' }), 5000);
  }, []);

  const fetchTemplates = useCallback(async () => {
    setDataLoading(true);
    setError(null);

    try {
      const params = new URLSearchParams({
        page: page.toString(),
        limit: '20',
        sortBy,
        sortOrder,
      });

      if (search) params.append('search', search);
      if (categoryFilter) params.append('category', categoryFilter);

      const result = await api.admin.getPrompts({
        page,
        pageSize: 20,
        search: search || undefined,
        sortBy,
        sortDirection: sortOrder as 'asc' | 'desc',
      });

      // eslint-disable-next-line @typescript-eslint/no-explicit-any -- API response type coercion
      setTemplates((result.items as any) || []);
      setTotalPages(Math.ceil((result.total || 0) / 20));
    } catch (err) {
      setError(getErrorMessage(err, 'Failed to fetch templates'));
      showToast('Failed to fetch templates', 'error');
    } finally {
      setDataLoading(false);
    }
  }, [page, search, categoryFilter, sortBy, sortOrder, showToast]);

  useEffect(() => {
    fetchTemplates();
  }, [fetchTemplates]);

  useEffect(() => {
    setPage(1);
  }, [search, categoryFilter]);

  const handleSort = (column: string) => {
    if (sortBy === column) {
      setSortOrder(sortOrder === 'asc' ? 'desc' : 'asc');
    } else {
      setSortBy(column);
      setSortOrder('asc');
    }
  };

  const openCreateModal = () => {
    setFormData({ name: '', description: '', category: '', initialContent: '' });
    setModalState({ isOpen: true, mode: 'create' });
  };

  const openEditModal = (template: PromptTemplate) => {
    setFormData({
      name: template.name,
      description: template.description,
      category: template.category,
      initialContent: '',
    });
    setModalState({ isOpen: true, mode: 'edit', template });
  };

  const closeModal = () => {
    setModalState({ isOpen: false, mode: 'create' });
    setFormData({ name: '', description: '', category: '', initialContent: '' });
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setFormLoading(true);

    try {
      if (modalState.mode === 'create') {
        await api.admin.createPrompt({
          name: formData.name,
          description: formData.description,
          content: formData.initialContent,
        });
        showToast('Template created successfully', 'success');
      } else if (modalState.template) {
        await api.admin.updatePrompt(modalState.template.id, {
          name: formData.name,
          description: formData.description,
        });
        showToast('Template updated successfully', 'success');
      }

      closeModal();
      fetchTemplates();
    } catch (err) {
      showToast(getErrorMessage(err, 'Operation failed'), 'error');
    } finally {
      setFormLoading(false);
    }
  };

  const openDeleteDialog = (template: PromptTemplate) => {
    setDeleteDialog({ isOpen: true, template });
  };

  const closeDeleteDialog = () => {
    setDeleteDialog({ isOpen: false });
  };

  const handleDelete = async () => {
    if (!deleteDialog.template) return;

    try {
      await api.admin.deletePrompt(deleteDialog.template.id);
      showToast('Template deleted successfully', 'success');
      closeDeleteDialog();
      fetchTemplates();
    } catch (err) {
      showToast(getErrorMessage(err, 'Delete failed'), 'error');
    }
  };

  const navigateToDetails = (id: string) => {
    router.push(`/admin/prompts/${id}`);
  };

  return (
    <AdminAuthGuard loading={authLoading} user={user} backgroundClass="min-h-dvh">
      <div
        className="min-h-dvh"
        style={{ background: 'linear-gradient(135deg, #667eea 0%, #764ba2 100%)' }}
      >
        <div className="max-w-7xl mx-auto p-8">
          <div className="bg-white rounded-xl shadow-2xl overflow-hidden">
            {/* Header */}
            <div
              className="p-8 text-white"
              style={{ background: 'linear-gradient(135deg, #667eea 0%, #764ba2 100%)' }}
            >
              <h1 className="text-3xl font-bold mb-2">Prompt Templates</h1>
              <p className="opacity-90">Manage system prompts and AI templates</p>
            </div>

            {/* Filters */}
            <div className="p-6 border-b border-gray-200 bg-gray-50">
              <div className="flex gap-4 flex-wrap items-center">
                <input
                  type="text"
                  placeholder="Search by name..."
                  value={search}
                  onChange={e => setSearch(e.target.value)}
                  className="flex-1 min-w-[200px] px-4 py-2 border border-gray-300 rounded-lg"
                />

                <select
                  value={categoryFilter}
                  onChange={e => setCategoryFilter(e.target.value)}
                  className="px-4 py-2 border border-gray-300 rounded-lg min-w-[200px]"
                >
                  <option value="">All Categories</option>
                  {CATEGORIES.map(cat => (
                    <option key={cat} value={cat}>
                      {cat}
                    </option>
                  ))}
                </select>

                <button
                  onClick={openCreateModal}
                  className="px-6 py-2 text-white border-none rounded-lg font-semibold cursor-pointer transition-transform hover:scale-105"
                  style={{ background: 'linear-gradient(135deg, #667eea 0%, #764ba2 100%)' }}
                >
                  Create Template
                </button>
              </div>
            </div>

            {/* Content */}
            <div className="p-6">
              {dataLoading && <div className="text-center p-8 text-gray-500">Loading...</div>}

              {error && (
                <div className="mb-4">
                  <ErrorDisplay
                    error={categorizeError(new Error(error))}
                    onRetry={fetchTemplates}
                    showTechnicalDetails={process.env.NODE_ENV === 'development'}
                  />
                </div>
              )}

              {!dataLoading && !error && templates.length === 0 && (
                <div className="text-center p-12 text-gray-500">
                  <p className="text-lg mb-2">No templates found</p>
                  <p className="text-sm">Create your first prompt template to get started</p>
                </div>
              )}

              {!dataLoading && !error && templates.length > 0 && (
                <>
                  <div className="overflow-x-auto">
                    <table className="w-full border-collapse">
                      <thead>
                        <tr className="bg-gray-50 border-b-2 border-gray-200">
                          <th
                            onClick={() => handleSort('name')}
                            className="p-4 text-left font-semibold cursor-pointer select-none"
                          >
                            Name {sortBy === 'name' && (sortOrder === 'asc' ? '↑' : '↓')}
                          </th>
                          <th className="p-4 text-left font-semibold">Description</th>
                          <th
                            onClick={() => handleSort('category')}
                            className="p-4 text-left font-semibold cursor-pointer select-none"
                          >
                            Category {sortBy === 'category' && (sortOrder === 'asc' ? '↑' : '↓')}
                          </th>
                          <th className="p-4 text-left font-semibold">Active Version</th>
                          <th
                            onClick={() => handleSort('createdAt')}
                            className="p-4 text-left font-semibold cursor-pointer select-none"
                          >
                            Created At {sortBy === 'createdAt' && (sortOrder === 'asc' ? '↑' : '↓')}
                          </th>
                          <th className="p-4 text-left font-semibold">Actions</th>
                        </tr>
                      </thead>
                      <tbody>
                        {templates.map(template => (
                          <tr key={template.id} className="border-b border-gray-200">
                            <td className="p-4 font-medium">{template.name}</td>
                            <td className="p-4 text-gray-500 max-w-xs overflow-hidden text-ellipsis whitespace-nowrap">
                              {template.description}
                            </td>
                            <td className="p-4">
                              <span className="px-3 py-1 bg-indigo-100 text-indigo-700 rounded-full text-sm font-medium">
                                {template.category}
                              </span>
                            </td>
                            <td className="p-4">
                              {template.activeVersionId ? (
                                <span className="text-emerald-600 font-medium">✓ Active</span>
                              ) : (
                                <span className="text-gray-500">No active version</span>
                              )}
                            </td>
                            <td className="p-4 text-gray-500">
                              {new Date(template.createdAt).toLocaleDateString()}
                            </td>
                            <td className="p-4">
                              <div className="flex gap-2">
                                <button
                                  onClick={() => navigateToDetails(template.id)}
                                  className="px-3 py-1 bg-indigo-100 text-indigo-700 border-none rounded-md text-sm cursor-pointer font-medium hover:bg-indigo-200"
                                >
                                  View
                                </button>
                                <button
                                  onClick={() => openEditModal(template)}
                                  className="px-3 py-1 bg-blue-100 text-blue-800 border-none rounded-md text-sm cursor-pointer font-medium hover:bg-blue-200"
                                >
                                  Edit
                                </button>
                                <button
                                  onClick={() => openDeleteDialog(template)}
                                  className="px-3 py-1 bg-red-100 text-red-800 border-none rounded-md text-sm cursor-pointer font-medium hover:bg-red-200"
                                >
                                  Delete
                                </button>
                              </div>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>

                  {/* Pagination */}
                  <div className="flex justify-center items-center gap-4 mt-6">
                    <button
                      onClick={() => setPage(p => Math.max(1, p - 1))}
                      disabled={page === 1}
                      className={cn(
                        'px-4 py-2 border-none rounded-lg font-medium',
                        page === 1
                          ? 'bg-gray-200 text-gray-400 cursor-not-allowed'
                          : 'bg-indigo-500 text-white cursor-pointer hover:bg-indigo-600'
                      )}
                    >
                      Previous
                    </button>
                    <span className="text-gray-500 font-medium">
                      Page {page} of {totalPages}
                    </span>
                    <button
                      onClick={() => setPage(p => Math.min(totalPages, p + 1))}
                      disabled={page === totalPages}
                      className={cn(
                        'px-4 py-2 border-none rounded-lg font-medium',
                        page === totalPages
                          ? 'bg-gray-200 text-gray-400 cursor-not-allowed'
                          : 'bg-indigo-500 text-white cursor-pointer hover:bg-indigo-600'
                      )}
                    >
                      Next
                    </button>
                  </div>
                </>
              )}
            </div>
          </div>
        </div>

        {/* Create/Edit Modal */}
        {modalState.isOpen && (
          <div
            className="fixed inset-0 bg-black/50 flex items-center justify-center z-50"
            onClick={closeModal}
          >
            <div
              onClick={e => e.stopPropagation()}
              className="bg-white rounded-xl p-8 max-w-3xl w-[90%] max-h-[90vh] overflow-y-auto"
            >
              <h2 className="text-2xl font-bold mb-6">
                {modalState.mode === 'create' ? 'Create Template' : 'Edit Template'}
              </h2>

              <form onSubmit={handleSubmit}>
                <div className="mb-4">
                  <label className="block mb-2 font-medium">Name</label>
                  <input
                    type="text"
                    required
                    value={formData.name}
                    onChange={e => setFormData({ ...formData, name: e.target.value })}
                    className="w-full px-4 py-2 border border-gray-300 rounded-lg"
                  />
                </div>

                <div className="mb-4">
                  <label className="block mb-2 font-medium">Description</label>
                  <textarea
                    required
                    value={formData.description}
                    onChange={e => setFormData({ ...formData, description: e.target.value })}
                    rows={3}
                    className="w-full px-4 py-2 border border-gray-300 rounded-lg"
                  />
                </div>

                <div className="mb-4">
                  <label className="block mb-2 font-medium">Category</label>
                  <select
                    required
                    value={formData.category}
                    onChange={e => setFormData({ ...formData, category: e.target.value })}
                    className="w-full px-4 py-2 border border-gray-300 rounded-lg"
                  >
                    <option value="">Select category...</option>
                    {CATEGORIES.map(cat => (
                      <option key={cat} value={cat}>
                        {cat}
                      </option>
                    ))}
                  </select>
                </div>

                {modalState.mode === 'create' && (
                  <div className="mb-4">
                    <label className="block mb-2 font-medium">Initial Content</label>
                    <textarea
                      required
                      value={formData.initialContent}
                      onChange={e => setFormData({ ...formData, initialContent: e.target.value })}
                      rows={10}
                      placeholder="Enter the initial prompt content..."
                      className="w-full px-4 py-2 border border-gray-300 rounded-lg font-mono text-sm"
                    />
                  </div>
                )}

                <div className="flex gap-4 justify-end">
                  <button
                    type="button"
                    onClick={closeModal}
                    className="px-6 py-2 bg-gray-200 text-gray-700 border-none rounded-lg cursor-pointer font-medium hover:bg-gray-300"
                  >
                    Cancel
                  </button>
                  <button
                    type="submit"
                    disabled={formLoading}
                    className={cn(
                      'px-6 py-2 text-white border-none rounded-lg font-medium',
                      formLoading
                        ? 'bg-gray-400 cursor-not-allowed'
                        : 'bg-indigo-500 cursor-pointer hover:bg-indigo-600'
                    )}
                  >
                    {formLoading ? 'Saving...' : modalState.mode === 'create' ? 'Create' : 'Update'}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}

        {/* Delete Confirmation Dialog */}
        {deleteDialog.isOpen && deleteDialog.template && (
          <div
            className="fixed inset-0 bg-black/50 flex items-center justify-center z-50"
            onClick={closeDeleteDialog}
          >
            <div
              onClick={e => e.stopPropagation()}
              className="bg-white rounded-xl p-8 max-w-lg w-[90%]"
            >
              <h2 className="text-2xl font-bold mb-4 text-red-800">Confirm Delete</h2>
              <p className="mb-6 text-gray-500">
                Are you sure you want to delete <strong>{deleteDialog.template.name}</strong>? This
                action cannot be undone.
              </p>
              <div className="flex gap-4 justify-end">
                <button
                  onClick={closeDeleteDialog}
                  className="px-6 py-2 bg-gray-200 text-gray-700 border-none rounded-lg cursor-pointer font-medium hover:bg-gray-300"
                >
                  Cancel
                </button>
                <button
                  onClick={handleDelete}
                  className="px-6 py-2 bg-red-600 text-white border-none rounded-lg cursor-pointer font-medium hover:bg-red-700"
                >
                  Delete
                </button>
              </div>
            </div>
          </div>
        )}

        {/* Toast Notification */}
        {toast.show && (
          <div
            className={cn(
              'fixed bottom-8 right-8 px-6 py-4 rounded-lg shadow-2xl z-[100] font-medium',
              toast.type === 'success' ? 'bg-emerald-500 text-white' : 'bg-red-500 text-white'
            )}
          >
            {toast.message}
          </div>
        )}
      </div>
    </AdminAuthGuard>
  );
}
