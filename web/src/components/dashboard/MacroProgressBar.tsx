import { useTranslation } from 'react-i18next';

/** Props for {@link MacroProgressBar}. */
interface MacroProgressBarProps {
  /** Accessible/visible name for this nutrient (e.g. "Calories", "Protein"). */
  label: string;
  /** Amount consumed so far, in `unit`. */
  consumed: number;
  /** Daily target; `null` renders an indeterminate (no-max) bar. */
  target: number | null;
  /** Remaining toward the target; `null` when no target is known. */
  remaining: number | null;
  /** Consumed as a percentage of target; `null` when no target is known. */
  percentOfTarget: number | null;
  /** Unit suffix for the figures (e.g. "kcal", "g"). */
  unit: string;
}

/**
 * Accessible progress bar for one nutrient (calories or a macro).
 *
 * Mirrors {@link WaterProgressBar}: exposes the `progressbar` role with
 * `aria-valuenow`/`min`/`max` and a human-readable `aria-valuetext` when a
 * target is known (determinate). When no target is set the bar is indeterminate:
 * per ARIA it omits `aria-valuenow`/`min`/`max`, so the consumed amount is
 * carried by the accessible name (`aria-label`) instead. Consumed values at or
 * over target are visually flagged.
 */
export default function MacroProgressBar({
  label,
  consumed,
  target,
  remaining,
  percentOfTarget,
  unit,
}: MacroProgressBarProps) {
  const { t } = useTranslation('dashboard');

  const hasTarget = target !== null && target > 0;
  const atOrOverTarget = hasTarget && consumed >= (target as number);
  // Prefer the server-supplied percentage; fall back to a client computation so
  // the fill still reflects progress if the backend omits it.
  const rawPercent = hasTarget
    ? (percentOfTarget ?? (consumed / (target as number)) * 100)
    : 0;
  const fillPercent = Math.min(100, Math.max(0, rawPercent));

  // Determinate (target known): full value range + valuetext. Indeterminate
  // (no target): omit valuenow/min/max per ARIA, and carry the consumed amount
  // in the accessible name so it is still announced.
  const ariaProps = hasTarget
    ? {
        'aria-label': label,
        'aria-valuenow': consumed,
        'aria-valuemin': 0,
        'aria-valuemax': target as number,
        'aria-valuetext': t('macroValueText', {
          consumed,
          target,
          unit,
          label,
        }),
      }
    : {
        'aria-label': t('macroValueTextNoTarget', { consumed, unit, label }),
      };

  return (
    <div className="macro-progress">
      <div className="macro-progress-head">
        <span className="macro-progress-label">{label}</span>
        <span className="macro-progress-figures">
          <span
            className={`macro-progress-consumed${atOrOverTarget ? ' macro-progress-consumed-over' : ''}`}
          >
            {Math.round(consumed)}
          </span>
          {hasTarget && (
            <span className="macro-progress-target">
              {' / '}
              {Math.round(target as number)}
            </span>
          )}{' '}
          <span className="macro-progress-unit">{unit}</span>
        </span>
      </div>

      <div role="progressbar" className="macro-progress-bar" {...ariaProps}>
        <div
          className={`macro-progress-fill${atOrOverTarget ? ' macro-progress-fill-over' : ''}`}
          style={{ width: `${fillPercent}%` }}
        />
      </div>

      {remaining !== null && (
        <p className="macro-progress-remaining">
          {atOrOverTarget
            ? t('macroOverTarget')
            : t('macroRemaining', { remaining: Math.round(remaining), unit })}
        </p>
      )}
    </div>
  );
}
