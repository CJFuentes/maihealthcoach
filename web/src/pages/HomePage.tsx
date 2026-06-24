import { useEffect, useState } from 'react';
import { ping, type PingResponse } from '../api/health';

type Status =
  | { state: 'idle' }
  | { state: 'loading' }
  | { state: 'success'; data: PingResponse }
  | { state: 'error'; message: string };

export default function HomePage() {
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
      <h1>MAI Health Coach</h1>
      {status.state === 'loading' && <p>Checking backend…</p>}
      {status.state === 'success' && (
        <p>
          Backend online — {status.data.service} v{status.data.version}
        </p>
      )}
      {status.state === 'error' && <p role="alert">Backend unavailable — {status.message}</p>}
    </section>
  );
}
