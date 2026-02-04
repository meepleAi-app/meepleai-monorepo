 
'use client';

/**
 * Issue #920: Report Builder UI
 *
 * Admin page for report generation and scheduling.
 * Features:
 * - Generate reports on-demand
 * - Schedule recurring reports with email delivery
 * - View scheduled reports and execution history
 * - Configure email recipients
 */

import { useState, useEffect } from 'react';

import { AdminAuthGuard } from '@/components/admin/AdminAuthGuard';
import { useAuthUser } from '@/components/auth/AuthProvider';
import { ErrorDisplay } from '@/components/errors';
import { toast } from '@/components/layout';
import { Badge } from '@/components/ui/data-display/badge';
import { Card } from '@/components/ui/data-display/card';
import { Separator } from '@/components/ui/navigation/separator';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/navigation/tabs';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { Textarea } from '@/components/ui/primitives/textarea';
import type {
  ScheduledReportDto,
  ReportExecutionDto,
  ReportTemplate,
  ReportFormat,
  ScheduleReportRequest,
} from '@/lib/api';
import { api } from '@/lib/api';
import { categorizeError } from '@/lib/errorUtils';

// ========== Types ==========

const REPORT_TEMPLATES: Array<{ value: ReportTemplate; label: string; description: string }> = [
  {
    value: 'SystemHealth',
    label: 'System Health',
    description: 'Infrastructure metrics, service uptime, error rates',
  },
  {
    value: 'UserActivity',
    label: 'User Activity',
    description: 'User engagement, session duration, feature usage',
  },
  {
    value: 'AIUsage',
    label: 'AI/LLM Usage',
    description: 'Token usage, model costs, query efficiency',
  },
  {
    value: 'ContentMetrics',
    label: 'Content Metrics',
    description: 'PDF uploads, vector embeddings, RAG performance',
  },
];

const REPORT_FORMATS: Array<{ value: ReportFormat; label: string }> = [
  { value: 'CSV', label: 'CSV' },
  { value: 'JSON', label: 'JSON' },
  { value: 'PDF', label: 'PDF' },
];

const CRON_PRESETS: Array<{ label: string; value: string; description: string }> = [
  { label: 'Daily (9 AM)', value: '0 9 * * *', description: 'Every day at 9:00 AM' },
  { label: 'Weekly (Monday)', value: '0 9 * * 1', description: 'Every Monday at 9:00 AM' },
  { label: 'Monthly (1st)', value: '0 9 1 * *', description: 'First day of month at 9:00 AM' },
  { label: 'Custom', value: 'custom', description: 'Enter cron expression manually' },
];

// ========== Main Component ==========

