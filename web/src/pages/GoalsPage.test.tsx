import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import GoalsPage from './GoalsPage';
import * as goalsApi from '../api/goals';
import { ApiError } from '../api/client';

vi.mock('../api/goals');

const sampleGoals: goalsApi.GoalsResponse = {
  calories: { value: 2200, computed: 2200, isOverridden: false },
  proteinGrams: { value: 165, computed: 165, isOverridden: false },
  carbohydrateGrams: { value: 248, computed: 248, isOverridden: false },
  fatGrams: { value: 73, computed: 73, isOverridden: false },
  waterMl: { value: 3000, computed: 3000, isOverridden: false },
  bmr: 1750,
  tdee: 2200,
  lastOverriddenAt: null,
};

function renderGoalsPage() {
  return render(
    <MemoryRouter>
      <GoalsPage />
    </MemoryRouter>,
  );
}

afterEach(() => {
  vi.restoreAllMocks();
});

describe('GoalsPage', () => {
  it('renders the targets on success', async () => {
    vi.mocked(goalsApi.getGoals).mockResolvedValue(sampleGoals);

    renderGoalsPage();

    await waitFor(() => {
      expect(screen.getByRole('heading', { name: 'Calories' })).toBeInTheDocument();
    });
    // The protein target (165) is unique to the protein card.
    expect(screen.getByText('165')).toBeInTheDocument();
    expect(screen.getByText(/BMR/)).toBeInTheDocument();
  });

  it('renders the incomplete-profile prompt with a link on a 409', async () => {
    vi.mocked(goalsApi.getGoals).mockRejectedValue(
      new ApiError(409, 'Incomplete profile.', { title: 'Incomplete profile.' }),
    );

    renderGoalsPage();

    await waitFor(() => {
      expect(screen.getByText('Incomplete profile.')).toBeInTheDocument();
    });
    const link = screen.getByRole('link', { name: /complete your profile/i });
    expect(link).toHaveAttribute('href', '/profile');
  });

  it('renders the incomplete-profile prompt with a link on a 404', async () => {
    vi.mocked(goalsApi.getGoals).mockRejectedValue(
      new ApiError(404, 'Profile not found.', { title: 'Profile not found.' }),
    );

    renderGoalsPage();

    await waitFor(() => {
      expect(screen.getByText('Profile not found.')).toBeInTheDocument();
    });
    const link = screen.getByRole('link', { name: /complete your profile/i });
    expect(link).toHaveAttribute('href', '/profile');
  });

  it('saves overrides and shows a confirmation', async () => {
    const user = userEvent.setup();
    vi.mocked(goalsApi.getGoals).mockResolvedValue(sampleGoals);
    vi.mocked(goalsApi.setGoalOverrides).mockResolvedValue({
      ...sampleGoals,
      calories: { value: 2500, computed: 2200, isOverridden: true },
      lastOverriddenAt: '2026-06-24T00:00:00Z',
    });

    renderGoalsPage();

    const caloriesInput = await screen.findByLabelText(/calories/i);
    await user.clear(caloriesInput);
    await user.type(caloriesInput, '2500');
    await user.click(screen.getByRole('button', { name: /save overrides/i }));

    await waitFor(() => {
      expect(goalsApi.setGoalOverrides).toHaveBeenCalledWith(
        expect.objectContaining({ caloriesKcal: 2500 }),
      );
    });
    expect(await screen.findByText('Overrides saved.')).toBeInTheDocument();
  });
});
