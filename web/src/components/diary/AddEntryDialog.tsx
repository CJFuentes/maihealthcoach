import { useEffect, useRef, useState } from 'react';
import { ApiError } from '../../api/client';
import {
  addDiaryEntry,
  updateDiaryEntry,
  type AddDiaryEntryRequest,
  type DiaryEntry,
  type MealType,
  type UpdateDiaryEntryRequest,
} from '../../api/diary';
import { getFood, type Food } from '../../api/foods';
import EntryConfigPanel from './EntryConfigPanel';
import FoodSearchPanel from './FoodSearchPanel';

/** Discriminates between adding a new entry and editing an existing one. */
export type DialogMode =
  | { kind: 'add'; mealType: MealType }
  | { kind: 'edit'; entry: DiaryEntry };

/** Props for {@link AddEntryDialog}. */
interface AddEntryDialogProps {
  mode: DialogMode;
  date: string;
  onSuccess: (entry: DiaryEntry) => void;
  onClose: () => void;
}

type FoodLoadStatus =
  | { state: 'idle' }
  | { state: 'loading' }
  | { state: 'ready'; food: Food }
  | { state: 'error'; message: string };

/**
 * Modal dialog for adding or editing a diary entry.
 *
 * Add flow: search → pick a food → configure meal/serving/quantity.
 * Edit flow: loads the entry's food, then jumps straight to configuration.
 *
 * Uses the native <dialog> element for built-in focus trapping, Escape-to-close
 * and backdrop semantics.
 */
export default function AddEntryDialog({
  mode,
  date,
  onSuccess,
  onClose,
}: AddEntryDialogProps) {
  const dialogRef = useRef<HTMLDialogElement>(null);
  const mountedRef = useRef(true);

  const [step, setStep] = useState<'search' | 'configure'>(
    mode.kind === 'edit' ? 'configure' : 'search',
  );
  const [selectedFood, setSelectedFood] = useState<Food | null>(null);
  const [foodStatus, setFoodStatus] = useState<FoodLoadStatus>({ state: 'idle' });
  const [saving, setSaving] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({});

  // Open the dialog once on mount, and track mounted state so the async submit
  // path never sets state after unmount. Resetting `mountedRef` to true here
  // (rather than relying on the initial useRef value) keeps it correct across
  // React Strict Mode's mount→unmount→remount cycle, which reuses the ref.
  useEffect(() => {
    mountedRef.current = true;
    dialogRef.current?.showModal();
    return () => {
      mountedRef.current = false;
    };
  }, []);

  // For the edit path, load the entry's food so the user can change serving etc.
  useEffect(() => {
    if (mode.kind !== 'edit') {
      return;
    }
    let cancelled = false;
    setFoodStatus({ state: 'loading' });
    getFood(mode.entry.foodId)
      .then((food) => {
        if (!cancelled) {
          setFoodStatus({ state: 'ready', food });
        }
      })
      .catch((err: unknown) => {
        if (!cancelled) {
          setFoodStatus({
            state: 'error',
            message: err instanceof Error ? err.message : 'Could not load food details.',
          });
        }
      });
    return () => {
      cancelled = true;
    };
  }, [mode]);

  function requestClose() {
    dialogRef.current?.close();
  }

  async function handleSubmit(req: AddDiaryEntryRequest | UpdateDiaryEntryRequest) {
    setSaving(true);
    setSubmitError(null);
    setFieldErrors({});
    try {
      const result =
        mode.kind === 'add'
          ? await addDiaryEntry(req as AddDiaryEntryRequest)
          : await updateDiaryEntry(mode.entry.id, req as UpdateDiaryEntryRequest);
      // Parent unmounts this dialog in its onSuccess handler; don't touch state
      // afterwards.
      onSuccess(result);
    } catch (err: unknown) {
      if (!mountedRef.current) {
        return;
      }
      if (err instanceof ApiError && err.problem?.errors) {
        setFieldErrors(err.problem.errors);
        setSubmitError(err.problem.title ?? 'Please correct the highlighted fields.');
      } else {
        setSubmitError(err instanceof Error ? err.message : 'Could not save entry.');
      }
    } finally {
      if (mountedRef.current) {
        setSaving(false);
      }
    }
  }

  function renderConfigure() {
    if (mode.kind === 'edit') {
      if (foodStatus.state === 'loading' || foodStatus.state === 'idle') {
        return <p>Loading food details…</p>;
      }
      if (foodStatus.state === 'error') {
        return (
          <p role="alert" className="message message-error">
            Could not load food — {foodStatus.message}
          </p>
        );
      }
      return (
        <EntryConfigPanel
          food={foodStatus.food}
          mode="edit"
          initialMealType={mode.entry.mealType}
          initialQuantity={mode.entry.quantity}
          initialServingSizeId={mode.entry.servingSizeId}
          date={date}
          saving={saving}
          submitError={submitError}
          fieldErrors={fieldErrors}
          onSubmit={handleSubmit}
        />
      );
    }

    if (!selectedFood) {
      return null;
    }
    return (
      <EntryConfigPanel
        food={selectedFood}
        mode="add"
        initialMealType={mode.mealType}
        date={date}
        saving={saving}
        submitError={submitError}
        fieldErrors={fieldErrors}
        onBack={() => setStep('search')}
        onSubmit={handleSubmit}
      />
    );
  }

  return (
    <dialog
      ref={dialogRef}
      className="diary-dialog"
      aria-labelledby="diary-dialog-title"
      onClose={onClose}
      onClick={(e) => {
        if (e.target === dialogRef.current) {
          requestClose();
        }
      }}
    >
      <div className="dialog-header">
        <h2 id="diary-dialog-title">{mode.kind === 'add' ? 'Add food' : 'Edit entry'}</h2>
        <button
          type="button"
          className="button-secondary"
          aria-label="Close dialog"
          onClick={requestClose}
        >
          Close
        </button>
      </div>

      {step === 'search' && mode.kind === 'add' ? (
        <FoodSearchPanel
          onSelect={(food) => {
            setSelectedFood(food);
            setStep('configure');
          }}
        />
      ) : (
        renderConfigure()
      )}
    </dialog>
  );
}
