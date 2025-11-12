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

  const getTimeSinceExport = () => {
    if (!status.lastExportTime) return 'No exports yet';
    const seconds = Math.floor((Date.now() - status.lastExportTime.getTime()) / 1000);
    if (seconds < 5) return 'Just now';
    if (seconds < 60) return `${seconds}s ago`;
    const minutes = Math.floor(seconds / 60);
    return `${minutes}m ago`;
  };

  return (
    <div className="telemetry-info">
      <div className="telemetry-status">
        <span className={`status-indicator ${statusClass}`}></span>
        <span>{statusText}</span>
      </div>
      {status.isActive && (
        <div className="telemetry-stats">
          <span>Last export: <strong>{getTimeSinceExport()}</strong></span>
        </div>
      )}
    </div>
  );
}
