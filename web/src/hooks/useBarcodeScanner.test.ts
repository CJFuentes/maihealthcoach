import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { createRef } from 'react';
import { isCameraSupported, useBarcodeScanner } from './useBarcodeScanner';

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

describe('isCameraSupported', () => {
  it('returns false when navigator.mediaDevices is unavailable (headless/CI)', () => {
    // jsdom provides no mediaDevices by default, but assert explicitly.
    vi.stubGlobal('navigator', {});
    expect(isCameraSupported()).toBe(false);
  });

  it('returns true when getUserMedia is present', () => {
    vi.stubGlobal('navigator', {
      mediaDevices: { getUserMedia: () => Promise.resolve({}) },
    });
    expect(isCameraSupported()).toBe(true);
  });
});

describe('useBarcodeScanner', () => {
  it('reports the unsupported state when no camera is available', () => {
    vi.stubGlobal('navigator', {});
    const videoRef = createRef<HTMLVideoElement>();
    const onDetected = vi.fn();

    const { result } = renderHook(() => useBarcodeScanner(videoRef, onDetected));

    expect(result.current.state).toBe('unsupported');
  });

  it('start() is a no-op and never touches the camera when unsupported', async () => {
    vi.stubGlobal('navigator', {});
    const videoRef = createRef<HTMLVideoElement>();
    const onDetected = vi.fn();

    const { result } = renderHook(() => useBarcodeScanner(videoRef, onDetected));

    await act(async () => {
      await result.current.start();
    });

    expect(result.current.state).toBe('unsupported');
    expect(onDetected).not.toHaveBeenCalled();
  });
});
