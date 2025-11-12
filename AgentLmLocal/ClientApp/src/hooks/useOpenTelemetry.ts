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
      setStatus({ isActive: true, lastExportTime: null });
    } catch (error) {
      console.error('Failed to initialize OpenTelemetry:', error);
      setStatus({
        isActive: false,
        lastError: (error as Error).message,
      });
    }

    // Poll for last export time updates
    const interval = setInterval(() => {
      const lastExportTime = telemetryService.getLastExportTime();
      setStatus((prev) => ({ ...prev, lastExportTime }));
    }, 1000);

    // Cleanup
    return () => {
      clearInterval(interval);
      telemetryService.shutdown();
    };
  }, []);

  return status;
}
