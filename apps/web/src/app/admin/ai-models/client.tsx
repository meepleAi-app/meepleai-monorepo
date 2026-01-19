/**
 * AI Models Dashboard Client (Issue #2521)
 *
 * Client component for AI models management with real-time updates.
 */

'use client';

import { useState } from 'react';

import { AdminLayout } from '@/components/admin/AdminLayout';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/data-display/card';
import { Badge } from '@/components/ui/data-display/badge';
import { Button } from '@/components/ui/primitives/button';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/feedback/alert';
import { AlertCircle, Star, TrendingUp } from 'lucide-react';

import { useAiModels, useCostTracking, useSetPrimaryModel } from '@/hooks/queries';
import { AiModelsTable } from '@/components/admin/AiModelsTable';
import { SetPrimaryModelDialog } from '@/components/admin/SetPrimaryModelDialog';
import { ModelConfigModal } from '@/components/admin/ModelConfigModal';
import { BudgetAlertBanner } from '@/components/admin/BudgetAlertBanner';
import { ExportUsageButton } from '@/components/admin/ExportUsageButton';
import { toast } from '@/components/layout/Toast';
import type { AiModelDto } from '@/lib/api';

export function AiModelsClient() {
  // Modal state
  const [setPrimaryDialog, setSetPrimaryDialog] = useState<{
    isOpen: boolean;
    modelId: string;
    modelName: string;
  }>({
    isOpen: false,
    modelId: '',
    modelName: '',
  });

  const [configureModal, setConfigureModal] = useState<{
    isOpen: boolean;
    modelId: string;
    model: AiModelDto | null;
  }>({
    isOpen: false,
    modelId: '',
    model: null,
  });

  // Fetch AI models and cost tracking data
  const { data: modelsData, isLoading: modelsLoading, error: modelsError } = useAiModels();
  const { data: costData, isLoading: costLoading, error: costError } = useCostTracking();
  const setPrimaryMutation = useSetPrimaryModel();

  const models = modelsData?.items || [];
  const primaryModel = models.find((m) => m.isPrimary);

  // Handlers
  const handleSetPrimaryClick = (modelId: string, modelName: string) => {
    setSetPrimaryDialog({
      isOpen: true,
      modelId,
      modelName,
    });
  };

  const handleSetPrimaryConfirm = async () => {
    try {
      await setPrimaryMutation.mutateAsync({ modelId: setPrimaryDialog.modelId });
      toast.success(`${setPrimaryDialog.modelName} impostato come modello primario!`);
      setSetPrimaryDialog((prev) => ({ ...prev, isOpen: false }));
    } catch (error) {
      toast.error(
        error instanceof Error
          ? error.message
          : 'Errore durante l\'impostazione del modello primario'
      );
    }
  };

  const handleConfigureClick = (modelId: string, model: AiModelDto) => {
    setConfigureModal({
      isOpen: true,
      modelId,
      model,
    });
  };

  return (
    <AdminLayout>
      <div className="space-y-6">
        {/* Page Header */}
        <div className="flex flex-col md:flex-row justify-between items-start md:items-center gap-4">
          <div>
            <h1 className="text-3xl font-bold mb-2">AI Model Configuration</h1>
            <p className="text-muted-foreground">
              Manage runtime AI models, configure parameters, and monitor costs
            </p>
          </div>
          <div className="flex items-center gap-2">
            <ExportUsageButton />
            {/* OpenRouter API Status */}
            <Badge variant="secondary" className="bg-green-100 text-green-800 dark:bg-green-950 dark:text-green-100">
              OpenRouter API ✅
            </Badge>
          </div>
        </div>

        {/* Budget Alert Banner */}
        {costData && <BudgetAlertBanner costData={costData} />}

        {/* Error State */}
        {(modelsError || costError) && (
          <Alert variant="destructive">
            <AlertCircle className="h-4 w-4" />
            <AlertTitle>Errore</AlertTitle>
            <AlertDescription>
              {modelsError instanceof Error
                ? modelsError.message
                : costError instanceof Error
                  ? costError.message
                  : 'Errore nel caricamento dei dati'}
            </AlertDescription>
          </Alert>
        )}

        {/* Current Primary Model Card */}
        {modelsLoading ? (
          <Skeleton className="h-32 w-full" />
        ) : primaryModel ? (
          <Card className="border-2 border-primary">
            <CardHeader>
              <div className="flex items-center justify-between">
                <div>
                  <CardTitle className="flex items-center gap-2">
                    🤖 Active Model
                    <Badge variant="default">
                      <Star className="h-3 w-3 mr-1" />
                      Primary
                    </Badge>
                  </CardTitle>
                  <CardDescription className="mt-1">{primaryModel.displayName}</CardDescription>
                </div>
              </div>
            </CardHeader>
            <CardContent>
              <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                <div>
                  <p className="text-sm text-muted-foreground">Model ID</p>
                  <p className="font-mono text-sm">{primaryModel.modelIdentifier}</p>
                </div>
                {primaryModel.usageStats && (
                  <>
                    <div>
                      <p className="text-sm text-muted-foreground">Usage Today</p>
                      <p className="font-semibold">
                        {primaryModel.usageStats.totalRequests.toLocaleString()} requests |{' '}
                        ${primaryModel.usageStats.estimatedCost.toFixed(2)}
                      </p>
                    </div>
                    <div>
                      <p className="text-sm text-muted-foreground">Configuration</p>
                      <p className="text-sm">
                        Temperature: {primaryModel.temperature} | Max Tokens: {primaryModel.maxTokens}
                      </p>
                    </div>
                  </>
                )}
              </div>
            </CardContent>
          </Card>
        ) : (
          <Alert>
            <AlertCircle className="h-4 w-4" />
            <AlertTitle>No Primary Model</AlertTitle>
            <AlertDescription>
              Nessun modello primario configurato. Seleziona un modello dalla tabella sottostante.
            </AlertDescription>
          </Alert>
        )}

        {/* Cost Tracking Card Placeholder */}
        {costLoading ? (
          <Skeleton className="h-48 w-full" />
        ) : costData ? (
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                💰 Cost Tracking
              </CardTitle>
            </CardHeader>
            <CardContent>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                {/* Today */}
                <div>
                  <p className="text-sm text-muted-foreground mb-2">Today</p>
                  <p className="text-2xl font-bold">
                    ${costData.today.totalCost.toFixed(2)}{' '}
                    <span className="text-sm text-muted-foreground">
                      / ${costData.today.budgetLimit.toFixed(2)}
                    </span>
                  </p>
                  <p className="text-sm text-muted-foreground">
                    {costData.today.percentageUsed.toFixed(1)}% used
                  </p>
                </div>
                {/* This Month */}
                <div>
                  <p className="text-sm text-muted-foreground mb-2">This Month</p>
                  <p className="text-2xl font-bold">
                    ${costData.thisMonth.totalCost.toFixed(2)}{' '}
                    <span className="text-sm text-muted-foreground">
                      / ${costData.thisMonth.budgetLimit.toFixed(2)}
                    </span>
                  </p>
                  <p className="text-sm text-muted-foreground">
                    {costData.thisMonth.percentageUsed.toFixed(1)}% used
                  </p>
                </div>
              </div>
              <div className="mt-4">
                <div className="flex items-center justify-between mb-2">
                  <span className="text-sm font-medium">Budget Status</span>
                  <Badge
                    variant={
                      costData.budgetStatus === 'exceeded'
                        ? 'destructive'
                        : costData.budgetStatus === 'critical'
                          ? 'destructive'
                          : costData.budgetStatus === 'warning'
                            ? 'secondary'
                            : 'default'
                    }
                  >
                    {costData.budgetStatus === 'on_track'
                      ? '✅ On Track'
                      : costData.budgetStatus === 'warning'
                        ? '⚠️ Warning'
                        : costData.budgetStatus === 'critical'
                          ? '🚨 Critical'
                          : '❌ Exceeded'}
                  </Badge>
                </div>
                <Button variant="outline" size="sm">
                  View Detailed Report
                </Button>
              </div>
            </CardContent>
          </Card>
        ) : null}

        {/* Models Table */}
        <Card>
          <CardHeader>
            <CardTitle>Available Models</CardTitle>
            <CardDescription>
              Configure AI models and set primary model for responses
            </CardDescription>
          </CardHeader>
          <CardContent>
            <AiModelsTable
              models={models}
              onSetPrimary={handleSetPrimaryClick}
              onConfigure={handleConfigureClick}
              isLoading={modelsLoading}
            />
          </CardContent>
        </Card>
      </div>

      {/* Set Primary Dialog */}
      <SetPrimaryModelDialog
        isOpen={setPrimaryDialog.isOpen}
        onClose={() => setSetPrimaryDialog((prev) => ({ ...prev, isOpen: false }))}
        onConfirm={handleSetPrimaryConfirm}
        modelName={setPrimaryDialog.modelName}
        isLoading={setPrimaryMutation.isPending}
      />

      {/* Configure Modal */}
      <ModelConfigModal
        isOpen={configureModal.isOpen}
        onClose={() => setConfigureModal((prev) => ({ ...prev, isOpen: false }))}
        modelId={configureModal.modelId}
        model={configureModal.model}
      />
    </AdminLayout>
  );
}
