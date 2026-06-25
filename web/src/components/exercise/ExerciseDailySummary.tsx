import { useTranslation } from 'react-i18next';

/** Props for {@link ExerciseDailySummary}. */
interface ExerciseDailySummaryProps {
  totalCaloriesBurned: number;
  entryCount: number;
}

/**
 * Daily exercise summary: total calories burned and the number of activities
 * logged. There is no progress bar because exercise has no goal to track
 * against (unlike the water feature's hydration goal).
 */
export default function ExerciseDailySummary({
  totalCaloriesBurned,
  entryCount,
}: ExerciseDailySummaryProps) {
  const { t } = useTranslation('exercise');

  return (
    <div className="card exercise-daily-summary">
      <dl className="exercise-summary-stats">
        <div className="exercise-summary-stat">
          <dt>{t('totalBurned')}</dt>
          <dd>
            {Math.round(totalCaloriesBurned)} {t('unitKcal')}
          </dd>
        </div>
        <div className="exercise-summary-stat">
          <dt>{t('activitiesLogged')}</dt>
          <dd>{entryCount}</dd>
        </div>
      </dl>
    </div>
  );
}
