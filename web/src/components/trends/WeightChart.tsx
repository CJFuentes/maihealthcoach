import type { WeightPoint } from '../../api/trends';

/** Props for {@link WeightChart}. */
interface WeightChartProps {
  /** Sparse weight series — measured days only, positioned by date. */
  points: WeightPoint[];
  /** Inclusive start of the window (ISO `YYYY-MM-DD`); anchors the x-axis. */
  from: string;
  /** Inclusive end of the window (ISO `YYYY-MM-DD`); anchors the x-axis. */
  to: string;
  /** Accessible/visible name for the chart. */
  label: string;
  /** Column header for the date column of the screen-reader data table. */
  dateHeader: string;
  /** Column header for the weight column of the screen-reader data table. */
  weightHeader: string;
  /** Message shown when there are no measurements in the window. */
  emptyMessage: string;
  /** Stroke/fill colour for the plotted line and points. */
  color?: string;
}

// SVG layout constants — matches {@link LineChart} so the two charts align.
const PADDING_X = 30;
const PADDING_Y = 10;
const CHART_W = 340;
const CHART_H = 100;

/** Whole calendar days from `a` to `b` (both ISO dates), rounded to absorb DST. */
function daysBetween(a: string, b: string): number {
  return Math.round((Date.parse(b) - Date.parse(a)) / 86400000);
}

/**
 * Accessible inline-SVG chart for the sparse body-weight series.
 *
 * Mirrors {@link LineChart}'s structure (decorative SVG + visually hidden data
 * table) but positions each point by its DATE rather than its array index:
 * because weigh-ins are sparse, x is derived from the day offset within
 * `[from, to]`, so gaps between measurements render as gaps — the polyline
 * connects measured points across them without inventing phantom data.
 *
 * Renders the `emptyMessage` (no SVG) when there are no measurements.
 */
export default function WeightChart({
  points,
  from,
  to,
  label,
  dateHeader,
  weightHeader,
  emptyMessage,
  color = '#7c3aed',
}: WeightChartProps) {
  if (points.length === 0) {
    return <p className="hint">{emptyMessage}</p>;
  }

  const weights = points.map((p) => p.weightKg);
  const min = Math.min(...weights);
  const max = Math.max(...weights);
  // Guard divide-by-zero when all measurements are equal (e.g. a single point).
  const range = max === min ? 1 : max - min;

  // Clamp the span to 1 day so a zero-length window can't divide by zero.
  const totalDays = Math.max(daysBetween(from, to), 1);

  const coords = points.map((p) => {
    const dayOffset = daysBetween(from, p.date);
    const x = PADDING_X + (dayOffset / totalDays) * CHART_W;
    const y = PADDING_Y + CHART_H - ((p.weightKg - min) / range) * CHART_H;
    return { x, y };
  });

  const polylinePoints = coords.map((c) => `${c.x},${c.y}`).join(' ');

  return (
    <figure className="trends-chart">
      <figcaption>{label}</figcaption>

      <svg
        role="img"
        aria-label={label}
        viewBox="0 0 400 120"
        className="trends-chart-svg"
        preserveAspectRatio="xMidYMid meet"
      >
        {/* Axis baseline + frame — decorative; the table is the data source. */}
        <polyline
          aria-hidden="true"
          className="trends-chart-axis"
          points={`${PADDING_X},${PADDING_Y} ${PADDING_X},${PADDING_Y + CHART_H} ${PADDING_X + CHART_W},${PADDING_Y + CHART_H}`}
          fill="none"
        />

        {/* The plotted series, connecting measured points across gaps. */}
        <polyline
          aria-hidden="true"
          className="trends-chart-line"
          points={polylinePoints}
          fill="none"
          stroke={color}
        />
        {coords.map((c, i) => (
          <circle
            key={points[i].date}
            aria-hidden="true"
            className="trends-chart-point"
            cx={c.x}
            cy={c.y}
            r={2}
            fill={color}
          />
        ))}

        {/* Min/max weight ticks on the y-axis. */}
        <text aria-hidden="true" className="trends-chart-tick" x={PADDING_X - 2} y={PADDING_Y + 4} textAnchor="end">
          {max}
        </text>
        <text aria-hidden="true" className="trends-chart-tick" x={PADDING_X - 2} y={PADDING_Y + CHART_H} textAnchor="end">
          {min}
        </text>

        {/* Window-bound date ticks on the x-axis. */}
        <text aria-hidden="true" className="trends-chart-tick" x={PADDING_X} y={PADDING_Y + CHART_H + 12} textAnchor="start">
          {from}
        </text>
        <text aria-hidden="true" className="trends-chart-tick" x={PADDING_X + CHART_W} y={PADDING_Y + CHART_H + 12} textAnchor="end">
          {to}
        </text>
      </svg>

      <table className="sr-only">
        <caption>{label}</caption>
        <thead>
          <tr>
            <th scope="col">{dateHeader}</th>
            <th scope="col">{weightHeader}</th>
          </tr>
        </thead>
        <tbody>
          {points.map((p) => (
            <tr key={p.date}>
              <td>{p.date}</td>
              <td>{p.weightKg}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </figure>
  );
}
