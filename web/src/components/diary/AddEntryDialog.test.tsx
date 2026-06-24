import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import AddEntryDialog from './AddEntryDialog';
import * as diaryApi from '../../api/diary';
import * as foodsApi from '../../api/foods';
import { ApiError } from '../../api/client';
import type { Food } from '../../api/foods';
import type { DiaryEntry } from '../../api/diary';

vi.mock('../../api/diary');
vi.mock('../../api/foods');

const sampleFood: Food = {
  id: 'food-1',
  name: 'Greek Yogurt',
  brand: 'Fage',
  nutrition: { calories: 120, proteinGrams: 18 },
  servingSizes: [
    { id: 'serving-1', label: '170 g pot', nutrition: { calories: 120, proteinGrams: 18 } },
    { id: 'serving-2', label: '100 g', nutrition: { calories: 71, proteinGrams: 10 } },
  ],
  defaultServingSizeId: 'serving-1',
};

const sampleEntry: DiaryEntry = {
  id: 'entry-9',
  foodId: 'food-1',
  foodName: 'Greek Yogurt',
  mealType: 'Lunch',
  date: '2026-06-24',
  quantity: 1,
  servingSizeId: 'serving-1',
  servingSizeLabel: '170 g pot',
  nutrition: { calories: 120, proteinGrams: 18 },
};

/** A resolved entry the add/update calls return; the dialog only forwards it. */
const savedEntry: DiaryEntry = { ...sampleEntry, id: 'entry-saved' };

beforeEach(() => {
  // jsdom does not implement the native <dialog> modal methods, so stub them.
  // showModal must set `open` so the dialog's contents are exposed to the
  // accessibility tree (otherwise role queries see a closed, hidden dialog).
  // The browser fires a `close` event from these; jsdom won't, so the tests
  // drive closure through the explicit Close/submit buttons instead.
  HTMLDialogElement.prototype.showModal = vi.fn(function showModal(this: HTMLDialogElement) {
    this.open = true;
  });
  HTMLDialogElement.prototype.close = vi.fn(function close(this: HTMLDialogElement) {
    this.open = false;
  });
});

afterEach(() => {
  vi.restoreAllMocks();
});

describe('AddEntryDialog', () => {
  it('searches, configures and posts a new entry, then reports success', async () => {
    const user = userEvent.setup();
    const onSuccess = vi.fn();
    vi.mocked(foodsApi.searchFoods).mockResolvedValue({ items: [sampleFood] });
    vi.mocked(diaryApi.addDiaryEntry).mockResolvedValue(savedEntry);

    render(
      <AddEntryDialog
        mode={{ kind: 'add', mealType: 'Breakfast' }}
        date="2026-06-24"
        onSuccess={onSuccess}
        onClose={vi.fn()}
      />,
    );

    // Search step: type a query and pick the matching food.
    await user.type(screen.getByLabelText(/search foods/i), 'yogurt');
    const result = await screen.findByRole('button', { name: /greek yogurt/i });
    await user.click(result);

    // Configure step: the chosen food's panel is now shown.
    const quantityInput = await screen.findByLabelText(/quantity/i);
    await user.clear(quantityInput);
    await user.type(quantityInput, '2');

    await user.click(screen.getByRole('button', { name: /add to diary/i }));

    await waitFor(() => {
      expect(diaryApi.addDiaryEntry).toHaveBeenCalledTimes(1);
    });
    expect(diaryApi.addDiaryEntry).toHaveBeenCalledWith({
      foodId: 'food-1',
      mealType: 'Breakfast',
      date: '2026-06-24',
      quantity: 2,
      servingSizeId: 'serving-1',
    });
    expect(onSuccess).toHaveBeenCalledWith(savedEntry);
  });

  it('loads the food in edit mode and PUTs the changed quantity', async () => {
    const user = userEvent.setup();
    const onSuccess = vi.fn();
    vi.mocked(foodsApi.getFood).mockResolvedValue(sampleFood);
    vi.mocked(diaryApi.updateDiaryEntry).mockResolvedValue(savedEntry);

    render(
      <AddEntryDialog
        mode={{ kind: 'edit', entry: sampleEntry }}
        date="2026-06-24"
        onSuccess={onSuccess}
        onClose={vi.fn()}
      />,
    );

    // Edit mode jumps straight to configuration once the food has loaded.
    const quantityInput = await screen.findByLabelText(/quantity/i);
    expect(foodsApi.getFood).toHaveBeenCalledWith('food-1');

    await user.clear(quantityInput);
    await user.type(quantityInput, '3');

    await user.click(screen.getByRole('button', { name: /save changes/i }));

    await waitFor(() => {
      expect(diaryApi.updateDiaryEntry).toHaveBeenCalledTimes(1);
    });
    expect(diaryApi.updateDiaryEntry).toHaveBeenCalledWith('entry-9', {
      mealType: 'Lunch',
      quantity: 3,
      servingSizeId: 'serving-1',
    });
    expect(onSuccess).toHaveBeenCalledWith(savedEntry);
  });

  it('surfaces server-side field validation errors from a 400 ProblemDetails', async () => {
    const user = userEvent.setup();
    vi.mocked(foodsApi.searchFoods).mockResolvedValue({ items: [sampleFood] });
    vi.mocked(diaryApi.addDiaryEntry).mockRejectedValue(
      new ApiError(400, 'Validation failed', {
        title: 'Validation failed',
        errors: { quantity: ['Must be positive.'] },
      }),
    );

    render(
      <AddEntryDialog
        mode={{ kind: 'add', mealType: 'Breakfast' }}
        date="2026-06-24"
        onSuccess={vi.fn()}
        onClose={vi.fn()}
      />,
    );

    await user.type(screen.getByLabelText(/search foods/i), 'yogurt');
    await user.click(await screen.findByRole('button', { name: /greek yogurt/i }));

    // A valid local quantity so submission reaches the server (and gets rejected).
    const quantityInput = await screen.findByLabelText(/quantity/i);
    await user.clear(quantityInput);
    await user.type(quantityInput, '1');

    await user.click(screen.getByRole('button', { name: /add to diary/i }));

    // The field-level message from ProblemDetails.errors is rendered inline.
    const fieldError = await screen.findByText('Must be positive.');
    expect(fieldError).toBeInTheDocument();
    expect(fieldError).toHaveAttribute('role', 'alert');
    // The quantity input is wired to its error for assistive tech.
    expect(quantityInput).toHaveAttribute('aria-invalid', 'true');

    // Surfacing the error must not falsely report success.
    expect(diaryApi.addDiaryEntry).toHaveBeenCalledTimes(1);
  });
});
