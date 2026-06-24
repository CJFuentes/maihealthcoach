import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { useAuth } from '@clerk/clerk-react';
import RequireAuth from './RequireAuth';
import { makeAuthState } from '../test/clerkTestUtils';

// Factory is inlined (not an imported helper) because vi.mock is hoisted above
// imports — referencing an imported binding here throws at load time.
vi.mock('@clerk/clerk-react', () => ({ useAuth: vi.fn() }));

function renderAt(initialPath: string) {
  return render(
    <MemoryRouter initialEntries={[initialPath]}>
      <Routes>
        <Route path="/sign-in" element={<div>Sign In Page</div>} />
        <Route element={<RequireAuth />}>
          <Route path="/" element={<div>Protected Home</div>} />
          <Route path="/dashboard" element={<div>Protected Dashboard</div>} />
        </Route>
      </Routes>
    </MemoryRouter>,
  );
}

beforeEach(() => {
  vi.mocked(useAuth).mockReturnValue(makeAuthState({ isLoaded: true, isSignedIn: false }));
});

describe('RequireAuth', () => {
  it('renders nothing while Clerk is still loading', () => {
    vi.mocked(useAuth).mockReturnValue(makeAuthState({ isLoaded: false, isSignedIn: false }));

    const { container } = renderAt('/');

    expect(container).toBeEmptyDOMElement();
    expect(screen.queryByText('Protected Home')).not.toBeInTheDocument();
    expect(screen.queryByText('Sign In Page')).not.toBeInTheDocument();
  });

  it('redirects to /sign-in when the visitor is signed out', () => {
    renderAt('/');

    expect(screen.getByText('Sign In Page')).toBeInTheDocument();
    expect(screen.queryByText('Protected Home')).not.toBeInTheDocument();
  });

  it('still redirects signed-out visitors away from a deeper protected route', () => {
    renderAt('/dashboard');

    expect(screen.getByText('Sign In Page')).toBeInTheDocument();
    expect(screen.queryByText('Protected Dashboard')).not.toBeInTheDocument();
  });

  it('renders the protected outlet when the visitor is signed in', () => {
    vi.mocked(useAuth).mockReturnValue(makeAuthState({ isLoaded: true, isSignedIn: true }));

    renderAt('/');

    expect(screen.getByText('Protected Home')).toBeInTheDocument();
    expect(screen.queryByText('Sign In Page')).not.toBeInTheDocument();
  });
});
