import { useEffect, useState } from 'react';
import { searchFoods, type Food } from '../../api/foods';

/** Props for {@link FoodSearchPanel}. */
interface FoodSearchPanelProps {
  onSelect: (food: Food) => void;
}

type SearchStatus =
  | { state: 'idle' }
  | { state: 'searching' }
  | { state: 'results'; items: Food[] }
  | { state: 'empty' }
  | { state: 'error'; message: string };

/** Don't search until the query is at least this long. */
const MIN_QUERY_LENGTH = 2;
/** Debounce window so we don't fire a request on every keystroke. */
const DEBOUNCE_MS = 350;

/**
 * Debounced food search: a search input plus a results listbox. Selecting a
 * result hands the full {@link Food} back to the parent.
 */
export default function FoodSearchPanel({ onSelect }: FoodSearchPanelProps) {
  const [query, setQuery] = useState('');
  const [status, setStatus] = useState<SearchStatus>({ state: 'idle' });

  useEffect(() => {
    if (query.trim().length < MIN_QUERY_LENGTH) {
      setStatus({ state: 'idle' });
      return;
    }

    let cancelled = false;
    const timer = setTimeout(() => {
      setStatus({ state: 'searching' });
      searchFoods(query.trim())
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
              message: err instanceof Error ? err.message : 'Search failed',
            });
          }
        });
    }, DEBOUNCE_MS);

    return () => {
      cancelled = true;
      clearTimeout(timer);
    };
  }, [query]);

  return (
    <div className="food-search-panel">
      <input
        type="search"
        aria-label="Search foods"
        placeholder="Search foods…"
        autoFocus
        value={query}
        onChange={(e) => setQuery(e.target.value)}
      />

      {status.state === 'idle' && (
        <p className="hint">Type to search the food database.</p>
      )}
      {status.state === 'searching' && <p>Searching…</p>}
      {status.state === 'results' && (
        <ul className="food-search-results">
          {status.items.map((food) => (
            <li key={food.id}>
              <button
                type="button"
                className="food-search-result-btn"
                onClick={() => onSelect(food)}
              >
                <span className="food-result-name">{food.name}</span>
                <span className="food-result-meta">
                  {food.brand ? `${food.brand} · ` : ''}
                  {Math.round(food.nutrition.calories)} kcal
                </span>
              </button>
            </li>
          ))}
        </ul>
      )}
      {status.state === 'empty' && (
        <p className="hint">No foods found for &ldquo;{query.trim()}&rdquo;.</p>
      )}
      {status.state === 'error' && (
        <p className="message message-error" role="alert">
          {status.message}
        </p>
      )}
    </div>
  );
}
