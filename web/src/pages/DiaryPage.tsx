import { useEffect, useRef, useState } from 'react';
import {
  deleteDiaryEntry,
  getDailySummary,
  getDiary,
  type DailySummary,
  type DiaryEntry,
  type DiaryResponse,
  type EntryNutrition,
  type MealType,
} from '../api/diary';
import AddEntryDialog, { type DialogMode } from '../components/diary/AddEntryDialog';
import DailySummaryBar from '../components/diary/DailySummaryBar';
import DateNav from '../components/diary/DateNav';
import MealSection from '../components/diary/MealSection';

/** The meals rendered, in display order. */
const MEAL_TYPES: MealType[] = ['Breakfast', 'Lunch', 'Dinner', 'Snack'];

/** Returns today's date as a local `YYYY-MM-DD` string. */
function todayLocalDate(): string {
  const now = new Date();
  return formatLocalDate(now);
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

/** Sums per-meal entry nutrition into day totals (used when no server summary). */
function computeTotals(meals: DiaryResponse['meals']): EntryNutrition {
  const totals: Required<
    Pick<EntryNutrition, 'calories' | 'proteinGrams' | 'carbohydrateGrams' | 'fatGrams'>
  > = {
    calories: 0,
    proteinGrams: 0,
    carbohydrateGrams: 0,
    fatGrams: 0,
  };

  for (const meal of MEAL_TYPES) {
    for (const entry of meals[meal] ?? []) {
      const n = entry.nutrition;
      if (!n) {
        continue;
      }
      totals.calories += n.calories ?? 0;
      totals.proteinGrams += n.proteinGrams ?? 0;
      totals.carbohydrateGrams += n.carbohydrateGrams ?? 0;
      totals.fatGrams += n.fatGrams ?? 0;
    }
  }

  return totals;
}

type DiaryStatus =
  | { state: 'loading' }
  | { state: 'ready'; diary: DiaryResponse }
  | { state: 'error'; message: string };

/**
 * Protected food-diary page: a day view grouped by meal with date navigation,
 * a running daily summary, and add/edit/delete of entries.
 */
export default function DiaryPage() {
  const [date, setDate] = useState<string>(todayLocalDate);
  const [status, setStatus] = useState<DiaryStatus>({ state: 'loading' });
  const [summary, setSummary] = useState<DailySummary | null>(null);
  const [dialogMode, setDialogMode] = useState<DialogMode | null>(null);
  const [deletingId, setDeletingId] = useState<string | null>(null);

  // Monotonic request id: only the latest load may apply its result, so rapid
  // date navigation (or a post-mutation reload) can't be clobbered by a stale
  // in-flight response.
  const requestRef = useRef(0);

  function loadDiary(targetDate: string) {
    const id = ++requestRef.current;
    setStatus({ state: 'loading' });
    setSummary(null);

    getDiary(targetDate)
      .then((diary) => {
        if (id === requestRef.current) {
          setStatus({ state: 'ready', diary });
        }
      })
      .catch((err: unknown) => {
        if (id === requestRef.current) {
          setStatus({
            state: 'error',
            message: err instanceof Error ? err.message : 'Unknown error',
          });
        }
      });

    getDailySummary(targetDate)
      .then((s) => {
        if (id === requestRef.current) {
          setSummary(s);
        }
      })
      .catch(() => {
        // Summary is best-effort; fall back to client-computed totals.
      });
  }

  useEffect(() => {
    loadDiary(date);
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

  function handleAddClick(mealType: MealType) {
    setDialogMode({ kind: 'add', mealType });
  }

  function handleEditClick(entry: DiaryEntry) {
    setDialogMode({ kind: 'edit', entry });
  }

  function handleDialogClose() {
    setDialogMode(null);
  }

  function handleEntryMutated() {
    setDialogMode(null);
    loadDiary(date);
  }

  function handleDelete(id: string) {
    // Capture the request token so a rapid date change before the delete
    // resolves cannot clobber the now-current day's state (mirrors loadDiary).
    const deletedDate = date;
    const requestId = requestRef.current;
    setDeletingId(id);
    deleteDiaryEntry(id)
      .then(() => {
        if (requestId !== requestRef.current) {
          return;
        }
        // Optimistically drop the entry from the current day without a refetch.
        setStatus((prev) => {
          if (prev.state !== 'ready') {
            return prev;
          }
          const meals: DiaryResponse['meals'] = {};
          for (const meal of MEAL_TYPES) {
            const remaining = (prev.diary.meals[meal] ?? []).filter((e) => e.id !== id);
            if (remaining.length > 0) {
              meals[meal] = remaining;
            }
          }
          return { state: 'ready', diary: { ...prev.diary, meals } };
        });
        // Refresh the server summary to reflect the deletion.
        getDailySummary(deletedDate)
          .then((s) => {
            if (requestId === requestRef.current) {
              setSummary(s);
            }
          })
          .catch(() => {});
      })
      .catch(() => {
        // Roll back to authoritative server state on failure (only if still
        // on the same day — otherwise the active load owns the state).
        if (requestId === requestRef.current) {
          loadDiary(deletedDate);
        }
      })
      .finally(() => {
        setDeletingId(null);
      });
  }

  return (
    <section>
      <h1>Food Diary</h1>

      <DateNav date={date} onPrev={handlePrev} onNext={handleNext} onToday={handleToday} />

      {status.state === 'loading' && <p>Loading diary…</p>}

      {status.state === 'error' && (
        <p role="alert" className="message message-error">
          Could not load diary — {status.message}
        </p>
      )}

      {status.state === 'ready' && (
        <>
          <DailySummaryBar
            totals={summary?.totals ?? computeTotals(status.diary.meals)}
            goals={summary?.goals}
          />

          {MEAL_TYPES.map((meal) => (
            <MealSection
              key={meal}
              mealType={meal}
              entries={status.diary.meals[meal] ?? []}
              onAdd={handleAddClick}
              onEdit={handleEditClick}
              onDelete={handleDelete}
              deletingId={deletingId}
            />
          ))}
        </>
      )}

      {dialogMode && (
        <AddEntryDialog
          mode={dialogMode}
          date={date}
          onSuccess={handleEntryMutated}
          onClose={handleDialogClose}
        />
      )}
    </section>
  );
}
