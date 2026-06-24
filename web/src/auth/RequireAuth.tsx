import { useAuth } from '@clerk/clerk-react';
import { Navigate, Outlet, useLocation } from 'react-router-dom';

/**
 * Protects child routes: redirects unauthenticated visitors to `/sign-in`.
 *
 * Renders nothing while Clerk is still initialising (`isLoaded === false`) to
 * avoid a flash of either the protected content or an incorrect redirect. Once
 * loaded, signed-out users are sent to `/sign-in` with a `redirect_url` query
 * param so they return to the originally requested page after authenticating.
 * Signed-in users get the nested route via `<Outlet />`.
 *
 * Usage (in App.tsx):
 *   <Route element={<RequireAuth />}>
 *     <Route path="/" element={<Layout />}>…</Route>
 *   </Route>
 */
export default function RequireAuth() {
  const { isLoaded, isSignedIn } = useAuth();
  const location = useLocation();

  if (!isLoaded) {
    // Clerk has not finished initialising — render nothing to avoid a flash.
    return null;
  }

  if (!isSignedIn) {
    const redirectUrl = encodeURIComponent(location.pathname + location.search);
    return <Navigate to={`/sign-in?redirect_url=${redirectUrl}`} replace />;
  }

  return <Outlet />;
}
