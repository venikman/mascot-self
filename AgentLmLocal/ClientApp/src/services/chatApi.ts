import type { ChatRequest, ChatResponse } from '../types';
import { telemetryService } from './telemetry';

export class ChatApi {
  private baseUrl: string;

  constructor(baseUrl = '') {
    this.baseUrl = baseUrl;
  }

  async sendMessage(message: string): Promise<string> {
    const chatSpan = telemetryService.startSpan('chat.interaction', {
      'chat.message': message,
      'chat.message.length': message.length,
    });

    try {
      // Create span for API request
      const apiSpan = telemetryService.startSpan('chat.api.request', {
        'http.method': 'POST',
        'http.url': '/chat',
      });

      const requestStartTime = performance.now();

      try {
        const response = await fetch(`${this.baseUrl}/chat`, {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
          },
          body: JSON.stringify({ message } as ChatRequest),
        });

        const requestDuration = performance.now() - requestStartTime;
        apiSpan.setAttribute('http.status_code', response.status);
        apiSpan.setAttribute('http.response_time_ms', requestDuration);

        if (!response.ok) {
          throw new Error(`HTTP error! status: ${response.status}`);
        }

        const data: ChatResponse = await response.json();

        apiSpan.setStatus({ code: 1 }); // OK
        apiSpan.end();
        telemetryService.incrementSpanCount();

        chatSpan.setStatus({ code: 1 }); // OK
        chatSpan.setAttribute('chat.success', true);
        chatSpan.setAttribute('chat.response.length', data.message.length);
        chatSpan.end();
        telemetryService.incrementSpanCount();

        return data.message;
      } catch (error) {
        apiSpan.setStatus({ code: 2, message: (error as Error).message }); // ERROR
        apiSpan.recordException(error as Error);
        apiSpan.end();
        telemetryService.incrementSpanCount();

        throw error;
      }
    } catch (error) {
      chatSpan.setStatus({ code: 2, message: (error as Error).message }); // ERROR
      chatSpan.setAttribute('chat.success', false);
      chatSpan.setAttribute('chat.error', (error as Error).message);
      chatSpan.end();
      telemetryService.incrementSpanCount();

      throw error;
    }
  }
}

export const chatApi = new ChatApi();
