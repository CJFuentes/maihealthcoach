import { Routes, Route } from 'react-router-dom';
import Layout from './components/Layout';
import HomePage from './pages/HomePage';
import AboutPage from './pages/AboutPage';
import ProfilePage from './pages/ProfilePage';
import GoalsPage from './pages/GoalsPage';
import NotFoundPage from './pages/NotFoundPage';
import SignInPage from './pages/SignInPage';
import SignUpPage from './pages/SignUpPage';
import RequireAuth from './auth/RequireAuth';

export default function App() {
  return (
    <Routes>
      {/*
        Public auth routes — no Layout, no auth guard. The trailing `/*` is
        required: Clerk's embedded <SignIn>/<SignUp> render their own internal
        sub-routes (OAuth callback, email verification, MFA), which react-router
        must let pass through to the component.
      */}
      <Route path="/sign-in/*" element={<SignInPage />} />
      <Route path="/sign-up/*" element={<SignUpPage />} />

      {/* Protected app shell — RequireAuth redirects signed-out users to /sign-in. */}
      <Route element={<RequireAuth />}>
        <Route path="/" element={<Layout />}>
          <Route index element={<HomePage />} />
          <Route path="about" element={<AboutPage />} />
          <Route path="profile" element={<ProfilePage />} />
          <Route path="goals" element={<GoalsPage />} />
          <Route path="*" element={<NotFoundPage />} />
        </Route>
      </Route>
    </Routes>
  );
}
