import type { DiaryEntry } from '../../api/diary';

/** Props for {@link DiaryEntryRow}. */
interface DiaryEntryRowProps {
  entry: DiaryEntry;
  onEdit: (entry: DiaryEntry) => void;
  onDelete: (id: string) => void;
  isDeleting: boolean;
}

/** Formats a macro value, or an em dash when it is absent. */
function macro(value: number | undefined): string {
  return value === undefined ? '—' : String(Math.round(value));
}

/**
 * A single logged food row: name/brand/serving on the left, nutrition in the
 * middle, and edit/remove actions on the right.
 */
export default function DiaryEntryRow({
  entry,
  onEdit,
  onDelete,
  isDeleting,
}: DiaryEntryRowProps) {
  const { nutrition } = entry;

  return (
    <li className="diary-entry-row">
      <div className="diary-entry-info">
        <span className="diary-entry-name">{entry.foodName}</span>
        {entry.brand && <span className="diary-entry-brand hint"> · {entry.brand}</span>}
        <span className="diary-entry-serving">
          {entry.quantity} × {entry.servingLabel ?? '100 g'}
        </span>
      </div>

      <div className="diary-entry-nutrition">
        <span className="diary-entry-kcal">
          {nutrition ? `${Math.round(nutrition.calories)} kcal` : '—'}
        </span>
        <span className="diary-entry-macros">
          P {macro(nutrition?.proteinGrams)} · C {macro(nutrition?.carbohydrateGrams)} · F{' '}
          {macro(nutrition?.fatGrams)}
        </span>
      </div>

      <div className="diary-entry-actions">
        <button
          type="button"
          className="button-secondary"
          aria-label={`Edit ${entry.foodName}`}
          onClick={() => onEdit(entry)}
        >
          Edit
        </button>
        <button
          type="button"
          className="button-secondary"
          aria-label={`Delete ${entry.foodName}`}
          disabled={isDeleting}
          onClick={() => onDelete(entry.id)}
        >
          {isDeleting ? 'Removing…' : 'Remove'}
        </button>
      </div>
    </li>
  );
}
