import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { getTrends, type TrendsRange, type TrendsResponse } from '../api/trends';
import LineChart from '../components/trends/LineChart';
import WeightChart from '../components/trends/WeightChart';

/** Preset window lengths offered by the range selector, in days. */
const RANGE_OPTIONS: TrendsRange[] = [7, 30, 90];

/** Default window length (in days) shown on first load. */
const DEFAULT_RANGE: TrendsRange = 30;

type TrendsStatus =
  | { state: 'loading' }
  | { state: 'success'; data: TrendsResponse }
  | { state: 'error'; message: string };

/**
 * History/trends page: dense daily calorie/water line charts plus the sparse
 * body-weight chart over a selectable window.
 *
 * Follows the app-wide page state machine (loading / success / error). The
 * selected `range` drives the fetch: changing it re-runs the effect, which
 * cancels any in-flight result to avoid a stale response clobbering a newer one.
 */
export default function TrendsPage() {
  const { t } = useTranslation('trends');

  const [range, setRange] = useState<TrendsRange>(DEFAULT_RANGE);
  const [status, setStatus] = useState<TrendsStatus>({ state: 'loading' });

  useEffect(() => {
    let cancelled = false;
    setStatus({ state: 'loading' });

    getTrends(range)
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
  }, [range, t]);

  return (
    <section className="trends">
      <h1>{t('title')}</h1>

      <RangeSelector selected={range} onSelect={setRange} />

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

      {status.state === 'success' && <TrendsContent data={status.data} />}
    </section>
  );
}

/** Props for {@link RangeSelector}. */
interface RangeSelectorProps {
  selected: TrendsRange;
  onSelect: (range: TrendsRange) => void;
}

/**
 * Segmented control for picking the trends window. Implemented as a labelled
 * group of toggle buttons: the active option carries `aria-pressed` so screen
 * readers announce the current selection.
 */
function RangeSelector({ selected, onSelect }: RangeSelectorProps) {
  const { t } = useTranslation('trends');

  return (
    <div role="group" aria-labelledby="trends-range-label" className="trends-range">
      <span id="trends-range-label" className="sr-only">
        {t('rangeLabel')}
      </span>
      {RANGE_OPTIONS.map((n) => (
        <button
          key={n}
          type="button"
          aria-pressed={selected === n}
          onClick={() => onSelect(n)}
          className={`trends-range-button${selected === n ? ' trends-range-button-active' : ''}`}
        >
          {t('rangeDays', { count: n })}
        </button>
      ))}
    </div>
  );
}

/** Props for {@link TrendsContent}. */
interface TrendsContentProps {
  data: TrendsResponse;
}

/** Renders the five trend charts, each in its own labelled section. */
function TrendsContent({ data }: TrendsContentProps) {
  const { t } = useTranslation('trends');

  return (
    <>
      <section aria-labelledby="trends-calories-heading" className="trends-section card">
        <h2 id="trends-calories-heading">{t('caloriesHeading')}</h2>
        <LineChart
          points={data.caloriesConsumed}
          label={t('caloriesLabel')}
          unit={t('unitKcal')}
          dateHeader={t('srTableDateHeader')}
          valueHeader={t('srTableValueHeader')}
          emptyMessage={t('noDataPeriod')}
          color="#2563eb"
        />
      </section>

      <section aria-labelledby="trends-burned-heading" className="trends-section card">
        <h2 id="trends-burned-heading">{t('burnedHeading')}</h2>
        <LineChart
          points={data.caloriesBurned}
          label={t('burnedLabel')}
          unit={t('unitKcal')}
          dateHeader={t('srTableDateHeader')}
          valueHeader={t('srTableValueHeader')}
          emptyMessage={t('noDataPeriod')}
          color="#dc2626"
        />
      </section>

      <section aria-labelledby="trends-net-heading" className="trends-section card">
        <h2 id="trends-net-heading">{t('netHeading')}</h2>
        <LineChart
          points={data.netCalories}
          label={t('netLabel')}
          unit={t('unitKcal')}
          dateHeader={t('srTableDateHeader')}
          valueHeader={t('srTableValueHeader')}
          emptyMessage={t('noDataPeriod')}
          color="#16a34a"
        />
      </section>

      <section aria-labelledby="trends-water-heading" className="trends-section card">
        <h2 id="trends-water-heading">{t('waterHeading')}</h2>
        <LineChart
          points={data.waterMl}
          label={t('waterLabel')}
          unit={t('unitMl')}
          dateHeader={t('srTableDateHeader')}
          valueHeader={t('srTableValueHeader')}
          emptyMessage={t('noDataPeriod')}
          color="#0891b2"
        />
      </section>

      <section aria-labelledby="trends-weight-heading" className="trends-section card">
        <h2 id="trends-weight-heading">{t('weightHeading')}</h2>
        <WeightChart
          points={data.weight}
          from={data.from}
          to={data.to}
          label={t('weightLabel')}
          dateHeader={t('srTableDateHeader')}
          weightHeader={t('srTableWeightHeader')}
          emptyMessage={t('noWeightData')}
          color="#7c3aed"
        />
      </section>
    </>
  );
}
