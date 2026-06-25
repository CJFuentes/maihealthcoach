import { vi } from 'vitest';
import '@testing-library/jest-dom';
// Initialize i18next (English, synchronous) for every test. This lets the
// existing page tests — which render with a bare MemoryRouter and query exact
// text — resolve t() calls to the byte-identical English strings without any
// per-test wiring.
import './i18n';

// jsdom does not implement scrollIntoView; ChatPanel calls it on its
// message-end sentinel after each message. Stub it globally so those renders
// do not throw.
Element.prototype.scrollIntoView = vi.fn();
