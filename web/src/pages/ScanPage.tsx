import { lazy, Suspense, useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { ApiError } from '../api/client';
import {
  lookupBarcode,
  normalizeBarcode,
  FoodServiceUnavailableError,
  type FoodDto,
} from '../api/foods';

// Lazy-loaded so the ZXing decoding library (the largest dependency) is split
// out of the initial bundle and only fetched when the scan page is visited.
const BarcodeScanner = lazy(() => import('../components/BarcodeScanner'));

/**
 * Lookup state machine.
 *
 * - `idle`        — nothing looked up yet
 * - `loading`     — a lookup is in flight (carries the code being resolved)
 * - `found`       — a food matched the barcode
 * - `notFound`    — backend returned 404; offer to create a custom food
 * - `unavailable` — backend returned 503 (upstream Open Food Facts down)
 * - `error`       — any other failure
 */
type LookupState =
  | { state: 'idle' }
  | { state: 'loading'; code: string }
  | { state: 'found'; food: FoodDto }
  | { state: 'notFound'; code: string }
  | { state: 'unavailable'; code: string }
  | { state: 'error'; code: string; message: string };

/**
 * Barcode scan page.
 *
 * Combines a webcam scanner (graceful no-op where unavailable) with an
 * always-present manual-entry fallback. Both feed the same backend lookup
 * (`GET /api/v1/foods/barcode/{code}`) and the same result/empty/error states.
 *
 * The "Add to diary" affordance is a deliberate placeholder: the web food diary
 * (#25) and backend diary (#22) are separate, not-yet-done work. For now it is a
 * clearly-marked TODO no-op so this slice has a clean integration seam.
 */
export default function ScanPage() {
  const { t } = useTranslation('scan');
  const [lookup, setLookup] = useState<LookupState>({ state: 'idle' });
  const [manualCode, setManualCode] = useState('');

  const isLoading = lookup.state === 'loading';

  async function runLookup(rawCode: string) {
    // Normalize once here only to drive the empty-input guard and the displayed
    // code; lookupBarcode is the single source of truth for normalization on
    // the wire, so it receives the raw value.
    const code = normalizeBarcode(rawCode);
    if (code === '') {
      setLookup({
        state: 'error',
        code: rawCode,
        message: t('invalidBarcode'),
      });
      return;
    }

    setLookup({ state: 'loading', code });

    try {
      const food = await lookupBarcode(rawCode);
      if (food === null) {
        setLookup({ state: 'notFound', code });
      } else {
        setLookup({ state: 'found', food });
      }
    } catch (error: unknown) {
      if (error instanceof FoodServiceUnavailableError) {
        setLookup({ state: 'unavailable', code });
      } else {
        const message =
          error instanceof ApiError
            ? t('lookupFailedStatus', { status: error.status })
            : t('lookupFailed');
        setLookup({ state: 'error', code, message });
      }
    }
  }

  function handleManualSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    void runLookup(manualCode);
  }

  function handleScanned(code: string) {
    setManualCode(code);
    void runLookup(code);
  }

  /**
   * Integration seam for #25 (web food diary). For now this is a no-op
   * placeholder so the "Add" affordance exists without implementing diary
   * logging here.
   *
   * TODO(#25): navigate to the food detail / add-to-diary flow once the web
   * food diary (and backend #22) lands.
   */
  function handleAddToDiary(food: FoodDto) {
    // Intentionally a no-op placeholder — see TODO(#25) above. The matched food
    // is referenced (void) to preserve the seam's signature without side effects.
    void food;
  }

  return (
    <section className="scan-page">
      <h1>{t('title')}</h1>
      <p className="hint">{t('hint')}</p>

      <Suspense fallback={<p className="hint">{t('loadingScanner')}</p>}>
        <BarcodeScanner onDetected={handleScanned} disabled={isLoading} />
      </Suspense>

      <form className="manual-entry" onSubmit={handleManualSubmit}>
        <label htmlFor="manual-barcode">{t('manualLabel')}</label>
        <div className="manual-entry-row">
          <input
            id="manual-barcode"
            name="manual-barcode"
            type="text"
            inputMode="numeric"
            autoComplete="off"
            placeholder={t('manualPlaceholder')}
            value={manualCode}
            onChange={(e) => setManualCode(e.target.value)}
          />
          <button type="submit" disabled={isLoading || manualCode.trim() === ''}>
            {isLoading ? t('lookingUp') : t('lookUp')}
          </button>
        </div>
      </form>

      <LookupResult
        lookup={lookup}
        onAddToDiary={handleAddToDiary}
        onRetry={(code) => void runLookup(code)}
      />
    </section>
  );
}

