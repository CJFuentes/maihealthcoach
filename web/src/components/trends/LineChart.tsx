import type { DailyPoint } from '../../api/trends';

/** Props for {@link LineChart}. */
interface LineChartProps {
  /** Dense daily series to plot (one point per day, zero-filled). */
  points: DailyPoint[];
  /** Accessible/visible name for the chart (e.g. "Daily calories consumed (kcal)"). */
  label: string;
  /** Unit suffix for the values (e.g. "kcal", "ml"); used by axis ticks. */
  unit: string;
  /** Column header for the date column of the screen-reader data table. */
  dateHeader: string;
  /** Column header for the value column of the screen-reader data table. */
  valueHeader: string;
  /** Message shown when there is nothing meaningful to plot. */
  emptyMessage: string;
  /** Stroke/fill colour for the plotted line and points. */
  color?: string;
}

// SVG layout constants. The viewBox is a fixed 400×120 canvas; the plot area is
// inset by the paddings so axis ticks/labels have room without being clipped.
const PADDING_X = 30;
const PADDING_Y = 10;
const CHART_W = 340;
const CHART_H = 100;

/**
 * Accessible inline-SVG line chart for a dense daily series.
 *
 * The SVG itself is purely visual: it carries `role="img"` with an `aria-label`,
 * and every decorative element inside (axis ticks, the polyline, the per-point
 * circles) is `aria-hidden`. The real data is exposed to assistive tech through
 * a visually hidden (`.sr-only`) data table, so screen-reader users get the
 * exact figures rather than an unreadable graphic.
 *
 * Renders the `emptyMessage` (no SVG) when the series is empty or entirely zero.
 */
export default function LineChart({
  points,
  label,
  unit,
  dateHeader,
  valueHeader,
  emptyMessage,
  color = '#2563eb',
}: LineChartProps) {
  if (points.length === 0 || points.every((p) => p.value === 0)) {
    return <p className="hint">{emptyMessage}</p>;
  }

  const values = points.map((p) => p.value);
  const min = Math.min(...values);
  const max = Math.max(...values);
  // Guard against a divide-by-zero when every value is identical: treat the
  // range as 1 so the line renders flat at ~50% height instead of NaN.
  const range = max === min ? 1 : max - min;

  // With a single point there is no segment to span; clamp the step divisor to 1.
  const xStep = CHART_W / Math.max(points.length - 1, 1);

  const coords = points.map((p, i) => {
    const x = PADDING_X + i * xStep;
    const y = PADDING_Y + CHART_H - ((p.value - min) / range) * CHART_H;
    return { x, y };
  });

  const polylinePoints = coords.map((c) => `${c.x},${c.y}`).join(' ');

  const firstDate = points[0].date;
  const lastDate = points[points.length - 1].date;

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

        {/* The plotted series. */}
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

        {/* Min/max value ticks on the y-axis. */}
        <text aria-hidden="true" className="trends-chart-tick" x={PADDING_X - 2} y={PADDING_Y + 4} textAnchor="end">
          {max} {unit}
        </text>
        <text aria-hidden="true" className="trends-chart-tick" x={PADDING_X - 2} y={PADDING_Y + CHART_H} textAnchor="end">
          {min} {unit}
        </text>

        {/* First/last date ticks on the x-axis. */}
        <text aria-hidden="true" className="trends-chart-tick" x={PADDING_X} y={PADDING_Y + CHART_H + 12} textAnchor="start">
          {firstDate}
        </text>
        <text aria-hidden="true" className="trends-chart-tick" x={PADDING_X + CHART_W} y={PADDING_Y + CHART_H + 12} textAnchor="end">
          {lastDate}
        </text>
      </svg>

      <table className="sr-only">
        <caption>{label}</caption>
        <thead>
          <tr>
            <th scope="col">{dateHeader}</th>
            <th scope="col">{valueHeader}</th>
          </tr>
        </thead>
        <tbody>
          {points.map((p) => (
            <tr key={p.date}>
              <td>{p.date}</td>
              <td>{p.value}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </figure>
  );
}
