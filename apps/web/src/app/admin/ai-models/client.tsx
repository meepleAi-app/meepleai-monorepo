/**
 * AI Models Dashboard Client (Issue #2521)
 *
 * Client component for AI models management with real-time updates.
 */

'use client';

import { useState } from 'react';

import { AdminLayout } from '@/components/admin/AdminLayout';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { AlertCircle, Star, TrendingUp } from 'lucide-react';

import { useAiModels, useCostTracking } from '@/hooks/queries';

export function AiModelsClient() {
  // Fetch AI models and cost tracking data
  const { data: modelsData, isLoading: modelsLoading, error: modelsError } = useAiModels();
  const { data: costData, isLoading: costLoading, error: costError } = useCostTracking();

  const models = modelsData?.items || [];
  const primaryModel = models.find((m) => m.isPrimary);

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
          {/* OpenRouter API Status */}
          <Badge variant="secondary" className="bg-green-100 text-green-800 dark:bg-green-950 dark:text-green-100">
            OpenRouter API ✅
          </Badge>
        </div>

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

        {/* Models Table Placeholder */}
        <Card>
          <CardHeader>
            <CardTitle>Available Models</CardTitle>
            <CardDescription>
              Configure AI models and set primary model for responses
            </CardDescription>
          </CardHeader>
          <CardContent>
            {modelsLoading ? (
              <div className="space-y-2">
                {Array.from({ length: 4 }).map((_, i) => (
                  <Skeleton key={i} className="h-16 w-full" />
                ))}
              </div>
            ) : (
              <p className="text-muted-foreground text-center py-8">
                Models table component coming in Phase 3
              </p>
            )}
          </CardContent>
        </Card>
      </div>
    </AdminLayout>
  );
}
