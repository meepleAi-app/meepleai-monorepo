'use client';

import * as React from 'react';

import {
  Copy,
  Eye,
  Globe,
  History,
  Monitor,
  Plus,
  Save,
  Search,
  Send,
  Smartphone,
} from 'lucide-react';

import {
  Badge,
  Button,
  Card,
  CardContent,
  CardHeader,
  CardTitle,
  Input,
  Label,
} from '@/components/ui';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { ScrollArea } from '@/components/ui/primitives/scroll-area';
import { Textarea } from '@/components/ui/primitives/textarea';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  useCreateEmailTemplate,
  useEmailTemplates,
  useEmailTemplateVersions,
  usePreviewEmailTemplate,
  usePublishEmailTemplate,
  useUpdateEmailTemplate,
} from '@/hooks/queries/useEmailTemplates';
import { useToast } from '@/hooks/useToast';
import type { EmailTemplateDto } from '@/lib/api';

import { getPlaceholdersForType, getDefaultTestData } from './_placeholders';

// ─── Locale Helpers ──────────────────────────────────────────────────────────

const LOCALE_OPTIONS = [
  { value: 'all', label: 'Tutte le lingue' },
  { value: 'it', label: 'Italiano' },
  { value: 'en', label: 'English' },
] as const;

function localeFlag(locale: string): string {
  switch (locale.toLowerCase()) {
    case 'it':
      return '\u{1F1EE}\u{1F1F9}';
    case 'en':
      return '\u{1F1EC}\u{1F1E7}';
    default:
      return '\u{1F310}';
  }
}

// ─── Main Content ────────────────────────────────────────────────────────────

