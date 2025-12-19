import { redirect } from 'next/navigation';

type SearchParamValue = string | string[];

export default function OAuthCallbackPage({
  searchParams,
}: {
  searchParams?: Record<string, SearchParamValue>;
}) {
  const qs = new URLSearchParams();

  if (searchParams) {
    Object.entries(searchParams).forEach(([key, value]) => {
      if (Array.isArray(value)) {
        value.forEach(v => qs.append(key, v));
      } else if (value !== undefined) {
        qs.append(key, value);
      }
    });
  }

  const suffix = qs.toString();
  redirect(suffix ? `/login?${suffix}` : '/login');
}
