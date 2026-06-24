import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import ProfilePage from './ProfilePage';
import * as profileApi from '../api/profile';
import { ApiError } from '../api/client';

vi.mock('../api/profile');

const sampleProfile: profileApi.ProfileResponse = {
  id: 'profile-1',
  userId: 'user-1',
  heightCm: 180,
  dateOfBirth: '1990-01-01',
  biologicalSex: 'Male',
  activityLevel: 'ModeratelyActive',
  primaryGoal: 'Maintain',
  units: 'Metric',
  dietType: 'None',
  allergies: null,
  latestWeightKg: 80,
  weightHistory: [],
  createdAt: '2026-06-24T00:00:00Z',
  updatedAt: '2026-06-24T00:00:00Z',
};

function renderProfilePage() {
  return render(
    <MemoryRouter>
      <ProfilePage />
    </MemoryRouter>,
  );
}

afterEach(() => {
  vi.restoreAllMocks();
});

describe('ProfilePage', () => {
  it('renders the form populated from the loaded profile', async () => {
    vi.mocked(profileApi.getProfile).mockResolvedValue(sampleProfile);

    renderProfilePage();

    await waitFor(() => {
      expect(screen.getByLabelText(/Height/)).toHaveValue(180);
    });
    expect(screen.getByLabelText(/Weight/)).toHaveValue(80);
    expect(screen.getByLabelText('Date of birth')).toHaveValue('1990-01-01');
  });

  it('submits changed fields plus units to updateProfile', async () => {
    vi.mocked(profileApi.getProfile).mockResolvedValue(sampleProfile);
    vi.mocked(profileApi.updateProfile).mockResolvedValue(sampleProfile);
    const user = userEvent.setup();

    renderProfilePage();

    await waitFor(() => {
      expect(screen.getByLabelText(/Height/)).toHaveValue(180);
    });

    await user.click(screen.getByRole('button', { name: /save profile/i }));

    await waitFor(() => {
      expect(profileApi.updateProfile).toHaveBeenCalledTimes(1);
    });
    expect(profileApi.updateProfile).toHaveBeenCalledWith(
      expect.objectContaining({
        units: 'Metric',
        heightCm: 180,
        weightKg: 80,
        dateOfBirth: '1990-01-01',
        biologicalSex: 'Male',
        activityLevel: 'ModeratelyActive',
        primaryGoal: 'Maintain',
        dietType: 'None',
      }),
    );
  });

  it('shows backend validation errors inline', async () => {
    vi.mocked(profileApi.getProfile).mockResolvedValue(sampleProfile);
    vi.mocked(profileApi.updateProfile).mockRejectedValue(
      new ApiError(400, 'Validation failed', {
        title: 'Validation failed',
        errors: { heightCm: ['Height must be between 50 and 272 cm.'] },
      }),
    );
    const user = userEvent.setup();

    renderProfilePage();

    await waitFor(() => {
      expect(screen.getByLabelText(/Height/)).toHaveValue(180);
    });

    await user.click(screen.getByRole('button', { name: /save profile/i }));

    await waitFor(() => {
      expect(screen.getByText('Height must be between 50 and 272 cm.')).toBeInTheDocument();
    });
    expect(screen.getByLabelText(/Height/)).toHaveAttribute('aria-invalid', 'true');
  });

  it('switches height and weight labels when the units toggle changes', async () => {
    vi.mocked(profileApi.getProfile).mockResolvedValue(sampleProfile);
    const user = userEvent.setup();

    renderProfilePage();

    await waitFor(() => {
      expect(screen.getByLabelText('Height (cm)')).toBeInTheDocument();
    });

    await user.click(screen.getByRole('radio', { name: /imperial/i }));

    expect(screen.getByLabelText('Height (in)')).toBeInTheDocument();
    expect(screen.getByLabelText('Weight (lb)')).toBeInTheDocument();
    // 180 cm ≈ 70.9 in
    expect(screen.getByLabelText('Height (in)')).toHaveValue(70.9);
  });
});
