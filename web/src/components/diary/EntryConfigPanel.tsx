import { useState, type FormEvent, type ReactElement } from 'react';
import {
  scaleNutrition,
  type AddDiaryEntryRequest,
  type EntryNutrition,
  type MealType,
  type UpdateDiaryEntryRequest,
} from '../../api/diary';
import type { FoodDto } from '../../api/foods';

/** Props for {@link EntryConfigPanel}. */
interface EntryConfigPanelProps {
  food: FoodDto;
  mode: 'add' | 'edit';
  initialMealType: MealType;
  initialQuantity?: number;
  initialServingLabel?: string;
  date: string;
  saving: boolean;
  submitError: string | null;
  fieldErrors: Record<string, string[]>;
  onBack?: () => void;
  onSubmit: (req: AddDiaryEntryRequest | UpdateDiaryEntryRequest) => void;
}

const MEAL_TYPES: MealType[] = ['Breakfast', 'Lunch', 'Dinner', 'Snack'];

/** Grams used when a food offers no named serving (raw "per 100 g" portion). */
const PER_100G = 100;

/**
 * Resolves the initial serving label, falling back gracefully when a previously
 * chosen serving is no longer offered by the food.
 *
 * Foods identify servings by label (there is no serving id), so selection and
 * persistence are keyed on the label string.
 */
function resolveInitialServing(
  food: FoodDto,
  initialServingLabel: string | undefined,
): { label: string; staleLabel: string | null } {
  const has = (label: string | undefined): boolean =>
    label !== undefined && food.servingSizes.some((s) => s.label === label);

  if (has(initialServingLabel)) {
    return { label: initialServingLabel as string, staleLabel: null };
  }

  const fallback = food.servingSizes[0]?.label ?? '';

  // Only flag staleness when the caller actually asked for a serving that has
  // since vanished — not on the add path where none was requested.
  const stale = initialServingLabel !== undefined && !has(initialServingLabel);

  return {
    label: fallback,
    staleLabel: stale ? fallback || 'the default serving' : null,
  };
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
  initialServingLabel,
  date,
  saving,
  submitError,
  fieldErrors,
  onBack,
  onSubmit,
}: EntryConfigPanelProps) {
  const [mealType, setMealType] = useState<MealType>(initialMealType);
  const [quantity, setQuantity] = useState<string>(String(initialQuantity ?? 1));
  const initialServing = useState(() => resolveInitialServing(food, initialServingLabel))[0];
  const [servingLabel, setServingLabel] = useState<string>(initialServing.label);
  const [localErrors, setLocalErrors] = useState<Record<string, string[]>>({});

  const mergedErrors: Record<string, string[]> = { ...fieldErrors, ...localErrors };

  const hasServings = food.servingSizes.length > 0;
  // Grams for the chosen serving; fall back to a raw 100 g portion.
  const grams = food.servingSizes.find((s) => s.label === servingLabel)?.grams ?? PER_100G;
  const qty = Number(quantity);
  const preview: EntryNutrition | null =
    Number.isFinite(qty) && qty > 0 ? scaleNutrition(food.nutritionPer100g, grams, qty) : null;

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
        servingLabel: servingLabel || undefined,
      });
    } else {
      onSubmit({ mealType, quantity: parsed, servingLabel: servingLabel || undefined });
    }
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    handleConfirm();
  }

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
            value={servingLabel}
            onChange={(e) => setServingLabel(e.target.value)}
          >
            {food.servingSizes.map((s) => (
              <option key={s.label} value={s.label}>
                {s.label} ({s.grams} g)
              </option>
            ))}
          </select>
        ) : (
          <select id="servingSize" disabled value="">
            <option value="">100 g</option>
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
