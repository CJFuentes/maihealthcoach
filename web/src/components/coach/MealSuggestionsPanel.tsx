import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ApiError } from '../../api/client';
import { getMealSuggestions, type MealSuggestionsResponse } from '../../api/coach';

type MealStatus =
  | { state: 'loading' }
  | { state: 'ready'; data: MealSuggestionsResponse }
  | { state: 'noProfile' }
  | { state: 'incompleteProfile' }
  | { state: 'error'; message: string };

/**
 * The meal-suggestions tab: fetches personalised options for the user's
 * remaining daily budget and renders them as cards, branching on the
 * profile-related error statuses (404/409) to prompt the user to complete their
 * profile.
 */
export default function MealSuggestionsPanel() {
  const { t } = useTranslation('coach');
  const [status, setStatus] = useState<MealStatus>({ state: 'loading' });

  // Resolve fallback strings up front so the load effect does not depend on `t`
  // (whose identity changes on language switch). Fetching is language-agnostic.
  const serviceUnavailableMsg = t('mealSuggestions.serviceUnavailable');
  const unknownErrorMsg = t('mealSuggestions.unknownError');

  useEffect(() => {
    let cancelled = false;
    setStatus({ state: 'loading' });

    getMealSuggestions()
      .then((data) => {
        if (!cancelled) {
          setStatus({ state: 'ready', data });
        }
      })
      .catch((error: unknown) => {
        if (cancelled) {
          return;
        }
        if (error instanceof ApiError && error.status === 404) {
          setStatus({ state: 'noProfile' });
        } else if (error instanceof ApiError && error.status === 409) {
          setStatus({ state: 'incompleteProfile' });
        } else if (error instanceof ApiError && (error.status === 502 || error.status === 503)) {
          setStatus({ state: 'error', message: serviceUnavailableMsg });
        } else {
          const message = error instanceof Error ? error.message : unknownErrorMsg;
          setStatus({ state: 'error', message });
        }
      });

    return () => {
      cancelled = true;
    };
  }, [serviceUnavailableMsg, unknownErrorMsg]);

  return (
    <section>
      <h2>{t('tabs.mealSuggestions')}</h2>

      {status.state === 'loading' && <p role="status">{t('mealSuggestions.loading')}</p>}

      {(status.state === 'noProfile' || status.state === 'incompleteProfile') && (
        <>
          <p className="message message-info">
            {status.state === 'noProfile'
              ? t('mealSuggestions.noProfile')
              : t('mealSuggestions.incompleteProfile')}
          </p>
          <p>
            <Link to="/profile">{t('mealSuggestions.completeProfileLink')}</Link>
          </p>
        </>
      )}

      {status.state === 'error' && (
        <p role="alert" className="message message-error">
          {t('mealSuggestions.loadError', { message: status.message })}
        </p>
      )}

      {status.state === 'ready' && status.data.options.length === 0 && (
        <p className="hint">{t('mealSuggestions.empty')}</p>
      )}

      {status.state === 'ready' && status.data.options.length > 0 && (
        <>
          <p className="coach-meal-remaining">
            {t('mealSuggestions.remaining', {
              calories: status.data.remainingCalories,
              protein: status.data.remainingProteinGrams,
              carbs: status.data.remainingCarbGrams,
              fat: status.data.remainingFatGrams,
            })}
          </p>
          <ul className="coach-meal-options">
            {status.data.options.map((option) => (
              <li key={option.name} className="card coach-meal-card">
                <h3>{option.name}</h3>
                {option.calories !== null && (
                  <p>{t('mealSuggestions.card.calories', { value: option.calories })}</p>
                )}
                {option.proteinGrams !== null && (
                  <p>{t('mealSuggestions.card.protein', { value: option.proteinGrams })}</p>
                )}
                {option.carbGrams !== null && (
                  <p>{t('mealSuggestions.card.carbs', { value: option.carbGrams })}</p>
                )}
                {option.fatGrams !== null && (
                  <p>{t('mealSuggestions.card.fat', { value: option.fatGrams })}</p>
                )}
                <p className="coach-meal-rationale">
                  {t('mealSuggestions.card.rationale', { text: option.rationale })}
                </p>
              </li>
            ))}
          </ul>
          {status.data.disclaimer && (
            <p className="coach-disclaimer">
              {t('mealSuggestions.disclaimer', { text: status.data.disclaimer })}
            </p>
          )}
        </>
      )}
    </section>
  );
}
