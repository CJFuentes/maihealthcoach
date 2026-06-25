import { useTranslation } from 'react-i18next';
import type { WaterEntry } from '../../api/water';

/** Props for {@link WaterEntryRow}. */
interface WaterEntryRowProps {
  entry: WaterEntry;
  isDeleting: boolean;
  /** True when this row is open in inline-edit mode. */
  isEditing: boolean;
  /** Controlled value of the edit input (only meaningful when `isEditing`). */
  editAmount: string;
  /** Validation error for the edit field, if any. */
  editError: string | null;
  onEditStart: (entry: WaterEntry) => void;
  onEditChange: (value: string) => void;
  onEditCommit: () => void;
  onEditCancel: () => void;
  onDelete: (id: string) => void;
}

/**
 * Formats an ISO timestamp as a short time in the active language's locale
 * (e.g. "08:30" or "8:30 AM"). Returns an empty string for an unparseable
 * timestamp.
 */
function formatTime(iso: string, locale: string): string {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) {
    return '';
  }
  return new Intl.DateTimeFormat(locale, {
    hour: '2-digit',
    minute: '2-digit',
  }).format(d);
}

/**
 * A single logged water entry: amount + time on the left, edit/remove actions
 * on the right. In edit mode the amount is swapped for a labelled number input
 * with Save/Cancel.
 */
export default function WaterEntryRow({
  entry,
  isDeleting,
  isEditing,
  editAmount,
  editError,
  onEditStart,
  onEditChange,
  onEditCommit,
  onEditCancel,
  onDelete,
}: WaterEntryRowProps) {
  const { t, i18n } = useTranslation('water');
  const time = formatTime(entry.loggedAt, i18n.language);
  const errorId = `water-edit-error-${entry.id}`;

  if (isEditing) {
    return (
      <li className="water-entry-row water-entry-row-editing">
        <div className="water-entry-edit">
          <label htmlFor={`water-edit-${entry.id}`} className="sr-only">
            {t('editAmountLabel')}
          </label>
          <input
            id={`water-edit-${entry.id}`}
            type="number"
            inputMode="numeric"
            min="1"
            className="water-edit-input"
            value={editAmount}
            aria-invalid={editError ? true : undefined}
            aria-describedby={editError ? errorId : undefined}
            onChange={(e) => onEditChange(e.target.value)}
          />
          <span className="water-entry-unit">{t('unitMl')}</span>
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
    <li className="water-entry-row">
      <div className="water-entry-info">
        <span className="water-entry-amount">
          {entry.amountMl} {t('unitMl')}
        </span>
        {time && <span className="water-entry-time hint"> · {time}</span>}
      </div>

      <div className="water-entry-actions">
        <button
          type="button"
          className="button-secondary"
          aria-label={t('editEntry', { amount: entry.amountMl })}
          onClick={() => onEditStart(entry)}
        >
          {t('editButton')}
        </button>
        <button
          type="button"
          className="button-secondary"
          aria-label={t('removeEntry', { amount: entry.amountMl })}
          disabled={isDeleting}
          onClick={() => onDelete(entry.id)}
        >
          {isDeleting ? t('removing') : t('removeButton')}
        </button>
      </div>
    </li>
  );
}
