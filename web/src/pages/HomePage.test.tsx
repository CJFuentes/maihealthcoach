import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import HomePage from './HomePage';
import * as healthApi from '../api/health';

function renderHomePage() {
  return render(
    <MemoryRouter>
      <HomePage />
    </MemoryRouter>,
  );
}

afterEach(() => {
  vi.restoreAllMocks();
});

describe('HomePage', () => {
  it('shows a loading state while the ping request is in flight', () => {
    vi.spyOn(healthApi, 'ping').mockReturnValue(new Promise<healthApi.PingResponse>(() => {}));

    renderHomePage();

    expect(screen.getByText('Checking backend…')).toBeInTheDocument();
  });

  it('shows the backend status on a successful ping', async () => {
    vi.spyOn(healthApi, 'ping').mockResolvedValue({
      service: 'mai-api',
      version: '1.0.0',
      timestamp: '2026-06-24T00:00:00Z',
    });

    renderHomePage();

    await waitFor(() => {
      expect(screen.getByText(/Backend online/)).toBeInTheDocument();
    });
    expect(screen.getByText(/mai-api/)).toBeInTheDocument();
    expect(screen.getByText(/1\.0\.0/)).toBeInTheDocument();
  });

  it('shows an alert when the ping request fails', async () => {
    vi.spyOn(healthApi, 'ping').mockRejectedValue(new Error('Network down'));

    renderHomePage();

    await waitFor(() => {
      expect(screen.getByRole('alert')).toBeInTheDocument();
    });
    expect(screen.getByRole('alert')).toHaveTextContent('Network down');
  });
});
