import { useEffect, useState, type FormEvent, type ReactElement } from 'react';
import { Link } from 'react-router-dom';
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

/** Override form rows: maps a request key to its label/unit and source goal. */
interface OverrideField {
  key: keyof SetGoalOverridesRequest;
  label: string;
  unit: string;
  goal: (g: GoalsResponse) => GoalValue;
}

const OVERRIDE_FIELDS: OverrideField[] = [
  { key: 'caloriesKcal', label: 'Calories', unit: 'kcal', goal: (g) => g.calories },
  { key: 'proteinGrams', label: 'Protein', unit: 'g', goal: (g) => g.proteinGrams },
  { key: 'carbohydrateGrams', label: 'Carbs', unit: 'g', goal: (g) => g.carbohydrateGrams },
  { key: 'fatGrams', label: 'Fat', unit: 'g', goal: (g) => g.fatGrams },
  { key: 'waterMl', label: 'Water', unit: 'ml', goal: (g) => g.waterMl },
];

function buildOverrideForm(goals: GoalsResponse): Record<string, string> {
  return Object.fromEntries(
    OVERRIDE_FIELDS.map((f) => [f.key, String(f.goal(goals).value)]),
  ) as Record<string, string>;
}

function formatDate(iso: string): string {
  const date = new Date(iso);
  return Number.isNaN(date.getTime()) ? iso : date.toLocaleDateString();
}

export default function GoalsPage() {
  const [status, setStatus] = useState<Status>({ state: 'loading' });
  const [overrides, setOverrides] = useState<Record<string, string>>({});
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({});
  const [submitError, setSubmitError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    setStatus({ state: 'loading' });

    getGoals()
      .then((goals) => {
        if (!cancelled) {
          setStatus({ state: 'ready', goals });
          setOverrides(buildOverrideForm(goals));
        }
      })
      .catch((error: unknown) => {
        if (cancelled) {
          return;
        }
        if (error instanceof ApiError && (error.status === 404 || error.status === 409)) {
          const message =
            error.problem?.title ??
            (error.status === 404
              ? 'Profile not found.'
              : 'Your profile is missing some required information.');
          setStatus({ state: 'incomplete', message });
        } else {
          const message = error instanceof Error ? error.message : 'Unknown error';
          setStatus({ state: 'error', message });
        }
      });

    return () => {
      cancelled = true;
    };
  }, []);

  function applyResult(goals: GoalsResponse) {
    setStatus({ state: 'ready', goals });
    setOverrides(buildOverrideForm(goals));
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
        setSubmitError(error.problem.title ?? 'Please correct the highlighted fields.');
      } else {
        const message = error instanceof Error ? error.message : 'Could not save overrides.';
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
        invalid[field.key] = ['Enter a number, or leave blank to use the computed value.'];
        continue;
      }
      req[field.key] = parsed;
    }
    if (Object.keys(invalid).length > 0) {
      setSaved(false);
      setSubmitError('Please correct the highlighted fields.');
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
        <h1>Goals</h1>
        <p>Loading your goals…</p>
      </section>
    );
  }

  if (status.state === 'incomplete') {
    return (
      <section>
        <h1>Goals</h1>
        <p className="message message-info">{status.message}</p>
        <p>
          Complete your profile to see your personalised goals.{' '}
          <Link to="/profile">Complete your profile</Link>
        </p>
      </section>
    );
  }

  if (status.state === 'error') {
    return (
      <section>
        <h1>Goals</h1>
        <p role="alert" className="message message-error">
          Could not load your goals — {status.message}
        </p>
      </section>
    );
  }

  const { goals } = status;

  const cards: { label: string; unit: string; goal: GoalValue }[] = [
    { label: 'Calories', unit: 'kcal', goal: goals.calories },
    { label: 'Protein', unit: 'g', goal: goals.proteinGrams },
    { label: 'Carbs', unit: 'g', goal: goals.carbohydrateGrams },
    { label: 'Fat', unit: 'g', goal: goals.fatGrams },
    { label: 'Water', unit: 'ml', goal: goals.waterMl },
  ];

  return (
    <section>
      <h1>Goals</h1>

      <ul className="goal-cards">
        {cards.map((card) => (
          <li key={card.label} className="card goal-card">
            <h2>{card.label}</h2>
            <p className="goal-value">
              {card.goal.value}
              <span className="goal-unit"> {card.unit}</span>
            </p>
            {card.goal.isOverridden && (
              <p className="goal-computed">(computed: {card.goal.computed})</p>
            )}
          </li>
        ))}
      </ul>

      <p className="goal-energy">
        BMR <strong>{goals.bmr}</strong> kcal · TDEE <strong>{goals.tdee}</strong> kcal
      </p>
      {goals.lastOverriddenAt && (
        <p className="hint">Last overridden {formatDate(goals.lastOverriddenAt)}</p>
      )}

      <h2>Customise your targets</h2>
      <p className="hint">Leave a field blank to use the computed value for that target.</p>
      <form onSubmit={handleSubmit} noValidate>
        <div className="form-grid">
          {OVERRIDE_FIELDS.map((field) => (
            <div className="form-field" key={field.key}>
              <label htmlFor={`override-${field.key}`}>
                {field.label} ({field.unit})
              </label>
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
            Overrides saved.
          </p>
        )}

        <div className="form-actions">
          <button type="submit" disabled={saving}>
            {saving ? 'Saving…' : 'Save overrides'}
          </button>
          <button
            type="button"
            className="button-secondary"
            onClick={handleReset}
            disabled={saving}
          >
            Reset to computed
          </button>
        </div>
      </form>
    </section>
  );
}
