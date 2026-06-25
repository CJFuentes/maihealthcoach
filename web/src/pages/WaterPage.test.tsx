import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import WaterPage from './WaterPage';
import * as waterApi from '../api/water';
import type { WaterDayResponse, WaterEntry } from '../api/water';

vi.mock('../api/water');

/** Returns today's local YYYY-MM-DD, mirroring the page's own logic. */
function todayLocalDate(): string {
  const d = new Date();
  const month = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${d.getFullYear()}-${month}-${day}`;
}

const sampleEntry: WaterEntry = {
  id: 'water-1',
  amountMl: 250,
  loggedAt: '2026-06-24T08:30:00.000Z',
};

function dayWith(entries: WaterEntry[], extra: Partial<WaterDayResponse> = {}): WaterDayResponse {
  const totalMl = entries.reduce((s, e) => s + e.amountMl, 0);
  return {
    date: todayLocalDate(),
    entries,
    totalMl,
    goalMl: 2000,
    remainingMl: Math.max(0, 2000 - totalMl),
    ...extra,
  };
}

function renderWaterPage() {
  return render(
    <MemoryRouter>
      <WaterPage />
    </MemoryRouter>,
  );
}

afterEach(() => {
  vi.restoreAllMocks();
});

beforeEach(() => {
  vi.mocked(waterApi.getWaterDay).mockResolvedValue(dayWith([sampleEntry]));
});

describe('WaterPage', () => {
  it('shows a loading state while the day is in flight', () => {
    vi.mocked(waterApi.getWaterDay).mockReturnValue(new Promise<WaterDayResponse>(() => {}));

    renderWaterPage();

    expect(screen.getByText('Loading water log…')).toBeInTheDocument();
  });

  it('renders an empty hint when no water is logged', async () => {
    vi.mocked(waterApi.getWaterDay).mockResolvedValue(dayWith([], { totalMl: 0, remainingMl: 2000 }));

    renderWaterPage();

    expect(await screen.findByText('No water logged yet.')).toBeInTheDocument();
  });

  it('renders the progress bar with consumed/goal aria values', async () => {
    vi.mocked(waterApi.getWaterDay).mockResolvedValue(
      dayWith([{ ...sampleEntry, amountMl: 750 }]),
    );

    renderWaterPage();

    const bar = await screen.findByRole('progressbar');
    expect(bar).toHaveAttribute('aria-valuenow', '750');
    expect(bar).toHaveAttribute('aria-valuemax', '2000');
    expect(bar).toHaveAttribute('aria-valuetext', '750 of 2000 ml');
  });

  it('computes the total client-side when the server omits aggregates', async () => {
    vi.mocked(waterApi.getWaterDay).mockResolvedValue({
      date: todayLocalDate(),
      entries: [
        { id: 'a', amountMl: 300, loggedAt: '2026-06-24T07:00:00.000Z' },
        { id: 'b', amountMl: 200, loggedAt: '2026-06-24T09:00:00.000Z' },
      ],
    });

    renderWaterPage();

    // No goal in the response -> indeterminate bar whose accessible name still
    // announces the client-computed consumed total (300 + 200 = 500 ml).
    const bar = await screen.findByRole('progressbar');
    expect(bar).toHaveAttribute('aria-label', '500 ml consumed');
    expect(bar).not.toHaveAttribute('aria-valuenow');
  });

  it('quick-adds 250 ml and refreshes the displayed total', async () => {
    const user = userEvent.setup();
    vi.mocked(waterApi.getWaterDay).mockResolvedValue(dayWith([], { totalMl: 0, remainingMl: 2000 }));
    vi.mocked(waterApi.addWaterEntry).mockResolvedValue({
      id: 'new-1',
      amountMl: 250,
      loggedAt: '2026-06-24T10:00:00.000Z',
    });

    renderWaterPage();

    const addButton = await screen.findByRole('button', { name: 'Add 250 ml' });
    await user.click(addButton);

    await waitFor(() => {
      expect(waterApi.addWaterEntry).toHaveBeenCalledWith({ amountMl: 250 });
    });

    // The progress bar reflects the optimistic total after the add.
    await waitFor(() => {
      expect(screen.getByRole('progressbar')).toHaveAttribute('aria-valuenow', '250');
    });
  });

  it('validates a non-positive custom amount without calling the API', async () => {
    const user = userEvent.setup();

    renderWaterPage();

    await screen.findByRole('progressbar');

    const input = screen.getByLabelText('Custom amount (ml)');
    await user.clear(input);
    await user.type(input, '0');
    await user.click(screen.getByRole('button', { name: 'Add' }));

    expect(await screen.findByText('Enter an amount greater than 0.')).toBeInTheDocument();
    expect(waterApi.addWaterEntry).not.toHaveBeenCalled();
  });

  it('posts a valid custom amount to the API', async () => {
    const user = userEvent.setup();
    vi.mocked(waterApi.addWaterEntry).mockResolvedValue({
      id: 'new-2',
      amountMl: 333,
      loggedAt: '2026-06-24T11:00:00.000Z',
    });

    renderWaterPage();

    await screen.findByRole('progressbar');

    const input = screen.getByLabelText('Custom amount (ml)');
    await user.clear(input);
    await user.type(input, '333');
    await user.click(screen.getByRole('button', { name: 'Add' }));

    await waitFor(() => {
      expect(waterApi.addWaterEntry).toHaveBeenCalledWith({ amountMl: 333 });
    });
  });

  it('deletes an entry when Remove is clicked', async () => {
    const user = userEvent.setup();
    vi.mocked(waterApi.deleteWaterEntry).mockResolvedValue(undefined);

    renderWaterPage();

    const removeButton = await screen.findByRole('button', { name: 'Remove 250 ml entry' });
    await user.click(removeButton);

    await waitFor(() => {
      expect(waterApi.deleteWaterEntry).toHaveBeenCalledWith('water-1');
    });
  });

  it('edits an entry and PUTs the new amount', async () => {
    const user = userEvent.setup();
    vi.mocked(waterApi.updateWaterEntry).mockResolvedValue({
      ...sampleEntry,
      amountMl: 400,
    });

    renderWaterPage();

    const editButton = await screen.findByRole('button', { name: 'Edit 250 ml entry' });
    await user.click(editButton);

    const editInput = screen.getByLabelText('Amount in ml');
    await user.clear(editInput);
    await user.type(editInput, '400');
    await user.click(screen.getByRole('button', { name: 'Save' }));

    await waitFor(() => {
      expect(waterApi.updateWaterEntry).toHaveBeenCalledWith('water-1', { amountMl: 400 });
    });
  });

  it('reloads when navigating to the previous day', async () => {
    const user = userEvent.setup();

    renderWaterPage();

    await waitFor(() => {
      expect(waterApi.getWaterDay).toHaveBeenCalledTimes(1);
    });

    await user.click(screen.getByRole('button', { name: /previous day/i }));

    await waitFor(() => {
      expect(waterApi.getWaterDay).toHaveBeenCalledTimes(2);
    });
    const calls = vi.mocked(waterApi.getWaterDay).mock.calls;
    const lastCall = calls[calls.length - 1];
    expect(lastCall?.[0]).not.toEqual(todayLocalDate());
  });

  it('surfaces a ProblemDetails validation error from a failed add', async () => {
    const user = userEvent.setup();
    const { ApiError } = await import('../api/client');
    vi.mocked(waterApi.addWaterEntry).mockRejectedValue(
      new ApiError(400, 'Bad Request', { errors: { amountMl: ['Amount is too large.'] } }),
    );

    renderWaterPage();

    const addButton = await screen.findByRole('button', { name: 'Add 500 ml' });
    await user.click(addButton);

    expect(await screen.findByText('Amount is too large.')).toBeInTheDocument();
  });

  it('shows an error state when the day fails to load', async () => {
    vi.mocked(waterApi.getWaterDay).mockRejectedValue(new Error('Network error'));

    renderWaterPage();

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Network error');
  });

  it('shows the entry amount in the list', async () => {
    renderWaterPage();

    const list = await screen.findByRole('list');
    expect(within(list).getByText(/250/)).toBeInTheDocument();
  });
});
