import { useTranslation } from 'react-i18next';

/** Props for {@link WaterProgressBar}. */
interface WaterProgressBarProps {
  totalMl: number;
  /** Daily goal; `undefined` renders an indeterminate (no-max) bar. */
  goalMl: number | undefined;
  /** Remaining volume; `undefined` when no goal is known. */
  remainingMl: number | undefined;
}

/**
 * Daily water-intake progress bar.
 *
 * Exposes the `progressbar` role with `aria-valuenow`/`min`/`max` and a
 * human-readable `aria-valuetext` so screen readers announce "750 of 2000 ml".
 * When no goal is known the bar is indeterminate: per ARIA, an indeterminate
 * progressbar omits `aria-valuenow`/`min`/`max` entirely, so the consumed
 * amount is instead carried by the accessible name (`aria-label`). Consumed
 * values at/over the goal are visually flagged.
 */
export default function WaterProgressBar({
  totalMl,
  goalMl,
  remainingMl,
}: WaterProgressBarProps) {
  const { t } = useTranslation('water');

  const hasGoal = goalMl !== undefined && goalMl > 0;
  const atOrOverGoal = hasGoal && totalMl >= (goalMl as number);
  const fillPercent = hasGoal
    ? Math.min(100, (totalMl / (goalMl as number)) * 100)
    : 0;

  // Determinate (goal known): full value range + valuetext. Indeterminate
  // (no goal): omit valuenow/min/max per ARIA, and carry the consumed amount
  // in the accessible name so it is still announced.
  const ariaProps = hasGoal
    ? {
        'aria-label': t('progressLabel'),
        'aria-valuenow': totalMl,
        'aria-valuemin': 0,
        'aria-valuemax': goalMl,
        'aria-valuetext': t('progressValueText', { total: totalMl, goal: goalMl }),
      }
    : {
        'aria-label': t('progressValueTextNoGoal', { total: totalMl }),
      };

  return (
    <div className="card water-progress">
      <div role="progressbar" className="water-progress-bar" {...ariaProps}>
        <div
          className={`water-progress-fill${atOrOverGoal ? ' water-progress-fill-over' : ''}`}
          style={{ width: `${fillPercent}%` }}
        />
      </div>

      <dl className="water-progress-stats">
        <div className="water-progress-stat">
          <dt>{t('consumed')}</dt>
          <dd>
            {totalMl} {t('unitMl')}
          </dd>
        </div>
        {hasGoal && (
          <div className="water-progress-stat">
            <dt>{t('goal')}</dt>
            <dd>
              {goalMl} {t('unitMl')}
            </dd>
          </div>
        )}
        {remainingMl !== undefined && (
          <div className="water-progress-stat">
            <dt>{t('remaining')}</dt>
            <dd>
              {atOrOverGoal ? (
                <span className="water-progress-reached">{t('overGoal')}</span>
              ) : (
                <>
                  {remainingMl} {t('unitMl')}
                </>
              )}
            </dd>
          </div>
        )}
      </dl>
    </div>
  );
}
