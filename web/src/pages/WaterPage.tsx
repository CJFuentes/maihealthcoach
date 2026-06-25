import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  addWaterEntry,
  deleteWaterEntry,
  getWaterDay,
  updateWaterEntry,
  type WaterDayResponse,
  type WaterEntry,
} from '../api/water';
import { ApiError } from '../api/client';
import DateNav from '../components/diary/DateNav';
import WaterProgressBar from '../components/water/WaterProgressBar';
import WaterEntryRow from '../components/water/WaterEntryRow';

/** The fixed quick-add amounts (ml). */
const QUICK_ADD_AMOUNTS = [250, 500] as const;

/** Returns today's date as a local `YYYY-MM-DD` string. */
function todayLocalDate(): string {
  return formatLocalDate(new Date());
}

/** Formats a Date as a local `YYYY-MM-DD` string (zero-padded). */
function formatLocalDate(d: Date): string {
  const year = d.getFullYear();
  const month = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

/**
 * Shifts an ISO date by `delta` days using local-time arithmetic so the result
 * is DST-safe (no UTC midnight rollover surprises).
 */
function shiftDate(date: string, delta: number): string {
  const [y, m, d] = date.split('-').map(Number);
  const nd = new Date(y, m - 1, d);
  nd.setDate(nd.getDate() + delta);
  return formatLocalDate(nd);
}

/** Sums entry amounts into a day total (used when the server omits aggregates). */
function computeTotal(entries: WaterEntry[]): number {
  return entries.reduce((sum, e) => sum + e.amountMl, 0);
}

/** Recomputes aggregate fields after the entry list changes (optimistic edits). */
function withAggregates(data: WaterDayResponse, entries: WaterEntry[]): WaterDayResponse {
  const totalMl = computeTotal(entries);
  const remainingMl =
    data.goalMl !== undefined ? Math.max(0, data.goalMl - totalMl) : undefined;
  return { ...data, entries, totalMl, remainingMl };
}

type WaterStatus =
  | { state: 'loading' }
  | { state: 'ready'; data: WaterDayResponse }
  | { state: 'error'; message: string };

/**
 * Protected water-tracking page: a day view with a progress bar toward the
 * daily goal, one-tap quick-add buttons, a custom-amount entry, date
 * navigation, and per-entry edit/delete.
 */
export default function WaterPage() {
  const { t } = useTranslation('water');

  const [date, setDate] = useState<string>(todayLocalDate);
  const [status, setStatus] = useState<WaterStatus>({ state: 'loading' });

  const [addingAmount, setAddingAmount] = useState<number | null>(null);
  const [customAmount, setCustomAmount] = useState<string>('');
  const [addError, setAddError] = useState<string | null>(null);

  const [editingId, setEditingId] = useState<string | null>(null);
  const [editAmount, setEditAmount] = useState<string>('');
  const [editError, setEditError] = useState<string | null>(null);

  const [deletingId, setDeletingId] = useState<string | null>(null);

  // Monotonic request id: only the latest load may apply its result, so rapid
  // date navigation (or a post-mutation reload) can't be clobbered by a stale
  // in-flight response.
  const requestRef = useRef(0);

  function loadWater(targetDate: string) {
    const id = ++requestRef.current;
    setStatus({ state: 'loading' });

    getWaterDay(targetDate)
      .then((data) => {
        if (id === requestRef.current) {
          setStatus({ state: 'ready', data });
        }
      })
      .catch((err: unknown) => {
        if (id === requestRef.current) {
          setStatus({
            state: 'error',
            message: err instanceof Error ? err.message : t('unknownError'),
          });
        }
      });
  }

  useEffect(() => {
    loadWater(date);
    // Reset transient per-day UI state when the day changes.
    setEditingId(null);
    setEditError(null);
    setAddError(null);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [date]);

  function handlePrev() {
    setDate((d) => shiftDate(d, -1));
  }

  function handleNext() {
    setDate((d) => shiftDate(d, 1));
  }

  function handleToday() {
    setDate(todayLocalDate());
  }

  /** Extracts a user-facing message from an API error, preferring field errors. */
  function messageFromError(err: unknown): string {
    if (err instanceof ApiError) {
      const fieldError = err.problem?.errors?.amountMl?.[0];
      if (fieldError) {
        return fieldError;
      }
      if (err.problem?.detail) {
        return err.problem.detail;
      }
      if (err.problem?.title) {
        return err.problem.title;
      }
    }
    return err instanceof Error ? err.message : t('unknownError');
  }

  function addAmount(amountMl: number) {
    setAddError(null);
    setAddingAmount(amountMl);
    const requestId = requestRef.current;

    addWaterEntry({ amountMl })
      .then((entry) => {
        if (requestId !== requestRef.current) {
          return;
        }
        // Optimistically append the new entry and recompute aggregates.
        setStatus((prev) => {
          if (prev.state !== 'ready') {
            return prev;
          }
          return {
            state: 'ready',
            data: withAggregates(prev.data, [...prev.data.entries, entry]),
          };
        });
      })
      .catch((err: unknown) => {
        if (requestId === requestRef.current) {
          setAddError(messageFromError(err));
        }
      })
      .finally(() => {
        setAddingAmount(null);
      });
  }

  function handleCustomAdd() {
    const parsed = Number(customAmount);
    if (!Number.isFinite(parsed) || parsed <= 0) {
      setAddError(t('errorAmountPositive'));
      return;
    }
    addAmount(parsed);
    setCustomAmount('');
  }

  function handleEditStart(entry: WaterEntry) {
    setEditingId(entry.id);
    setEditAmount(String(entry.amountMl));
    setEditError(null);
  }

  function handleEditCancel() {
    setEditingId(null);
    setEditError(null);
  }

  function handleEditCommit() {
    const id = editingId;
    if (id === null) {
      return;
    }
    const parsed = Number(editAmount);
    if (!Number.isFinite(parsed) || parsed <= 0) {
      setEditError(t('errorAmountPositive'));
      return;
    }
    const requestId = requestRef.current;
    updateWaterEntry(id, { amountMl: parsed })
      .then((updated) => {
        if (requestId !== requestRef.current) {
          return;
        }
        setStatus((prev) => {
          if (prev.state !== 'ready') {
            return prev;
          }
          const entries = prev.data.entries.map((e) => (e.id === id ? updated : e));
          return { state: 'ready', data: withAggregates(prev.data, entries) };
        });
        setEditingId(null);
        setEditError(null);
      })
      .catch((err: unknown) => {
        setEditError(messageFromError(err));
      });
  }

  function handleDelete(id: string) {
    const deletedDate = date;
    const requestId = requestRef.current;
    setDeletingId(id);

    deleteWaterEntry(id)
      .then(() => {
        if (requestId !== requestRef.current) {
          return;
        }
        // Optimistically drop the entry and recompute aggregates.
        setStatus((prev) => {
          if (prev.state !== 'ready') {
            return prev;
          }
          const entries = prev.data.entries.filter((e) => e.id !== id);
          return { state: 'ready', data: withAggregates(prev.data, entries) };
        });
      })
      .catch(() => {
        // Roll back to authoritative server state on failure (only if still on
        // the same day — otherwise the active load owns the state).
        if (requestId === requestRef.current) {
          loadWater(deletedDate);
        }
      })
      .finally(() => {
        setDeletingId(null);
      });
  }

  const data = status.state === 'ready' ? status.data : null;
  const totalMl = data ? (data.totalMl ?? computeTotal(data.entries)) : 0;
  const goalMl = data ? data.goalMl : undefined;
  const remainingMl = data
    ? (data.remainingMl ?? (goalMl !== undefined ? Math.max(0, goalMl - totalMl) : undefined))
    : undefined;

  return (
    <section>
      <h1>{t('title')}</h1>

      <DateNav date={date} onPrev={handlePrev} onNext={handleNext} onToday={handleToday} />

      {status.state === 'loading' && <p>{t('loading')}</p>}

      {status.state === 'error' && (
        <p role="alert" className="message message-error">
          {t('loadError', { message: status.message })}
        </p>
      )}

      {status.state === 'ready' && (
        <>
          <WaterProgressBar totalMl={totalMl} goalMl={goalMl} remainingMl={remainingMl} />

          <div className="card water-quick-add">
            <h2>{t('quickAddHeading')}</h2>
            <div className="water-quick-add-buttons">
              {QUICK_ADD_AMOUNTS.map((amount) => (
                <button
                  key={amount}
                  type="button"
                  className="button-primary"
                  aria-label={t(amount === 250 ? 'quickAdd250Label' : 'quickAdd500Label')}
                  disabled={addingAmount !== null}
                  onClick={() => addAmount(amount)}
                >
                  {addingAmount === amount
                    ? t('adding')
                    : t(amount === 250 ? 'quickAdd250' : 'quickAdd500')}
                </button>
              ))}
            </div>

            <div className="water-custom-add">
              <label htmlFor="water-custom-amount">{t('customAmountLabel')}</label>
              <input
                id="water-custom-amount"
                type="number"
                inputMode="numeric"
                min="1"
                className="water-custom-input"
                value={customAmount}
                aria-invalid={addError ? true : undefined}
                aria-describedby={addError ? 'water-add-error' : undefined}
                onChange={(e) => setCustomAmount(e.target.value)}
              />
              <span className="water-entry-unit">{t('unitMl')}</span>
              <button
                type="button"
                className="button-secondary"
                disabled={addingAmount !== null}
                onClick={handleCustomAdd}
              >
                {t('addButton')}
              </button>
            </div>

            {addError && (
              <p id="water-add-error" role="alert" className="field-error">
                {addError}
              </p>
            )}
          </div>

          <h2>{t('entriesHeading')}</h2>
          {data && data.entries.length === 0 ? (
            <p className="hint">{t('emptyHint')}</p>
          ) : (
            <ul className="water-entry-list">
              {data?.entries.map((entry) => (
                <WaterEntryRow
                  key={entry.id}
                  entry={entry}
                  isDeleting={deletingId === entry.id}
                  isEditing={editingId === entry.id}
                  editAmount={editingId === entry.id ? editAmount : ''}
                  editError={editingId === entry.id ? editError : null}
                  onEditStart={handleEditStart}
                  onEditChange={setEditAmount}
                  onEditCommit={handleEditCommit}
                  onEditCancel={handleEditCancel}
                  onDelete={handleDelete}
                />
              ))}
            </ul>
          )}
        </>
      )}
    </section>
  );
}
