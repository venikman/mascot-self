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
      setStatus((prev) => ({ ...prev, isActive: true, spanCount: telemetryService.getSpanCount() }));
    } catch (error) {
      console.error('Failed to initialize OpenTelemetry:', error);
      setStatus((prev) => ({
        ...prev,
        isActive: false,
        lastError: (error as Error).message,
      }));
    }

    // Subscribe to span count changes
    const unsubscribe = telemetryService.subscribeToSpanCount((count) => {
      setStatus((prev) => ({ ...prev, spanCount: count }));
    });

    // Track visibility changes
    const handleVisibilityChange = () => {
      telemetryService.recordVisibilityChange(document.hidden);
    };

    // Track errors
    const handleError = (event: ErrorEvent) => {
      // Guard against null/undefined errors to prevent telemetry crashes
      if (event.error) {
        telemetryService.recordError(event.error, {
          filename: event.filename,
          lineno: event.lineno,
          colno: event.colno,
        });
      }
    };

    document.addEventListener('visibilitychange', handleVisibilityChange);
    window.addEventListener('error', handleError);

    // Cleanup
    return () => {
      unsubscribe();
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
