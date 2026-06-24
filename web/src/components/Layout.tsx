import { UserButton } from '@clerk/clerk-react';
import { NavLink, Outlet } from 'react-router-dom';

export default function Layout() {
  return (
    <>
      <header>
        <nav aria-label="Primary">
          <NavLink to="/" end>
            Home
          </NavLink>
          <NavLink to="/about">About</NavLink>
          <NavLink to="/profile">Profile</NavLink>
          <NavLink to="/goals">Goals</NavLink>
        </nav>
        {/* Clerk account menu: profile + sign-out. Renders nothing when signed out.
            Sign-out redirect is configured once on <ClerkProvider> (afterSignOutUrl). */}
        <UserButton />
      </header>
      <main>
        <Outlet />
      </main>
    </>
  );
}
