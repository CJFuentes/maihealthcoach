import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ApiError } from '../../api/client';
import { getNudge, type NudgeResponse } from '../../api/coach';

type NudgeStatus =
  | { state: 'loading' }
  | { state: 'ready'; data: NudgeResponse }
  | { state: 'error'; message: string };

/**
 * The nudge tab: fetches a short motivational message. This endpoint never
 * returns profile errors (404/409) — only service-unavailable (502/503).
 */
export default function NudgePanel() {
  const { t } = useTranslation('coach');
  const [status, setStatus] = useState<NudgeStatus>({ state: 'loading' });

  // Resolve fallback strings up front so the load effect does not depend on `t`
  // (whose identity changes on language switch). Fetching is language-agnostic.
  const serviceUnavailableMsg = t('nudge.serviceUnavailable');
  const unknownErrorMsg = t('nudge.unknownError');

  useEffect(() => {
    let cancelled = false;
    setStatus({ state: 'loading' });

    getNudge()
      .then((data) => {
        if (!cancelled) {
          setStatus({ state: 'ready', data });
        }
      })
      .catch((error: unknown) => {
        if (cancelled) {
          return;
        }
        if (error instanceof ApiError && (error.status === 502 || error.status === 503)) {
          setStatus({ state: 'error', message: serviceUnavailableMsg });
        } else {
          const message = error instanceof Error ? error.message : unknownErrorMsg;
          setStatus({ state: 'error', message });
        }
      });

    return () => {
      cancelled = true;
    };
  }, [serviceUnavailableMsg, unknownErrorMsg]);

  return (
    <section>
      <h2>{t('tabs.nudge')}</h2>

      {status.state === 'loading' && <p role="status">{t('nudge.loading')}</p>}

      {status.state === 'error' && (
        <p role="alert" className="message message-error">
          {t('nudge.loadError', { message: status.message })}
        </p>
      )}

      {status.state === 'ready' && (
        <>
          <p className="coach-nudge-message">{status.data.message}</p>
          {status.data.disclaimer && (
            <p className="coach-disclaimer">
              {t('nudge.disclaimer', { text: status.data.disclaimer })}
            </p>
          )}
        </>
      )}
    </section>
  );
}
