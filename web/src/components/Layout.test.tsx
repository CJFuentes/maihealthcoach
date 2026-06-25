import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import { I18nextProvider } from 'react-i18next';
import Layout from './Layout';
import i18n from '../i18n';

// Clerk's <UserButton> would try to reach Clerk's servers; stub it to nothing.
vi.mock('@clerk/clerk-react', () => ({
  UserButton: () => null,
}));

function renderLayout() {
  return render(
    <I18nextProvider i18n={i18n}>
      <MemoryRouter initialEntries={['/']}>
        <Routes>
          <Route path="/" element={<Layout />}>
            <Route index element={<h1>Home page heading</h1>} />
          </Route>
        </Routes>
      </MemoryRouter>
    </I18nextProvider>,
  );
}

describe('Layout — accessibility landmarks', () => {
  it('renders a skip link targeting the main content', () => {
    renderLayout();

    const skip = screen.getByRole('link', { name: /skip to main content/i });
    expect(skip).toHaveAttribute('href', '#main-content');
  });

  it('exposes banner, navigation, and main landmarks', () => {
    renderLayout();

    expect(screen.getByRole('banner')).toBeInTheDocument();

    const nav = screen.getByRole('navigation', { name: /primary/i });
    expect(nav).toBeInTheDocument();

    const main = screen.getByRole('main');
    expect(main).toBeInTheDocument();
    expect(main).toHaveAttribute('id', 'main-content');
  });

  it('renders all five primary nav links', () => {
    renderLayout();

    const nav = screen.getByRole('navigation', { name: /primary/i });
    for (const name of [/home/i, /about/i, /profile/i, /goals/i, /scan/i]) {
      expect(screen.getByRole('link', { name })).toBeInTheDocument();
    }
    // The links live inside the primary nav landmark.
    expect(nav).toBeInTheDocument();
  });
});
