export interface ChatMessage {
  id: string;
  role: 'user' | 'assistant' | 'system';
  content: string;
  timestamp: Date;
}

export interface ChatRequest {
  message: string;
}

export interface ChatResponse {
  message: string;
}

export interface TelemetryStatus {
  isActive: boolean;
  lastError?: string;
  lastExportTime?: Date | null;
}

export interface OtelExporterConfig {
  url: string;
  headers?: Record<string, string>;
}
