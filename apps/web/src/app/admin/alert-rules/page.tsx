/**
 * Alert Rules Management Page (Issue #921)
 *
 * Complete UI for managing alert rules:
 * - Create/Edit/Delete alert rules
 * - Enable/Disable rules with toggle
 * - Apply templates for common scenarios
 * - Real-time validation with Zod schemas
 */

'use client';

import { useState } from 'react';

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Plus, AlertTriangle, Sparkles } from 'lucide-react';
import { toast } from 'sonner';

import { AdminAuthGuard } from '@/components/admin/AdminAuthGuard';
import { AlertRuleForm } from '@/components/admin/alert-rules/AlertRuleForm';
import { AlertRuleList } from '@/components/admin/alert-rules/AlertRuleList';
import { useAuthUser } from '@/components/auth/AuthProvider';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { ConfirmDialog } from '@/components/ui/feedback/confirm-dialog';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/navigation/tabs';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { alertRulesApi } from '@/lib/api/alert-rules.api';
import type {
  AlertRule,
  AlertTemplate,
  CreateAlertRule,
} from '@/lib/api/schemas/alert-rules.schemas';

/**
 * Maps an alert template to form data with type-safe severity handling
 */
function mapTemplateToFormData(template: AlertTemplate): Partial<CreateAlertRule> {
  const severityMap: Record<string, CreateAlertRule['severity']> = {
    Info: 'Info',
    Warning: 'Warning',
    Error: 'Error',
    Critical: 'Critical',
  };

  return {
    name: template.name,
    alertType: template.alertType,
    severity: severityMap[template.severity] ?? 'Warning',
    thresholdValue: template.thresholdValue,
    thresholdUnit: template.thresholdUnit,
    durationMinutes: template.durationMinutes,
    description: template.description,
  };
}

