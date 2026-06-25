import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ApiError } from '../../api/client';
import {
  getConversation,
  getConversations,
  sendChatMessage,
} from '../../api/coach';

type HistoryStatus =
  | { state: 'loading' }
  | { state: 'ready' }
  | { state: 'error'; message: string };

type SendStatus =
  | { state: 'idle' }
  | { state: 'sending' }
  | { state: 'rateLimited' }
  | { state: 'error'; message: string };

/**
 * A chat message as rendered in the log. `disclaimer` carries the assistant's
 * per-reply disclaimer (null for user messages and for messages loaded from
 * history, which the backend does not re-attach disclaimers to).
 */
interface DisplayMessage {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  disclaimer: string | null;
}

/**
 * The chat tab: loads the most recent conversation, renders the message log,
 * and sends new messages optimistically.
 *
 * `isActive` is true when the chat tab is the selected tab; the auto-scroll
 * effect only scrolls when active so switching back to an already-mounted tab
 * does not yank focus while a different tab is on screen.
 */
export default function ChatPanel({ isActive }: { isActive: boolean }) {
  const { t } = useTranslation('coach');
  const [historyStatus, setHistoryStatus] = useState<HistoryStatus>({ state: 'loading' });
  const [messages, setMessages] = useState<DisplayMessage[]>([]);
  const [conversationId, setConversationId] = useState<string | null>(null);
  const [inputValue, setInputValue] = useState('');
  const [sendStatus, setSendStatus] = useState<SendStatus>({ state: 'idle' });
  const messagesEndRef = useRef<HTMLDivElement>(null);

  // Liveness guard for the async send: handleSend awaits a network call and then
  // sets state, so an unmount mid-flight would otherwise warn (and waste work).
  const isMountedRef = useRef(true);
  useEffect(() => {
    return () => {
      isMountedRef.current = false;
    };
  }, []);

  // Resolve the history-load fallback string up front so the load effect does
  // not depend on `t` (whose identity changes on language switch and would
  // otherwise re-run the fetch). History loading is independent of UI language.
  const unknownErrorMsg = t('chat.unknownError');

  useEffect(() => {
    let cancelled = false;
    setHistoryStatus({ state: 'loading' });

    getConversations()
      .then((list) => {
        const latest = list.conversations[0];
        if (list.conversations.length === 0 || !latest) {
          if (!cancelled) {
            setHistoryStatus({ state: 'ready' });
          }
          return;
        }
        return getConversation(latest.id).then((detail) => {
          if (cancelled) {
            return;
          }
          setMessages(
            detail.messages.map((m) => ({
              id: m.id,
              role: m.role,
              content: m.content,
              disclaimer: null,
            })),
          );
          setConversationId(detail.id);
          setHistoryStatus({ state: 'ready' });
        });
      })
      .catch((error: unknown) => {
        if (cancelled) {
          return;
        }
        const message = error instanceof Error ? error.message : unknownErrorMsg;
        setHistoryStatus({ state: 'error', message });
      });

    return () => {
      cancelled = true;
    };
  }, [unknownErrorMsg]);

  // Auto-scroll to the newest message when this tab is active. scrollIntoView is
  // guarded for environments (jsdom) where it may be undefined.
  useEffect(() => {
    if (isActive) {
      messagesEndRef.current?.scrollIntoView?.({ behavior: 'smooth' });
    }
  }, [messages, isActive]);

  async function handleSend() {
    if (sendStatus.state === 'sending') {
      return;
    }
    const trimmed = inputValue.trim();
    if (!trimmed) {
      return;
    }

    const optimisticId = crypto.randomUUID();
    setMessages((prev) => [
      ...prev,
      { id: optimisticId, role: 'user', content: trimmed, disclaimer: null },
    ]);
    setInputValue('');
    setSendStatus({ state: 'sending' });

    try {
      const res = await sendChatMessage({
        message: trimmed,
        conversationId: conversationId ?? undefined,
      });
      if (!isMountedRef.current) {
        return;
      }
      setMessages((prev) => [
        ...prev,
        { id: res.messageId, role: 'assistant', content: res.reply, disclaimer: res.disclaimer },
      ]);
      setConversationId(res.conversationId);
      setSendStatus({ state: 'idle' });
    } catch (error: unknown) {
      if (!isMountedRef.current) {
        return;
      }
      if (error instanceof ApiError && error.status === 429) {
        // Keep the optimistic message so the user can see what they sent.
        setSendStatus({ state: 'rateLimited' });
        return;
      }
      // Roll back the optimistic message on every other failure.
      setMessages((prev) => prev.filter((m) => m.id !== optimisticId));
      if (error instanceof ApiError && error.status === 400) {
        setSendStatus({ state: 'error', message: t('chat.validationError') });
      } else if (error instanceof ApiError && error.status === 404) {
        // The conversation vanished server-side; start fresh next send.
        setConversationId(null);
        setSendStatus({ state: 'error', message: t('chat.conversationNotFound') });
      } else if (error instanceof ApiError && (error.status === 502 || error.status === 503)) {
        setSendStatus({ state: 'error', message: t('chat.serviceUnavailable') });
      } else {
        const message = error instanceof Error ? error.message : t('chat.unknownError');
        setSendStatus({ state: 'error', message: t('chat.sendError', { message }) });
      }
    }
  }

  return (
    <section>
      <h2>{t('tabs.chat')}</h2>

      {historyStatus.state === 'loading' && <p role="status">{t('chat.loading')}</p>}

      {historyStatus.state === 'error' && (
        <p role="alert" className="message message-error">
          {t('chat.historyLoadError', { message: historyStatus.message })}
        </p>
      )}

      <div role="log" aria-label={t('tabs.chat')} className="coach-chat-log">
        {messages.length === 0 && historyStatus.state === 'ready' && (
          <p className="hint">{t('chat.emptyHistory')}</p>
        )}
        {messages.map((msg) => (
          <div key={msg.id} className={`coach-message coach-message-${msg.role}`}>
            <span className="coach-message-role">
              {msg.role === 'user' ? t('chat.you') : t('chat.coach')}
            </span>
            <p className="coach-message-content">{msg.content}</p>
            {msg.disclaimer && (
              <p className="coach-disclaimer">{t('chat.disclaimer', { text: msg.disclaimer })}</p>
            )}
          </div>
        ))}
        <div ref={messagesEndRef} aria-hidden="true" />
      </div>

      {sendStatus.state === 'rateLimited' && (
        <p role="alert" className="message message-warning">
          {t('chat.rateLimited')}
        </p>
      )}

      {sendStatus.state === 'error' && (
        <p role="alert" className="message message-error">
          {sendStatus.message}
        </p>
      )}

      <form
        onSubmit={(e) => {
          e.preventDefault();
          void handleSend();
        }}
        noValidate
      >
        <div className="form-field">
          <label htmlFor="coach-message-input">{t('chat.inputLabel')}</label>
          <textarea
            id="coach-message-input"
            name="message"
            rows={3}
            value={inputValue}
            onChange={(e) => setInputValue(e.target.value)}
            placeholder={t('chat.inputPlaceholder')}
            disabled={sendStatus.state === 'sending'}
            onKeyDown={(e) => {
              if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault();
                void handleSend();
              }
            }}
          />
        </div>
        <button
          type="submit"
          disabled={sendStatus.state === 'sending' || inputValue.trim() === ''}
        >
          {sendStatus.state === 'sending' ? t('chat.sending') : t('chat.sendButton')}
        </button>
      </form>
    </section>
  );
}
