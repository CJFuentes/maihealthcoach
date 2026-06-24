import { useCallback, useEffect, useRef, useState } from 'react';
import { BrowserMultiFormatReader, type IScannerControls } from '@zxing/browser';

/**
 * Whether the current environment can access a webcam.
 *
 * Guards every camera code path so the feature degrades gracefully (AC: "the
 * feature degrades gracefully where camera access is unavailable") and so the
 * headless CI/test environment — which has no `getUserMedia` — never attempts a
 * real camera request. Exported for the component to branch on before rendering
 * the live-scan UI.
 */
export function isCameraSupported(): boolean {
  return (
    typeof navigator !== 'undefined' && typeof navigator.mediaDevices?.getUserMedia === 'function'
  );
}

/** Lifecycle state of the webcam scanner. */
export type ScannerState = 'idle' | 'unsupported' | 'starting' | 'scanning' | 'error';

export interface UseBarcodeScannerResult {
  /** Current scanner lifecycle state. */
  state: ScannerState;
  /** Human-readable error (permission denied, no device, …), when state === 'error'. */
  error: string | null;
  /** Begins decoding from the webcam into the provided <video> element. */
  start: () => Promise<void>;
  /** Stops the camera and releases the stream. Safe to call when already stopped. */
  stop: () => void;
}

/**
 * React hook wrapping ZXing's {@link BrowserMultiFormatReader} for webcam
 * barcode decoding.
 *
 * Camera access is gated behind {@link isCameraSupported}: when unsupported the
 * hook reports state `'unsupported'` and `start()` is a no-op, so callers can
 * fall back to manual entry without the camera ever being touched. This is what
 * lets the build, typecheck, and tests run headlessly.
 *
 * @param videoRef - Ref to the <video> element the camera stream renders into.
 * @param onDetected - Called with the decoded barcode text on a successful scan.
 *   The hook automatically stops the camera after a detection.
 */
export function useBarcodeScanner(
  videoRef: React.RefObject<HTMLVideoElement | null>,
  onDetected: (code: string) => void,
): UseBarcodeScannerResult {
  const supported = isCameraSupported();
  const [state, setState] = useState<ScannerState>(supported ? 'idle' : 'unsupported');
  const [error, setError] = useState<string | null>(null);

  const controlsRef = useRef<IScannerControls | null>(null);
  // Monotonic token: bumped on every stop() so a start() awaiting the camera
  // can detect it was cancelled mid-flight and tear the stream down.
  const sessionRef = useRef(0);
  // Keep the latest callback without re-subscribing the decode loop.
  const onDetectedRef = useRef(onDetected);
  onDetectedRef.current = onDetected;

  const stop = useCallback(() => {
    sessionRef.current += 1;
    controlsRef.current?.stop();
    controlsRef.current = null;
    setState((prev) => (prev === 'unsupported' ? prev : 'idle'));
  }, []);

  const start = useCallback(async () => {
    if (!supported) {
      setState('unsupported');
      return;
    }
    if (controlsRef.current) {
      // Already scanning — ignore repeat starts.
      return;
    }

    const video = videoRef.current;
    if (!video) {
      setState('error');
      setError('Camera preview is not ready. Please try again.');
      return;
    }

    const session = (sessionRef.current += 1);
    setState('starting');
    setError(null);

    try {
      const reader = new BrowserMultiFormatReader();
      const controls = await reader.decodeFromVideoDevice(
        undefined, // let the browser pick the default (rear) camera
        video,
        (result, _err, ctrls) => {
          if (result) {
            const text = result.getText();
            ctrls.stop();
            controlsRef.current = null;
            setState('idle');
            onDetectedRef.current(text);
          }
          // Decode errors per-frame (no barcode in view) are expected and ignored.
        },
      );
      // If stop()/unmount fired during the await above, sessionRef advanced —
      // honour that and tear the just-started stream down so the camera is
      // never left running.
      if (sessionRef.current !== session) {
        controls.stop();
        return;
      }
      controlsRef.current = controls;
      setState('scanning');
    } catch (err: unknown) {
      controlsRef.current = null;
      setState('error');
      setError(describeCameraError(err));
    }
  }, [supported, videoRef]);

  // Release the camera if the component unmounts mid-scan.
  useEffect(() => stop, [stop]);

  return { state, error, start, stop };
}

/** Maps a getUserMedia/ZXing failure to a friendly, actionable message. */
function describeCameraError(err: unknown): string {
  if (err instanceof DOMException || (err instanceof Error && 'name' in err)) {
    const name = (err as { name: string }).name;
    switch (name) {
      case 'NotAllowedError':
      case 'SecurityError':
        return 'Camera permission was denied. Enable it in your browser settings, or enter the barcode manually below.';
      case 'NotFoundError':
      case 'OverconstrainedError':
        return 'No camera was found. Please enter the barcode manually below.';
      case 'NotReadableError':
        return 'The camera is already in use by another application.';
      default:
        break;
    }
  }
  return 'Could not start the camera. Please enter the barcode manually below.';
}
