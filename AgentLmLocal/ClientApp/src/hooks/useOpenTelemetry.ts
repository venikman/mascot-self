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

    // Cleanup
    return () => {
      unsubscribe();
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
