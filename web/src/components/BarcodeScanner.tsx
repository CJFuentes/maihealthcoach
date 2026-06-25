import { useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { useBarcodeScanner } from '../hooks/useBarcodeScanner';

interface BarcodeScannerProps {
  /** Called with the decoded barcode digits when a scan succeeds. */
  onDetected: (code: string) => void;
  /** Disables the start control while a lookup triggered by a prior scan runs. */
  disabled?: boolean;
}

/**
 * Webcam barcode scanner.
 *
 * Renders a live camera preview and a start/stop control. Camera access is
 * gated by {@link useBarcodeScanner}: when the environment has no webcam the
 * component renders an inline notice and no video, so the parent's manual-entry
 * fallback remains the path to use. On a successful decode it invokes
 * `onDetected` with the barcode text and stops the camera.
 */
export default function BarcodeScanner({ onDetected, disabled = false }: BarcodeScannerProps) {
  const { t } = useTranslation('scan');
  const videoRef = useRef<HTMLVideoElement | null>(null);
  const { state, error, start, stop } = useBarcodeScanner(videoRef, onDetected);

  if (state === 'unsupported') {
    return (
      <p className="hint" role="status">
        {t('noCamera')}
      </p>
    );
  }

  const isActive = state === 'starting' || state === 'scanning';

  return (
    <div className="scanner">
      <div className="scanner-viewport">
        {/* Live camera feed for barcode decoding — no captions applicable. */}
        <video
          ref={videoRef}
          className="scanner-video"
          aria-label={t('cameraPreviewLabel')}
          hidden={!isActive}
          muted
          playsInline
        />
        {!isActive && (
          <div className="scanner-placeholder" aria-hidden="true">
            {t('cameraOff')}
          </div>
        )}
      </div>

      <div className="scanner-controls">
        {isActive ? (
          <button type="button" onClick={stop}>
            {t('stopCamera')}
          </button>
        ) : (
          <button type="button" onClick={() => void start()} disabled={disabled}>
            {state === 'error' ? t('retryCamera') : t('scanWithCamera')}
          </button>
        )}
        {state === 'scanning' && (
          <span className="scanner-status" role="status">
            {t('pointCamera')}
          </span>
        )}
      </div>

      {/* TODO(i18n): return error code enum; translate in BarcodeScanner.
          The hook (useBarcodeScanner) currently produces English error strings
          directly; once it returns a code, map it through t('cameraErrors.*'). */}
      {error && (
        <p role="alert" className="message message-error">
          {error}
        </p>
      )}
    </div>
  );
}
