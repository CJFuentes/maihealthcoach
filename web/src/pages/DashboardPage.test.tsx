import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { I18nextProvider } from 'react-i18next';
import i18n from '../i18n';
import DashboardPage from './DashboardPage';
import * as dashboardApi from '../api/dashboard';
import * as coachApi from '../api/coach';
import type { DashboardResponse } from '../api/dashboard';
import type { NudgeResponse } from '../api/coach';

vi.mock('../api/dashboard');
vi.mock('../api/coach');

const sampleNudge: NudgeResponse = {
  message: 'Nice work staying on track today!',
  tone: 'encouraging',
  disclaimer: 'This is general wellness guidance, not medical advice.',
};

function dashboardWith(overrides: Partial<DashboardResponse> = {}): DashboardResponse {
  return {
    date: '2026-06-25',
    goalsAvailable: true,
    calories: {
      calories: { consumed: 1200, target: 2000, remaining: 800, percentOfTarget: 60 },
      proteinG: { consumed: 80, target: 120, remaining: 40, percentOfTarget: 67 },
      carbohydrateG: { consumed: 150, target: 250, remaining: 100, percentOfTarget: 60 },
      fatG: { consumed: 40, target: 70, remaining: 30, percentOfTarget: 57 },
      entryCount: 3,
    },
    water: { goalsAvailable: true, consumedMl: 750, goalMl: 2000, remainingMl: 1250 },
    exercise: { totalCaloriesBurned: 320, entryCount: 1 },
    netCalories: 880,
    streak: {
      currentStreak: 5,
      longestStreak: 12,
      caloriesAdherence7d: 71,
      waterAdherence7d: 86,
    },
    ...overrides,
  };
}

const emptyDashboard: DashboardResponse = {
  date: '2026-06-25',
  goalsAvailable: false,
  calories: {
    calories: { consumed: 0, target: null, remaining: null, percentOfTarget: null },
    proteinG: { consumed: 0, target: null, remaining: null, percentOfTarget: null },
    carbohydrateG: { consumed: 0, target: null, remaining: null, percentOfTarget: null },
    fatG: { consumed: 0, target: null, remaining: null, percentOfTarget: null },
    entryCount: 0,
  },
  water: { goalsAvailable: false, consumedMl: 0, goalMl: null, remainingMl: null },
  exercise: { totalCaloriesBurned: 0, entryCount: 0 },
  netCalories: null,
  streak: {
    currentStreak: 0,
    longestStreak: 0,
    caloriesAdherence7d: null,
    waterAdherence7d: null,
  },
};

function renderDashboard() {
  return render(
    <I18nextProvider i18n={i18n}>
      <MemoryRouter>
        <DashboardPage />
      </MemoryRouter>
    </I18nextProvider>,
  );
}

afterEach(() => {
  vi.restoreAllMocks();
});

beforeEach(() => {
  vi.mocked(dashboardApi.getDashboard).mockResolvedValue(dashboardWith());
  vi.mocked(coachApi.getNudge).mockResolvedValue(sampleNudge);
});

describe('DashboardPage', () => {
  it('shows a loading status while the dashboard is in flight', () => {
    vi.mocked(dashboardApi.getDashboard).mockReturnValue(
      new Promise<DashboardResponse>(() => {}),
    );

    renderDashboard();

    const status = screen.getByRole('status');
    expect(status).toHaveTextContent('Loading your day…');
    expect(status).toHaveAttribute('aria-busy', 'true');
  });

  it('renders the snapshot: calories, water, exercise, and streak', async () => {
    renderDashboard();

    // Calories/macros progressbars (4) + the water progressbar (1).
    const bars = await screen.findAllByRole('progressbar');
    expect(bars.length).toBe(5);

    const caloriesBar = screen.getByRole('progressbar', { name: 'Calories' });
    expect(caloriesBar).toHaveAttribute('aria-valuenow', '1200');
    expect(caloriesBar).toHaveAttribute('aria-valuemax', '2000');

    const waterBar = screen.getByRole('progressbar', { name: 'Daily water intake' });
    expect(waterBar).toHaveAttribute('aria-valuenow', '750');
    expect(waterBar).toHaveAttribute('aria-valuemax', '2000');

    // Section headings present (exact names to avoid matching "Net calories").
    expect(screen.getByRole('heading', { name: 'Calories & macros', level: 2 })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Water', level: 2 })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Exercise', level: 2 })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Streak', level: 2 })).toBeInTheDocument();

    // Exercise + streak figures.
    expect(screen.getByText(/320/)).toBeInTheDocument();
    expect(screen.getByText(/5-day streak/i)).toBeInTheDocument();
  });

  it('renders the coach nudge when getNudge resolves', async () => {
    renderDashboard();

    expect(
      await screen.findByText('Nice work staying on track today!'),
    ).toBeInTheDocument();
  });

  it('renders the empty hint and zeroed bars when nothing is logged', async () => {
    vi.mocked(dashboardApi.getDashboard).mockResolvedValue(emptyDashboard);

    renderDashboard();

    expect(await screen.findByText('Nothing logged yet today.')).toBeInTheDocument();

    // The bars still render (indeterminate, no target) — accessible name carries
    // the zeroed consumed amount.
    const caloriesBar = screen.getByRole('progressbar', { name: /0 kcal Calories/i });
    expect(caloriesBar).not.toHaveAttribute('aria-valuenow');
  });

  it('renders an alert when the dashboard fails to load', async () => {
    vi.mocked(dashboardApi.getDashboard).mockRejectedValue(new Error('Network down'));

    renderDashboard();

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Network down');
  });

  it('hides the nudge silently when getNudge rejects', async () => {
    vi.mocked(coachApi.getNudge).mockRejectedValue(new Error('coach unavailable'));

    renderDashboard();

    // Dashboard still renders.
    await screen.findByRole('heading', { name: /streak/i, level: 2 });

    // No nudge heading, and no alert raised by the nudge failure.
    await waitFor(() => {
      expect(
        screen.queryByRole('heading', { name: /a nudge from mai/i }),
      ).not.toBeInTheDocument();
    });
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });
});
