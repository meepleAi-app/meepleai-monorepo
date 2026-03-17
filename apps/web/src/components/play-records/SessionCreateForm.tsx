/**
 * SessionCreateForm - Multi-step Play Record Creation Wizard
 *
 * 3-step form for creating new play records:
 * 1. Game selection (catalog or free-form)
 * 2. Session details (date, visibility, notes)
 * 3. Scoring configuration (optional)
 *
 * Issue #3892: Play Records Frontend UI
 */

'use client';

import { useState } from 'react';

import { zodResolver } from '@hookform/resolvers/zod';
import { Calendar, Users, Trophy, ArrowLeft, ArrowRight, Check } from 'lucide-react';
import { useForm } from 'react-hook-form';
import { toast } from 'sonner';

import { AddPrivateGameWithBgg } from '@/components/library/AddPrivateGameWithBgg';
import {
  Form,
  FormControl,
  FormDescription,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/forms/form';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/overlays/dialog';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Button } from '@/components/ui/primitives/button';
import { Checkbox } from '@/components/ui/primitives/checkbox';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { RadioGroup, RadioGroupItem } from '@/components/ui/primitives/radio-group';
import { Textarea } from '@/components/ui/primitives/textarea';
import {
  SessionCreateFormSchema,
  type SessionCreateForm as SessionCreateFormData,
} from '@/lib/api/schemas/play-records.schemas';
import { logger } from '@/lib/logger';
import { usePlayRecordsStore } from '@/lib/stores/play-records-store';

import { GameCombobox } from './GameCombobox';

export interface SessionCreateFormProps {
  onSubmit: (data: SessionCreateFormData) => void;
  onCancel?: () => void;
  isSubmitting?: boolean;
}

const STEPS = [
  { id: 0, title: 'Game Selection', icon: Trophy },
  { id: 1, title: 'Session Details', icon: Calendar },
  { id: 2, title: 'Scoring Setup', icon: Users },
];

