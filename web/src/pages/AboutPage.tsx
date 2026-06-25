import { useTranslation } from 'react-i18next';

export default function AboutPage() {
  const { t } = useTranslation('about');

  return (
    <section>
      <h1>{t('title')}</h1>
      <p>{t('body')}</p>
    </section>
  );
}
