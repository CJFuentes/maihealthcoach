import { useEffect, useState, type FormEvent, type ReactElement } from 'react';
import { useTranslation } from 'react-i18next';
import { ApiError } from '../api/client';
import {
  getProfile,
  updateProfile,
  type ActivityLevel,
  type BiologicalSex,
  type DietType,
  type PrimaryGoal,
  type ProfileResponse,
  type UnitsPreference,
  type UpdateProfileRequest,
} from '../api/profile';

type Status =
  | { state: 'loading' }
  | { state: 'ready'; profile: ProfileResponse | null }
  | { state: 'error'; message: string };

/** Editable form values. All numeric inputs are kept as strings (raw input). */
interface FormState {
  height: string;
  weight: string;
  dateOfBirth: string;
  biologicalSex: BiologicalSex | '';
  activityLevel: ActivityLevel | '';
  primaryGoal: PrimaryGoal | '';
  units: UnitsPreference;
  dietType: DietType | '';
  allergies: string;
}

// Enum option keys, in display order. The display label for each is resolved
// via t(`activity.${key}`) etc.; the <option value> stays the raw enum key.
const ACTIVITY_LEVELS: ActivityLevel[] = [
  'Sedentary',
  'LightlyActive',
  'ModeratelyActive',
  'VeryActive',
  'ExtraActive',
];

const PRIMARY_GOALS: PrimaryGoal[] = ['Lose', 'Maintain', 'Gain'];

const DIET_TYPES: DietType[] = ['None', 'Vegetarian', 'Vegan', 'Pescatarian', 'Keto', 'Paleo'];

const CM_PER_INCH = 2.54;
const LB_PER_KG = 2.20462;

/** Rounds to 1 decimal place, returning '' for null/undefined. */
function round1(value: number): string {
  return String(Math.round(value * 10) / 10);
}

function cmToDisplay(cm: number | null, units: UnitsPreference): string {
  if (cm == null) {
    return '';
  }
  return units === 'Imperial' ? round1(cm / CM_PER_INCH) : round1(cm);
}

function kgToDisplay(kg: number | null, units: UnitsPreference): string {
  if (kg == null) {
    return '';
  }
  return units === 'Imperial' ? round1(kg * LB_PER_KG) : round1(kg);
}

/** Re-renders a numeric string into the target unit, preserving an empty field. */
function reExpressHeight(value: string, from: UnitsPreference, to: UnitsPreference): string {
  if (value.trim() === '' || from === to) {
    return value;
  }
  const n = Number(value);
  if (Number.isNaN(n)) {
    return value;
  }
  const cm = from === 'Imperial' ? n * CM_PER_INCH : n;
  return cmToDisplay(cm, to);
}

function reExpressWeight(value: string, from: UnitsPreference, to: UnitsPreference): string {
  if (value.trim() === '' || from === to) {
    return value;
  }
  const n = Number(value);
  if (Number.isNaN(n)) {
    return value;
  }
  const kg = from === 'Imperial' ? n / LB_PER_KG : n;
  return kgToDisplay(kg, to);
}

/** Builds the PUT payload, omitting empty optionals but always sending units. */
function buildPayload(state: FormState): UpdateProfileRequest {
  const payload: UpdateProfileRequest = { units: state.units };

  if (state.height.trim() !== '' && !Number.isNaN(Number(state.height))) {
    const n = Number(state.height);
    payload.heightCm = state.units === 'Imperial' ? n * CM_PER_INCH : n;
  }
  if (state.weight.trim() !== '' && !Number.isNaN(Number(state.weight))) {
    const n = Number(state.weight);
    payload.weightKg = state.units === 'Imperial' ? n / LB_PER_KG : n;
  }
  if (state.dateOfBirth) {
    payload.dateOfBirth = state.dateOfBirth;
  }
  if (state.biologicalSex) {
    payload.biologicalSex = state.biologicalSex;
  }
  if (state.activityLevel) {
    payload.activityLevel = state.activityLevel;
  }
  if (state.primaryGoal) {
    payload.primaryGoal = state.primaryGoal;
  }
  if (state.dietType) {
    payload.dietType = state.dietType;
  }
  if (state.allergies.trim() !== '') {
    payload.allergies = state.allergies;
  }

  return payload;
}

