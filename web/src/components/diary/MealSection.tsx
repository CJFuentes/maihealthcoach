import type { DiaryEntry, MealType } from '../../api/diary';
import DiaryEntryRow from './DiaryEntryRow';

/** Props for {@link MealSection}. */
interface MealSectionProps {
  mealType: MealType;
  entries: DiaryEntry[];
  onAdd: (mealType: MealType) => void;
  onEdit: (entry: DiaryEntry) => void;
  onDelete: (id: string) => void;
  deletingId: string | null;
}

/** Display labels per meal (Snack is pluralised). */
const MEAL_LABELS: Record<MealType, string> = {
  Breakfast: 'Breakfast',
  Lunch: 'Lunch',
  Dinner: 'Dinner',
  Snack: 'Snacks',
};

/**
 * One meal group: heading with a calorie subtotal, the list of entries (or an
 * empty hint), and an "Add food" action.
 */
export default function MealSection({
  mealType,
  entries,
  onAdd,
  onEdit,
  onDelete,
  deletingId,
}: MealSectionProps) {
  const kcal = Math.round(
    entries.reduce((sum, entry) => sum + (entry.nutrition?.calories ?? 0), 0),
  );

  return (
    <section className="diary-meal-section">
      <div className="diary-meal-header">
        <h2>{MEAL_LABELS[mealType]}</h2>
        {kcal > 0 && <span className="diary-meal-kcal">{kcal} kcal</span>}
      </div>

      {entries.length === 0 ? (
        <p className="hint">No entries yet.</p>
      ) : (
        <ul className="diary-entry-list">
          {entries.map((entry) => (
            <DiaryEntryRow
              key={entry.id}
              entry={entry}
              onEdit={onEdit}
              onDelete={onDelete}
              isDeleting={deletingId === entry.id}
            />
          ))}
        </ul>
      )}

      <button
        type="button"
        className="button-secondary diary-add-btn"
        onClick={() => onAdd(mealType)}
      >
        Add food
      </button>
    </section>
  );
}
