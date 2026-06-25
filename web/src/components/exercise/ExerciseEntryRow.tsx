import { useTranslation } from 'react-i18next';
import type { ExerciseEntry } from '../../api/exercise';

/** Props for {@link ExerciseEntryRow}. */
interface ExerciseEntryRowProps {
  entry: ExerciseEntry;
  isDeleting: boolean;
  /** True when this row is open in inline-edit mode. */
  isEditing: boolean;
  /** Controlled value of the edit input (only meaningful when `isEditing`). */
  editDuration: string;
  /** Validation error for the edit field, if any. */
  editError: string | null;
  onEditStart: (entry: ExerciseEntry) => void;
  onEditChange: (value: string) => void;
  onEditCommit: () => void;
  onEditCancel: () => void;
  onDelete: (id: string) => void;
}

/**
 * A single logged exercise entry: activity name, category, duration, and
 * calories burned on the left, edit/remove actions on the right. In edit mode
 * the duration is swapped for a labelled number input with Save/Cancel.
 */
export default function ExerciseEntryRow({
  entry,
  isDeleting,
  isEditing,
  editDuration,
  editError,
  onEditStart,
  onEditChange,
  onEditCommit,
  onEditCancel,
  onDelete,
}: ExerciseEntryRowProps) {
  const { t } = useTranslation('exercise');
  const errorId = `exercise-edit-error-${entry.id}`;

  if (isEditing) {
    return (
      <li className="exercise-entry-row exercise-entry-row-editing">
        <div className="exercise-entry-edit">
          <label htmlFor={`exercise-edit-${entry.id}`} className="sr-only">
            {t('editDurationLabel')}
          </label>
          <input
            id={`exercise-edit-${entry.id}`}
            type="number"
            inputMode="numeric"
            min="1"
            className="exercise-edit-input"
            value={editDuration}
            aria-invalid={editError ? true : undefined}
            aria-describedby={editError ? errorId : undefined}
            onChange={(e) => onEditChange(e.target.value)}
          />
          <span className="exercise-entry-unit">{t('unitMin')}</span>
          <button type="button" className="button-secondary" onClick={onEditCommit}>
            {t('saveButton')}
          </button>
          <button type="button" className="button-secondary" onClick={onEditCancel}>
            {t('cancelButton')}
          </button>
        </div>
        {editError && (
          <p id={errorId} role="alert" className="field-error">
            {editError}
          </p>
        )}
      </li>
    );
  }

  return (
    <li className="exercise-entry-row">
      <div className="exercise-entry-info">
        <span className="exercise-entry-name">{entry.activityName}</span>
        <span className="exercise-entry-category hint"> · {entry.category}</span>
        <span className="exercise-entry-duration">
          {' '}
          {entry.durationMinutes} {t('unitMin')}
        </span>
        <span className="exercise-entry-calories">
          {' '}
          {Math.round(entry.caloriesBurned)} {t('unitKcal')}
        </span>
      </div>

      <div className="exercise-entry-actions">
        <button
          type="button"
          className="button-secondary"
          aria-label={t('editEntry', { name: entry.activityName })}
          onClick={() => onEditStart(entry)}
        >
          {t('editButton')}
        </button>
        <button
          type="button"
          className="button-secondary"
          aria-label={t('removeEntry', { name: entry.activityName })}
          disabled={isDeleting}
          onClick={() => onDelete(entry.id)}
        >
          {isDeleting ? t('removing') : t('removeButton')}
        </button>
      </div>
    </li>
  );
}
