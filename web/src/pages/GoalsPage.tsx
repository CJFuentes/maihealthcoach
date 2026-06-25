import { useEffect, useMemo, useState, type FormEvent, type ReactElement } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ApiError } from '../api/client';
import {
  getGoals,
  setGoalOverrides,
  type GoalsResponse,
  type GoalValue,
  type SetGoalOverridesRequest,
} from '../api/goals';

type Status =
  | { state: 'loading' }
  | { state: 'ready'; goals: GoalsResponse }
  | { state: 'incomplete'; message: string }
  | { state: 'error'; message: string };

/**
 * Override form rows: maps a request key to its translation key for the label
 * and the source goal accessor. The label is resolved via t(`override.${i18nKey}`)
 * inside the component so it stays localizable.
 */
interface OverrideField {
  key: keyof SetGoalOverridesRequest;
  i18nKey: 'calories' | 'protein' | 'carbs' | 'fat' | 'water';
  goal: (g: GoalsResponse) => GoalValue;
}

function buildOverrideForm(fields: OverrideField[], goals: GoalsResponse): Record<string, string> {
  return Object.fromEntries(
    fields.map((f) => [f.key, String(f.goal(goals).value)]),
  ) as Record<string, string>;
}

export default function GoalsPage() {
  const { t, i18n } = useTranslation('goals');
  const [status, setStatus] = useState<Status>({ state: 'loading' });
  const [overrides, setOverrides] = useState<Record<string, string>>({});
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({});
  const [submitError, setSubmitError] = useState<string | null>(null);

  // Moved inside the component so the override labels can use the active
  // translation. Memoized so its identity is stable across renders, keeping the
  // load effect's dependency list honest. The field set itself is static.
  const OVERRIDE_FIELDS = useMemo<OverrideField[]>(
    () => [
      { key: 'caloriesKcal', i18nKey: 'calories', goal: (g) => g.calories },
      { key: 'proteinGrams', i18nKey: 'protein', goal: (g) => g.proteinGrams },
      { key: 'carbohydrateGrams', i18nKey: 'carbs', goal: (g) => g.carbohydrateGrams },
      { key: 'fatGrams', i18nKey: 'fat', goal: (g) => g.fatGrams },
      { key: 'waterMl', i18nKey: 'water', goal: (g) => g.waterMl },
    ],
    [],
  );

  function formatDate(iso: string): string {
    const date = new Date(iso);
    if (Number.isNaN(date.getTime())) {
      return iso;
    }
    return new Intl.DateTimeFormat(i18n.language).format(date);
  }

  // Resolve the load-error fallback strings up front so the data-fetch effect
  // does not depend on `t` (whose identity changes on language switch and would
  // otherwise trigger a spurious re-fetch). Data fetching is independent of the
  // active UI language.
  const profileNotFoundMsg = t('profileNotFound');
  const profileMissingInfoMsg = t('profileMissingInfo');
  const unknownErrorMsg = t('unknownError');

  useEffect(() => {
    let cancelled = false;
    setStatus({ state: 'loading' });

    getGoals()
      .then((goals) => {
        if (!cancelled) {
          setStatus({ state: 'ready', goals });
          setOverrides(buildOverrideForm(OVERRIDE_FIELDS, goals));
        }
      })
      .catch((error: unknown) => {
        if (cancelled) {
          return;
        }
        if (error instanceof ApiError && (error.status === 404 || error.status === 409)) {
          const message =
            error.problem?.title ??
            (error.status === 404 ? profileNotFoundMsg : profileMissingInfoMsg);
          setStatus({ state: 'incomplete', message });
        } else {
          const message = error instanceof Error ? error.message : unknownErrorMsg;
          setStatus({ state: 'error', message });
        }
      });

    return () => {
      cancelled = true;
    };
  }, [OVERRIDE_FIELDS, profileNotFoundMsg, profileMissingInfoMsg, unknownErrorMsg]);

  function applyResult(goals: GoalsResponse) {
    setStatus({ state: 'ready', goals });
    setOverrides(buildOverrideForm(OVERRIDE_FIELDS, goals));
  }

  async function submitOverrides(req: SetGoalOverridesRequest) {
    setSaving(true);
    setSaved(false);
    setSubmitError(null);
    setFieldErrors({});
    try {
      const updated = await setGoalOverrides(req);
      applyResult(updated);
      setSaved(true);
    } catch (error: unknown) {
      if (error instanceof ApiError && error.problem?.errors) {
        setFieldErrors(error.problem.errors);
        setSubmitError(error.problem.title ?? t('correctFields'));
      } else {
        const message = error instanceof Error ? error.message : t('saveError');
        setSubmitError(message);
      }
    } finally {
      setSaving(false);
    }
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const req: SetGoalOverridesRequest = {};
    const invalid: Record<string, string[]> = {};
    for (const field of OVERRIDE_FIELDS) {
      const raw = overrides[field.key]?.trim() ?? '';
      if (raw === '') {
        // Blank clears that override, reverting it to the computed value.
        req[field.key] = null;
        continue;
      }
      const parsed = Number(raw);
      if (Number.isNaN(parsed)) {
        // Reject rather than silently clearing the override on bad input.
        invalid[field.key] = [t('invalidNumber')];
        continue;
      }
      req[field.key] = parsed;
    }
    if (Object.keys(invalid).length > 0) {
      setSaved(false);
      setSubmitError(t('correctFields'));
      setFieldErrors(invalid);
      return;
    }
    void submitOverrides(req);
  }

  function handleReset() {
    void submitOverrides({});
  }

  function fieldError(key: string): ReactElement | null {
    const errors = fieldErrors[key];
    if (!errors || errors.length === 0) {
      return null;
    }
    return (
      <p className="field-error" id={`${key}-error`} role="alert">
        {errors.join(' ')}
      </p>
    );
  }

  if (status.state === 'loading') {
    return (
      <section>
        <h1>{t('title')}</h1>
        <p>{t('loading')}</p>
      </section>
    );
  }

  if (status.state === 'incomplete') {
    return (
      <section>
        <h1>{t('title')}</h1>
        <p className="message message-info">{status.message}</p>
        <p>
          {t('incompleteBody')} <Link to="/profile">{t('completeProfileLink')}</Link>
        </p>
      </section>
    );
  }

  if (status.state === 'error') {
    return (
      <section>
        <h1>{t('title')}</h1>
        <p role="alert" className="message message-error">
          {t('loadError', { message: status.message })}
        </p>
      </section>
    );
  }

  const { goals } = status;

  const cards: { id: string; label: string; unit: string; goal: GoalValue }[] = [
    { id: 'calories', label: t('calories'), unit: t('units.kcal'), goal: goals.calories },
    { id: 'protein', label: t('protein'), unit: t('units.g'), goal: goals.proteinGrams },
    { id: 'carbs', label: t('carbs'), unit: t('units.g'), goal: goals.carbohydrateGrams },
    { id: 'fat', label: t('fat'), unit: t('units.g'), goal: goals.fatGrams },
    { id: 'water', label: t('water'), unit: t('units.ml'), goal: goals.waterMl },
  ];

  return (
    <section>
      <h1>{t('title')}</h1>

      <ul className="goal-cards">
        {cards.map((card) => (
          <li key={card.id} className="card goal-card">
            <h2>{card.label}</h2>
            <p className="goal-value">
              {card.goal.value}
              <span className="goal-unit"> {card.unit}</span>
            </p>
            {card.goal.isOverridden && (
              <p className="goal-computed">{t('computed', { value: card.goal.computed })}</p>
            )}
          </li>
        ))}
      </ul>

      {/* Preserve the <strong> structure (and the · separator) so existing
          tests querying /BMR/ and the bold numbers keep matching. */}
      <p className="goal-energy">
        {t('bmrLabel')} <strong>{goals.bmr}</strong> {t('units.kcal')}
        {t('energySeparator')}
        {t('tdeeLabel')} <strong>{goals.tdee}</strong> {t('units.kcal')}
      </p>
      {goals.lastOverriddenAt && (
        <p className="hint">{t('lastOverridden', { date: formatDate(goals.lastOverriddenAt) })}</p>
      )}

      <h2>{t('customiseTitle')}</h2>
      <p className="hint">{t('customiseHint')}</p>
      <form onSubmit={handleSubmit} noValidate>
        <div className="form-grid">
          {OVERRIDE_FIELDS.map((field) => (
            <div className="form-field" key={field.key}>
              <label htmlFor={`override-${field.key}`}>{t(`override.${field.i18nKey}`)}</label>
              <input
                id={`override-${field.key}`}
                name={field.key}
                type="number"
                inputMode="numeric"
                value={overrides[field.key] ?? ''}
                onChange={(e) => setOverrides((prev) => ({ ...prev, [field.key]: e.target.value }))}
                aria-describedby={fieldErrors[field.key]?.length ? `${field.key}-error` : undefined}
                aria-invalid={Boolean(fieldErrors[field.key])}
              />
              {fieldError(field.key)}
            </div>
          ))}
        </div>

        {submitError && (
          <p role="alert" className="message message-error">
            {submitError}
          </p>
        )}
        {saved && !submitError && (
          <p role="status" className="message message-success">
            {t('saved')}
          </p>
        )}

        <div className="form-actions">
          <button type="submit" disabled={saving}>
            {saving ? t('saving') : t('saveButton')}
          </button>
          <button
            type="button"
            className="button-secondary"
            onClick={handleReset}
            disabled={saving}
          >
            {t('resetButton')}
          </button>
        </div>
      </form>
    </section>
  );
}
