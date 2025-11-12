import { useEffect, useState } from 'react';
import { telemetryService } from '../services/telemetry';
import type { TelemetryStatus } from '../types';

export function useOpenTelemetry() {
  const [status, setStatus] = useState<TelemetryStatus>({
    isActive: false,
  });

  useEffect(() => {
    // Initialize OpenTelemetry
    try {
      telemetryService.initialize();
      setStatus({ isActive: true });
    } catch (error) {
      console.error('Failed to initialize OpenTelemetry:', error);
      setStatus({
        isActive: false,
        lastError: (error as Error).message,
      });
    }

    // Cleanup
    return () => {
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
    };
  }, [spanName]);
}
