import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import ChatPanel from './ChatPanel';
import * as coachApi from '../../api/coach';
import { ApiError } from '../../api/client';

vi.mock('../../api/coach');

function renderChatPanel(isActive = true) {
  return render(
    <MemoryRouter>
      <ChatPanel isActive={isActive} />
    </MemoryRouter>,
  );
}

afterEach(() => {
  vi.restoreAllMocks();
});

describe('ChatPanel', () => {
  it('shows a loading status while history loads', () => {
    vi.mocked(coachApi.getConversations).mockReturnValue(new Promise(() => {}));

    renderChatPanel();

    expect(screen.getByText('Loading conversation…')).toBeInTheDocument();
  });

  it('shows the empty-history hint and a usable form when there are no conversations', async () => {
    vi.mocked(coachApi.getConversations).mockResolvedValue({ conversations: [] });

    renderChatPanel();

    expect(await screen.findByText(/no messages yet/i)).toBeInTheDocument();
    expect(screen.getByLabelText('Message')).toBeInTheDocument();
  });

  it('renders prior messages from the most recent conversation', async () => {
    vi.mocked(coachApi.getConversations).mockResolvedValue({
      conversations: [
        {
          id: 'conv-1',
          title: null,
          messageCount: 2,
          createdAt: '2026-06-25T00:00:00Z',
          updatedAt: '2026-06-25T00:00:00Z',
        },
      ],
    });
    vi.mocked(coachApi.getConversation).mockResolvedValue({
      id: 'conv-1',
      title: null,
      messages: [
        { id: 'm1', role: 'user', content: 'How much protein?', createdAt: '2026-06-25T00:00:00Z' },
        { id: 'm2', role: 'assistant', content: 'About 165 g.', createdAt: '2026-06-25T00:00:01Z' },
      ],
    });

    renderChatPanel();

    expect(await screen.findByText('How much protein?')).toBeInTheDocument();
    expect(screen.getByText('About 165 g.')).toBeInTheDocument();
  });

  it('sends a message and renders the reply', async () => {
    const user = userEvent.setup();
    vi.mocked(coachApi.getConversations).mockResolvedValue({ conversations: [] });
    vi.mocked(coachApi.sendChatMessage).mockResolvedValue({
      conversationId: 'conv-9',
      messageId: 'reply-1',
      reply: 'Drink more water.',
      disclaimer: null,
      modelUsed: 'claude',
      createdAt: '2026-06-25T00:00:00Z',
    });

    renderChatPanel();
    await screen.findByText(/no messages yet/i);

    await user.type(screen.getByLabelText('Message'), 'Hi coach');
    await user.click(screen.getByRole('button', { name: 'Send' }));

    expect(await screen.findByText('Drink more water.')).toBeInTheDocument();
    expect(screen.getByText('Hi coach')).toBeInTheDocument();
    expect(coachApi.sendChatMessage).toHaveBeenCalledWith({
      message: 'Hi coach',
      conversationId: undefined,
    });
  });

  it('disables the send button while sending', async () => {
    const user = userEvent.setup();
    vi.mocked(coachApi.getConversations).mockResolvedValue({ conversations: [] });
    let resolveSend: (value: coachApi.ChatSendResponse) => void = () => {};
    vi.mocked(coachApi.sendChatMessage).mockReturnValue(
      new Promise((resolve) => {
        resolveSend = resolve;
      }),
    );

    renderChatPanel();
    await screen.findByText(/no messages yet/i);

    await user.type(screen.getByLabelText('Message'), 'Hi');
    await user.click(screen.getByRole('button', { name: 'Send' }));

    const button = screen.getByRole('button', { name: 'Sending…' });
    expect(button).toBeDisabled();

    resolveSend({
      conversationId: 'c',
      messageId: 'r',
      reply: 'ok',
      disclaimer: null,
      modelUsed: null,
      createdAt: '2026-06-25T00:00:00Z',
    });
    await waitFor(() => {
      expect(screen.getByText('ok')).toBeInTheDocument();
    });
  });

  it('shows a rate-limit alert and keeps the optimistic message on a 429', async () => {
    const user = userEvent.setup();
    vi.mocked(coachApi.getConversations).mockResolvedValue({ conversations: [] });
    vi.mocked(coachApi.sendChatMessage).mockRejectedValue(new ApiError(429, 'rate limited'));

    renderChatPanel();
    await screen.findByText(/no messages yet/i);

    await user.type(screen.getByLabelText('Message'), 'Too fast');
    await user.click(screen.getByRole('button', { name: 'Send' }));

    expect(await screen.findByText(/sending messages too quickly/i)).toBeInTheDocument();
    // Optimistic user message is retained.
    expect(screen.getByText('Too fast')).toBeInTheDocument();
  });

  it('shows a service-unavailable alert on a 502 and rolls back the optimistic message', async () => {
    const user = userEvent.setup();
    vi.mocked(coachApi.getConversations).mockResolvedValue({ conversations: [] });
    vi.mocked(coachApi.sendChatMessage).mockRejectedValue(new ApiError(502, 'down'));

    renderChatPanel();
    await screen.findByText(/no messages yet/i);

    await user.type(screen.getByLabelText('Message'), 'Hello');
    await user.click(screen.getByRole('button', { name: 'Send' }));

    expect(await screen.findByText(/coach service is temporarily unavailable/i)).toBeInTheDocument();
    expect(screen.queryByText('Hello')).not.toBeInTheDocument();
  });

  it('renders an assistant disclaimer when present', async () => {
    const user = userEvent.setup();
    vi.mocked(coachApi.getConversations).mockResolvedValue({ conversations: [] });
    vi.mocked(coachApi.sendChatMessage).mockResolvedValue({
      conversationId: 'c',
      messageId: 'r',
      reply: 'Eat well.',
      disclaimer: 'Not medical advice.',
      modelUsed: null,
      createdAt: '2026-06-25T00:00:00Z',
    });

    renderChatPanel();
    await screen.findByText(/no messages yet/i);

    await user.type(screen.getByLabelText('Message'), 'Tips?');
    await user.click(screen.getByRole('button', { name: 'Send' }));

    expect(await screen.findByText(/Disclaimer: Not medical advice\./i)).toBeInTheDocument();
  });

  it('submits on Enter without Shift', async () => {
    const user = userEvent.setup();
    vi.mocked(coachApi.getConversations).mockResolvedValue({ conversations: [] });
    vi.mocked(coachApi.sendChatMessage).mockResolvedValue({
      conversationId: 'c',
      messageId: 'r',
      reply: 'Replied.',
      disclaimer: null,
      modelUsed: null,
      createdAt: '2026-06-25T00:00:00Z',
    });

    renderChatPanel();
    await screen.findByText(/no messages yet/i);

    await user.type(screen.getByLabelText('Message'), 'Quick question{Enter}');

    expect(await screen.findByText('Replied.')).toBeInTheDocument();
    expect(coachApi.sendChatMessage).toHaveBeenCalledTimes(1);
  });

  it('does not submit on Shift+Enter', async () => {
    const user = userEvent.setup();
    vi.mocked(coachApi.getConversations).mockResolvedValue({ conversations: [] });

    renderChatPanel();
    await screen.findByText(/no messages yet/i);

    await user.type(screen.getByLabelText('Message'), 'Line one{Shift>}{Enter}{/Shift}');

    expect(coachApi.sendChatMessage).not.toHaveBeenCalled();
  });

  it('shows a history-load error but still renders a working form', async () => {
    const user = userEvent.setup();
    vi.mocked(coachApi.getConversations).mockRejectedValue(new Error('network down'));
    vi.mocked(coachApi.sendChatMessage).mockResolvedValue({
      conversationId: 'c',
      messageId: 'r',
      reply: 'Still works.',
      disclaimer: null,
      modelUsed: null,
      createdAt: '2026-06-25T00:00:00Z',
    });

    renderChatPanel();

    expect(await screen.findByText(/could not load conversation history/i)).toBeInTheDocument();

    await user.type(screen.getByLabelText('Message'), 'Hi');
    await user.click(screen.getByRole('button', { name: 'Send' }));

    expect(await screen.findByText('Still works.')).toBeInTheDocument();
  });
});