export function ReportsPageClient() {
  const { user, loading: authLoading } = useAuthUser();

  // State
  const [activeTab, setActiveTab] = useState<'generate' | 'scheduled' | 'history'>('generate');
  const [scheduledReports, setScheduledReports] = useState<ScheduledReportDto[]>([]);
  const [executions, setExecutions] = useState<ReportExecutionDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Dialog states
  const [showScheduleDialog, setShowScheduleDialog] = useState(false);
  const [showEditDialog, setShowEditDialog] = useState(false);
  const [showGenerateDialog, setShowGenerateDialog] = useState(false);
  const [editingReport, setEditingReport] = useState<ScheduledReportDto | null>(null);

  // Form states
  const [scheduleForm, setScheduleForm] = useState<Partial<ScheduleReportRequest>>({
    name: '',
    description: '',
    template: 'SystemHealth',
    format: 'CSV',
    scheduleExpression: '0 9 * * *',
    emailRecipients: [],
    parameters: {},
  });

  const [generateForm, setGenerateForm] = useState<{
    template: ReportTemplate;
    format: ReportFormat;
  }>({
    template: 'SystemHealth',
    format: 'CSV',
  });

  const [emailInput, setEmailInput] = useState('');
  const [cronPreset, setCronPreset] = useState('0 9 * * *');
  const [customCron, setCustomCron] = useState('');
  const [isGenerating, setIsGenerating] = useState(false);

  // Load data
  useEffect(() => {
    loadData();
  }, []);

  const loadData = async () => {
    try {
      setLoading(true);
      setError(null);

      const [reports, history] = await Promise.all([
        api.admin.getScheduledReports(),
        api.admin.getReportExecutions(),
      ]);

      setScheduledReports(reports);
      setExecutions(history);
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to load reports';
      setError(errorMessage);
      toast.error(errorMessage);
    } finally {
      setLoading(false);
    }
  };

  // ========== Generate Report ==========

  const handleGenerateReport = async () => {
    try {
      setIsGenerating(true);

      const blob = await api.admin.generateReport({
        template: generateForm.template,
        format: generateForm.format,
        parameters: {},
      });

      // Download file
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `report_${generateForm.template}_${Date.now()}.${generateForm.format.toLowerCase()}`;
      document.body.appendChild(a);
      a.click();
      window.URL.revokeObjectURL(url);
      document.body.removeChild(a);

      toast.success('Report generated successfully');
      setShowGenerateDialog(false);
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to generate report';
      toast.error(errorMessage);
    } finally {
      setIsGenerating(false);
    }
  };

  // ========== Schedule Report ==========

  const handleScheduleReport = async () => {
    try {
      if (!scheduleForm.name || !scheduleForm.description) {
        toast.error('Name and description are required');
        return;
      }

      const cronExpression = cronPreset === 'custom' ? customCron : cronPreset;

      if (!cronExpression) {
        toast.error('Schedule expression is required');
        return;
      }

      await api.admin.scheduleReport({
        name: scheduleForm.name,
        description: scheduleForm.description,
        // eslint-disable-next-line @typescript-eslint/no-non-null-assertion -- Validated above before this code path
        template: scheduleForm.template!,
        // eslint-disable-next-line @typescript-eslint/no-non-null-assertion -- Validated above before this code path
        format: scheduleForm.format!,
        scheduleExpression: cronExpression,
        emailRecipients: scheduleForm.emailRecipients,
        parameters: scheduleForm.parameters ?? {},
      });

      toast.success('Report scheduled successfully');
      setShowScheduleDialog(false);
      resetScheduleForm();
      loadData();
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to schedule report';
      toast.error(errorMessage);
    }
  };

  // ========== Update Schedule ==========

  const handleUpdateSchedule = async () => {
    if (!editingReport) return;

    try {
      const cronExpression = cronPreset === 'custom' ? customCron : cronPreset;

      await api.admin.updateReportSchedule(editingReport.id, {
        scheduleExpression: cronExpression,
        isActive: editingReport.isActive,
        emailRecipients: scheduleForm.emailRecipients,
      });

      toast.success('Report schedule updated');
      setShowEditDialog(false);
      setEditingReport(null);
      loadData();
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to update schedule';
      toast.error(errorMessage);
    }
  };

  // ========== Delete Report ==========

  const handleDeleteReport = async (reportId: string) => {
    if (!confirm('Are you sure you want to delete this scheduled report?')) return;

    try {
      await api.admin.deleteScheduledReport(reportId);
      toast.success('Report deleted');
      loadData();
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to delete report';
      toast.error(errorMessage);
    }
  };

  // ========== Email Management ==========

  const handleAddEmail = () => {
    if (!emailInput.trim()) return;

    const emails = scheduleForm.emailRecipients || [];
    if (emails.length >= 10) {
      toast.error('Maximum 10 email recipients allowed');
      return;
    }

    if (emails.includes(emailInput.trim())) {
      toast.error('Email already added');
      return;
    }

    // Basic email validation
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(emailInput.trim())) {
      toast.error('Invalid email format');
      return;
    }

    setScheduleForm({
      ...scheduleForm,
      emailRecipients: [...emails, emailInput.trim()],
    });
    setEmailInput('');
  };

  const handleRemoveEmail = (email: string) => {
    setScheduleForm({
      ...scheduleForm,
      emailRecipients: (scheduleForm.emailRecipients || []).filter(e => e !== email),
    });
  };

  // ========== Utilities ==========

  const resetScheduleForm = () => {
    setScheduleForm({
      name: '',
      description: '',
      template: 'SystemHealth',
      format: 'CSV',
      scheduleExpression: '0 9 * * *',
      emailRecipients: [],
      parameters: {},
    });
    setEmailInput('');
    setCronPreset('0 9 * * *');
    setCustomCron('');
  };

  const openEditDialog = (report: ScheduledReportDto) => {
    setEditingReport(report);
    setScheduleForm({
      ...scheduleForm,
      emailRecipients: report.emailRecipients,
    });

    // Set cron preset
    const preset = CRON_PRESETS.find(p => p.value === report.scheduleExpression);
    if (preset && preset.value !== 'custom') {
      setCronPreset(preset.value);
    } else {
      setCronPreset('custom');
      setCustomCron(report.scheduleExpression || '');
    }

    setShowEditDialog(true);
  };

  const formatDate = (dateString: string | null) => {
    if (!dateString) return 'Never';
    return new Date(dateString).toLocaleString();
  };

  const getStatusBadge = (status: string) => {
    const variants: Record<string, 'default' | 'secondary' | 'destructive' | 'outline'> = {
      Pending: 'default',
      Running: 'secondary',
      Completed: 'outline',
      Failed: 'destructive',
    };

    return <Badge variant={variants[status] || 'default'}>{status}</Badge>;
  };

  // ========== Render ==========

  if (loading) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <div className="text-center">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-primary mx-auto mb-4"></div>
          <p className="text-muted-foreground">Loading reports...</p>
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="container mx-auto p-6">
        <ErrorDisplay error={categorizeError(new Error(error))} />
      </div>
    );
  }

  return (
    <AdminAuthGuard loading={authLoading} user={user}>
      <div className="container mx-auto p-6 space-y-6">
        {/* Header */}
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-3xl font-bold">Reports</h1>
            <p className="text-muted-foreground mt-1">
              Generate and schedule system reports with email delivery
            </p>
          </div>
          <div className="flex gap-2">
            <Button onClick={() => setShowGenerateDialog(true)}>📊 Generate Report</Button>
            <Button variant="outline" onClick={() => setShowScheduleDialog(true)}>
              📅 Schedule Report
            </Button>
          </div>
        </div>

        <Separator />

        {/* Tabs */}
        {/* eslint-disable-next-line @typescript-eslint/no-explicit-any -- Tabs value type coercion */}
        <Tabs value={activeTab} onValueChange={v => setActiveTab(v as any)}>
          <TabsList>
            <TabsTrigger value="generate">Generate</TabsTrigger>
            <TabsTrigger value="scheduled">Scheduled ({scheduledReports.length})</TabsTrigger>
            <TabsTrigger value="history">History ({executions.length})</TabsTrigger>
          </TabsList>

          {/* Generate Tab */}
          <TabsContent value="generate" className="space-y-4">
            <Card className="p-6">
              <h2 className="text-xl font-semibold mb-4">Quick Generate</h2>
              <p className="text-sm text-muted-foreground mb-6">
                Generate a report immediately and download it to your device
              </p>

              <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
                {REPORT_TEMPLATES.map(template => (
                  <Card
                    key={template.value}
                    className="p-4 cursor-pointer hover:border-primary transition-colors"
                    onClick={() => {
                      setGenerateForm({ ...generateForm, template: template.value });
                      setShowGenerateDialog(true);
                    }}
                  >
                    <h3 className="font-semibold mb-2">{template.label}</h3>
                    <p className="text-sm text-muted-foreground">{template.description}</p>
                  </Card>
                ))}
              </div>
            </Card>
          </TabsContent>

          {/* Scheduled Tab */}
          <TabsContent value="scheduled" className="space-y-4">
            {scheduledReports.length === 0 ? (
              <Card className="p-12 text-center">
                <p className="text-muted-foreground mb-4">No scheduled reports yet</p>
                <Button onClick={() => setShowScheduleDialog(true)}>Create First Schedule</Button>
              </Card>
            ) : (
              <div className="grid gap-4">
                {scheduledReports.map(report => (
                  <Card key={report.id} className="p-6">
                    <div className="flex items-start justify-between">
                      <div className="flex-1">
                        <div className="flex items-center gap-2 mb-2">
                          <h3 className="text-lg font-semibold">{report.name}</h3>
                          {!report.isActive && <Badge variant="destructive">Inactive</Badge>}
                        </div>
                        <p className="text-sm text-muted-foreground mb-4">{report.description}</p>

                        <div className="grid grid-cols-2 gap-4 text-sm">
                          <div>
                            <span className="text-muted-foreground">Template:</span>{' '}
                            <strong>{report.template}</strong>
                          </div>
                          <div>
                            <span className="text-muted-foreground">Format:</span>{' '}
                            <strong>{report.format}</strong>
                          </div>
                          <div>
                            <span className="text-muted-foreground">Schedule:</span>{' '}
                            <code className="text-xs bg-muted px-2 py-1 rounded">
                              {report.scheduleExpression}
                            </code>
                          </div>
                          <div>
                            <span className="text-muted-foreground">Last Run:</span>{' '}
                            {formatDate(report.lastExecutedAt)}
                          </div>
                        </div>

                        {report.emailRecipients.length > 0 && (
                          <div className="mt-4">
                            <span className="text-sm text-muted-foreground">Email Recipients:</span>
                            <div className="flex flex-wrap gap-2 mt-2">
                              {report.emailRecipients.map(email => (
                                <Badge key={email} variant="outline">
                                  {email}
                                </Badge>
                              ))}
                            </div>
                          </div>
                        )}
                      </div>

                      <div className="flex gap-2">
                        <Button size="sm" variant="outline" onClick={() => openEditDialog(report)}>
                          Edit
                        </Button>
                        <Button
                          size="sm"
                          variant="destructive"
                          onClick={() => handleDeleteReport(report.id)}
                        >
                          Delete
                        </Button>
                      </div>
                    </div>
                  </Card>
                ))}
              </div>
            )}
          </TabsContent>

          {/* History Tab */}
          <TabsContent value="history" className="space-y-4">
            {executions.length === 0 ? (
              <Card className="p-12 text-center">
                <p className="text-muted-foreground">No execution history yet</p>
              </Card>
            ) : (
              <div className="space-y-2">
                {executions.map(execution => (
                  <Card key={execution.id} className="p-4">
                    <div className="flex items-center justify-between">
                      <div className="flex-1">
                        <div className="flex items-center gap-3">
                          <strong>{execution.reportName}</strong>
                          {getStatusBadge(execution.status)}
                          <span className="text-sm text-muted-foreground">
                            {execution.template}
                          </span>
                        </div>
                        <div className="text-sm text-muted-foreground mt-1">
                          Started: {formatDate(execution.startedAt)}
                          {execution.completedAt && (
                            <> • Completed: {formatDate(execution.completedAt)}</>
                          )}
                        </div>
                        {execution.errorMessage && (
                          <div className="text-sm text-destructive mt-1">
                            Error: {execution.errorMessage}
                          </div>
                        )}
                      </div>
                      {execution.fileSize && (
                        <Badge variant="outline">{(execution.fileSize / 1024).toFixed(2)} KB</Badge>
                      )}
                    </div>
                  </Card>
                ))}
              </div>
            )}
          </TabsContent>
        </Tabs>

        {/* Generate Dialog */}
        <Dialog open={showGenerateDialog} onOpenChange={setShowGenerateDialog}>
          <DialogContent>
            <DialogHeader>
              <DialogTitle>Generate Report</DialogTitle>
              <DialogDescription>Generate a report immediately and download it</DialogDescription>
            </DialogHeader>

            <div className="space-y-4">
              <div>
                <Label>Template</Label>
                <Select
                  value={generateForm.template}
                  onValueChange={v =>
                    setGenerateForm({ ...generateForm, template: v as ReportTemplate })
                  }
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {REPORT_TEMPLATES.map(t => (
                      <SelectItem key={t.value} value={t.value}>
                        {t.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              <div>
                <Label>Format</Label>
                <Select
                  value={generateForm.format}
                  onValueChange={v =>
                    setGenerateForm({ ...generateForm, format: v as ReportFormat })
                  }
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {REPORT_FORMATS.map(f => (
                      <SelectItem key={f.value} value={f.value}>
                        {f.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>

            <DialogFooter>
              <Button variant="outline" onClick={() => setShowGenerateDialog(false)}>
                Cancel
              </Button>
              <Button onClick={handleGenerateReport} disabled={isGenerating}>
                {isGenerating ? 'Generating...' : 'Generate'}
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>

        {/* Schedule Dialog */}
        <Dialog open={showScheduleDialog} onOpenChange={setShowScheduleDialog}>
          <DialogContent className="max-w-2xl max-h-[90vh] overflow-y-auto">
            <DialogHeader>
              <DialogTitle>Schedule Report</DialogTitle>
              <DialogDescription>Create a recurring report with email delivery</DialogDescription>
            </DialogHeader>

            <div className="space-y-4">
              <div>
                <Label>Report Name *</Label>
                <Input
                  value={scheduleForm.name}
                  onChange={e => setScheduleForm({ ...scheduleForm, name: e.target.value })}
                  placeholder="e.g., Weekly System Health Report"
                />
              </div>

              <div>
                <Label>Description *</Label>
                <Textarea
                  value={scheduleForm.description}
                  onChange={e => setScheduleForm({ ...scheduleForm, description: e.target.value })}
                  placeholder="Brief description of this report"
                  rows={3}
                />
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div>
                  <Label>Template</Label>
                  <Select
                    value={scheduleForm.template}
                    onValueChange={v =>
                      setScheduleForm({ ...scheduleForm, template: v as ReportTemplate })
                    }
                  >
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {REPORT_TEMPLATES.map(t => (
                        <SelectItem key={t.value} value={t.value}>
                          {t.label}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>

                <div>
                  <Label>Format</Label>
                  <Select
                    value={scheduleForm.format}
                    onValueChange={v =>
                      setScheduleForm({ ...scheduleForm, format: v as ReportFormat })
                    }
                  >
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {REPORT_FORMATS.map(f => (
                        <SelectItem key={f.value} value={f.value}>
                          {f.label}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              </div>

              <div>
                <Label>Schedule</Label>
                <Select value={cronPreset} onValueChange={setCronPreset}>
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {CRON_PRESETS.map(preset => (
                      <SelectItem key={preset.value} value={preset.value}>
                        <div>
                          <div>{preset.label}</div>
                          <div className="text-xs text-muted-foreground">{preset.description}</div>
                        </div>
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              {cronPreset === 'custom' && (
                <div>
                  <Label>Cron Expression</Label>
                  <Input
                    value={customCron}
                    onChange={e => setCustomCron(e.target.value)}
                    placeholder="e.g., 0 9 * * *"
                  />
                  <p className="text-xs text-muted-foreground mt-1">
                    Format: minute hour day month weekday
                  </p>
                </div>
              )}

              <Separator />

              <div>
                <Label>Email Recipients (max 10)</Label>
                <div className="flex gap-2 mt-2">
                  <Input
                    value={emailInput}
                    onChange={e => setEmailInput(e.target.value)}
                    placeholder="email@example.com"
                    onKeyDown={e => {
                      if (e.key === 'Enter') {
                        e.preventDefault();
                        handleAddEmail();
                      }
                    }}
                  />
                  <Button type="button" onClick={handleAddEmail}>
                    Add
                  </Button>
                </div>

                {scheduleForm.emailRecipients && scheduleForm.emailRecipients.length > 0 && (
                  <div className="flex flex-wrap gap-2 mt-3">
                    {scheduleForm.emailRecipients.map(email => (
                      <Badge key={email} variant="outline" className="gap-2">
                        {email}
                        <button
                          onClick={() => handleRemoveEmail(email)}
                          className="text-destructive hover:text-destructive/80"
                        >
                          ×
                        </button>
                      </Badge>
                    ))}
                  </div>
                )}
              </div>
            </div>

            <DialogFooter>
              <Button
                variant="outline"
                onClick={() => {
                  setShowScheduleDialog(false);
                  resetScheduleForm();
                }}
              >
                Cancel
              </Button>
              <Button onClick={handleScheduleReport}>Schedule Report</Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>

        {/* Edit Dialog */}
        <Dialog open={showEditDialog} onOpenChange={setShowEditDialog}>
          <DialogContent className="max-w-2xl">
            <DialogHeader>
              <DialogTitle>Edit Report Schedule</DialogTitle>
              <DialogDescription>
                Update schedule and email recipients for {editingReport?.name}
              </DialogDescription>
            </DialogHeader>

            <div className="space-y-4">
              <div>
                <Label>Schedule</Label>
                <Select value={cronPreset} onValueChange={setCronPreset}>
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {CRON_PRESETS.map(preset => (
                      <SelectItem key={preset.value} value={preset.value}>
                        {preset.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              {cronPreset === 'custom' && (
                <div>
                  <Label>Cron Expression</Label>
                  <Input
                    value={customCron}
                    onChange={e => setCustomCron(e.target.value)}
                    placeholder="e.g., 0 9 * * *"
                  />
                </div>
              )}

              <Separator />

              <div>
                <Label>Email Recipients (max 10)</Label>
                <div className="flex gap-2 mt-2">
                  <Input
                    value={emailInput}
                    onChange={e => setEmailInput(e.target.value)}
                    placeholder="email@example.com"
                    onKeyDown={e => {
                      if (e.key === 'Enter') {
                        e.preventDefault();
                        handleAddEmail();
                      }
                    }}
                  />
                  <Button type="button" onClick={handleAddEmail}>
                    Add
                  </Button>
                </div>

                {scheduleForm.emailRecipients && scheduleForm.emailRecipients.length > 0 && (
                  <div className="flex flex-wrap gap-2 mt-3">
                    {scheduleForm.emailRecipients.map(email => (
                      <Badge key={email} variant="outline" className="gap-2">
                        {email}
                        <button
                          onClick={() => handleRemoveEmail(email)}
                          className="text-destructive hover:text-destructive/80"
                        >
                          ×
                        </button>
                      </Badge>
                    ))}
                  </div>
                )}
              </div>
            </div>

            <DialogFooter>
              <Button
                variant="outline"
                onClick={() => {
                  setShowEditDialog(false);
                  setEditingReport(null);
                }}
              >
                Cancel
              </Button>
              <Button onClick={handleUpdateSchedule}>Update Schedule</Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      </div>
    </AdminAuthGuard>
  );
}