function AlertRulesClient() {
  const { user, loading: authLoading } = useAuthUser();
  const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
  const [editingRule, setEditingRule] = useState<AlertRule | null>(null);
  const [deletingRuleId, setDeletingRuleId] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState<'rules' | 'templates'>('rules');
  const [templateData, setTemplateData] = useState<Partial<CreateAlertRule> | null>(null);

  const queryClient = useQueryClient();

  // Fetch all alert rules
  const {
    data: rules = [],
    isLoading,
    error,
  } = useQuery({
    queryKey: ['alert-rules'],
    queryFn: alertRulesApi.getAll,
    refetchInterval: 30000, // Refresh every 30s
  });

  // Fetch templates
  const { data: templates = [] } = useQuery({
    queryKey: ['alert-templates'],
    queryFn: alertRulesApi.getTemplates,
    enabled: activeTab === 'templates',
  });

  // Toggle rule mutation
  const toggleMutation = useMutation({
    mutationFn: alertRulesApi.toggle,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['alert-rules'] });
      toast.success('Alert rule updated');
    },
    onError: (error: Error) => {
      toast.error('Failed to toggle rule', { description: error.message });
    },
  });

  // Delete rule mutation
  const deleteMutation = useMutation({
    mutationFn: alertRulesApi.delete,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['alert-rules'] });
      toast.success('Alert rule deleted');
      setDeletingRuleId(null);
    },
    onError: (error: Error) => {
      toast.error('Failed to delete rule', { description: error.message });
    },
  });

  const handleEdit = (rule: AlertRule) => {
    setEditingRule(rule);
  };

  const handleDelete = (id: string) => {
    setDeletingRuleId(id);
  };

  const handleToggle = (id: string) => {
    toggleMutation.mutate(id);
  };

  const handleFormSuccess = () => {
    queryClient.invalidateQueries({ queryKey: ['alert-rules'] });
    setIsCreateDialogOpen(false);
    setEditingRule(null);
    setTemplateData(null);
  };

  const handleApplyTemplate = (template: AlertTemplate) => {
    const formData = mapTemplateToFormData(template);
    setTemplateData(formData);
    setIsCreateDialogOpen(true);
  };

  const confirmDelete = () => {
    if (deletingRuleId) {
      deleteMutation.mutate(deletingRuleId);
    }
  };

  const deletingRule = rules.find(r => r.id === deletingRuleId);

  return (
    <AdminAuthGuard loading={authLoading} user={user}>
      {isLoading ? (
        <div className="container mx-auto p-6">
          <div className="flex items-center justify-center h-64">
            <div className="text-muted-foreground">Loading alert rules...</div>
          </div>
        </div>
      ) : error ? (
        <div className="container mx-auto p-6">
          <Card className="border-destructive">
            <CardContent className="pt-6">
              <div className="flex items-center gap-2 text-destructive">
                <AlertTriangle className="h-5 w-5" />
                <p>Failed to load alert rules</p>
              </div>
            </CardContent>
          </Card>
        </div>
      ) : (
        <div className="container mx-auto p-6 space-y-6">
          {/* Header */}
          <div className="flex justify-between items-center">
            <div>
              <h1 className="text-3xl font-bold">Alert Rules</h1>
              <p className="text-muted-foreground mt-1">
                Configure alert rules for system monitoring and notifications
              </p>
            </div>
            <Button onClick={() => setIsCreateDialogOpen(true)}>
              <Plus className="mr-2 h-4 w-4" />
              Create Rule
            </Button>
          </div>

          {/* Stats Cards */}
          <div className="grid gap-4 md:grid-cols-4">
            <Card>
              <CardHeader className="pb-2">
                <CardDescription>Total Rules</CardDescription>
                <CardTitle className="text-3xl">{rules.length}</CardTitle>
              </CardHeader>
            </Card>
            <Card>
              <CardHeader className="pb-2">
                <CardDescription>Active Rules</CardDescription>
                <CardTitle className="text-3xl text-green-600">
                  {rules.filter(r => r.isEnabled).length}
                </CardTitle>
              </CardHeader>
            </Card>
            <Card>
              <CardHeader className="pb-2">
                <CardDescription>Critical Rules</CardDescription>
                <CardTitle className="text-3xl text-red-600">
                  {rules.filter(r => r.severity === 'Critical').length}
                </CardTitle>
              </CardHeader>
            </Card>
            <Card>
              <CardHeader className="pb-2">
                <CardDescription>Templates</CardDescription>
                <CardTitle className="text-3xl">{templates.length}</CardTitle>
              </CardHeader>
            </Card>
          </div>

          {/* Tabs: Rules vs Templates */}
          <Tabs value={activeTab} onValueChange={v => setActiveTab(v as 'rules' | 'templates')}>
            <TabsList>
              <TabsTrigger value="rules">Alert Rules</TabsTrigger>
              <TabsTrigger value="templates">
                <Sparkles className="mr-2 h-4 w-4" />
                Templates
              </TabsTrigger>
            </TabsList>

            <TabsContent value="rules" className="space-y-4">
              <Card>
                <CardContent className="pt-6">
                  <AlertRuleList
                    rules={rules}
                    onEdit={handleEdit}
                    onDelete={handleDelete}
                    onToggle={handleToggle}
                  />
                </CardContent>
              </Card>
            </TabsContent>

            <TabsContent value="templates" className="space-y-4">
              <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
                {templates.map((template, idx) => (
                  <Card key={idx} className="hover:shadow-lg transition-shadow cursor-pointer">
                    <CardHeader>
                      <CardTitle className="text-lg">{template.name}</CardTitle>
                      <CardDescription>{template.description}</CardDescription>
                    </CardHeader>
                    <CardContent>
                      <div className="space-y-2 text-sm">
                        <div className="flex justify-between">
                          <span className="text-muted-foreground">Type:</span>
                          <span className="font-medium">{template.alertType}</span>
                        </div>
                        <div className="flex justify-between">
                          <span className="text-muted-foreground">Threshold:</span>
                          <span className="font-medium">
                            {template.thresholdValue} {template.thresholdUnit}
                          </span>
                        </div>
                        <div className="flex justify-between">
                          <span className="text-muted-foreground">Duration:</span>
                          <span className="font-medium">{template.durationMinutes} min</span>
                        </div>
                      </div>
                      <Button
                        className="w-full mt-4"
                        variant="outline"
                        onClick={() => handleApplyTemplate(template)}
                      >
                        Apply Template
                      </Button>
                    </CardContent>
                  </Card>
                ))}
              </div>
            </TabsContent>
          </Tabs>

          {/* Create Dialog */}
          <Dialog
            open={isCreateDialogOpen}
            onOpenChange={open => {
              setIsCreateDialogOpen(open);
              if (!open) setTemplateData(null);
            }}
          >
            <DialogContent className="max-w-2xl">
              <DialogHeader>
                <DialogTitle>Create Alert Rule</DialogTitle>
              </DialogHeader>
              <AlertRuleForm
                rule={null}
                initialData={templateData ?? undefined}
                onSubmit={handleFormSuccess}
                onCancel={() => {
                  setIsCreateDialogOpen(false);
                  setTemplateData(null);
                }}
              />
            </DialogContent>
          </Dialog>

          {/* Edit Dialog */}
          <Dialog open={!!editingRule} onOpenChange={open => !open && setEditingRule(null)}>
            <DialogContent className="max-w-2xl">
              <DialogHeader>
                <DialogTitle>Edit Alert Rule</DialogTitle>
              </DialogHeader>
              <AlertRuleForm
                rule={editingRule}
                onSubmit={handleFormSuccess}
                onCancel={() => setEditingRule(null)}
              />
            </DialogContent>
          </Dialog>

          {/* Delete Confirmation */}
          <ConfirmDialog
            open={!!deletingRuleId}
            onOpenChange={open => !open && setDeletingRuleId(null)}
            title="Delete Alert Rule"
            message={`Are you sure you want to delete "${deletingRule?.name}"? This action cannot be undone.`}
            variant="destructive"
            confirmText="Delete"
            onConfirm={confirmDelete}
          />
        </div>
      )}
    </AdminAuthGuard>
  );
}

export default function AlertRulesPage() {
  return <AlertRulesClient />;
}
