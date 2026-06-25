import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import NudgePanel from './NudgePanel';
import * as coachApi from '../../api/coach';
import { ApiError } from '../../api/client';

vi.mock('../../api/coach');

function renderPanel() {
  return render(
    <MemoryRouter>
      <NudgePanel />
    </MemoryRouter>,
  );
}

afterEach(() => {
  vi.restoreAllMocks();
});

describe('NudgePanel', () => {
  it('shows a loading status while the nudge loads', () => {
    vi.mocked(coachApi.getNudge).mockReturnValue(new Promise(() => {}));

    renderPanel();

    expect(screen.getByText('Loading your nudge…')).toBeInTheDocument();
  });

  it('renders the nudge message', async () => {
    vi.mocked(coachApi.getNudge).mockResolvedValue({
      message: 'You hit your protein goal three days running — keep it up!',
      tone: 'encouraging',
      disclaimer: null,
    });

    renderPanel();

    expect(await screen.findByText(/keep it up/i)).toBeInTheDocument();
  });

  it('renders the disclaimer when present', async () => {
    vi.mocked(coachApi.getNudge).mockResolvedValue({
      message: 'Stay hydrated.',
      tone: null,
      disclaimer: 'Not medical advice.',
    });

    renderPanel();

    expect(await screen.findByText(/Disclaimer: Not medical advice\./i)).toBeInTheDocument();
  });

  it('shows a service-unavailable alert on a 503', async () => {
    vi.mocked(coachApi.getNudge).mockRejectedValue(new ApiError(503, 'down'));

    renderPanel();

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent(/coach service is temporarily unavailable/i);
  });

  it('shows the error message on a network failure', async () => {
    vi.mocked(coachApi.getNudge).mockRejectedValue(new Error('network down'));

    renderPanel();

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent(/network down/i);
  });
});
