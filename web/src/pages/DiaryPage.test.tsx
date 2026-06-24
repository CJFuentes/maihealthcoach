import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import DiaryPage from './DiaryPage';
import * as diaryApi from '../api/diary';
import type { DiaryEntry, DiaryResponse } from '../api/diary';

vi.mock('../api/diary');
vi.mock('../api/foods');

const sampleEntry: DiaryEntry = {
  id: 'entry-1',
  foodId: 'food-1',
  foodName: 'Chicken Breast',
  mealType: 'Lunch',
  date: '2026-06-24',
  quantity: 1,
  servingSizeId: 'serving-1',
  servingSizeLabel: '100 g',
  nutrition: { calories: 165, proteinGrams: 31 },
};

const sampleDiary: DiaryResponse = {
  date: '2026-06-24',
  meals: { Lunch: [sampleEntry] },
};

/** Returns today's local YYYY-MM-DD, mirroring the page's own logic. */
function todayLocalDate(): string {
  const d = new Date();
  const month = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${d.getFullYear()}-${month}-${day}`;
}

function renderDiaryPage() {
  return render(
    <MemoryRouter>
      <DiaryPage />
    </MemoryRouter>,
  );
}

beforeEach(() => {
  // Summary is best-effort: default to unavailable so client totals are used.
  vi.mocked(diaryApi.getDailySummary).mockRejectedValue(new Error('not available'));
});

afterEach(() => {
  vi.restoreAllMocks();
});

describe('DiaryPage', () => {
  it('renders an empty hint for every meal on an empty day', async () => {
    vi.mocked(diaryApi.getDiary).mockResolvedValue({ date: todayLocalDate(), meals: {} });

    renderDiaryPage();

    await waitFor(() => {
      expect(screen.getAllByText('No entries yet.')).toHaveLength(4);
    });
  });

  it('groups entries under their meal heading', async () => {
    vi.mocked(diaryApi.getDiary).mockResolvedValue(sampleDiary);

    renderDiaryPage();

    expect(await screen.findByRole('heading', { name: 'Lunch' })).toBeInTheDocument();
    expect(screen.getByText('Chicken Breast')).toBeInTheDocument();
  });

  it('shows client-computed totals when the summary is unavailable', async () => {
    vi.mocked(diaryApi.getDiary).mockResolvedValue(sampleDiary);

    renderDiaryPage();

    // The Lunch subtotal and the summary bar both show 165 kcal.
    expect(await screen.findAllByText('165')).not.toHaveLength(0);
  });

  it('shows server totals when the summary is present', async () => {
    vi.mocked(diaryApi.getDiary).mockResolvedValue(sampleDiary);
    vi.mocked(diaryApi.getDailySummary).mockResolvedValue({
      date: todayLocalDate(),
      totals: { calories: 1800 },
      goals: { calories: 2200 },
    });

    renderDiaryPage();

    expect(await screen.findByText('1800')).toBeInTheDocument();
  });

  it('reloads the diary when navigating to the previous day', async () => {
    const user = userEvent.setup();
    vi.mocked(diaryApi.getDiary).mockResolvedValue({ date: todayLocalDate(), meals: {} });

    renderDiaryPage();

    await waitFor(() => {
      expect(diaryApi.getDiary).toHaveBeenCalledTimes(1);
    });

    await user.click(screen.getByRole('button', { name: /previous day/i }));

    await waitFor(() => {
      expect(diaryApi.getDiary).toHaveBeenCalledTimes(2);
    });
    // The second call must target a different (earlier) date than today.
    const calls = vi.mocked(diaryApi.getDiary).mock.calls;
    const lastCall = calls[calls.length - 1];
    expect(lastCall?.[0]).not.toEqual(todayLocalDate());
  });

  it('deletes an entry when Remove is clicked', async () => {
    const user = userEvent.setup();
    vi.mocked(diaryApi.getDiary).mockResolvedValue(sampleDiary);
    vi.mocked(diaryApi.deleteDiaryEntry).mockResolvedValue(undefined);

    renderDiaryPage();

    const deleteButton = await screen.findByRole('button', { name: /delete chicken breast/i });
    await user.click(deleteButton);

    await waitFor(() => {
      expect(diaryApi.deleteDiaryEntry).toHaveBeenCalledWith('entry-1');
    });
  });

  it('shows an error message when the diary fails to load', async () => {
    vi.mocked(diaryApi.getDiary).mockRejectedValue(new Error('Network error'));

    renderDiaryPage();

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Network error');
  });

  it('shows a loading state while the diary is in flight', () => {
    vi.mocked(diaryApi.getDiary).mockReturnValue(new Promise<DiaryResponse>(() => {}));

    renderDiaryPage();

    expect(screen.getByText('Loading diary…')).toBeInTheDocument();
  });
});
