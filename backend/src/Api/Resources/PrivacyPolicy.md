# MAI Health Coach — Privacy Policy

**Last Updated:** 2026-06-25

MAI Health Coach ("we", "us", "the Service") helps you track nutrition, hydration, exercise, and body-weight goals, and provides AI-assisted coaching. This Privacy Policy explains what personal data we collect, why we collect it, who we share it with, how long we keep it, and the rights you have over your data under the EU General Data Protection Regulation (GDPR) and equivalent privacy laws.

This policy applies to the data you provide while using the Service through any of our clients (web, Android, iOS) and our backend API.

## 1. Who We Are (Data Controller)

MAI Health Coach is the data controller for the personal data described below. For any privacy-related question or request, contact us at **privacy@maihealthcoach.app**.

## 2. Data We Collect

We only collect data that is necessary to provide the Service. We do **not** sell your personal data.

### 2.1 Account & Identity

- **Email address** — used to identify your account and for service-related communication.
- **Authentication subject identifier** — a stable identifier issued by our authentication provider (Clerk) that links your sign-in identity to your MAI Health Coach account.

### 2.2 Health Profile

- **Height**
- **Date of birth**
- **Biological sex** (used for metabolic calculations)
- **Weight history** (your recorded body-weight measurements over time)
- **Activity level**
- **Primary goal** (e.g. lose, maintain, or gain weight)
- **Dietary preferences and allergies** (diet type and free-text allergy/intolerance notes)

These are health-related data and are treated as a special category of personal data under GDPR. We process them only to operate the features you use, on the basis of your consent and to perform our contract with you.

### 2.3 Activity & Logging Data

- **Food diary** — the foods, meals, servings, and quantities you log.
- **Water log** — your recorded hydration entries.
- **Exercise log** — the activities, durations, and estimated calories burned you log.
- **Custom foods** — foods (and their serving sizes and nutrition) you create yourself.
- **Goal overrides** — manual adjustments you make to your calorie, macronutrient, or water targets.

### 2.4 Coach Conversations

- **Coach conversations** — the messages you exchange with the MAI AI coach, including your prompts and the assistant's responses.

## 3. How We Use Your Data

We use your data to:

- Provide core functionality: track your diary, water, exercise, weight, and goals.
- Calculate personalized nutrition and hydration targets from your health profile.
- Power the MAI AI coach so it can give you contextual, personalized guidance.
- Look up nutrition information for foods you search for.
- Maintain the security and integrity of the Service.

We do **not** use your data for advertising or profiling unrelated to the Service.

## 4. Data Sharing & Processors

We share data only with the processors necessary to operate the Service, and only to the extent required for them to perform their function. Each processor is bound by contractual obligations to protect your data.

- **Anthropic (Claude)** — When you use the MAI AI coach, the relevant context (such as your coaching messages and a summary of the profile and logging data needed to answer you) is sent to Anthropic's Claude API to generate a response. Anthropic acts as a data processor on our behalf. We do not send more than is necessary to produce a coaching reply.
- **Open Food Facts** — When you search for a food, the search terms or barcode are sent to the Open Food Facts service to retrieve nutrition information. We do not send your identity or health profile to Open Food Facts.
- **Clerk** — Our authentication provider, which manages your sign-in identity.

We may also disclose data where required by law, or to protect the rights, safety, and security of our users and the Service.

## 5. International Transfers

Some of our processors (including Anthropic) may process data outside your country of residence. Where data is transferred internationally, we rely on appropriate safeguards such as Standard Contractual Clauses to ensure your data remains protected.

## 6. Data Retention

We retain your personal data for as long as your account is active. When you delete your account, we permanently erase your account and all data you own — including your health profile, weight history, food/water/exercise logs, custom foods, favorites, goal overrides, and coach conversations — as described in Section 8.

Shared reference data that is not personal to you (for example, globally seeded foods and exercise activities) is not deleted, because it does not identify you and is shared across all users.

## 7. Data Security

We protect your data using industry-standard measures, including:

- Encryption of data in transit (HTTPS/TLS).
- Authentication and per-user authorization so each user can access only their own data.
- The principle of least privilege for systems and personnel.
- Operational logging and monitoring to detect and respond to security events.

No system is perfectly secure, but we work continuously to safeguard your data.

## 8. Your GDPR Rights

You have the following rights over your personal data. To exercise any of them, use the in-app controls described below or contact **privacy@maihealthcoach.app**.

- **Right of access** — You can obtain a copy of the personal data we hold about you via `GET /api/v1/me/data-export`.
- **Right to data portability** — The same export (`GET /api/v1/me/data-export`) provides your data in a structured, machine-readable JSON format that you can take elsewhere.
- **Right to erasure ("right to be forgotten")** — You can permanently delete your account and all data you own via `DELETE /api/v1/me`. This action is irreversible.
- **Right to rectification** — You can correct or update your profile, goals, and logged data at any time through the Service's editing features.
- **Right to restrict or object to processing**, and the **right to withdraw consent** — You may contact us to restrict processing or withdraw consent; note that some processing is necessary to provide the Service, so withdrawing consent may require deleting your account.
- **Right to lodge a complaint** — You may complain to your local data protection authority if you believe we have not handled your data lawfully.

## 9. Children's Privacy

The Service is not directed at children under the age required by your jurisdiction to consent to data processing. We do not knowingly collect data from children below that age. If you believe a child has provided us data, contact us and we will delete it.

## 10. Changes to This Policy

We may update this Privacy Policy from time to time. When we make material changes, we will update the **Last Updated** date above and, where appropriate, notify you through the Service. Your continued use of the Service after changes take effect constitutes acceptance of the revised policy.

## 11. Contact Us

For any questions, requests, or concerns about your privacy or this policy, contact us at:

**privacy@maihealthcoach.app**
