import type { TelemetryStatus as TelemetryStatusType } from '../types';

interface TelemetryStatusProps {
  status: TelemetryStatusType;
}

export function TelemetryStatus({ status }: TelemetryStatusProps) {
  const statusClass = status.isActive ? 'active' : status.lastError ? 'error' : '';
  const statusText = status.isActive
    ? 'OpenTelemetry Active'
    : status.lastError
      ? `Error: ${status.lastError}`
      : 'Initializing...';

  return (
    <div className="telemetry-info">
      <div className="telemetry-status">
        <span className={`status-indicator ${statusClass}`}></span>
        <span>{statusText}</span>
      </div>
      <div className="telemetry-stats">
        <span>
          Spans sent: <strong>{status.spanCount}</strong>
        </span>
      </div>
    </div>
  );
}
