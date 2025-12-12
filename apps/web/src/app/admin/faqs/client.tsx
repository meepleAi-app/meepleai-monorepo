/**
 * FAQ Management Client - Issue #2028
 *
 * Admin interface for managing game-specific FAQs:
 * - List all FAQs with game filter
 * - Create new FAQ (modal)
 * - Edit existing FAQ (inline or modal)
 * - Delete FAQ (confirmation)
 * - View upvote counts
 *
 * Backend Integration:
 * - GetGameFAQsQuery (via api.games.getFAQs)
 * - CreateGameFAQCommand (via api endpoint)
 * - UpdateGameFAQCommand (via api endpoint)
 * - DeleteGameFAQCommand (via api endpoint)
 */

'use client';

import { useState, useCallback, useEffect } from 'react';
import { api, type GameFAQ, type Game } from '@/lib/api';
import { AdminAuthGuard } from '@/components/admin';
import { useAuthUser } from '@/components/auth/AuthProvider';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Plus, Trash2, Edit, ThumbsUp, AlertCircle } from 'lucide-react';
import { Spinner } from '@/components/loading';

type ToastMessage = {
  id: string;
  type: 'success' | 'error' | 'info';
  message: string;
};

type FAQFormData = {
  question: string;
  answer: string;
};

type EditingFAQ = GameFAQ | null;

