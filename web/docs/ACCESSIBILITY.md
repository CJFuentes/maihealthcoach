# Accessibility & i18n baseline

This document is the accessibility (a11y) checklist and internationalisation (i18n)
guide for the MAI Health Coach web app. The baseline targets **WCAG 2.2 level AA**.
Treat it as a living checklist: apply the per-PR list to every feature, and extend
the guidance as new patterns appear.

---

## 1. Landmarks & page structure

Every page renders inside the shared `Layout` (`src/components/Layout.tsx`), which
provides the document landmarks:

| Landmark     | Element                          | Notes                                            |
| ------------ | -------------------------------- | ------------------------------------------------ |
| `banner`     | `<header>`                       | App nav + account menu + language switcher.      |
| `navigation` | `<nav aria-label="Primary">`     | Labelled so screen readers can distinguish navs. |
| `main`       | `<main id="main-content">`       | Single main per page; `tabIndex={-1}` for focus. |

Rules:

- Exactly **one `<h1>` per page**, and it is the first heading inside `<main>`.
- Headings descend without skipping levels (`h1 → h2 → h3`); never pick a heading
  for its size — style with CSS instead.
- Use semantic elements (`<button>`, `<a>`, `<nav>`, `<ul>`, `<fieldset>`,
  `<label>`) before reaching for `<div>` + ARIA.

## 2. Skip link

`Layout` renders a skip link as the **first focusable element**:

```tsx
<a href="#main-content" className="skip-link">{t('skipToContent')}</a>
```

It is visually hidden (`.skip-link`) until focused (`.skip-link:focus`), then anchors
to the top-left. The target `<main id="main-content" tabIndex={-1}>` can receive
programmatic focus. Keep this link first whenever the layout header changes.

## 3. Focus management & route announcements

- This is a single-page app, so route changes do not reload the page. The
  `RouteAnnouncer` (`src/components/RouteAnnouncer.tsx`) is mounted as the first
  child of `<main>`. On each pathname change it copies the new page's `<h1>` text
  into a visually-hidden `aria-live="polite"` region, giving screen-reader users a
  spoken cue that navigation happened.
- The announcer reads the heading after `setTimeout(…, 0)` (so the new route has
  committed) and **clears the timer on cleanup**, which keeps it correct under
  React StrictMode's double-invoked effects.
- When you add a flow that moves focus (modals, drawers, multi-step forms), move
  focus into the new context on open and restore it to the trigger on close.

## 4. Focus indicators

`src/index.css` defines a shared visible focus ring for links, buttons, inputs,
selects, and textareas via `:focus-visible`:

```css
outline: 2px solid var(--color-primary);
outline-offset: 2px;
```

**Never remove an outline without providing a visible replacement.** New
interactive elements must show a clear focus indicator.

## 5. Colour contrast

All token pairs below were verified against WCAG AA (normal text needs ≥ 4.5:1,
large/bold text and UI components need ≥ 3:1). Ratios are computed from the tokens
in `src/index.css`.

| Foreground            | Background              | Ratio   | Use                         |
| --------------------- | ----------------------- | ------- | --------------------------- |
| `--color-text` #1c2230 | `--color-bg` #f7f8fa    | 14.96:1 | Body text on page           |
| `--color-text` #1c2230 | `--color-surface` #fff  | 15.90:1 | Body text on cards          |
| `--color-muted` #5b6472 | `--color-bg` #f7f8fa   | 5.63:1  | Hints/secondary text        |
| `--color-muted` #5b6472 | `--color-surface` #fff | 5.98:1  | Hints on cards              |
| `--color-primary-contrast` #fff | `--color-primary` #2563eb       | 5.17:1  | Button label / active nav   |
| `--color-primary-contrast` #fff | `--color-primary-hover` #1d4ed8 | 6.70:1  | Button label on hover       |
| `--color-error` #b91c1c | `--color-error-bg` #fee2e2   | 5.30:1  | Error messages              |
| `--color-success` #15803d | `--color-success-bg` #dcfce7 | 4.57:1  | Success messages            |
| `--color-text` #1c2230 | `--color-info-bg` #e0edff    | 13.42:1 | Info messages               |

When adding tokens or colour pairings, recompute the ratio and add a row here.
Do not rely on colour alone to convey meaning — pair it with text/icons (e.g. the
error/success messages also carry `role="alert"` / `role="status"`).

## 6. Forms

Checklist for every form control:

- Each control has a programmatic label — a `<label htmlFor>` tied to the control's
  `id`, or an `aria-label` where a visible label is not possible.
- Validation: set `aria-invalid` on the field and link the error message with
  `aria-describedby={\`${field}-error\`}`; render the error in an element with that
  `id` and `role="alert"`. See `ProfilePage` / `GoalsPage` for the pattern.
