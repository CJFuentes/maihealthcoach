import { SignIn } from '@clerk/clerk-react';

/**
 * Sign-in page — renders Clerk's prebuilt `<SignIn>` component.
 *
 * Mounted at `/sign-in/*` (the wildcard lets Clerk render its internal flow
 * sub-routes). Clerk handles credential entry, OAuth, MFA, and the post-sign-in
 * redirect. After signing in, the user is sent to `redirect_url` (set by
 * RequireAuth) when present, otherwise to `fallbackRedirectUrl` ("/").
 */
export default function SignInPage() {
  return (
    <section>
      <SignIn routing="path" path="/sign-in" signUpUrl="/sign-up" fallbackRedirectUrl="/" />
    </section>
  );
}
