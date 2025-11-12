import { useEffect, useState } from 'react';
import { telemetryService } from '../services/telemetry';
import type { TelemetryStatus } from '../types';

export function useOpenTelemetry() {
  const [status, setStatus] = useState<TelemetryStatus>({
    isActive: false,
    spanCount: 0,
  });

  useEffect(() => {
    // Initialize OpenTelemetry
    try {
      telemetryService.initialize();
      setStatus((prev) => ({ ...prev, isActive: true }));
    } catch (error) {
      console.error('Failed to initialize OpenTelemetry:', error);
      setStatus((prev) => ({
        ...prev,
        isActive: false,
        lastError: (error as Error).message,
      }));
    }

    // Track visibility changes
    const handleVisibilityChange = () => {
      telemetryService.recordVisibilityChange(document.hidden);
      updateSpanCount();
    };

    // Track errors
    const handleError = (event: ErrorEvent) => {
      telemetryService.recordError(event.error, {
        filename: event.filename,
        lineno: event.lineno,
        colno: event.colno,
      });
      updateSpanCount();
    };

    // Update span count periodically
    const updateSpanCount = () => {
      setStatus((prev) => ({
        ...prev,
        spanCount: telemetryService.getSpanCount(),
      }));
    };

    const intervalId = setInterval(updateSpanCount, 1000);

    document.addEventListener('visibilitychange', handleVisibilityChange);
    window.addEventListener('error', handleError);

    // Cleanup
    return () => {
      clearInterval(intervalId);
      document.removeEventListener('visibilitychange', handleVisibilityChange);
      window.removeEventListener('error', handleError);
      telemetryService.shutdown();
    };
  }, []);

  return status;
}

export function useTelemetrySpan(spanName: string) {
  useEffect(() => {
    const span = telemetryService.startSpan(spanName);

    return () => {
      span.end();
      telemetryService.incrementSpanCount();
    };
  }, [spanName]);
}
