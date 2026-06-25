import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { I18nextProvider } from 'react-i18next';
import i18n from '../i18n';
import TrendsPage from './TrendsPage';
import * as trendsApi from '../api/trends';
import type { TrendsResponse } from '../api/trends';

vi.mock('../api/trends');

function sampleTrends(overrides: Partial<TrendsResponse> = {}): TrendsResponse {
  return {
    from: '2026-06-19',
    to: '2026-06-25',
    caloriesConsumed: [
      { date: '2026-06-19', value: 1800 },
      { date: '2026-06-20', value: 2100 },
      { date: '2026-06-21', value: 0 },
      { date: '2026-06-22', value: 1950 },
      { date: '2026-06-23', value: 2200 },
      { date: '2026-06-24', value: 1700 },
      { date: '2026-06-25', value: 1850 },
    ],
    caloriesBurned: [
      { date: '2026-06-19', value: 300 },
      { date: '2026-06-20', value: 0 },
      { date: '2026-06-21', value: 450 },
      { date: '2026-06-22', value: 200 },
      { date: '2026-06-23', value: 380 },
      { date: '2026-06-24', value: 0 },
      { date: '2026-06-25', value: 410 },
    ],
    netCalories: [
      { date: '2026-06-19', value: 1500 },
      { date: '2026-06-20', value: 2100 },
      { date: '2026-06-21', value: -450 },
      { date: '2026-06-22', value: 1750 },
      { date: '2026-06-23', value: 1820 },
      { date: '2026-06-24', value: 1700 },
      { date: '2026-06-25', value: 1440 },
    ],
    waterMl: [
      { date: '2026-06-19', value: 1500 },
      { date: '2026-06-20', value: 2000 },
      { date: '2026-06-21', value: 0 },
      { date: '2026-06-22', value: 1800 },
      { date: '2026-06-23', value: 2200 },
      { date: '2026-06-24', value: 1600 },
      { date: '2026-06-25', value: 1900 },
    ],
    weight: [
      { date: '2026-06-20', weightKg: 80.5 },
      { date: '2026-06-24', weightKg: 79.8 },
    ],
    ...overrides,
  };
}

function renderTrends() {
  return render(
    <I18nextProvider i18n={i18n}>
      <MemoryRouter>
        <TrendsPage />
      </MemoryRouter>
    </I18nextProvider>,
  );
}

afterEach(() => {
  vi.restoreAllMocks();
});

beforeEach(() => {
  vi.mocked(trendsApi.getTrends).mockResolvedValue(sampleTrends());
});

describe('TrendsPage', () => {
  it('shows a loading status while the trends are in flight', () => {
    vi.mocked(trendsApi.getTrends).mockReturnValue(new Promise<TrendsResponse>(() => {}));

    renderTrends();

    const status = screen.getByRole('status');
    expect(status).toHaveTextContent('Loading trends…');
    expect(status).toHaveAttribute('aria-busy', 'true');
  });

  it('renders the five chart section headings on success', async () => {
    renderTrends();

    expect(
      await screen.findByRole('heading', { name: 'Calories consumed', level: 2 }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole('heading', { name: 'Calories burned', level: 2 }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole('heading', { name: 'Net calories', level: 2 }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole('heading', { name: 'Water intake', level: 2 }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole('heading', { name: 'Body weight', level: 2 }),
    ).toBeInTheDocument();
  });

  it('renders three range buttons with 30 days pressed by default', async () => {
    renderTrends();

    await screen.findByRole('heading', { name: 'Calories consumed', level: 2 });

    const buttons = screen.getAllByRole('button');
    expect(buttons).toHaveLength(3);

    const thirty = screen.getByRole('button', { name: '30 days' });
    expect(thirty).toHaveAttribute('aria-pressed', 'true');
  });

  it('resolves the plural range labels exactly', async () => {
    renderTrends();

    await screen.findByRole('heading', { name: 'Calories consumed', level: 2 });

    expect(screen.getByRole('button', { name: '7 days' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '30 days' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '90 days' })).toBeInTheDocument();
  });

  it('fetches the 7-day window when "7 days" is clicked', async () => {
    const user = userEvent.setup();
    renderTrends();

    await screen.findByRole('heading', { name: 'Calories consumed', level: 2 });
    await user.click(screen.getByRole('button', { name: '7 days' }));

    await waitFor(() => {
      expect(trendsApi.getTrends).toHaveBeenCalledWith(7);
    });
  });

  it('fetches the 90-day window when "90 days" is clicked', async () => {
    const user = userEvent.setup();
    renderTrends();

    await screen.findByRole('heading', { name: 'Calories consumed', level: 2 });
    await user.click(screen.getByRole('button', { name: '90 days' }));

    await waitFor(() => {
      expect(trendsApi.getTrends).toHaveBeenCalledWith(90);
    });
  });

  it('re-fetches when the range changes', async () => {
    const user = userEvent.setup();
    renderTrends();

    await screen.findByRole('heading', { name: 'Calories consumed', level: 2 });
    await user.click(screen.getByRole('button', { name: '7 days' }));

    await waitFor(() => {
      expect(vi.mocked(trendsApi.getTrends).mock.calls.length).toBeGreaterThanOrEqual(2);
    });
  });

  it('renders an alert when the trends fail to load', async () => {
    vi.mocked(trendsApi.getTrends).mockRejectedValue(new Error('Network error'));

    renderTrends();

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Network error');
  });

  it('renders screen-reader data tables on success', async () => {
    renderTrends();

    await screen.findByRole('heading', { name: 'Calories consumed', level: 2 });

    const tables = screen.getAllByRole('table');
    expect(tables.length).toBeGreaterThanOrEqual(1);
  });

  it('shows the no-weight-data message when the weight series is empty', async () => {
    vi.mocked(trendsApi.getTrends).mockResolvedValue(sampleTrends({ weight: [] }));

    renderTrends();

    expect(await screen.findByText('No weight data for this period.')).toBeInTheDocument();
  });
});
