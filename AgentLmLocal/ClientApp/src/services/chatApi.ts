import type { ChatRequest, ChatResponse } from '../types';
import { telemetryService } from './telemetry';

export class ChatApi {
  private baseUrl: string;

  constructor(baseUrl = '') {
    this.baseUrl = baseUrl;
  }

  async sendMessage(message: string): Promise<string> {
    return telemetryService.withSpan(
      'chat.interaction',
      async (chatSpan) => {
        chatSpan.setAttribute('chat.message', message);
        chatSpan.setAttribute('chat.message.length', message.length);

        const responseText = await telemetryService.withSpan(
          'chat.api.request',
          async (apiSpan) => {
            apiSpan.setAttribute('http.method', 'POST');
            apiSpan.setAttribute('http.url', '/chat');

            const requestStartTime = performance.now();

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
            return data.message;
          }
        );

        chatSpan.setAttribute('chat.success', true);
        chatSpan.setAttribute('chat.response.length', responseText.length);

        return responseText;
      }
    );
  }
}

export const chatApi = new ChatApi();
