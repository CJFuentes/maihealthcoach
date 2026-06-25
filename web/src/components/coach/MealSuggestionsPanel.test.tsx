import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import MealSuggestionsPanel from './MealSuggestionsPanel';
import * as coachApi from '../../api/coach';
import { ApiError } from '../../api/client';

vi.mock('../../api/coach');

const sampleResponse: coachApi.MealSuggestionsResponse = {
  options: [
    {
      name: 'Grilled chicken salad',
      calories: 450,
      proteinGrams: 40,
      carbGrams: 20,
      fatGrams: 18,
      rationale: 'High protein, fits your remaining budget.',
    },
    {
      name: 'Greek yogurt bowl',
      calories: 300,
      proteinGrams: 25,
      carbGrams: null,
      fatGrams: null,
      rationale: 'Quick protein top-up.',
    },
  ],
  remainingCalories: 800,
  remainingProteinGrams: 60,
  remainingCarbGrams: 90,
  remainingFatGrams: 30,
  disclaimer: 'Not medical advice.',
};

function renderPanel() {
  return render(
    <MemoryRouter>
      <MealSuggestionsPanel />
    </MemoryRouter>,
  );
}

afterEach(() => {
  vi.restoreAllMocks();
});

describe('MealSuggestionsPanel', () => {
  it('shows a loading status while suggestions load', () => {
    vi.mocked(coachApi.getMealSuggestions).mockReturnValue(new Promise(() => {}));

    renderPanel();

    expect(screen.getByText('Loading meal suggestions…')).toBeInTheDocument();
  });

  it('renders option cards and the remaining-macros line', async () => {
    vi.mocked(coachApi.getMealSuggestions).mockResolvedValue(sampleResponse);

    renderPanel();

    expect(
      await screen.findByRole('heading', { name: 'Grilled chicken salad' }),
    ).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Greek yogurt bowl' })).toBeInTheDocument();
    expect(screen.getByText('450 kcal')).toBeInTheDocument();
    expect(screen.getByText('Protein: 40 g')).toBeInTheDocument();
    expect(
      screen.getByText(/Remaining today: 800 kcal · 60 g protein · 90 g carbs · 30 g fat/),
    ).toBeInTheDocument();
    // The second option omits carbs/fat (null), so those rows are not rendered.
    expect(screen.queryByText('Carbs: null g')).not.toBeInTheDocument();
  });

  it('renders the disclaimer when present', async () => {
    vi.mocked(coachApi.getMealSuggestions).mockResolvedValue(sampleResponse);

    renderPanel();

    expect(await screen.findByText(/Disclaimer: Not medical advice\./i)).toBeInTheDocument();
  });

  it('shows the no-profile prompt with a /profile link on a 404', async () => {
    vi.mocked(coachApi.getMealSuggestions).mockRejectedValue(new ApiError(404, 'no profile'));

    renderPanel();

    expect(await screen.findByText(/complete your profile to receive/i)).toBeInTheDocument();
    const link = screen.getByRole('link', { name: /complete your profile/i });
    expect(link).toHaveAttribute('href', '/profile');
  });

  it('shows the incomplete-profile prompt on a 409', async () => {
    vi.mocked(coachApi.getMealSuggestions).mockRejectedValue(new ApiError(409, 'incomplete'));

    renderPanel();

    expect(await screen.findByText(/missing some required information/i)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /complete your profile/i })).toHaveAttribute(
      'href',
      '/profile',
    );
  });

  it('shows a service-unavailable alert on a 503', async () => {
    vi.mocked(coachApi.getMealSuggestions).mockRejectedValue(new ApiError(503, 'down'));

    renderPanel();

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent(/coach service is temporarily unavailable/i);
  });

  it('shows an empty hint when there are no options', async () => {
    vi.mocked(coachApi.getMealSuggestions).mockResolvedValue({
      ...sampleResponse,
      options: [],
    });

    renderPanel();

    expect(await screen.findByText(/no meal suggestions available/i)).toBeInTheDocument();
  });
});
