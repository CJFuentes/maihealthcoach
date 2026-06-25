import { useEffect, useRef } from 'react';
import { useLocation } from 'react-router-dom';

/**
 * Announces client-side route changes to assistive technology.
 *
 * In a single-page app the document title/heading changes without a full page
 * load, so screen readers get no signal that navigation happened. This mounts a
 * visually-hidden polite live region and, on each pathname change, copies the
 * new page's <h1> text into it — giving screen-reader users a spoken cue.
 *
 * The text is read after a `setTimeout(…, 0)` so the new route's content has
 * committed to the DOM before we query its heading. The timer is cleared on
 * cleanup, which keeps it safe under React StrictMode's double-invoked effects.
 */
export default function RouteAnnouncer() {
  const { pathname } = useLocation();
  const regionRef = useRef<HTMLDivElement | null>(null);

  useEffect(() => {
    const id = setTimeout(() => {
      const region = regionRef.current;
      if (!region) {
        return;
      }
      const heading = document.querySelector('#main-content h1');
      region.textContent = heading?.textContent ?? '';
    }, 0);

    return () => clearTimeout(id);
  }, [pathname]);

  return <div ref={regionRef} className="sr-only" aria-live="polite" role="status" />;
}
