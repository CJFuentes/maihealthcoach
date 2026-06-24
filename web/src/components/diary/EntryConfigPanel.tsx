import { useState, type FormEvent, type ReactElement } from 'react';
import {
  scaleNutrition,
  type AddDiaryEntryRequest,
  type EntryNutrition,
  type MealType,
  type UpdateDiaryEntryRequest,
} from '../../api/diary';
import type { Food } from '../../api/foods';

/** Props for {@link EntryConfigPanel}. */
interface EntryConfigPanelProps {
  food: Food;
  mode: 'add' | 'edit';
  initialMealType: MealType;
  initialQuantity?: number;
  initialServingSizeId?: string;
  date: string;
  saving: boolean;
  submitError: string | null;
  fieldErrors: Record<string, string[]>;
  onBack?: () => void;
  onSubmit: (req: AddDiaryEntryRequest | UpdateDiaryEntryRequest) => void;
}

const MEAL_TYPES: MealType[] = ['Breakfast', 'Lunch', 'Dinner', 'Snack'];

/**
 * Resolves the initial serving id, falling back gracefully when a previously
 * chosen serving is no longer offered by the food.
 */
function resolveInitialServingId(
  food: Food,
  initialServingSizeId: string | undefined,
): { id: string; staleLabel: string | null } {
  const has = (id: string | undefined): boolean =>
    id !== undefined && food.servingSizes.some((s) => s.id === id);

  if (has(initialServingSizeId)) {
    return { id: initialServingSizeId as string, staleLabel: null };
  }

  const fallback = has(food.defaultServingSizeId)
    ? (food.defaultServingSizeId as string)
    : (food.servingSizes[0]?.id ?? '');

  // Only flag staleness when the caller actually asked for a serving that has
  // since vanished — not on the add path where none was requested.
  const stale = initialServingSizeId !== undefined && !has(initialServingSizeId);
  const fallbackLabel =
    food.servingSizes.find((s) => s.id === fallback)?.label ?? 'the default serving';

  return { id: fallback, staleLabel: stale ? fallbackLabel : null };
}

/**
 * Meal/serving/quantity picker with a live nutrition preview. Used for both
 * adding a new entry and editing an existing one.
 */
export default function EntryConfigPanel({
  food,
  mode,
  initialMealType,
  initialQuantity,
  initialServingSizeId,
  date,
  saving,
  submitError,
  fieldErrors,
  onBack,
  onSubmit,
}: EntryConfigPanelProps) {
  const [mealType, setMealType] = useState<MealType>(initialMealType);
  const [quantity, setQuantity] = useState<string>(String(initialQuantity ?? 1));
  const initialServing = useState(() => resolveInitialServingId(food, initialServingSizeId))[0];
  const [servingSizeId, setServingSizeId] = useState<string>(initialServing.id);
  const [localErrors, setLocalErrors] = useState<Record<string, string[]>>({});

  const mergedErrors: Record<string, string[]> = { ...fieldErrors, ...localErrors };

  // Nutrition for the chosen serving, falling back to the food's base figures.
  const serving = food.servingSizes.find((s) => s.id === servingSizeId);
  const servingNutrition = serving?.nutrition ?? food.nutrition;
  const qty = Number(quantity);
  const preview: EntryNutrition | null =
    Number.isFinite(qty) && qty > 0 ? scaleNutrition(servingNutrition, qty) : null;

  function fieldError(key: string): ReactElement | null {
    const errors = mergedErrors[key];
    if (!errors || errors.length === 0) {
      return null;
    }
    return (
      <p className="field-error" id={`${key}-error`} role="alert">
        {errors.join(' ')}
      </p>
    );
  }

  function handleConfirm() {
    const parsed = Number(quantity);
    if (!Number.isFinite(parsed) || parsed <= 0) {
      setLocalErrors({ quantity: ['Enter a positive number.'] });
      return;
    }
    setLocalErrors({});

    if (mode === 'add') {
      onSubmit({
        foodId: food.id,
        mealType,
        date,
        quantity: parsed,
        servingSizeId: servingSizeId || undefined,
      });
    } else {
      onSubmit({ mealType, quantity: parsed, servingSizeId: servingSizeId || undefined });
    }
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    handleConfirm();
  }

  const hasServings = food.servingSizes.length > 0;
  const previewValue = (value: number | undefined): string =>
    value === undefined ? '—' : String(Math.round(value));

  return (
    <form onSubmit={handleSubmit} noValidate>
      <p className="diary-config-food">
        <strong>{food.name}</strong>
        {food.brand && <span className="hint"> · {food.brand}</span>}
      </p>

      {initialServing.staleLabel && (
        <p className="hint">
          The original serving is no longer available; defaulted to{' '}
          {initialServing.staleLabel}.
        </p>
      )}

      <div className="form-field">
        <label htmlFor="mealType">Meal</label>
        <select
          id="mealType"
          value={mealType}
          onChange={(e) => setMealType(e.target.value as MealType)}
        >
          {MEAL_TYPES.map((m) => (
            <option key={m} value={m}>
              {m}
            </option>
          ))}
        </select>
      </div>

      <div className="form-field">
        <label htmlFor="servingSize">Serving</label>
        {hasServings ? (
          <select
            id="servingSize"
            value={servingSizeId}
            onChange={(e) => setServingSizeId(e.target.value)}
          >
            {food.servingSizes.map((s) => (
              <option key={s.id} value={s.id}>
                {s.label}
              </option>
            ))}
          </select>
        ) : (
          <select id="servingSize" disabled value="">
            <option value="">1 serving</option>
          </select>
        )}
      </div>

      <div className="form-field">
        <label htmlFor="quantity">Quantity</label>
        <input
          id="quantity"
          type="number"
          min="0.25"
          step="0.25"
          inputMode="decimal"
          value={quantity}
          onChange={(e) => setQuantity(e.target.value)}
          aria-invalid={Boolean(mergedErrors.quantity)}
          aria-describedby={mergedErrors.quantity?.length ? 'quantity-error' : undefined}
        />
        {fieldError('quantity')}
      </div>

      <div className="entry-nutrition-preview">
        <div className="entry-preview-item">
          <span className="entry-preview-value">{previewValue(preview?.calories)}</span>
          <span className="entry-preview-label">Calories</span>
        </div>
        <div className="entry-preview-item">
          <span className="entry-preview-value">{previewValue(preview?.proteinGrams)}</span>
          <span className="entry-preview-label">Protein</span>
        </div>
        <div className="entry-preview-item">
          <span className="entry-preview-value">{previewValue(preview?.carbohydrateGrams)}</span>
          <span className="entry-preview-label">Carbs</span>
        </div>
        <div className="entry-preview-item">
          <span className="entry-preview-value">{previewValue(preview?.fatGrams)}</span>
          <span className="entry-preview-label">Fat</span>
        </div>
      </div>

      {submitError && (
        <p role="alert" className="message message-error">
          {submitError}
        </p>
      )}

      <div className="form-actions">
        {onBack && (
          <button type="button" className="button-secondary" onClick={onBack}>
            Back
          </button>
        )}
        <button type="submit" disabled={saving}>
          {saving ? 'Saving…' : mode === 'add' ? 'Add to diary' : 'Save changes'}
        </button>
      </div>
    </form>
  );
}
