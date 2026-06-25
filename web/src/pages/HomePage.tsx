import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ping, type PingResponse } from '../api/health';

type Status =
  | { state: 'idle' }
  | { state: 'loading' }
  | { state: 'success'; data: PingResponse }
  | { state: 'error'; message: string };

export default function HomePage() {
  const { t } = useTranslation('home');
  const [status, setStatus] = useState<Status>({ state: 'idle' });

  useEffect(() => {
    let cancelled = false;
    setStatus({ state: 'loading' });

    ping()
      .then((data) => {
        if (!cancelled) {
          setStatus({ state: 'success', data });
        }
      })
      .catch((error: unknown) => {
        if (!cancelled) {
          const message = error instanceof Error ? error.message : 'Unknown error';
          setStatus({ state: 'error', message });
        }
      });

    return () => {
      cancelled = true;
    };
  }, []);

  return (
    <section>
      <h1>{t('title')}</h1>
      {status.state === 'loading' && <p>{t('checking')}</p>}
      {status.state === 'success' && (
        <p>{t('online', { service: status.data.service, version: status.data.version })}</p>
      )}
      {status.state === 'error' && (
        <p role="alert">{t('unavailable', { message: status.message })}</p>
      )}
    </section>
  );
}
