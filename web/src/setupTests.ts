import '@testing-library/jest-dom';
// Initialize i18next (English, synchronous) for every test. This lets the
// existing page tests — which render with a bare MemoryRouter and query exact
// text — resolve t() calls to the byte-identical English strings without any
// per-test wiring.
import './i18n';
