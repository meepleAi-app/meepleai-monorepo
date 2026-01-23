/**
 * Contact Page
 *
 * Contact form page with:
 * - Functional form with validation
 * - Subject dropdown
 * - Contact information
 * - Bilingual support (IT/EN) via i18n
 *
 * @see Issue for legal pages implementation
 */

'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Separator } from '@/components/ui/navigation/separator';
import { Button } from '@/components/ui/primitives/button';
import { useTranslation } from '@/hooks/useTranslation';

// Subject keys for the dropdown
const SUBJECT_KEYS = ['general', 'support', 'feedback', 'partnership', 'press', 'other'] as const;

type FormState = {
  name: string;
  email: string;
  subject: string;
  message: string;
};

type FormStatus = 'idle' | 'sending' | 'success' | 'error';

export default function ContactPage() {
  const router = useRouter();
  const { t, locale } = useTranslation();

  const [formData, setFormData] = useState<FormState>({
    name: '',
    email: '',
    subject: '',
    message: '',
  });
  const [status, setStatus] = useState<FormStatus>('idle');

  const handleChange = (
    e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement>
  ) => {
    const { name, value } = e.target;
    setFormData((prev) => ({ ...prev, [name]: value }));
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setStatus('sending');

    // Simulate API call
    try {
      // TODO: Replace with actual API call
      await new Promise((resolve) => setTimeout(resolve, 1500));
      setStatus('success');
      setFormData({ name: '', email: '', subject: '', message: '' });
    } catch {
      setStatus('error');
    }
  };

  return (
    <div className="min-h-dvh bg-background py-8 px-4">
      <div className="max-w-4xl mx-auto">
        {/* Header */}
        <div className="mb-8 text-center">
          <h1
            className="text-3xl font-bold text-foreground"
            data-testid="contact-heading"
          >
            {t('pages.contact.title')}
          </h1>
          <p className="text-slate-600 dark:text-slate-400 mt-2 max-w-2xl mx-auto">
            {t('pages.contact.description')}
          </p>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-3 gap-8">
          {/* Contact Form */}
          <Card className="md:col-span-2">
            <CardHeader>
              <CardTitle className="text-lg">
                {locale === 'it' ? 'Invia un messaggio' : 'Send a Message'}
              </CardTitle>
            </CardHeader>
            <CardContent>
              <form onSubmit={handleSubmit} className="space-y-4">
                {/* Name Field */}
                <div>
                  <label
                    htmlFor="name"
                    className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1"
                  >
                    {t('pages.contact.form.name')}
                  </label>
                  <input
                    type="text"
                    id="name"
                    name="name"
                    value={formData.name}
                    onChange={handleChange}
                    required
                    placeholder={t('pages.contact.form.namePlaceholder')}
                    className="w-full px-3 py-2 border border-border/50 dark:border-border/30 rounded-md bg-card/90 backdrop-blur-[12px] dark:bg-card dark:backdrop-blur-none text-foreground placeholder-slate-400 focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                  />
                </div>

                {/* Email Field */}
                <div>
                  <label
                    htmlFor="email"
                    className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1"
                  >
                    {t('pages.contact.form.email')}
                  </label>
                  <input
                    type="email"
                    id="email"
                    name="email"
                    value={formData.email}
                    onChange={handleChange}
                    required
                    placeholder={t('pages.contact.form.emailPlaceholder')}
                    className="w-full px-3 py-2 border border-border/50 dark:border-border/30 rounded-md bg-card/90 backdrop-blur-[12px] dark:bg-card dark:backdrop-blur-none text-foreground placeholder-slate-400 focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                  />
                </div>

                {/* Subject Field */}
                <div>
                  <label
                    htmlFor="subject"
                    className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1"
                  >
                    {t('pages.contact.form.subject')}
                  </label>
                  <select
                    id="subject"
                    name="subject"
                    value={formData.subject}
                    onChange={handleChange}
                    required
                    className="w-full px-3 py-2 border border-border/50 dark:border-border/30 rounded-md bg-card/90 backdrop-blur-[12px] dark:bg-card dark:backdrop-blur-none text-foreground focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                  >
                    <option value="">{t('pages.contact.form.subjectPlaceholder')}</option>
                    {SUBJECT_KEYS.map((key) => (
                      <option key={key} value={key}>
                        {t(`pages.contact.subjects.${key}`)}
                      </option>
                    ))}
                  </select>
                </div>

                {/* Message Field */}
                <div>
                  <label
                    htmlFor="message"
                    className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1"
                  >
                    {t('pages.contact.form.message')}
                  </label>
                  <textarea
                    id="message"
                    name="message"
                    value={formData.message}
                    onChange={handleChange}
                    required
                    rows={5}
                    placeholder={t('pages.contact.form.messagePlaceholder')}
                    className="w-full px-3 py-2 border border-border/50 dark:border-border/30 rounded-md bg-card/90 backdrop-blur-[12px] dark:bg-card dark:backdrop-blur-none text-foreground placeholder-slate-400 focus:ring-2 focus:ring-blue-500 focus:border-transparent resize-none"
                  />
                </div>

                {/* Submit Button */}
                <Button
                  type="submit"
                  disabled={status === 'sending'}
                  className="w-full"
                >
                  {status === 'sending'
                    ? t('pages.contact.form.sending')
                    : t('pages.contact.form.submit')}
                </Button>

                {/* Status Messages */}
                {status === 'success' && (
                  <p className="text-green-600 dark:text-green-400 text-sm text-center">
                    {t('pages.contact.form.success')}
                  </p>
                )}
                {status === 'error' && (
                  <p className="text-red-600 dark:text-red-400 text-sm text-center">
                    {t('pages.contact.form.error')}
                  </p>
                )}
              </form>
            </CardContent>
          </Card>

          {/* Contact Info Sidebar */}
          <div className="space-y-4">
            <Card>
              <CardHeader>
                <CardTitle className="text-lg">{t('pages.contact.info.title')}</CardTitle>
              </CardHeader>
              <CardContent className="space-y-4">
                <div>
                  <p className="text-sm font-medium text-slate-500 dark:text-slate-400">
                    {t('pages.contact.info.email')}
                  </p>
                  <a
                    href={`mailto:${t('pages.contact.info.emailValue')}`}
                    className="text-blue-600 dark:text-blue-400 hover:underline"
                  >
                    {t('pages.contact.info.emailValue')}
                  </a>
                </div>
                <Separator />
                <div>
                  <p className="text-sm font-medium text-slate-500 dark:text-slate-400">
                    {t('pages.contact.info.response')}
                  </p>
                  <p className="text-foreground">
                    {t('pages.contact.info.responseValue')}
                  </p>
                </div>
                <Separator />
                <div>
                  <p className="text-sm font-medium text-slate-500 dark:text-slate-400">
                    {t('pages.contact.info.social')}
                  </p>
                  <div className="flex gap-2 mt-2">
                    <Button variant="ghost" size="sm">
                      Twitter
                    </Button>
                    <Button variant="ghost" size="sm">
                      Discord
                    </Button>
                  </div>
                </div>
              </CardContent>
            </Card>
          </div>
        </div>

        {/* Footer Navigation */}
        <div className="mt-12 flex flex-col sm:flex-row justify-between items-center gap-4 pt-8 border-t border-slate-200 dark:border-slate-700">
          <Button variant="ghost" onClick={() => router.push('/')}>
            ← {locale === 'it' ? 'Torna alla Home' : 'Back to Home'}
          </Button>
          <Button variant="outline" onClick={() => router.push('/faq')}>
            {locale === 'it' ? 'Domande Frequenti' : 'FAQ'} →
          </Button>
        </div>
      </div>
    </div>
  );
}