interface LookupResultProps {
  lookup: LookupState;
  onAddToDiary: (food: FoodDto) => void;
  onRetry: (code: string) => void;
}

/** Renders the outcome of the current barcode lookup. */
function LookupResult({ lookup, onAddToDiary, onRetry }: LookupResultProps) {
  const { t } = useTranslation('scan');

  switch (lookup.state) {
    case 'idle':
      return null;

    case 'loading':
      return <p role="status">{t('lookingUpCode', { code: lookup.code })}</p>;

    case 'notFound':
      return (
        <div className="lookup-result lookup-empty" role="status">
          {/* Preserve the text + <strong>{code}</strong> + "." structure so the
              code remains its own queryable element while the surrounding text
              is translated. */}
          <p>
            {t('notFoundPrefix')}
            <strong>{lookup.code}</strong>.
          </p>
          {/* TODO(#25): wire this to the custom-food creation flow. */}
          <button type="button" disabled>
            {t('createCustomFood')}
          </button>
        </div>
      );

    case 'unavailable':
      return (
        <div className="lookup-result" role="alert">
          <p className="message message-error">{t('unavailable')}</p>
          <button type="button" onClick={() => onRetry(lookup.code)}>
            {t('retry')}
          </button>
        </div>
      );

    case 'error':
      return (
        <p role="alert" className="message message-error">
          {lookup.message}
        </p>
      );

    case 'found':
      return <FoodResult food={lookup.food} onAdd={onAddToDiary} />;

    default:
      return null;
  }
}

interface FoodResultProps {
  food: FoodDto;
  onAdd: (food: FoodDto) => void;
}

/** Displays a matched food: name/brand, nutrition per 100 g, and serving sizes. */
function FoodResult({ food, onAdd }: FoodResultProps) {
  const { t } = useTranslation('scan');
  const n = food.nutritionPer100g;

  return (
    <article className="lookup-result food-card" aria-label={t('foodLabel', { name: food.name })}>
      <header className="food-card-header">
        <h2>{food.name}</h2>
        {food.brand && <p className="food-brand">{food.brand}</p>}
        <p className="food-meta">
          {food.barcode && <span>{t('barcodeLabel', { code: food.barcode })}</span>}
          {food.source && <span> · {food.source}</span>}
        </p>
      </header>

      <h3>{t('nutritionTitle')}</h3>
      <ul className="nutrition-list">
        <li>
          <span>{t('energy')}</span>
          <span>
            {n.energyKcal} {t('units.kcal')}
          </span>
        </li>
        <li>
          <span>{t('protein')}</span>
          <span>
            {n.proteinG} {t('units.g')}
          </span>
        </li>
        <li>
          <span>{t('carbohydrate')}</span>
          <span>
            {n.carbohydrateG} {t('units.g')}
          </span>
        </li>
        <li>
          <span>{t('fat')}</span>
          <span>
            {n.fatG} {t('units.g')}
          </span>
        </li>
        {n.fiberG != null && (
          <li>
            <span>{t('fibre')}</span>
            <span>
              {n.fiberG} {t('units.g')}
            </span>
          </li>
        )}
        {n.sugarsG != null && (
          <li>
            <span>{t('sugars')}</span>
            <span>
              {n.sugarsG} {t('units.g')}
            </span>
          </li>
        )}
      </ul>

      {food.servingSizes.length > 0 && (
        <>
          <h3>{t('servingSizesTitle')}</h3>
          <ul className="serving-list">
            {food.servingSizes.map((s) => (
              <li key={`${s.label}-${s.grams}`}>
                {t('servingSize', { label: s.label, grams: s.grams })}
              </li>
            ))}
          </ul>
        </>
      )}

      {/* Integration seam for #25 — disabled placeholder, no diary logging here. */}
      <button type="button" onClick={() => onAdd(food)}>
        {t('addToDiary')}
      </button>
    </article>
  );
}
