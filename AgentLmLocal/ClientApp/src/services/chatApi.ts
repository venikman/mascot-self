import type { ChatRequest, ChatResponse } from '../types';

export class ChatApi {
  private baseUrl: string;

  constructor(baseUrl = '') {
    this.baseUrl = baseUrl;
  }

  async sendMessage(message: string): Promise<string> {
    // Fetch is auto-instrumented, no manual spans needed
    const response = await fetch(`${this.baseUrl}/chat`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ message } as ChatRequest),
    });

    if (!response.ok) {
      throw new Error(`HTTP error! status: ${response.status}`);
    }

    const data: ChatResponse = await response.json();
    return data.message;
  }
}

export const chatApi = new ChatApi();
