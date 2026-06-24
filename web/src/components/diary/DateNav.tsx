/** Props for {@link DateNav}. */
interface DateNavProps {
  date: string;
  onPrev: () => void;
  onNext: () => void;
  onToday: () => void;
}

/**
 * Parses an ISO `YYYY-MM-DD` string into a local-time Date.
 *
 * Avoids `new Date(string)`, which interprets a date-only string as UTC and
 * can shift the displayed day across timezone boundaries.
 */
function parseLocalDate(s: string): Date {
  const [y, m, d] = s.split('-').map(Number);
  return new Date(y, m - 1, d);
}

const dateFormatter = new Intl.DateTimeFormat('en-GB', {
  weekday: 'long',
  year: 'numeric',
  month: 'long',
  day: 'numeric',
});

/**
 * Date navigation bar for the diary: previous/next day, a formatted label, and
 * a jump-to-today control.
 */
export default function DateNav({ date, onPrev, onNext, onToday }: DateNavProps) {
  return (
    <nav aria-label="Date navigation" className="diary-date-nav">
      <button type="button" className="button-secondary" aria-label="Previous day" onClick={onPrev}>
        ‹
      </button>
      <time dateTime={date} className="diary-date-label">
        {dateFormatter.format(parseLocalDate(date))}
      </time>
      <button type="button" className="button-secondary" aria-label="Next day" onClick={onNext}>
        ›
      </button>
      <button type="button" className="button-secondary" onClick={onToday}>
        Today
      </button>
    </nav>
  );
}
