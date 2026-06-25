import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { searchExercises, type Exercise } from '../../api/exercise';

/** Props for {@link ExerciseSearchPanel}. */
interface ExerciseSearchPanelProps {
  onSelect: (exercise: Exercise) => void;
}

type SearchStatus =
  | { state: 'idle' }
  | { state: 'searching' }
  | { state: 'results'; items: Exercise[] }
  | { state: 'empty' }
  | { state: 'error'; message: string };

/** Don't search until the query is at least this long. */
const MIN_QUERY_LENGTH = 2;
/** Debounce window so we don't fire a request on every keystroke. */
const DEBOUNCE_MS = 350;

/** Catalog categories the filter exposes (empty value = no category filter). */
const CATEGORIES = ['', 'Cardio', 'Strength', 'Flexibility', 'Sports'] as const;

/**
 * Debounced exercise-catalog search: a search input, a category filter, and a
 * results list. Selecting a result hands the full {@link Exercise} back to the
 * parent.
 */
export default function ExerciseSearchPanel({ onSelect }: ExerciseSearchPanelProps) {
  const { t } = useTranslation('exercise');
  const [query, setQuery] = useState('');
  const [category, setCategory] = useState('');
  const [status, setStatus] = useState<SearchStatus>({ state: 'idle' });
  const searchInputRef = useRef<HTMLInputElement>(null);

  // Move focus into the search field on mount (replaces the autoFocus prop, which
  // jsx-a11y disallows) so keyboard users land in the field immediately.
  useEffect(() => {
    searchInputRef.current?.focus();
  }, []);

  useEffect(() => {
    if (query.trim().length < MIN_QUERY_LENGTH) {
      setStatus({ state: 'idle' });
      return;
    }

    let cancelled = false;
    const timer = setTimeout(() => {
      setStatus({ state: 'searching' });
      searchExercises(query.trim(), category || undefined)
        .then((res) => {
          if (!cancelled) {
            setStatus(
              res.items.length ? { state: 'results', items: res.items } : { state: 'empty' },
            );
          }
        })
        .catch((err: unknown) => {
          if (!cancelled) {
            setStatus({
              state: 'error',
              message: err instanceof Error ? err.message : t('unknownError'),
            });
          }
        });
    }, DEBOUNCE_MS);

    return () => {
      cancelled = true;
      clearTimeout(timer);
    };
  }, [query, category, t]);

  // TODO(custom-activity): implement when backend supports creating custom activities

  return (
    <div className="exercise-search-panel">
      <div className="exercise-search-controls">
        <label htmlFor="exercise-search-input" className="sr-only">
          {t('searchLabel')}
        </label>
        <input
          id="exercise-search-input"
          ref={searchInputRef}
          type="search"
          placeholder={t('searchPlaceholder')}
          value={query}
          onChange={(e) => setQuery(e.target.value)}
        />

        <label htmlFor="exercise-category-filter" className="sr-only">
          {t('categoryFilterLabel')}
        </label>
        <select
          id="exercise-category-filter"
          value={category}
          onChange={(e) => setCategory(e.target.value)}
        >
          {CATEGORIES.map((value) => (
            <option key={value || 'all'} value={value}>
              {value
                ? t(`category${value}` as 'categoryCardio')
                : t('categoryAll')}
            </option>
          ))}
        </select>
      </div>

      {status.state === 'idle' && <p className="hint">{t('searchHint')}</p>}
      {status.state === 'searching' && <p>{t('searching')}</p>}
      {status.state === 'results' && (
        <ul className="exercise-search-results">
          {status.items.map((exercise) => (
            <li key={exercise.id}>
              <button
                type="button"
                className="exercise-search-result-btn"
                onClick={() => onSelect(exercise)}
              >
                <span className="exercise-result-name">{exercise.name}</span>
                <span className="exercise-result-meta">
                  {exercise.category} · MET {exercise.met}
                </span>
              </button>
            </li>
          ))}
        </ul>
      )}
      {status.state === 'empty' && (
        <>
          <p className="hint">{t('noResults', { query: query.trim() })}</p>
          <p className="hint">{t('customActivityDeferred')}</p>
        </>
      )}
      {status.state === 'error' && (
        <p role="alert" className="message message-error">
          {status.message}
        </p>
      )}
    </div>
  );
}