function buildFormState(profile: ProfileResponse | null): FormState {
  const units: UnitsPreference = profile?.units ?? 'Metric';
  return {
    height: cmToDisplay(profile?.heightCm ?? null, units),
    weight: kgToDisplay(profile?.latestWeightKg ?? null, units),
    dateOfBirth: profile?.dateOfBirth ?? '',
    biologicalSex: profile?.biologicalSex ?? '',
    activityLevel: profile?.activityLevel ?? '',
    primaryGoal: profile?.primaryGoal ?? '',
    units,
    dietType: profile?.dietType ?? '',
    allergies: profile?.allergies ?? '',
  };
}

export default function ProfilePage() {
  const { t } = useTranslation('profile');
  const [status, setStatus] = useState<Status>({ state: 'loading' });
  const [form, setForm] = useState<FormState>(() => buildFormState(null));
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({});
  const [submitError, setSubmitError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    setStatus({ state: 'loading' });

    getProfile()
      .then((profile) => {
        if (!cancelled) {
          setStatus({ state: 'ready', profile });
          setForm(buildFormState(profile));
        }
      })
      .catch((error: unknown) => {
        if (!cancelled) {
          const message = error instanceof Error ? error.message : 'Unknown error';
          setStatus({ state: 'error', message });
        }
      });

    return () => {
      cancelled = true;
    };
  }, []);

  const isNewProfile = status.state === 'ready' && status.profile === null;

  const heightLabel = form.units === 'Imperial' ? t('heightIn') : t('heightCm');
  const weightLabel = form.units === 'Imperial' ? t('weightLb') : t('weightKg');

  function handleUnitsChange(next: UnitsPreference) {
    setForm((prev) => ({
      ...prev,
      units: next,
      height: reExpressHeight(prev.height, prev.units, next),
      weight: reExpressWeight(prev.weight, prev.units, next),
    }));
  }

  function update<K extends keyof FormState>(key: K, value: FormState[K]) {
    setForm((prev) => ({ ...prev, [key]: value }));
    setSaved(false);
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSaving(true);
    setSaved(false);
    setSubmitError(null);
    setFieldErrors({});

    try {
      const updated = await updateProfile(buildPayload(form));
      setStatus({ state: 'ready', profile: updated });
      setForm(buildFormState(updated));
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

  /** Renders the inline error message for a backend field key, if present. */
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

  function describedBy(key: string): string | undefined {
    return fieldErrors[key]?.length ? `${key}-error` : undefined;
  }

  if (status.state === 'loading') {
    return (
      <section>
        <h1>{t('title')}</h1>
        <p>{t('loading')}</p>
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

  return (
    <section>
      <h1>{t('title')}</h1>
      {isNewProfile && <p className="hint">{t('newProfileHint')}</p>}

      <form onSubmit={handleSubmit} noValidate>
        <fieldset>
          <legend>{t('unitsLegend')}</legend>
          <div className="units-toggle" role="radiogroup" aria-label={t('measurementUnits')}>
            <label>
              <input
                type="radio"
                name="units"
                value="Metric"
                checked={form.units === 'Metric'}
                onChange={() => handleUnitsChange('Metric')}
              />
              {t('unitsMetric')}
            </label>
            <label>
              <input
                type="radio"
                name="units"
                value="Imperial"
                checked={form.units === 'Imperial'}
                onChange={() => handleUnitsChange('Imperial')}
              />
              {t('unitsImperial')}
            </label>
          </div>
        </fieldset>

        <div className="form-grid">
          <div className="form-field">
            <label htmlFor="height">{heightLabel}</label>
            <input
              id="height"
              name="height"
              type="number"
              inputMode="decimal"
              step="0.1"
              value={form.height}
              onChange={(e) => update('height', e.target.value)}
              aria-describedby={describedBy('heightCm')}
              aria-invalid={Boolean(fieldErrors.heightCm)}
            />
            {fieldError('heightCm')}
          </div>

          <div className="form-field">
            <label htmlFor="weight">{weightLabel}</label>
            <input
              id="weight"
              name="weight"
              type="number"
              inputMode="decimal"
              step="0.1"
              value={form.weight}
              onChange={(e) => update('weight', e.target.value)}
              aria-describedby={describedBy('weightKg')}
              aria-invalid={Boolean(fieldErrors.weightKg)}
            />
            {fieldError('weightKg')}
          </div>

          <div className="form-field">
            <label htmlFor="dateOfBirth">{t('dateOfBirth')}</label>
            <input
              id="dateOfBirth"
              name="dateOfBirth"
              type="date"
              value={form.dateOfBirth}
              onChange={(e) => update('dateOfBirth', e.target.value)}
              aria-describedby={describedBy('dateOfBirth')}
              aria-invalid={Boolean(fieldErrors.dateOfBirth)}
            />
            {fieldError('dateOfBirth')}
          </div>

          <div className="form-field">
            <label htmlFor="biologicalSex">{t('biologicalSex')}</label>
            <select
              id="biologicalSex"
              name="biologicalSex"
              value={form.biologicalSex}
              onChange={(e) => update('biologicalSex', e.target.value as BiologicalSex | '')}
              aria-describedby={describedBy('biologicalSex')}
              aria-invalid={Boolean(fieldErrors.biologicalSex)}
            >
              <option value="">{t('selectPlaceholder')}</option>
              <option value="Male">{t('sex.Male')}</option>
              <option value="Female">{t('sex.Female')}</option>
            </select>
            {fieldError('biologicalSex')}
          </div>

          <div className="form-field">
            <label htmlFor="activityLevel">{t('activityLevel')}</label>
            <select
              id="activityLevel"
              name="activityLevel"
              value={form.activityLevel}
              onChange={(e) => update('activityLevel', e.target.value as ActivityLevel | '')}
              aria-describedby={describedBy('activityLevel')}
              aria-invalid={Boolean(fieldErrors.activityLevel)}
            >
              <option value="">{t('selectPlaceholder')}</option>
              {ACTIVITY_LEVELS.map((level) => (
                <option key={level} value={level}>
                  {t(`activity.${level}`)}
                </option>
              ))}
            </select>
            {fieldError('activityLevel')}
          </div>

          <div className="form-field">
            <label htmlFor="primaryGoal">{t('primaryGoal')}</label>
            <select
              id="primaryGoal"
              name="primaryGoal"
              value={form.primaryGoal}
              onChange={(e) => update('primaryGoal', e.target.value as PrimaryGoal | '')}
              aria-describedby={describedBy('primaryGoal')}
              aria-invalid={Boolean(fieldErrors.primaryGoal)}
            >
              <option value="">{t('selectPlaceholder')}</option>
              {PRIMARY_GOALS.map((goal) => (
                <option key={goal} value={goal}>
                  {t(`goal.${goal}`)}
                </option>
              ))}
            </select>
            {fieldError('primaryGoal')}
          </div>

          <div className="form-field">
            <label htmlFor="dietType">{t('dietType')}</label>
            <select
              id="dietType"
              name="dietType"
              value={form.dietType}
              onChange={(e) => update('dietType', e.target.value as DietType | '')}
              aria-describedby={describedBy('dietType')}
              aria-invalid={Boolean(fieldErrors.dietType)}
            >
              <option value="">{t('selectPlaceholder')}</option>
              {DIET_TYPES.map((diet) => (
                <option key={diet} value={diet}>
                  {t(`diet.${diet}`)}
                </option>
              ))}
            </select>
            {fieldError('dietType')}
          </div>

          <div className="form-field form-field-wide">
            <label htmlFor="allergies">{t('allergies')}</label>
            <textarea
              id="allergies"
              name="allergies"
              rows={3}
              value={form.allergies}
              onChange={(e) => update('allergies', e.target.value)}
              placeholder={t('allergiesPlaceholder')}
              aria-describedby={describedBy('allergies')}
              aria-invalid={Boolean(fieldErrors.allergies)}
            />
            {fieldError('allergies')}
          </div>
        </div>

        {submitError && (
          <p role="alert" className="message message-error">
            {submitError}
          </p>
        )}
        {saved && (
          <p role="status" className="message message-success">
            {t('saved')}
          </p>
        )}

        <button type="submit" disabled={saving}>
          {saving ? t('saving') : t('saveButton')}
        </button>
      </form>
    </section>
  );
}
