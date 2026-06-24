import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import ScanPage from './ScanPage';
import * as foodsApi from '../api/foods';
import { ApiError } from '../api/client';

// Mock only the network function; keep the real normalizeBarcode and the real
// FoodServiceUnavailableError class so `instanceof` checks in the page work.
vi.mock('../api/foods', async () => {
  const actual = await vi.importActual<typeof foodsApi>('../api/foods');
  return { ...actual, lookupBarcode: vi.fn() };
});

// Replace the webcam scanner with a stub that exposes a button to simulate a
// successful decode — this keeps getUserMedia/ZXing out of the headless test
// environment entirely while still exercising the scan → lookup wiring.
vi.mock('../components/BarcodeScanner', () => ({
  default: ({ onDetected }: { onDetected: (code: string) => void }) => (
    <button type="button" onClick={() => onDetected('5000159484695')}>
      Simulate scan
    </button>
  ),
}));

const sampleFood: foodsApi.FoodDto = {
  id: 'food-1',
  name: 'Dark Chocolate 70%',
  brand: 'CocoaCo',
  barcode: '5000159484695',
  source: 'OpenFoodFacts',
  nutritionPer100g: {
    energyKcal: 598,
    proteinG: 7.8,
    carbohydrateG: 45.9,
    fatG: 42.6,
    fiberG: 11,
    sugarsG: 24,
  },
  servingSizes: [{ label: '1 square', grams: 10 }],
};

function renderScanPage() {
  return render(
    <MemoryRouter>
      <ScanPage />
    </MemoryRouter>,
  );
}

async function lookUpManually(code: string) {
  const user = userEvent.setup();
  const input = screen.getByLabelText(/enter barcode manually/i);
  await user.clear(input);
  await user.type(input, code);
  await user.click(screen.getByRole('button', { name: /look up/i }));
}

afterEach(() => {
  vi.restoreAllMocks();
  vi.mocked(foodsApi.lookupBarcode).mockReset();
});

describe('ScanPage — manual entry lookup', () => {
  it('renders the matched food (name, brand, nutrition, serving sizes) on success', async () => {
    vi.mocked(foodsApi.lookupBarcode).mockResolvedValue(sampleFood);

    renderScanPage();
    await lookUpManually('5000159484695');

    expect(await screen.findByRole('heading', { name: 'Dark Chocolate 70%' })).toBeInTheDocument();
    expect(screen.getByText('CocoaCo')).toBeInTheDocument();
    expect(screen.getByText('598 kcal')).toBeInTheDocument();
    expect(screen.getByText(/1 square — 10 g/)).toBeInTheDocument();
    // Integration seam for #25 — the "Add to diary" affordance is present.
    expect(screen.getByRole('button', { name: /add to diary/i })).toBeInTheDocument();
  });

  it('shows a not-found state with a create-custom-food prompt on 404 (null result)', async () => {
    vi.mocked(foodsApi.lookupBarcode).mockResolvedValue(null);

    renderScanPage();
    await lookUpManually('0000000000000');

    expect(await screen.findByText(/no food found for barcode/i)).toBeInTheDocument();
    expect(screen.getByText('0000000000000')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /create a custom food/i })).toBeInTheDocument();
  });

  it('shows a distinct service-unavailable state with a retry on 503', async () => {
    const user = userEvent.setup();
    vi.mocked(foodsApi.lookupBarcode).mockRejectedValueOnce(
      new foodsApi.FoodServiceUnavailableError(),
    );

    renderScanPage();
    await lookUpManually('5000159484695');

    expect(
      await screen.findByText(/food lookup service is temporarily unavailable/i),
    ).toBeInTheDocument();

    // Retry re-issues the lookup; second attempt succeeds.
    vi.mocked(foodsApi.lookupBarcode).mockResolvedValueOnce(sampleFood);
    await user.click(screen.getByRole('button', { name: /retry/i }));

    expect(await screen.findByRole('heading', { name: 'Dark Chocolate 70%' })).toBeInTheDocument();
  });

  it('shows a generic error state on other API failures', async () => {
    vi.mocked(foodsApi.lookupBarcode).mockRejectedValue(new ApiError(500, 'boom'));

    renderScanPage();
    await lookUpManually('5000159484695');

    expect(await screen.findByText(/lookup failed \(error 500\)/i)).toBeInTheDocument();
  });

  it('rejects empty / non-numeric input without calling the API', async () => {
    renderScanPage();
    await lookUpManually('abc');

    expect(await screen.findByText(/please enter a valid numeric barcode/i)).toBeInTheDocument();
    expect(foodsApi.lookupBarcode).not.toHaveBeenCalled();
  });
});

describe('ScanPage — webcam scan path', () => {
  it('looks up the food when the scanner reports a decoded barcode', async () => {
    const user = userEvent.setup();
    vi.mocked(foodsApi.lookupBarcode).mockResolvedValue(sampleFood);

    renderScanPage();
    await user.click(screen.getByRole('button', { name: /simulate scan/i }));

    await waitFor(() => {
      expect(foodsApi.lookupBarcode).toHaveBeenCalledWith('5000159484695');
    });
    expect(await screen.findByRole('heading', { name: 'Dark Chocolate 70%' })).toBeInTheDocument();
  });
});
