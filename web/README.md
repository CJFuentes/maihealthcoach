# Web — React + TypeScript (Vite)

This directory contains the browser-based front-end for MAI Health Coach.

## Stack

| Concern       | Technology                              |
|---------------|-----------------------------------------|
| Framework     | React 18                                |
| Language      | TypeScript                              |
| Build tool    | Vite                                    |
| Auth          | Clerk React SDK                         |
| Styling       | TBD (Tailwind CSS planned)              |
| Testing       | Vitest (unit), Playwright (E2E, later)  |
| Linting       | ESLint + Prettier                       |

## Planned Structure

```
web/
├── public/
├── src/
│   ├── api/           # Typed API client wrappers
│   ├── components/    # Shared UI components
│   ├── features/      # Feature-sliced modules (auth, log, coach, …)
│   ├── hooks/         # Custom React hooks
│   ├── pages/         # Route-level page components
│   └── main.tsx       # App entry point
├── .env.example
├── index.html
├── package.json
├── tsconfig.json
└── vite.config.ts
```

## Coming in Later Tickets

- M1: Vite scaffold, Clerk auth flow, routing skeleton
- M2: Daily log UI (food, water, exercise)
- M3: Barcode scanner integration (browser camera API)
- M4: AI coach chat/nudge UI

## Local Run

See the root [README.md](../README.md#web-react--vite) for prerequisites and setup steps.
