import { SignUp } from '@clerk/clerk-react';

/**
 * Sign-up page — renders Clerk's prebuilt `<SignUp>` component.
 *
 * Mounted at `/sign-up/*` (the wildcard lets Clerk render its internal flow
 * sub-routes). After signing up, the user lands on the authenticated app shell
 * via `fallbackRedirectUrl` ("/").
 */
export default function SignUpPage() {
  return (
    <section>
      <SignUp routing="path" path="/sign-up" signInUrl="/sign-in" fallbackRedirectUrl="/" />
    </section>
  );
}