export function SessionCreateForm({
  onSubmit,
  onCancel,
  isSubmitting = false,
}: SessionCreateFormProps) {
  const { sessionCreation, nextStep, prevStep, resetSessionCreation } = usePlayRecordsStore();

  const [showBggDialog, setShowBggDialog] = useState(false);
  const [isAddingGame, setIsAddingGame] = useState(false);

  const form = useForm<SessionCreateFormData>({
    resolver: zodResolver(SessionCreateFormSchema),
    defaultValues: {
      gameType: 'catalog',
      gameName: '',
      sessionDate: new Date(),
      visibility: 'Private',
      enableScoring: false,
      scoringDimensions: [],
    },
  });

  const currentStep = sessionCreation.currentStep;

  const handleNext = async () => {
    // Validate current step fields
    const fieldsToValidate: Array<keyof SessionCreateFormData> =
      currentStep === 0
        ? ['gameType', 'gameId', 'gameName']
        : currentStep === 1
          ? ['sessionDate', 'visibility', 'groupId']
          : ['enableScoring', 'scoringDimensions'];

    const isValid = await form.trigger(fieldsToValidate);
    if (isValid) nextStep();
  };

  const handlePrev = () => {
    prevStep();
  };

  const handleSubmit = form.handleSubmit(data => {
    onSubmit(data);
    resetSessionCreation();
    form.reset();
  });

  const handleCancel = () => {
    resetSessionCreation();
    form.reset();
    onCancel?.();
  };

  return (
    <div className="max-w-3xl mx-auto space-y-8">
      {/* Progress Steps */}
      <div className="flex items-center justify-between">
        {STEPS.map((step, index) => (
          <div key={step.id} className="flex items-center flex-1">
            <div
              className={`flex items-center justify-center w-10 h-10 rounded-full border-2 transition-colors ${
                currentStep > step.id
                  ? 'bg-primary border-primary text-primary-foreground'
                  : currentStep === step.id
                    ? 'border-primary text-primary'
                    : 'border-muted-foreground/30 text-muted-foreground'
              }`}
            >
              {currentStep > step.id ? (
                <Check className="w-5 h-5" />
              ) : (
                <step.icon className="w-5 h-5" />
              )}
            </div>
            <div className="ml-3 flex-1">
              <p
                className={`text-sm font-medium ${
                  currentStep >= step.id ? 'text-foreground' : 'text-muted-foreground'
                }`}
              >
                {step.title}
              </p>
            </div>
            {index < STEPS.length - 1 && (
              <div
                className={`h-[2px] flex-1 mx-2 ${
                  currentStep > step.id ? 'bg-primary' : 'bg-muted-foreground/20'
                }`}
              />
            )}
          </div>
        ))}
      </div>

      {/* Form */}
      <Form {...form}>
        <form onSubmit={handleSubmit} className="space-y-6">
          {/* Step 1: Game Selection */}
          {currentStep === 0 && (
            <div className="space-y-6">
              <FormField
                control={form.control}
                name="gameType"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Game Source</FormLabel>
                    <FormControl>
                      <RadioGroup
                        onValueChange={field.onChange}
                        defaultValue={field.value}
                        className="flex gap-4"
                      >
                        <div className="flex items-center space-x-2">
                          <RadioGroupItem value="catalog" id="catalog" />
                          <Label htmlFor="catalog">From Catalog</Label>
                        </div>
                        <div className="flex items-center space-x-2">
                          <RadioGroupItem value="freeform" id="freeform" />
                          <Label htmlFor="freeform">Free-form Game</Label>
                        </div>
                      </RadioGroup>
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />

              {form.watch('gameType') === 'catalog' ? (
                <FormField
                  control={form.control}
                  name="gameId"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Select Game</FormLabel>
                      <FormControl>
                        <GameCombobox
                          value={field.value}
                          onSelect={(gameId, gameName) => {
                            field.onChange(gameId);
                            form.setValue('gameName', gameName);
                          }}
                          onNotFound={() => setShowBggDialog(true)}
                          disabled={isSubmitting}
                          placeholder="Search your library..."
                        />
                      </FormControl>
                      <FormDescription>
                        Search your library, private games, or the shared catalog
                      </FormDescription>
                      <FormMessage />
                    </FormItem>
                  )}
                />
              ) : (
                <FormField
                  control={form.control}
                  name="gameName"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Game Name</FormLabel>
                      <FormControl>
                        <Input placeholder="Enter game name..." {...field} />
                      </FormControl>
                      <FormDescription>
                        Enter the name of any board game (not in catalog)
                      </FormDescription>
                      <FormMessage />
                    </FormItem>
                  )}
                />
              )}
            </div>
          )}

          {/* Step 2: Session Details */}
          {currentStep === 1 && (
            <div className="space-y-6">
              <FormField
                control={form.control}
                name="sessionDate"
                render={({ field }) => (
                  <FormItem className="flex flex-col">
                    <FormLabel>Session Date</FormLabel>
                    <FormControl>
                      <Input
                        type="datetime-local"
                        value={
                          field.value instanceof Date ? field.value.toISOString().slice(0, 16) : ''
                        }
                        onChange={e => field.onChange(new Date(e.target.value))}
                        max={new Date().toISOString().slice(0, 16)}
                      />
                    </FormControl>
                    <FormDescription>When did you play this game?</FormDescription>
                    <FormMessage />
                  </FormItem>
                )}
              />

              <FormField
                control={form.control}
                name="visibility"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Visibility</FormLabel>
                    <Select onValueChange={field.onChange} defaultValue={field.value}>
                      <FormControl>
                        <SelectTrigger>
                          <SelectValue />
                        </SelectTrigger>
                      </FormControl>
                      <SelectContent>
                        <SelectItem value="Private">Private (only me)</SelectItem>
                        <SelectItem value="Group">Group (shared with group)</SelectItem>
                      </SelectContent>
                    </Select>
                    <FormMessage />
                  </FormItem>
                )}
              />

              {form.watch('visibility') === 'Group' && (
                <FormField
                  control={form.control}
                  name="groupId"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Select Group</FormLabel>
                      <Select onValueChange={field.onChange} defaultValue={field.value}>
                        <FormControl>
                          <SelectTrigger>
                            <SelectValue placeholder="Choose a group..." />
                          </SelectTrigger>
                        </FormControl>
                        <SelectContent>
                          {/* TODO: Load user's groups */}
                          <SelectItem value="placeholder">Loading groups...</SelectItem>
                        </SelectContent>
                      </Select>
                      <FormMessage />
                    </FormItem>
                  )}
                />
              )}

              <FormField
                control={form.control}
                name="location"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Location (Optional)</FormLabel>
                    <FormControl>
                      <Input placeholder="Where did you play?" {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />

              <FormField
                control={form.control}
                name="notes"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Notes (Optional)</FormLabel>
                    <FormControl>
                      <Textarea
                        placeholder="Any memorable moments or notes about this session..."
                        className="resize-none"
                        rows={4}
                        {...field}
                      />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </div>
          )}

          {/* Step 3: Scoring Setup */}
          {currentStep === 2 && (
            <div className="space-y-6">
              <FormField
                control={form.control}
                name="enableScoring"
                render={({ field }) => (
                  <FormItem className="flex flex-row items-start space-x-3 space-y-0 rounded-md border p-4">
                    <FormControl>
                      <Checkbox checked={field.value} onCheckedChange={field.onChange} />
                    </FormControl>
                    <div className="space-y-1 leading-none">
                      <FormLabel>Enable Custom Scoring</FormLabel>
                      <FormDescription>
                        Track multiple scoring dimensions (points, rankings, wins, etc.)
                      </FormDescription>
                    </div>
                  </FormItem>
                )}
              />

              {form.watch('enableScoring') && (
                <>
                  <FormField
                    control={form.control}
                    name="scoringDimensions"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>Scoring Dimensions</FormLabel>
                        <FormControl>
                          <div className="space-y-2">
                            {['Points', 'Ranking', 'Wins', 'Custom'].map(dim => (
                              <div key={dim} className="flex items-center space-x-2">
                                <Checkbox
                                  checked={field.value?.includes(dim)}
                                  onCheckedChange={checked => {
                                    const current = field.value || [];
                                    field.onChange(
                                      checked ? [...current, dim] : current.filter(d => d !== dim)
                                    );
                                  }}
                                />
                                <Label>{dim}</Label>
                              </div>
                            ))}
                          </div>
                        </FormControl>
                        <FormDescription>
                          Select which scoring metrics to track for this game
                        </FormDescription>
                        <FormMessage />
                      </FormItem>
                    )}
                  />

                  {form.watch('scoringDimensions')?.includes('Custom') && (
                    <FormItem>
                      <FormLabel>Custom Dimension Units</FormLabel>
                      <FormDescription>
                        Define units for custom dimensions (e.g., &quot;Victory Points&quot;,
                        &quot;Resources&quot;)
                      </FormDescription>
                      {/* TODO: Dynamic key-value inputs for dimension units */}
                      <Input placeholder="Coming soon..." disabled />
                    </FormItem>
                  )}
                </>
              )}
            </div>
          )}

          {/* Navigation Buttons */}
          <div className="flex justify-between pt-6 border-t">
            <div>
              {currentStep > 0 && (
                <Button
                  type="button"
                  variant="outline"
                  onClick={handlePrev}
                  disabled={isSubmitting}
                >
                  <ArrowLeft className="w-4 h-4 mr-2" />
                  Previous
                </Button>
              )}
              {currentStep === 0 && onCancel && (
                <Button
                  type="button"
                  variant="ghost"
                  onClick={handleCancel}
                  disabled={isSubmitting}
                >
                  Cancel
                </Button>
              )}
            </div>

            <div className="flex gap-2">
              {currentStep < STEPS.length - 1 ? (
                <Button type="button" onClick={handleNext} disabled={isSubmitting}>
                  Next
                  <ArrowRight className="w-4 h-4 ml-2" />
                </Button>
              ) : (
                <Button type="submit" disabled={isSubmitting}>
                  {isSubmitting ? (
                    <>Creating...</>
                  ) : (
                    <>
                      <Check className="w-4 h-4 mr-2" />
                      Create Session
                    </>
                  )}
                </Button>
              )}
            </div>
          </div>
        </form>
      </Form>

      {/* BGG Search Dialog - Issue #4274 */}
      <Dialog open={showBggDialog} onOpenChange={setShowBggDialog}>
        <DialogContent className="max-w-2xl max-h-[80vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Search BoardGameGeek</DialogTitle>
          </DialogHeader>
          <AddPrivateGameWithBgg
            onSubmit={async (gameData, source, bggId) => {
              setIsAddingGame(true);
              try {
                // Create private game via API
                const response = await fetch('/api/v1/user-library/private-games', {
                  method: 'POST',
                  headers: { 'Content-Type': 'application/json' },
                  body: JSON.stringify({
                    ...gameData,
                    source,
                    bggId,
                  }),
                });

                if (!response.ok) throw new Error('Failed to add game');

                const privateGame = await response.json();

                // Auto-select in wizard
                form.setValue('gameId', privateGame.id);
                form.setValue('gameName', privateGame.title);

                // Close dialog and show success
                setShowBggDialog(false);
                toast.success('Game added to library');
              } catch (err) {
                logger.error('Add game error:', err);
                toast.error('Failed to add game. Please try again.');
              } finally {
                setIsAddingGame(false);
              }
            }}
            onCancel={() => setShowBggDialog(false)}
            isSubmitting={isAddingGame}
          />
        </DialogContent>
      </Dialog>
    </div>
  );
}