export function FAQManagementClient() {
  const { user, loading: authLoading } = useAuthUser();

  // Games list for filter
  const [games, setGames] = useState<Game[]>([]);
  const [selectedGameId, setSelectedGameId] = useState<string>('');

  // FAQs data
  const [faqs, setFaqs] = useState<GameFAQ[]>([]);
  const [loading, setLoading] = useState(false);

  // Modal states
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [isEditModalOpen, setIsEditModalOpen] = useState(false);
  const [editingFAQ, setEditingFAQ] = useState<EditingFAQ>(null);

  // Form data
  const [formData, setFormData] = useState<FAQFormData>({
    question: '',
    answer: '',
  });

  // Toast management
  const [toasts, setToasts] = useState<ToastMessage[]>([]);

  const addToast = useCallback((type: 'success' | 'error' | 'info', message: string) => {
    const id = `toast-${Date.now()}-${Math.random()}`;
    setToasts(prev => [...prev, { id, type, message }]);
    setTimeout(() => {
      setToasts(prev => prev.filter(t => t.id !== id));
    }, 5000);
  }, []);

  // Fetch games for filter dropdown
  useEffect(() => {
    const fetchGames = async () => {
      try {
        const result = await api.games.getAll();
        setGames(result.games);
        if (result.games.length > 0) {
          setSelectedGameId(result.games[0].id);
        }
      } catch (err) {
        console.error('Failed to fetch games:', err);
        addToast('error', 'Errore nel caricamento dei giochi');
      }
    };

    fetchGames();
  }, [addToast]);

  // Fetch FAQs when game selection changes
  useEffect(() => {
    if (!selectedGameId) return;

    const fetchFAQs = async () => {
      try {
        setLoading(true);
        const result = await api.games.getFAQs(selectedGameId, 100, 0);
        setFaqs(result.faqs);
      } catch (err) {
        console.error('Failed to fetch FAQs:', err);
        addToast('error', 'Errore nel caricamento delle FAQ');
      } finally {
        setLoading(false);
      }
    };

    fetchFAQs();
  }, [selectedGameId, addToast]);

  // Create FAQ
  const handleCreate = async () => {
    // Validation (Issue #2028: Code review fix - match backend constraints)
    if (!selectedGameId || !formData.question.trim() || !formData.answer.trim()) {
      addToast('error', 'Domanda e risposta sono obbligatorie');
      return;
    }

    if (formData.question.length > 500) {
      addToast('error', 'Domanda troppo lunga (max 500 caratteri)');
      return;
    }

    if (formData.answer.length > 5000) {
      addToast('error', 'Risposta troppo lunga (max 5000 caratteri)');
      return;
    }

    try {
      const response = await fetch(
        `${process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080'}/api/v1/games/${selectedGameId}/faqs`,
        {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          credentials: 'include',
          body: JSON.stringify({
            question: formData.question,
            answer: formData.answer,
          }),
        }
      );

      if (!response.ok) throw new Error('Failed to create FAQ');

      addToast('success', 'FAQ creata con successo');
      setIsCreateModalOpen(false);
      setFormData({ question: '', answer: '' });

      // Refresh FAQs
      const result = await api.games.getFAQs(selectedGameId, 100, 0);
      setFaqs(result.faqs);
    } catch (err) {
      console.error('Failed to create FAQ:', err);
      addToast('error', 'Errore nella creazione della FAQ');
    }
  };

  // Update FAQ
  const handleUpdate = async () => {
    // Validation (Issue #2028: Code review fix - match backend constraints)
    if (!editingFAQ || !formData.question.trim() || !formData.answer.trim()) {
      addToast('error', 'Domanda e risposta sono obbligatorie');
      return;
    }

    if (formData.question.length > 500) {
      addToast('error', 'Domanda troppo lunga (max 500 caratteri)');
      return;
    }

    if (formData.answer.length > 5000) {
      addToast('error', 'Risposta troppo lunga (max 5000 caratteri)');
      return;
    }

    try {
      const response = await fetch(
        `${process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080'}/api/v1/faqs/${editingFAQ.id}`,
        {
          method: 'PUT',
          headers: { 'Content-Type': 'application/json' },
          credentials: 'include',
          body: JSON.stringify({
            question: formData.question,
            answer: formData.answer,
          }),
        }
      );

      if (!response.ok) throw new Error('Failed to update FAQ');

      addToast('success', 'FAQ aggiornata con successo');
      setIsEditModalOpen(false);
      setEditingFAQ(null);
      setFormData({ question: '', answer: '' });

      // Refresh FAQs
      if (selectedGameId) {
        const result = await api.games.getFAQs(selectedGameId, 100, 0);
        setFaqs(result.faqs);
      }
    } catch (err) {
      console.error('Failed to update FAQ:', err);
      addToast('error', "Errore nell'aggiornamento della FAQ");
    }
  };

  // Delete FAQ
  const handleDelete = async (faqId: string) => {
    if (!confirm('Sei sicuro di voler eliminare questa FAQ?')) return;

    try {
      const response = await fetch(
        `${process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080'}/api/v1/faqs/${faqId}`,
        {
          method: 'DELETE',
          credentials: 'include',
        }
      );

      if (!response.ok) throw new Error('Failed to delete FAQ');

      addToast('success', 'FAQ eliminata con successo');

      // Refresh FAQs
      if (selectedGameId) {
        const result = await api.games.getFAQs(selectedGameId, 100, 0);
        setFaqs(result.faqs);
      }
    } catch (err) {
      console.error('Failed to delete FAQ:', err);
      addToast('error', "Errore nell'eliminazione della FAQ");
    }
  };

  // Open edit modal
  const handleEditClick = (faq: GameFAQ) => {
    setEditingFAQ(faq);
    setFormData({
      question: faq.question,
      answer: faq.answer,
    });
    setIsEditModalOpen(true);
  };

  return (
    <AdminAuthGuard loading={authLoading} user={user}>
      <div className="container mx-auto p-6 max-w-7xl">
        <Card>
          <CardHeader>
            <CardTitle>FAQ Management</CardTitle>
            <CardDescription>
              Gestisci le FAQ per ogni gioco. Le FAQ vengono visualizzate nella pagina dettaglio
              gioco ordinate per upvotes.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-6">
            {/* Filters & Actions */}
            <div className="flex items-center justify-between gap-4">
              <div className="flex items-center gap-4 flex-1">
                <Label htmlFor="game-filter" className="shrink-0">
                  Gioco:
                </Label>
                <Select value={selectedGameId} onValueChange={setSelectedGameId}>
                  <SelectTrigger id="game-filter" className="w-[300px]">
                    <SelectValue placeholder="Seleziona un gioco" />
                  </SelectTrigger>
                  <SelectContent>
                    {games.map(game => (
                      <SelectItem key={game.id} value={game.id}>
                        {game.title}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              <Button onClick={() => setIsCreateModalOpen(true)} disabled={!selectedGameId}>
                <Plus className="h-4 w-4 mr-2" />
                Nuova FAQ
              </Button>
            </div>

            {/* Loading State */}
            {loading && (
              <div className="flex justify-center py-12">
                <Spinner size="lg" />
              </div>
            )}

            {/* Empty State */}
            {!loading && faqs.length === 0 && selectedGameId && (
              <Alert>
                <AlertCircle className="h-4 w-4" />
                <AlertDescription>
                  Nessuna FAQ per questo gioco. Clicca "Nuova FAQ" per aggiungerne una.
                </AlertDescription>
              </Alert>
            )}

            {/* FAQ Table */}
            {!loading && faqs.length > 0 && (
              <div className="border rounded-lg">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead className="w-[30%]">Domanda</TableHead>
                      <TableHead className="w-[45%]">Risposta</TableHead>
                      <TableHead className="w-[10%] text-center">Upvotes</TableHead>
                      <TableHead className="w-[15%] text-right">Azioni</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {faqs.map(faq => (
                      <TableRow key={faq.id}>
                        <TableCell className="font-medium">{faq.question}</TableCell>
                        <TableCell className="text-sm text-muted-foreground">
                          {faq.answer.substring(0, 100)}
                          {faq.answer.length > 100 ? '...' : ''}
                        </TableCell>
                        <TableCell className="text-center">
                          <div className="flex items-center justify-center gap-1">
                            <ThumbsUp className="h-4 w-4 text-muted-foreground" />
                            <span>{faq.upvotes}</span>
                          </div>
                        </TableCell>
                        <TableCell className="text-right">
                          <div className="flex items-center justify-end gap-2">
                            <Button variant="ghost" size="sm" onClick={() => handleEditClick(faq)}>
                              <Edit className="h-4 w-4" />
                            </Button>
                            <Button variant="ghost" size="sm" onClick={() => handleDelete(faq.id)}>
                              <Trash2 className="h-4 w-4 text-destructive" />
                            </Button>
                          </div>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>
            )}
          </CardContent>
        </Card>

        {/* Create FAQ Modal */}
        <Dialog open={isCreateModalOpen} onOpenChange={setIsCreateModalOpen}>
          <DialogContent className="max-w-2xl">
            <DialogHeader>
              <DialogTitle>Crea Nuova FAQ</DialogTitle>
              <DialogDescription>
                Aggiungi una domanda frequente per{' '}
                {games.find(g => g.id === selectedGameId)?.title || 'il gioco selezionato'}
              </DialogDescription>
            </DialogHeader>
            <div className="space-y-4 mt-4">
              <div>
                <Label htmlFor="create-question">Domanda</Label>
                <Input
                  id="create-question"
                  placeholder="Es: Come si vince al gioco?"
                  maxLength={500}
                  value={formData.question}
                  onChange={e => setFormData(prev => ({ ...prev, question: e.target.value }))}
                />
                <p className="text-xs text-muted-foreground mt-1">
                  {formData.question.length}/500 caratteri
                </p>
              </div>
              <div>
                <Label htmlFor="create-answer">Risposta</Label>
                <Textarea
                  id="create-answer"
                  placeholder="Inserisci la risposta dettagliata..."
                  rows={6}
                  maxLength={5000}
                  value={formData.answer}
                  onChange={e => setFormData(prev => ({ ...prev, answer: e.target.value }))}
                />
                <p className="text-xs text-muted-foreground mt-1">
                  {formData.answer.length}/5000 caratteri
                </p>
              </div>
            </div>
            <DialogFooter className="mt-6">
              <Button
                variant="outline"
                onClick={() => {
                  setIsCreateModalOpen(false);
                  setFormData({ question: '', answer: '' });
                }}
              >
                Annulla
              </Button>
              <Button onClick={handleCreate}>Crea FAQ</Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>

        {/* Edit FAQ Modal */}
        <Dialog open={isEditModalOpen} onOpenChange={setIsEditModalOpen}>
          <DialogContent className="max-w-2xl">
            <DialogHeader>
              <DialogTitle>Modifica FAQ</DialogTitle>
              <DialogDescription>Aggiorna domanda e risposta</DialogDescription>
            </DialogHeader>
            <div className="space-y-4 mt-4">
              <div>
                <Label htmlFor="edit-question">Domanda</Label>
                <Input
                  id="edit-question"
                  placeholder="Es: Come si vince al gioco?"
                  maxLength={500}
                  value={formData.question}
                  onChange={e => setFormData(prev => ({ ...prev, question: e.target.value }))}
                />
                <p className="text-xs text-muted-foreground mt-1">
                  {formData.question.length}/500 caratteri
                </p>
              </div>
              <div>
                <Label htmlFor="edit-answer">Risposta</Label>
                <Textarea
                  id="edit-answer"
                  placeholder="Inserisci la risposta dettagliata..."
                  rows={6}
                  maxLength={5000}
                  value={formData.answer}
                  onChange={e => setFormData(prev => ({ ...prev, answer: e.target.value }))}
                />
                <p className="text-xs text-muted-foreground mt-1">
                  {formData.answer.length}/5000 caratteri
                </p>
              </div>
            </div>
            <DialogFooter className="mt-6">
              <Button
                variant="outline"
                onClick={() => {
                  setIsEditModalOpen(false);
                  setEditingFAQ(null);
                  setFormData({ question: '', answer: '' });
                }}
              >
                Annulla
              </Button>
              <Button onClick={handleUpdate}>Salva Modifiche</Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>

        {/* Toast Notifications */}
        <div className="fixed bottom-4 right-4 z-50 space-y-2">
          {toasts.map(toast => (
            <Alert
              key={toast.id}
              variant={toast.type === 'error' ? 'destructive' : 'default'}
              className="w-96"
            >
              <AlertDescription>{toast.message}</AlertDescription>
            </Alert>
          ))}
        </div>
      </div>
    </AdminAuthGuard>
  );
}
