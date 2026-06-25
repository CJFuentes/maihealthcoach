// Module augmentation that gives `t()` full key autocompletion and type-checking
// from the English resource JSON. `import type` is required throughout because
// `verbatimModuleSyntax` forbids value imports that are only used as types.
import type common from './locales/en/common.json';
import type nav from './locales/en/nav.json';
import type home from './locales/en/home.json';
import type about from './locales/en/about.json';
import type profile from './locales/en/profile.json';
import type goals from './locales/en/goals.json';
import type scan from './locales/en/scan.json';
import type auth from './locales/en/auth.json';

declare module 'i18next' {
  interface CustomTypeOptions {
    defaultNS: 'common';
    resources: {
      common: typeof common;
      nav: typeof nav;
      home: typeof home;
      about: typeof about;
      profile: typeof profile;
      goals: typeof goals;
      scan: typeof scan;
      auth: typeof auth;
    };
  }
}
