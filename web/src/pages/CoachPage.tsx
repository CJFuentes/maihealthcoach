import { useRef, useState, type KeyboardEvent } from 'react';
import { useTranslation } from 'react-i18next';
import ChatPanel from '../components/coach/ChatPanel';
import MealSuggestionsPanel from '../components/coach/MealSuggestionsPanel';
import NudgePanel from '../components/coach/NudgePanel';

type TabId = 'chat' | 'meal' | 'nudge';

interface TabDef {
  id: TabId;
  labelKey: 'tabs.chat' | 'tabs.mealSuggestions' | 'tabs.nudge';
}

const TABS: TabDef[] = [
  { id: 'chat', labelKey: 'tabs.chat' },
  { id: 'meal', labelKey: 'tabs.mealSuggestions' },
  { id: 'nudge', labelKey: 'tabs.nudge' },
];

/**
 * The Coach page: an ARIA tablist over three panels (chat, meal suggestions,
 * nudge). Panels are lazily mounted on first activation (`mountedTabs`) so their
 * data fetches only fire when the user opens that tab, but stay mounted
 * afterwards to preserve state when switching back. Tabpanels use the `hidden`
 * attribute rather than unmounting.
 *
 * Keyboard support follows the WAI-ARIA tabs pattern: Arrow Left/Right move
 * (and activate) between tabs with wraparound, Home/End jump to the first/last.
 */
export default function CoachPage() {
  const { t } = useTranslation('coach');
  const [activeTab, setActiveTab] = useState<TabId>('chat');
  const [mountedTabs, setMountedTabs] = useState<Set<TabId>>(new Set(['chat']));
  const tabRefs = useRef<(HTMLButtonElement | null)[]>([]);

  function handleTabChange(tab: TabId) {
    setActiveTab(tab);
    if (!mountedTabs.has(tab)) {
      setMountedTabs((prev) => new Set(prev).add(tab));
    }
  }

  function handleKeyDown(event: KeyboardEvent<HTMLButtonElement>, index: number) {
    let next: number | null = null;
    if (event.key === 'ArrowRight') {
      next = (index + 1) % TABS.length;
    } else if (event.key === 'ArrowLeft') {
      next = (index - 1 + TABS.length) % TABS.length;
    } else if (event.key === 'Home') {
      next = 0;
    } else if (event.key === 'End') {
      next = TABS.length - 1;
    }
    if (next === null) {
      return;
    }
    event.preventDefault();
    tabRefs.current[next]?.focus();
    handleTabChange(TABS[next].id);
  }

  return (
    <section>
      <h1>{t('title')}</h1>

      <div role="tablist" aria-label={t('title')}>
        {TABS.map((tab, i) => (
          <button
            key={tab.id}
            ref={(el) => {
              tabRefs.current[i] = el;
            }}
            role="tab"
            id={`coach-tab-${tab.id}`}
            type="button"
            aria-selected={activeTab === tab.id}
            aria-controls={`coach-panel-${tab.id}`}
            tabIndex={activeTab === tab.id ? 0 : -1}
            onClick={() => handleTabChange(tab.id)}
            onKeyDown={(e) => handleKeyDown(e, i)}
          >
            {t(tab.labelKey)}
          </button>
        ))}
      </div>

      <div
        role="tabpanel"
        id="coach-panel-chat"
        aria-labelledby="coach-tab-chat"
        hidden={activeTab !== 'chat'}
      >
        {mountedTabs.has('chat') && <ChatPanel isActive={activeTab === 'chat'} />}
      </div>

      <div
        role="tabpanel"
        id="coach-panel-meal"
        aria-labelledby="coach-tab-meal"
        hidden={activeTab !== 'meal'}
      >
        {mountedTabs.has('meal') && <MealSuggestionsPanel />}
      </div>

      <div
        role="tabpanel"
        id="coach-panel-nudge"
        aria-labelledby="coach-tab-nudge"
        hidden={activeTab !== 'nudge'}
      >
        {mountedTabs.has('nudge') && <NudgePanel />}
      </div>
    </section>
  );
}
