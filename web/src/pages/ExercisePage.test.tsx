import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import ExercisePage from './ExercisePage';
import * as exerciseApi from '../api/exercise';
import type { Exercise, ExerciseDayResponse, ExerciseEntry } from '../api/exercise';

vi.mock('../api/exercise');

/** Returns today's local YYYY-MM-DD, mirroring the page's own logic. */
function todayLocalDate(): string {
  const d = new Date();
  const month = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${d.getFullYear()}-${month}-${day}`;
}

const sampleExercise: Exercise = {
  id: 'ex-1',
  name: 'Running',
  category: 'Cardio',
  met: 9.8,
};

const sampleEntry: ExerciseEntry = {
  id: 'entry-1',
  activityId: 'ex-1',
  activityName: 'Running',
  category: 'Cardio',
  durationMinutes: 30,
  caloriesBurned: 280,
  loggedAt: '2026-06-25T07:00:00.000Z',
  date: todayLocalDate(),
};

function dayWith(
  entries: ExerciseEntry[],
  extra: Partial<ExerciseDayResponse> = {},
): ExerciseDayResponse {
  const totalCaloriesBurned = entries.reduce((s, e) => s + e.caloriesBurned, 0);
  return {
    date: todayLocalDate(),
    entries,
    totalCaloriesBurned,
    ...extra,
  };
}

function renderExercisePage() {
  return render(
    <MemoryRouter>
      <ExercisePage />
    </MemoryRouter>,
  );
}

/**
 * Matches the search-result button uniquely. Its accessible name combines the
 * activity name with the "MET" meta line, so it never collides with the
 * entry-row Edit/Remove buttons (whose names are "Edit/Remove Running entry").
 */
function findResultButton() {
  return screen.findByRole('button', { name: /MET/ });
}

afterEach(() => {
  vi.restoreAllMocks();
});

beforeEach(() => {
  vi.mocked(exerciseApi.getExerciseDay).mockResolvedValue(dayWith([sampleEntry]));
});

describe('ExercisePage', () => {
  it('shows a loading state while the day is in flight', () => {
    vi.mocked(exerciseApi.getExerciseDay).mockReturnValue(
      new Promise<ExerciseDayResponse>(() => {}),
    );

    renderExercisePage();

    expect(screen.getByText('Loading exercise log…')).toBeInTheDocument();
  });

  it('renders an empty hint when no exercise is logged', async () => {
    vi.mocked(exerciseApi.getExerciseDay).mockResolvedValue(dayWith([]));

    renderExercisePage();

    expect(await screen.findByText('No exercises logged yet.')).toBeInTheDocument();
  });

  it('shows the day total calories burned in the summary', async () => {
    renderExercisePage();

    // The "280" value and "kcal" unit render as separate text nodes inside the
    // summary's <dd>, so match on the normalised definition-list text instead.
    const term = await screen.findByText('Calories burned');
    const dd = term.nextElementSibling;
    expect(dd?.textContent?.replace(/\s+/g, ' ').trim()).toBe('280 kcal');
  });

  it('shows the search panel after load', async () => {
    renderExercisePage();

    expect(await screen.findByRole('searchbox')).toBeInTheDocument();
  });

  it('shows a duration form after selecting an exercise from results', async () => {
    const user = userEvent.setup();
    vi.mocked(exerciseApi.searchExercises).mockResolvedValue({ items: [sampleExercise] });

    renderExercisePage();

    const search = await screen.findByRole('searchbox');
    await user.type(search, 'run');

    const resultButton = await findResultButton();
    await user.click(resultButton);

    expect(await screen.findByLabelText('Duration')).toBeInTheDocument();
  });

  it('rejects a non-positive duration without calling the API', async () => {
    const user = userEvent.setup();
    vi.mocked(exerciseApi.searchExercises).mockResolvedValue({ items: [sampleExercise] });

    renderExercisePage();

    const search = await screen.findByRole('searchbox');
    await user.type(search, 'run');
    await user.click(await findResultButton());

    const input = await screen.findByLabelText('Duration');
    await user.clear(input);
    await user.type(input, '0');
    await user.click(screen.getByRole('button', { name: 'Log' }));

    expect(await screen.findByText('Enter a duration greater than 0.')).toBeInTheDocument();
    expect(exerciseApi.logExercise).not.toHaveBeenCalled();
  });

  it('rejects a non-integer and garbage duration without calling the API', async () => {
    const user = userEvent.setup();
    vi.mocked(exerciseApi.searchExercises).mockResolvedValue({ items: [sampleExercise] });

    renderExercisePage();

    const search = await screen.findByRole('searchbox');
    await user.type(search, 'run');
    await user.click(await findResultButton());

    const input = await screen.findByLabelText('Duration');

    // Non-integer: rejected by the Number.isInteger guard.
    await user.clear(input);
    await user.type(input, '1.5');
    await user.click(screen.getByRole('button', { name: 'Log' }));
    expect(await screen.findByText('Enter a duration greater than 0.')).toBeInTheDocument();

    // Garbage: the number input strips non-numeric characters, so the handler
    // sees an empty value (Number('') === 0), which the guard also rejects.
    await user.clear(input);
    await user.type(input, 'abc');
    await user.click(screen.getByRole('button', { name: 'Log' }));

    expect(exerciseApi.logExercise).not.toHaveBeenCalled();
  });

  it('logs an exercise and shows the new entry', async () => {
    const user = userEvent.setup();
    vi.mocked(exerciseApi.getExerciseDay).mockResolvedValue(dayWith([]));
    vi.mocked(exerciseApi.searchExercises).mockResolvedValue({ items: [sampleExercise] });
    vi.mocked(exerciseApi.logExercise).mockResolvedValue(sampleEntry);

    renderExercisePage();

    const search = await screen.findByRole('searchbox');
    await user.type(search, 'run');
    await user.click(await findResultButton());

    const input = await screen.findByLabelText('Duration');
    await user.clear(input);
    await user.type(input, '30');
    await user.click(screen.getByRole('button', { name: 'Log' }));

    await waitFor(() => {
      expect(exerciseApi.logExercise).toHaveBeenCalledWith({
        activityId: 'ex-1',
        durationMinutes: 30,
        date: todayLocalDate(),
      });
    });

    expect(await screen.findByText(/Running/)).toBeInTheDocument();
  });

  it('updates the daily total after logging', async () => {
    const user = userEvent.setup();
    vi.mocked(exerciseApi.getExerciseDay).mockResolvedValue(dayWith([]));
    vi.mocked(exerciseApi.searchExercises).mockResolvedValue({ items: [sampleExercise] });
    vi.mocked(exerciseApi.logExercise).mockResolvedValue(sampleEntry);

    renderExercisePage();

    const search = await screen.findByRole('searchbox');
    await user.type(search, 'run');
    await user.click(await findResultButton());

    const input = await screen.findByLabelText('Duration');
    await user.clear(input);
    await user.type(input, '30');
    await user.click(screen.getByRole('button', { name: 'Log' }));

    // After the optimistic add the summary's total reflects the new entry.
    await waitFor(() => {
      const term = screen.getByText('Calories burned');
      const dd = term.nextElementSibling;
      expect(dd?.textContent?.replace(/\s+/g, ' ').trim()).toBe('280 kcal');
    });
  });

  it('shows a complete-profile link when logging returns a 422', async () => {
    const user = userEvent.setup();
    const { ApiError } = await import('../api/client');
    vi.mocked(exerciseApi.searchExercises).mockResolvedValue({ items: [sampleExercise] });
    vi.mocked(exerciseApi.logExercise).mockRejectedValue(
      new ApiError(422, 'Unprocessable Entity', {}),
    );

    renderExercisePage();

    const search = await screen.findByRole('searchbox');
    await user.type(search, 'run');
    await user.click(await findResultButton());

    const input = await screen.findByLabelText('Duration');
    await user.clear(input);
    await user.type(input, '30');
    await user.click(screen.getByRole('button', { name: 'Log' }));

    expect(
      await screen.findByRole('link', { name: /complete.*profile/i }),
    ).toBeInTheDocument();
  });

  it('clears the 422 prompt after navigating to the previous day', async () => {
    const user = userEvent.setup();
    const { ApiError } = await import('../api/client');
    vi.mocked(exerciseApi.searchExercises).mockResolvedValue({ items: [sampleExercise] });
    vi.mocked(exerciseApi.logExercise).mockRejectedValue(
      new ApiError(422, 'Unprocessable Entity', {}),
    );

    renderExercisePage();

    const search = await screen.findByRole('searchbox');
    await user.type(search, 'run');
    await user.click(await findResultButton());

    const input = await screen.findByLabelText('Duration');
    await user.clear(input);
    await user.type(input, '30');
    await user.click(screen.getByRole('button', { name: 'Log' }));

    await screen.findByRole('link', { name: /complete.*profile/i });

    await user.click(screen.getByRole('button', { name: /previous day/i }));

    await waitFor(() => {
      expect(
        screen.queryByRole('link', { name: /complete.*profile/i }),
      ).not.toBeInTheDocument();
    });
  });

  it('deletes an entry when Remove is clicked', async () => {
    const user = userEvent.setup();
    vi.mocked(exerciseApi.deleteExerciseEntry).mockResolvedValue(undefined);

    renderExercisePage();

    const removeButton = await screen.findByRole('button', { name: 'Remove Running entry' });
    await user.click(removeButton);

    await waitFor(() => {
      expect(exerciseApi.deleteExerciseEntry).toHaveBeenCalledWith('entry-1');
    });
  });

  it('edits an entry and PUTs the new duration', async () => {
    const user = userEvent.setup();
    vi.mocked(exerciseApi.updateExerciseEntry).mockResolvedValue({
      ...sampleEntry,
      durationMinutes: 45,
    });

    renderExercisePage();

    const editButton = await screen.findByRole('button', { name: 'Edit Running entry' });
    await user.click(editButton);

    const editInput = screen.getByLabelText('Duration in minutes');
    await user.clear(editInput);
    await user.type(editInput, '45');
    await user.click(screen.getByRole('button', { name: 'Save' }));

    await waitFor(() => {
      expect(exerciseApi.updateExerciseEntry).toHaveBeenCalledWith('entry-1', {
        durationMinutes: 45,
      });
    });
  });

  it('reloads when navigating to the previous day', async () => {
    const user = userEvent.setup();

    renderExercisePage();

    await waitFor(() => {
      expect(exerciseApi.getExerciseDay).toHaveBeenCalledTimes(1);
    });

    await user.click(screen.getByRole('button', { name: /previous day/i }));

    await waitFor(() => {
      expect(exerciseApi.getExerciseDay).toHaveBeenCalledTimes(2);
    });
  });

  it('surfaces a ProblemDetails validation error from a failed log', async () => {
    const user = userEvent.setup();
    const { ApiError } = await import('../api/client');
    vi.mocked(exerciseApi.searchExercises).mockResolvedValue({ items: [sampleExercise] });
    vi.mocked(exerciseApi.logExercise).mockRejectedValue(
      new ApiError(400, 'Bad Request', { errors: { durationMinutes: ['Too long.'] } }),
    );

    renderExercisePage();

    const search = await screen.findByRole('searchbox');
    await user.type(search, 'run');
    await user.click(await findResultButton());

    const input = await screen.findByLabelText('Duration');
    await user.clear(input);
    await user.type(input, '30');
    await user.click(screen.getByRole('button', { name: 'Log' }));

    expect(await screen.findByText('Too long.')).toBeInTheDocument();
  });

  it('shows an error state when the day fails to load', async () => {
    vi.mocked(exerciseApi.getExerciseDay).mockRejectedValue(new Error('Network error'));

    renderExercisePage();

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Network error');
  });
});
