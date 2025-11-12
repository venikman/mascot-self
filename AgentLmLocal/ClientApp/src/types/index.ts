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

  // Batch span processor configuration
  batchSettings?: {
    maxQueueSize?: number;          // Default: 256
    maxExportBatchSize?: number;    // Default: 50
    scheduledDelayMillis?: number;  // Default: 2000 (2s)
  };

  // Auto-instrumentation configuration
  instrumentations?: {
    documentLoad?: boolean;    // Default: true
    userInteraction?: boolean; // Default: true
    fetch?: boolean;          // Default: true
    xhr?: boolean;            // Default: true
  };

  // Resource attributes configuration
  resourceAttributes?: {
    serviceName?: string;      // Default: 'ai-chat-frontend'
    serviceVersion?: string;   // Default: from package.json
    environment?: string;      // Default: import.meta.env.MODE
    [key: string]: string | undefined; // Custom attributes
  };
}
