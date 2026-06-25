import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import {
  deleteExerciseEntry,
  getExerciseDay,
  logExercise,
  updateExerciseEntry,
  type Exercise,
  type ExerciseDayResponse,
  type ExerciseEntry,
} from '../api/exercise';
import { ApiError } from '../api/client';
import DateNav from '../components/diary/DateNav';
import ExerciseDailySummary from '../components/exercise/ExerciseDailySummary';
import ExerciseEntryRow from '../components/exercise/ExerciseEntryRow';
import ExerciseSearchPanel from '../components/exercise/ExerciseSearchPanel';

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

/** Sums entry calories into a day total (used when the server omits aggregates). */
function computeTotal(entries: ExerciseEntry[]): number {
  return entries.reduce((sum, e) => sum + e.caloriesBurned, 0);
}

/** Recomputes aggregate fields after the entry list changes (optimistic edits). */
function withAggregates(
  data: ExerciseDayResponse,
  entries: ExerciseEntry[],
): ExerciseDayResponse {
  return { ...data, entries, totalCaloriesBurned: computeTotal(entries) };
}

type ExerciseStatus =
  | { state: 'loading' }
  | { state: 'ready'; data: ExerciseDayResponse }
  | { state: 'error'; message: string };

/**
 * Protected exercise-tracking page: a day view with a calories-burned summary,
 * a catalog search to log activities, date navigation, and per-entry
 * edit/delete. Logging requires a body weight on the profile; when it is
 * missing the backend returns 422 and the page prompts the user to complete
 * their profile.
 */
export default function ExercisePage() {
  const { t } = useTranslation('exercise');

  const [date, setDate] = useState<string>(todayLocalDate);
  const [status, setStatus] = useState<ExerciseStatus>({ state: 'loading' });

  const [pendingExercise, setPendingExercise] = useState<Exercise | null>(null);
  const [durationInput, setDurationInput] = useState<string>('');
  const [addError, setAddError] = useState<string | null>(null);
  const [isLogging, setIsLogging] = useState(false);
  const [missingBodyWeight, setMissingBodyWeight] = useState(false);

  const [editingId, setEditingId] = useState<string | null>(null);
  const [editDuration, setEditDuration] = useState<string>('');
  const [editError, setEditError] = useState<string | null>(null);

  const [deletingId, setDeletingId] = useState<string | null>(null);

  // Monotonic request id: only the latest load may apply its result, so rapid
  // date navigation (or a post-mutation reload) can't be clobbered by a stale
  // in-flight response.
  const requestRef = useRef(0);

  function loadExercises(targetDate: string) {
    const id = ++requestRef.current;
    setStatus({ state: 'loading' });

    getExerciseDay(targetDate)
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
    loadExercises(date);
    // Reset transient per-day UI state when the day changes.
    setEditingId(null);
    setEditError(null);
    setAddError(null);
    setPendingExercise(null);
    setDurationInput('');
    setMissingBodyWeight(false);
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
      const fieldError =
        err.problem?.errors?.durationMinutes?.[0] ?? err.problem?.errors?.activityId?.[0];
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

  function handleExerciseSelect(exercise: Exercise) {
    setPendingExercise(exercise);
    setDurationInput('');
    setAddError(null);
    setMissingBodyWeight(false);
  }

  function handleCancelAdd() {
    setPendingExercise(null);
    setDurationInput('');
    setAddError(null);
    setMissingBodyWeight(false);
  }

  function handleLog() {
    if (pendingExercise === null) {
      return;
    }
    const n = Number(durationInput.trim());
    if (!Number.isInteger(n) || n <= 0) {
      setAddError(t('errorDurationPositive'));
      return;
    }
    setAddError(null);
    setMissingBodyWeight(false);
    setIsLogging(true);
    const requestId = requestRef.current;

    logExercise({ activityId: pendingExercise.id, durationMinutes: n, date })
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
        setPendingExercise(null);
        setDurationInput('');
      })
      .catch((err: unknown) => {
        // A 422 without field-level errors signals the user has no body weight
        // set; everything else is surfaced as a generic add error.
        if (requestId === requestRef.current) {
          if (
            err instanceof ApiError &&
            err.status === 422 &&
            !(err.problem?.errors?.durationMinutes || err.problem?.errors?.activityId)
          ) {
            setMissingBodyWeight(true);
          } else {
            setAddError(messageFromError(err));
          }
        }
      })
      .finally(() => {
        setIsLogging(false);
      });
  }

  function handleEditStart(entry: ExerciseEntry) {
    setEditingId(entry.id);
    setEditDuration(String(entry.durationMinutes));
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
    const n = Number(editDuration.trim());
    if (!Number.isInteger(n) || n <= 0) {
      setEditError(t('errorDurationPositive'));
      return;
    }
    const requestId = requestRef.current;
    updateExerciseEntry(id, { durationMinutes: n })
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

    deleteExerciseEntry(id)
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
          loadExercises(deletedDate);
        }
      })
      .finally(() => {
        setDeletingId(null);
      });
  }

  const data = status.state === 'ready' ? status.data : null;

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

      {status.state === 'ready' && data && (
        <>
          <ExerciseDailySummary
            totalCaloriesBurned={data.totalCaloriesBurned ?? computeTotal(data.entries)}
            entryCount={data.entries.length}
          />

          <div className="card exercise-log-form">
            <h2>{t('logHeading')}</h2>

            {missingBodyWeight && (
              <p id="exercise-missing-weight" role="alert" className="message message-error">
                {t('missingBodyWeight')} <Link to="/profile">{t('completeProfile')}</Link>
              </p>
            )}

            {pendingExercise === null ? (
              <ExerciseSearchPanel onSelect={handleExerciseSelect} />
            ) : (
              <div className="exercise-duration-form">
                <p className="exercise-selected-name">{pendingExercise.name}</p>
                <div className="exercise-duration-row">
                  <label htmlFor="exercise-duration-input">{t('durationLabel')}</label>
                  <input
                    id="exercise-duration-input"
                    type="number"
                    inputMode="numeric"
                    min="1"
                    step="1"
                    className="exercise-duration-input"
                    value={durationInput}
                    aria-invalid={addError || missingBodyWeight ? true : undefined}
                    aria-describedby={
                      addError
                        ? 'exercise-add-error'
                        : missingBodyWeight
                          ? 'exercise-missing-weight'
                          : undefined
                    }
                    onChange={(e) => setDurationInput(e.target.value)}
                  />
                  <span className="exercise-entry-unit">{t('unitMin')}</span>
                  <button
                    type="button"
                    className="button-primary"
                    disabled={isLogging}
                    onClick={handleLog}
                  >
                    {isLogging ? t('logging') : t('logButton')}
                  </button>
                  <button
                    type="button"
                    className="button-secondary"
                    onClick={handleCancelAdd}
                  >
                    {t('cancelButton')}
                  </button>
                </div>
                {addError && (
                  <p id="exercise-add-error" role="alert" className="field-error">
                    {addError}
                  </p>
                )}
              </div>
            )}
          </div>

          <h2>{t('entriesHeading')}</h2>
          {data.entries.length === 0 ? (
            <p className="hint">{t('emptyHint')}</p>
          ) : (
            <ul className="exercise-entry-list">
              {data.entries.map((entry) => (
                <ExerciseEntryRow
                  key={entry.id}
                  entry={entry}
                  isDeleting={deletingId === entry.id}
                  isEditing={editingId === entry.id}
                  editDuration={editingId === entry.id ? editDuration : ''}
                  editError={editingId === entry.id ? editError : null}
                  onEditStart={handleEditStart}
                  onEditChange={setEditDuration}
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