export function EmailTemplatesContent() {
  const { toast } = useToast();

  // Filters
  const [search, setSearch] = React.useState('');
  const [localeFilter, setLocaleFilter] = React.useState('all');

  // Selection
  const [selectedId, setSelectedId] = React.useState<string | null>(null);

  // Editor state
  const [editSubject, setEditSubject] = React.useState('');
  const [editHtmlBody, setEditHtmlBody] = React.useState('');
  const textareaRef = React.useRef<HTMLTextAreaElement>(null);

  // Preview
  const [previewOpen, setPreviewOpen] = React.useState(false);
  const [previewHtml, setPreviewHtml] = React.useState('');
  const [previewWidth, setPreviewWidth] = React.useState<'desktop' | 'mobile'>('desktop');
  const [testData, setTestData] = React.useState<Record<string, string>>({});

  // Create dialog
  const [createOpen, setCreateOpen] = React.useState(false);
  const [newName, setNewName] = React.useState('');
  const [newLocale, setNewLocale] = React.useState('it');
  const [newSubject, setNewSubject] = React.useState('');
  const [newHtmlBody, setNewHtmlBody] = React.useState('');

  // Version history
  const [versionsOpen, setVersionsOpen] = React.useState(false);

  // Queries
  const queryParams = localeFilter === 'all' ? undefined : { locale: localeFilter };
  const { data: templates, isLoading } = useEmailTemplates(queryParams);

  const selected = React.useMemo(
    () => templates?.find(t => t.id === selectedId) ?? null,
    [templates, selectedId]
  );

  const { data: versions } = useEmailTemplateVersions(selected?.name ?? '', selected?.locale);

  // Mutations
  const updateMutation = useUpdateEmailTemplate();
  const publishMutation = usePublishEmailTemplate();
  const previewMutation = usePreviewEmailTemplate();
  const createMutation = useCreateEmailTemplate();

  // Sync editor state when selection changes
  React.useEffect(() => {
    if (selected) {
      setEditSubject(selected.subject);
      setEditHtmlBody(selected.htmlBody);
      setTestData(getDefaultTestData(selected.name));
    }
  }, [selected]);

  // Filter templates
  const filtered = React.useMemo(() => {
    if (!templates) return [];
    const q = search.toLowerCase().trim();
    if (!q) return templates;
    return templates.filter(
      t => t.name.toLowerCase().includes(q) || t.subject.toLowerCase().includes(q)
    );
  }, [templates, search]);

  // ── Handlers ─────────────────────────────────────────────────────────────

  const handleSaveDraft = React.useCallback(async () => {
    if (!selectedId) return;
    try {
      await updateMutation.mutateAsync({
        id: selectedId,
        data: { subject: editSubject, htmlBody: editHtmlBody },
      });
      toast({ title: 'Bozza salvata', description: 'Nuova versione creata con successo.' });
    } catch {
      toast({
        title: 'Errore',
        description: 'Impossibile salvare la bozza.',
        variant: 'destructive',
      });
    }
  }, [selectedId, editSubject, editHtmlBody, updateMutation, toast]);

  const handlePublish = React.useCallback(async () => {
    if (!selectedId) return;
    try {
      await publishMutation.mutateAsync(selectedId);
      toast({ title: 'Pubblicato', description: 'Il template è ora attivo.' });
    } catch {
      toast({
        title: 'Errore',
        description: 'Impossibile pubblicare il template.',
        variant: 'destructive',
      });
    }
  }, [selectedId, publishMutation, toast]);

  const handlePreview = React.useCallback(async () => {
    if (!selectedId) return;
    try {
      const html = await previewMutation.mutateAsync({ id: selectedId, testData });
      setPreviewHtml(html);
      setPreviewOpen(true);
    } catch {
      toast({
        title: 'Errore',
        description: "Impossibile generare l'anteprima.",
        variant: 'destructive',
      });
    }
  }, [selectedId, testData, previewMutation, toast]);

  const handleCreate = React.useCallback(async () => {
    try {
      const result = await createMutation.mutateAsync({
        name: newName,
        locale: newLocale,
        subject: newSubject,
        htmlBody: newHtmlBody,
      });
      setCreateOpen(false);
      setNewName('');
      setNewSubject('');
      setNewHtmlBody('');
      setSelectedId(result.id);
      toast({ title: 'Template creato', description: `"${newName}" creato con successo.` });
    } catch {
      toast({
        title: 'Errore',
        description: 'Impossibile creare il template.',
        variant: 'destructive',
      });
    }
  }, [newName, newLocale, newSubject, newHtmlBody, createMutation, toast]);

  const handleInsertPlaceholder = React.useCallback(
    (placeholder: string) => {
      const tag = `{{${placeholder}}}`;
      const textarea = textareaRef.current;
      if (textarea) {
        const start = textarea.selectionStart;
        const end = textarea.selectionEnd;
        const before = editHtmlBody.slice(0, start);
        const after = editHtmlBody.slice(end);
        const newValue = before + tag + after;
        setEditHtmlBody(newValue);
        // Restore cursor position after React re-render
        requestAnimationFrame(() => {
          textarea.focus();
          const pos = start + tag.length;
          textarea.setSelectionRange(pos, pos);
        });
      } else {
        setEditHtmlBody(prev => prev + tag);
      }
    },
    [editHtmlBody]
  );

  const handleLoadVersion = React.useCallback(
    (version: EmailTemplateDto) => {
      setEditSubject(version.subject);
      setEditHtmlBody(version.htmlBody);
      setVersionsOpen(false);
      toast({
        title: 'Versione caricata',
        description: `Versione ${version.version} caricata nell'editor.`,
      });
    },
    [toast]
  );

  // ── Placeholders for selected template ───────────────────────────────────

  const placeholders = React.useMemo(
    () => (selected ? getPlaceholdersForType(selected.name) : []),
    [selected]
  );

  // ── Render ───────────────────────────────────────────────────────────────

  return (
    <div className="flex h-[calc(100vh-12rem)] gap-4 p-6">
      {/* ─── Left Panel: Template List ─── */}
      <div className="flex w-80 shrink-0 flex-col gap-3">
        <div className="flex items-center justify-between">
          <h1 className="font-quicksand text-lg font-bold tracking-tight text-foreground">
            Template Email
          </h1>
          <Button size="sm" variant="outline" onClick={() => setCreateOpen(true)}>
            <Plus className="mr-1 h-4 w-4" />
            Nuovo
          </Button>
        </div>

        {/* Search */}
        <div className="relative">
          <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            placeholder="Cerca template..."
            value={search}
            onChange={(e: React.ChangeEvent<HTMLInputElement>) => setSearch(e.target.value)}
            className="pl-9"
          />
        </div>

        {/* Locale filter */}
        <Select value={localeFilter} onValueChange={setLocaleFilter}>
          <SelectTrigger className="w-full">
            <Globe className="mr-2 h-4 w-4 text-muted-foreground" />
            <SelectValue placeholder="Lingua" />
          </SelectTrigger>
          <SelectContent>
            {LOCALE_OPTIONS.map(opt => (
              <SelectItem key={opt.value} value={opt.value}>
                {opt.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>

        {/* Template list */}
        <ScrollArea className="flex-1">
          {isLoading ? (
            <div className="space-y-2">
              {Array.from({ length: 5 }).map((_, i) => (
                <Skeleton key={i} className="h-16 w-full rounded-lg" />
              ))}
            </div>
          ) : filtered.length === 0 ? (
            <p className="py-8 text-center text-sm text-muted-foreground">
              Nessun template trovato.
            </p>
          ) : (
            <div className="space-y-1.5 pr-2">
              {filtered.map(t => (
                <button
                  key={t.id}
                  type="button"
                  onClick={() => setSelectedId(t.id)}
                  className={`w-full rounded-lg border p-3 text-left transition-colors hover:bg-accent/50 ${
                    selectedId === t.id ? 'border-primary bg-primary/5' : 'border-border bg-card'
                  }`}
                >
                  <div className="flex items-center justify-between">
                    <span className="text-sm font-medium truncate">{t.name}</span>
                    <Badge
                      variant={t.isActive ? 'default' : 'secondary'}
                      className="ml-2 shrink-0 text-xs"
                    >
                      {t.isActive ? 'Attivo' : 'Bozza'}
                    </Badge>
                  </div>
                  <div className="mt-1 flex items-center gap-2 text-xs text-muted-foreground">
                    <span>
                      {localeFlag(t.locale)} {t.locale.toUpperCase()}
                    </span>
                    <span>v{t.version}</span>
                  </div>
                </button>
              ))}
            </div>
          )}
        </ScrollArea>
      </div>

      {/* ─── Right Panel: Editor ─── */}
      <div className="flex flex-1 flex-col gap-4 overflow-hidden">
        {!selected ? (
          <div className="flex flex-1 items-center justify-center">
            <p className="text-sm text-muted-foreground">
              Seleziona un template dalla lista per iniziare a modificarlo.
            </p>
          </div>
        ) : (
          <>
            {/* Header */}
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-3">
                <h2 className="font-quicksand text-xl font-bold">{selected.name}</h2>
                <Badge variant="outline">
                  {localeFlag(selected.locale)} {selected.locale.toUpperCase()}
                </Badge>
                <Badge variant="secondary">v{selected.version}</Badge>
                <Badge variant={selected.isActive ? 'default' : 'secondary'}>
                  {selected.isActive ? 'Attivo' : 'Bozza'}
                </Badge>
              </div>
              <Button size="sm" variant="ghost" onClick={() => setVersionsOpen(true)}>
                <History className="mr-1 h-4 w-4" />
                Cronologia
              </Button>
            </div>

            {/* Subject */}
            <div className="space-y-1.5">
              <Label htmlFor="template-subject">Oggetto</Label>
              <Input
                id="template-subject"
                value={editSubject}
                onChange={(e: React.ChangeEvent<HTMLInputElement>) =>
                  setEditSubject(e.target.value)
                }
                placeholder="Oggetto dell'email..."
              />
            </div>

            {/* HTML Body */}
            <div className="flex flex-1 flex-col gap-1.5 overflow-hidden">
              <Label htmlFor="template-body">Corpo HTML</Label>
              <Textarea
                ref={textareaRef}
                id="template-body"
                value={editHtmlBody}
                onChange={(e: React.ChangeEvent<HTMLTextAreaElement>) =>
                  setEditHtmlBody(e.target.value)
                }
                className="flex-1 resize-none font-mono text-sm"
                style={{ minHeight: '300px' }}
                placeholder="<html>...</html>"
              />
            </div>

            {/* Placeholders */}
            {placeholders.length > 0 && (
              <Card className="shrink-0">
                <CardHeader className="py-2 px-3">
                  <CardTitle className="text-xs font-medium text-muted-foreground">
                    Placeholder disponibili
                  </CardTitle>
                </CardHeader>
                <CardContent className="flex flex-wrap gap-1.5 px-3 pb-3 pt-0">
                  {placeholders.map(p => (
                    <button
                      key={p}
                      type="button"
                      onClick={() => handleInsertPlaceholder(p)}
                      className="inline-flex items-center gap-1 rounded-md border border-dashed border-border bg-muted/50 px-2 py-1 text-xs font-mono text-muted-foreground transition-colors hover:bg-accent hover:text-accent-foreground"
                      title={`Inserisci {{${p}}}`}
                    >
                      <Copy className="h-3 w-3" />
                      {`{{${p}}}`}
                    </button>
                  ))}
                </CardContent>
              </Card>
            )}

            {/* Test Data for Preview */}
            <Card className="shrink-0">
              <CardHeader className="py-2 px-3">
                <CardTitle className="text-xs font-medium text-muted-foreground">
                  Dati di test (per anteprima)
                </CardTitle>
              </CardHeader>
              <CardContent className="px-3 pb-3 pt-0">
                <div className="grid grid-cols-2 gap-2 lg:grid-cols-3">
                  {placeholders.map(p => (
                    <div key={p} className="space-y-1">
                      <Label className="text-xs">{p}</Label>
                      <Input
                        value={testData[p] ?? ''}
                        onChange={(e: React.ChangeEvent<HTMLInputElement>) =>
                          setTestData(prev => ({ ...prev, [p]: e.target.value }))
                        }
                        className="h-8 text-xs"
                        placeholder={p}
                      />
                    </div>
                  ))}
                </div>
              </CardContent>
            </Card>

            {/* Action Buttons */}
            <div className="flex shrink-0 items-center gap-2 border-t pt-3">
              <Button
                onClick={handleSaveDraft}
                disabled={updateMutation.isPending}
                variant="outline"
              >
                <Save className="mr-1 h-4 w-4" />
                {updateMutation.isPending ? 'Salvataggio...' : 'Salva bozza'}
              </Button>
              <Button onClick={handlePublish} disabled={publishMutation.isPending}>
                <Send className="mr-1 h-4 w-4" />
                {publishMutation.isPending ? 'Pubblicazione...' : 'Pubblica'}
              </Button>
              <Button
                onClick={handlePreview}
                disabled={previewMutation.isPending}
                variant="secondary"
              >
                <Eye className="mr-1 h-4 w-4" />
                {previewMutation.isPending ? 'Generazione...' : 'Anteprima'}
              </Button>
            </div>
          </>
        )}
      </div>

      {/* ─── Preview Dialog ─── */}
      <Dialog open={previewOpen} onOpenChange={setPreviewOpen}>
        <DialogContent className="max-w-4xl max-h-[90vh] flex flex-col">
          <DialogHeader>
            <DialogTitle className="flex items-center justify-between">
              <span>Anteprima Email</span>
              <div className="flex items-center gap-1">
                <Button
                  size="sm"
                  variant={previewWidth === 'desktop' ? 'default' : 'outline'}
                  onClick={() => setPreviewWidth('desktop')}
                >
                  <Monitor className="h-4 w-4" />
                </Button>
                <Button
                  size="sm"
                  variant={previewWidth === 'mobile' ? 'default' : 'outline'}
                  onClick={() => setPreviewWidth('mobile')}
                >
                  <Smartphone className="h-4 w-4" />
                </Button>
              </div>
            </DialogTitle>
          </DialogHeader>
          <div className="flex-1 overflow-auto flex justify-center bg-muted/30 rounded-lg p-4">
            <iframe
              title="Anteprima email"
              srcDoc={previewHtml}
              sandbox="allow-same-origin"
              className="border rounded-lg bg-white"
              style={{
                width: previewWidth === 'desktop' ? '100%' : '375px',
                maxWidth: '100%',
                height: '600px',
              }}
            />
          </div>
        </DialogContent>
      </Dialog>

      {/* ─── Create Dialog ─── */}
      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>Nuovo Template Email</DialogTitle>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-1.5">
              <Label htmlFor="new-name">Nome template</Label>
              <Input
                id="new-name"
                value={newName}
                onChange={(e: React.ChangeEvent<HTMLInputElement>) => setNewName(e.target.value)}
                placeholder="es. game_night_invitation"
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="new-locale">Lingua</Label>
              <Select value={newLocale} onValueChange={setNewLocale}>
                <SelectTrigger id="new-locale" className="w-full">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="it">Italiano</SelectItem>
                  <SelectItem value="en">English</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="new-subject">Oggetto</Label>
              <Input
                id="new-subject"
                value={newSubject}
                onChange={(e: React.ChangeEvent<HTMLInputElement>) => setNewSubject(e.target.value)}
                placeholder="Oggetto dell'email..."
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="new-body">Corpo HTML</Label>
              <Textarea
                id="new-body"
                value={newHtmlBody}
                onChange={(e: React.ChangeEvent<HTMLTextAreaElement>) =>
                  setNewHtmlBody(e.target.value)
                }
                className="font-mono text-sm"
                rows={8}
                placeholder="<html>...</html>"
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCreateOpen(false)}>
              Annulla
            </Button>
            <Button
              onClick={handleCreate}
              disabled={createMutation.isPending || !newName || !newSubject || !newHtmlBody}
            >
              {createMutation.isPending ? 'Creazione...' : 'Crea template'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ─── Version History Dialog ─── */}
      <Dialog open={versionsOpen} onOpenChange={setVersionsOpen}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>Cronologia versioni &mdash; {selected?.name}</DialogTitle>
          </DialogHeader>
          <ScrollArea className="max-h-[400px]">
            {!versions || versions.length === 0 ? (
              <p className="py-8 text-center text-sm text-muted-foreground">
                Nessuna versione precedente.
              </p>
            ) : (
              <div className="space-y-2 pr-2">
                {versions.map(v => (
                  <Card key={v.id} className="cursor-pointer hover:bg-accent/50 transition-colors">
                    <CardContent className="flex items-center justify-between p-3">
                      <div>
                        <div className="flex items-center gap-2">
                          <span className="font-medium text-sm">v{v.version}</span>
                          <Badge variant={v.isActive ? 'default' : 'secondary'} className="text-xs">
                            {v.isActive ? 'Attivo' : 'Bozza'}
                          </Badge>
                        </div>
                        <p className="text-xs text-muted-foreground mt-0.5">
                          {new Date(v.createdAt).toLocaleString('it-IT')}
                        </p>
                        <p className="text-xs text-muted-foreground truncate max-w-[280px]">
                          {v.subject}
                        </p>
                      </div>
                      <Button size="sm" variant="ghost" onClick={() => handleLoadVersion(v)}>
                        Carica
                      </Button>
                    </CardContent>
                  </Card>
                ))}
              </div>
            )}
          </ScrollArea>
        </DialogContent>
      </Dialog>
    </div>
  );
}
