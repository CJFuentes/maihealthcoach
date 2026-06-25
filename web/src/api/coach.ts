import { apiFetch } from './client';

/**
 * Body for `POST /api/v1/me/coach/chat`.
 *
 * Omit `conversationId` to start a new conversation; supply it to continue an
 * existing one.
 */
export interface SendChatMessageRequest {
  message: string;
  conversationId?: string;
}

/**
 * Response from `POST /api/v1/me/coach/chat`.
 *
 * `disclaimer` is a non-null safety/medical disclaimer when the coach attached
 * one to this reply; `modelUsed` names the model that generated the reply (null
 * when unavailable).
 */
export interface ChatSendResponse {
  conversationId: string;
  messageId: string;
  reply: string;
  disclaimer: string | null;
  modelUsed: string | null;
  createdAt: string;
}

/**
 * A single conversation in the user's history list.
 *
 * `title` is null until the backend derives one; `messageCount` includes both
 * user and assistant messages.
 */
export interface ConversationSummary {
  id: string;
  title: string | null;
  messageCount: number;
  createdAt: string;
  updatedAt: string;
}

/**
 * Response from `GET /api/v1/me/coach/chat` — the user's conversations, most
 * recent first.
 */
export interface ConversationsListResponse {
  conversations: ConversationSummary[];
}

/**
 * A single persisted message within a conversation.
 */
export interface ConversationMessage {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  createdAt: string;
}

/**
 * Response from `GET /api/v1/me/coach/chat/{conversationId}` — the full message
 * thread for one conversation.
 */
export interface ConversationDetailResponse {
  id: string;
  title: string | null;
  messages: ConversationMessage[];
}

/**
 * A single suggested meal option.
 *
 * Each macro figure may be null when the backend could not estimate it;
 * `rationale` explains why the option fits the user's remaining targets.
 */
export interface MealOption {
  name: string;
  calories: number | null;
  proteinGrams: number | null;
  carbGrams: number | null;
  fatGrams: number | null;
  rationale: string;
}

/**
 * Response from `GET /api/v1/me/coach/meal-suggestions`.
 *
 * The `remaining*` fields report the user's remaining daily budget against
 * which the options were chosen; `disclaimer` is a non-null safety/medical
 * disclaimer when the coach attached one.
 */
export interface MealSuggestionsResponse {
  options: MealOption[];
  remainingCalories: number;
  remainingProteinGrams: number;
  remainingCarbGrams: number;
  remainingFatGrams: number;
  disclaimer: string | null;
}

/**
 * Response from `GET /api/v1/me/coach/nudge`.
 *
 * `tone` describes the intended voice of the nudge (null when unspecified);
 * `disclaimer` is a non-null safety/medical disclaimer when present.
 */
export interface NudgeResponse {
  message: string;
  tone: string | null;
  disclaimer: string | null;
}

/**
 * Sends a chat message to the coach.
 *
 * Throws {@link ApiError} with status 429 (rate-limited), 400 (validation),
 * 404 (conversation not found), or 502/503 (coach service unavailable) so the
 * caller can surface the appropriate state.
 */
export async function sendChatMessage(req: SendChatMessageRequest): Promise<ChatSendResponse> {
  return apiFetch<ChatSendResponse>('/api/v1/me/coach/chat', {
    method: 'POST',
    body: JSON.stringify(req),
  });
}

/**
 * Fetches the user's conversation history (summaries only), most recent first.
 *
 * Throws {@link ApiError} on a non-2xx response.
 */
export async function getConversations(): Promise<ConversationsListResponse> {
  return apiFetch<ConversationsListResponse>('/api/v1/me/coach/chat');
}

/**
 * Fetches the full message thread for a single conversation.
 *
 * Throws {@link ApiError} with status 404 when the conversation does not exist.
 */
export async function getConversation(conversationId: string): Promise<ConversationDetailResponse> {
  return apiFetch<ConversationDetailResponse>(
    `/api/v1/me/coach/chat/${encodeURIComponent(conversationId)}`,
  );
}

/**
 * Fetches personalised meal suggestions for the user's remaining daily budget.
 *
 * Throws {@link ApiError} with status 404 (no profile), 409 (incomplete
 * profile), or 502/503 (coach service unavailable) so the caller can prompt the
 * user to complete their profile or surface a retry.
 */
export async function getMealSuggestions(): Promise<MealSuggestionsResponse> {
  return apiFetch<MealSuggestionsResponse>('/api/v1/me/coach/meal-suggestions');
}

/**
 * Fetches a short motivational nudge for the user.
 *
 * Throws {@link ApiError} with status 502/503 (coach service unavailable). This
 * endpoint never returns 404/409 — a nudge is always available when the service
 * is up.
 */
export async function getNudge(): Promise<NudgeResponse> {
  return apiFetch<NudgeResponse>('/api/v1/me/coach/nudge');
}
