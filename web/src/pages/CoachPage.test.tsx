import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import CoachPage from './CoachPage';
import * as coachApi from '../api/coach';

vi.mock('../api/coach');

function renderCoachPage() {
  return render(
    <MemoryRouter>
      <CoachPage />
    </MemoryRouter>,
  );
}

beforeEach(() => {
  // Mock every read so lazily-mounted panels never reject unhandled.
  vi.mocked(coachApi.getConversations).mockResolvedValue({ conversations: [] });
  vi.mocked(coachApi.getMealSuggestions).mockResolvedValue({
    options: [],
    remainingCalories: 0,
    remainingProteinGrams: 0,
    remainingCarbGrams: 0,
    remainingFatGrams: 0,
    disclaimer: null,
  });
  vi.mocked(coachApi.getNudge).mockResolvedValue({
    message: 'Keep going.',
    tone: null,
    disclaimer: null,
  });
});

afterEach(() => {
  vi.restoreAllMocks();
});

describe('CoachPage', () => {
  it('renders the page heading', async () => {
    renderCoachPage();
    // Await ChatPanel's getConversations() mock settling so the resulting
    // setState happens inside act() (the empty-history hint signals settlement).
    await screen.findByText(/no messages yet/i);

    expect(screen.getByRole('heading', { level: 1, name: 'Coach' })).toBeInTheDocument();
  });

  it('selects the Chat tab by default', async () => {
    renderCoachPage();
    // Await ChatPanel's getConversations() mock settling so the resulting
    // setState happens inside act() (the empty-history hint signals settlement).
    await screen.findByText(/no messages yet/i);

    const chatTab = screen.getByRole('tab', { name: 'Chat' });
    expect(chatTab).toHaveAttribute('aria-selected', 'true');

    const chatPanel = document.getElementById('coach-panel-chat');
    const mealPanel = document.getElementById('coach-panel-meal');
    expect(chatPanel).not.toHaveAttribute('hidden');
    expect(mealPanel).toHaveAttribute('hidden');
  });

  it('unhides and mounts the Meal Suggestions panel when its tab is clicked', async () => {
    const user = userEvent.setup();
    renderCoachPage();

    await user.click(screen.getByRole('tab', { name: 'Meal Suggestions' }));

    const mealPanel = document.getElementById('coach-panel-meal');
    expect(mealPanel).not.toHaveAttribute('hidden');
    // The mounted panel renders its own heading.
    expect(await screen.findByRole('heading', { name: 'Meal Suggestions' })).toBeInTheDocument();

    // The chat panel is now hidden.
    expect(document.getElementById('coach-panel-chat')).toHaveAttribute('hidden');
  });
});
