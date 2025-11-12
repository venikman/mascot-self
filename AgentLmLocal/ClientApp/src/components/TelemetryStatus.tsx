import type { TelemetryStatus as TelemetryStatusType } from '../types';

interface TelemetryStatusProps {
  status: TelemetryStatusType;
}

export function TelemetryStatus({ status }: TelemetryStatusProps) {
  const statusClass = status.isActive ? 'active' : status.lastError ? 'error' : '';
  const statusText = status.isActive
    ? 'OpenTelemetry Active (Auto-instrumented)'
    : status.lastError
      ? `Error: ${status.lastError}`
      : 'Initializing...';

  return (
    <div className="telemetry-info">
      <div className="telemetry-status">
        <span className={`status-indicator ${statusClass}`}></span>
        <span>{statusText}</span>
      </div>
    </div>
  );
}