- Use `noValidate` on `<form>` and own validation so messages are consistent and
  localised, rather than relying on native browser bubbles.
- Group related radios/checkboxes in a `<fieldset>` with a `<legend>` (or a
  `role="radiogroup"` with `aria-label`, as the units toggle does).
- `<select>` option **values stay raw enum keys**; only the visible option text is
  translated (`t(...)`). Never translate a `value` attribute.
- Placeholders are hints, not labels — always provide a real label too.

## 7. ARIA guidelines

- Prefer native semantics; reach for ARIA only to fill a genuine gap.
- Live regions: `role="status"` / `aria-live="polite"` for non-urgent updates
  (loading, "saved"); `role="alert"` for errors that need immediate attention.
- Decorative-only nodes get `aria-hidden="true"` (e.g. the "Camera off" placeholder).
- Don't add redundant roles to elements that already have them (`<nav>` is already
  `navigation`, `<button>` is already `button`).

## 8. Media

The only `<video>` is the live barcode-decoding camera preview — it has no audio and
no captionable content — so `jsx-a11y/media-has-caption` is disabled for `src/**`
in `eslint.config.js`. Any future media with audio/spoken content MUST provide
captions/transcripts and must not rely on that disable.

---

## 9. Per-PR feature accessibility checklist

Before merging any UI work, confirm:

- [ ] Semantic HTML; correct single `<h1>` and non-skipping heading order.
- [ ] All interactive elements reachable and operable by keyboard (Tab/Shift-Tab,
      Enter/Space); logical focus order.
- [ ] Visible focus indicator on every interactive element.
- [ ] All form controls have programmatic labels; errors use `aria-invalid` +
      `aria-describedby` + `role="alert"`.
- [ ] Loading / empty / error states announced via `role="status"` or `role="alert"`.
- [ ] Colour contrast meets AA; meaning never conveyed by colour alone.
- [ ] No user-facing hard-coded strings — everything goes through `t()` (see below).
- [ ] If focus is moved (modal/drawer/step), it is moved in and restored on close.
- [ ] `npm run typecheck && npm run lint && npm run test && npm run build` all green.

---

## 10. Internationalisation (i18n)

The app uses **i18next + react-i18next**. The single instance is created in
`src/i18n/index.ts`, initialised synchronously with bundled inline resources (no
network backend). The app is English-only today, structured so adding locales is
trivial.

### Namespace organisation

One JSON file per namespace under `src/i18n/locales/<lang>/`:

| Namespace | File           | Covers                                            |
| --------- | -------------- | ------------------------------------------------- |
| `common`  | `common.json`  | Cross-cutting: skip link, 404 page, shared errors |
| `nav`     | `nav.json`     | Primary nav labels + nav landmark label           |
| `home`    | `home.json`    | Home page (backend status)                        |
| `about`   | `about.json`   | About page                                        |
| `profile` | `profile.json` | Profile form: labels, options, messages           |
| `goals`   | `goals.json`   | Goals cards, BMR/TDEE, override form               |
| `scan`    | `scan.json`    | Barcode scan page + scanner camera UI             |
| `auth`    | `auth.json`    | Sign-in / sign-up screen titles                   |

The default namespace is `common`. Keys are typed end-to-end: `src/i18n/types.d.ts`
augments i18next's `CustomTypeOptions` with `typeof` each JSON, so `t()` gives key
autocompletion and rejects unknown keys at compile time.

### Conventions

- **Translate values, never keys.** The JSON keys are stable identifiers shared
  across locales.
- Interpolate dynamic values with `{{name}}` placeholders, e.g.
  `"online": "Backend online — {{service}} v{{version}}"`.
- When a string interleaves with markup (e.g. a bolded `<strong>` value), keep the
  surrounding text in `t()` and preserve the element structure in JSX rather than
  embedding HTML in the JSON.
- Language preference is persisted to `localStorage` under `mai-lang` and restored
  on load.

### How to add a locale (e.g. French `fr`)

1. Copy `src/i18n/locales/en/` to `src/i18n/locales/fr/` and translate every JSON
   **value** (leave keys unchanged).
2. In `src/i18n/index.ts`, import the new namespace JSON files and add them under
   `resources.fr`.
3. In `src/components/LanguageSwitcher.tsx`, add `{ code: 'fr', label: 'Français' }`
   to `SUPPORTED_LOCALES`. (The switcher renders nothing while only one locale is
   supported, so it appears automatically once a second locale exists.)
4. Run `npm run typecheck && npm run test` to confirm the new resources are
   well-typed and nothing regressed.

No code changes beyond those three files are required — components already read all
copy through `t()`.
