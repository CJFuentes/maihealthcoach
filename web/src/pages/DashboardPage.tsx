import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { getDashboard, type DashboardResponse } from '../api/dashboard';
import { getNudge, type NudgeResponse } from '../api/coach';
import MacroProgressBar from '../components/dashboard/MacroProgressBar';

/**
 * Returns today's date as a *local* `YYYY-MM-DD` string.
 *
 * Uses the local calendar fields (not `toISOString()`, which is UTC) so the
 * dashboard fetches the same day the Diary/Water/Exercise pages show — those
 * pages use the identical `formatLocalDate` pattern. Using UTC here would show a
 * different day's snapshot during the local-vs-UTC midnight overlap window.
 */
function todayISO(): string {
  const d = new Date();
  const year = d.getFullYear();
  const month = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

type DashboardStatus =
  | { state: 'loading' }
  | { state: 'success'; data: DashboardResponse }
  | { state: 'error'; message: string };

/** Best-effort coach nudge: starts hidden, only appears on success. */
type NudgeStatus = { state: 'hidden' } | { state: 'shown'; nudge: NudgeResponse };

/**
 * Authenticated landing dashboard: the day's snapshot — calorie/macro progress,
 * water progress, exercise summary, net calories, and the logging streak, plus
 * an optional best-effort coach nudge.
 *
 * Follows the page state machine used across the app (loading / success /
 * error). The nudge is fetched separately and rendered best-effort: any failure
 * leaves it hidden rather than degrading the dashboard.
 */
export default function DashboardPage() {
  const { t } = useTranslation('dashboard');

  const [status, setStatus] = useState<DashboardStatus>({ state: 'loading' });
  const [nudge, setNudge] = useState<NudgeStatus>({ state: 'hidden' });

  useEffect(() => {
    let cancelled = false;
    setStatus({ state: 'loading' });

    getDashboard(todayISO())
      .then((data) => {
        if (!cancelled) {
          setStatus({ state: 'success', data });
        }
      })
      .catch((error: unknown) => {
        if (!cancelled) {
          const message = error instanceof Error ? error.message : t('unknownError');
          setStatus({ state: 'error', message });
        }
      });

    return () => {
      cancelled = true;
    };
  }, [t]);

  useEffect(() => {
    let cancelled = false;

    // Best-effort: a failed nudge must never surface an error to the user, so we
    // swallow the rejection and simply leave the card hidden.
    getNudge()
      .then((data) => {
        if (!cancelled) {
          setNudge({ state: 'shown', nudge: data });
        }
      })
      .catch(() => {
        /* hide silently */
      });

    return () => {
      cancelled = true;
    };
  }, []);

  return (
    <section className="dashboard">
      <h1>{t('title')}</h1>

      {status.state === 'loading' && (
        <p role="status" aria-busy="true">
          {t('loading')}
        </p>
      )}

      {status.state === 'error' && (
        <p role="alert" className="message message-error">
          {t('loadError', { message: status.message })}
        </p>
      )}

      {status.state === 'success' && (
        <DashboardContent data={status.data} nudge={nudge} />
      )}
    </section>
  );
}

/** Props for {@link DashboardContent}. */
interface DashboardContentProps {
  data: DashboardResponse;
  nudge: NudgeStatus;
}

/** Renders the loaded snapshot: each block is its own labelled section. */
function DashboardContent({ data, nudge }: DashboardContentProps) {
  const { t } = useTranslation('dashboard');

  const { calories, water, exercise, streak, netCalories } = data;

  const nothingLogged =
    calories.entryCount === 0 && exercise.entryCount === 0 && water.consumedMl === 0;

  const macros = [
    { key: 'calories', label: t('calories'), unit: t('unitKcal'), summary: calories.calories },
    { key: 'protein', label: t('protein'), unit: t('unitGram'), summary: calories.proteinG },
    { key: 'carbs', label: t('carbs'), unit: t('unitGram'), summary: calories.carbohydrateG },
    { key: 'fat', label: t('fat'), unit: t('unitGram'), summary: calories.fatG },
  ] as const;

  // Water progress: determinate vs indeterminate ARIA, mirroring WaterProgressBar.
  const hasWaterGoal = water.goalsAvailable && water.goalMl !== null && water.goalMl > 0;
  const atOrOverWater = hasWaterGoal && water.consumedMl >= (water.goalMl as number);
  const waterFill = hasWaterGoal
    ? Math.min(100, (water.consumedMl / (water.goalMl as number)) * 100)
    : 0;
  const waterAria = hasWaterGoal
    ? {
        'aria-label': t('waterProgressLabel'),
        'aria-valuenow': water.consumedMl,
        'aria-valuemin': 0,
        'aria-valuemax': water.goalMl as number,
        'aria-valuetext': t('waterValueText', {
          consumed: water.consumedMl,
          goal: water.goalMl,
        }),
      }
    : { 'aria-label': t('waterValueTextNoGoal', { consumed: water.consumedMl }) };

  return (
    <>
      {nothingLogged && <p className="hint dashboard-empty">{t('emptyHint')}</p>}

      <section aria-labelledby="dashboard-calories-heading" className="dashboard-section card">
        <h2 id="dashboard-calories-heading">{t('caloriesHeading')}</h2>
        {calories.entryCount === 0 && <p className="hint">{t('caloriesEmpty')}</p>}
        <div className="dashboard-macros">
          {macros.map((m) => (
            <MacroProgressBar
              key={m.key}
              label={m.label}
              consumed={m.summary.consumed}
              target={m.summary.target}
              remaining={m.summary.remaining}
              percentOfTarget={m.summary.percentOfTarget}
              unit={m.unit}
            />
          ))}
        </div>
      </section>

      <section aria-labelledby="dashboard-water-heading" className="dashboard-section card">
        <h2 id="dashboard-water-heading">{t('waterHeading')}</h2>
        <div role="progressbar" className="water-progress-bar" {...waterAria}>
          <div
            className={`water-progress-fill${atOrOverWater ? ' water-progress-fill-over' : ''}`}
            style={{ width: `${waterFill}%` }}
          />
        </div>
        <dl className="dashboard-stats">
          <div className="dashboard-stat">
            <dt>{t('waterConsumed')}</dt>
            <dd>
              {water.consumedMl} {t('unitMl')}
            </dd>
          </div>
          {hasWaterGoal && (
            <div className="dashboard-stat">
              <dt>{t('waterGoal')}</dt>
              <dd>
                {water.goalMl} {t('unitMl')}
              </dd>
            </div>
          )}
          {water.remainingMl !== null && (
            <div className="dashboard-stat">
              <dt>{t('waterRemaining')}</dt>
              <dd>
                {atOrOverWater ? (
                  <span className="water-progress-reached">{t('waterReached')}</span>
                ) : (
                  <>
                    {water.remainingMl} {t('unitMl')}
                  </>
                )}
              </dd>
            </div>
          )}
        </dl>
      </section>

      <section aria-labelledby="dashboard-exercise-heading" className="dashboard-section card">
        <h2 id="dashboard-exercise-heading">{t('exerciseHeading')}</h2>
        {exercise.entryCount === 0 ? (
          <p className="hint">{t('exerciseEmpty')}</p>
        ) : (
          <dl className="dashboard-stats">
            <div className="dashboard-stat">
              <dt>{t('exerciseBurned')}</dt>
              <dd>
                {exercise.totalCaloriesBurned} {t('unitKcal')}
              </dd>
            </div>
            <div className="dashboard-stat">
              <dt>{t('exerciseEntries')}</dt>
              <dd>{exercise.entryCount}</dd>
            </div>
          </dl>
        )}
      </section>

      {netCalories !== null && (
        <section aria-labelledby="dashboard-net-heading" className="dashboard-section card">
          <h2 id="dashboard-net-heading">{t('netHeading')}</h2>
          <p className="dashboard-net-value">
            {netCalories} {t('unitKcal')}
          </p>
          <p className="hint">{t('netHint')}</p>
        </section>
      )}

      <section aria-labelledby="dashboard-streak-heading" className="dashboard-section card">
        <h2 id="dashboard-streak-heading">{t('streakHeading')}</h2>
        <p className="dashboard-streak-badge">
          {t('streakCurrent', { count: streak.currentStreak })}
        </p>
        <dl className="dashboard-stats">
          <div className="dashboard-stat">
            <dt>{t('streakLongest')}</dt>
            <dd>{t('streakDays', { count: streak.longestStreak })}</dd>
          </div>
          {streak.caloriesAdherence7d !== null && (
            <div className="dashboard-stat">
              <dt>{t('streakCaloriesAdherence')}</dt>
              <dd>{streak.caloriesAdherence7d}%</dd>
            </div>
          )}
          {streak.waterAdherence7d !== null && (
            <div className="dashboard-stat">
              <dt>{t('streakWaterAdherence')}</dt>
              <dd>{streak.waterAdherence7d}%</dd>
            </div>
          )}
        </dl>
      </section>

      {nudge.state === 'shown' && (
        <section
          aria-labelledby="dashboard-nudge-heading"
          className="dashboard-section card dashboard-nudge"
        >
          <h2 id="dashboard-nudge-heading">{t('nudgeHeading')}</h2>
          <p className="dashboard-nudge-message">{nudge.nudge.message}</p>
          {nudge.nudge.disclaimer && (
            <p className="hint dashboard-nudge-disclaimer">{nudge.nudge.disclaimer}</p>
          )}
        </section>
      )}
    </>
  );
}
