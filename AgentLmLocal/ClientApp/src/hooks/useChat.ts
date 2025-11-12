import { useState, useCallback } from 'react';
import type { ChatMessage } from '../types';
import { chatApi } from '../services/chatApi';
import { telemetryService } from '../services/telemetry';

export function useChat() {
  const [messages, setMessages] = useState<ChatMessage[]>([
    {
      id: '0',
      role: 'system',
      content:
        'Welcome! This chat demonstrates OpenTelemetry integration. All interactions are traced and sent to the backend for logging.',
      timestamp: new Date(),
    },
  ]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const sendMessage = useCallback(async (content: string) => {
    if (!content.trim()) return;

    // Record user input telemetry
    telemetryService.recordUserInput(content.length);

    // Add user message
    const userMessage: ChatMessage = {
      id: crypto.randomUUID(),
      role: 'user',
      content,
      timestamp: new Date(),
    };

    setMessages((prev) => [...prev, userMessage]);
    setIsLoading(true);
    setError(null);

    const uiSpan = telemetryService.startSpan('chat.ui.update');

    try {
      const responseText = await chatApi.sendMessage(content);

      // Add assistant message
      const assistantMessage: ChatMessage = {
        id: crypto.randomUUID(),
        role: 'assistant',
        content: responseText,
        timestamp: new Date(),
      };

      setMessages((prev) => [...prev, assistantMessage]);

      uiSpan.setAttribute('chat.response.length', responseText.length);
      uiSpan.setAttribute('chat.success', true);
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to get response';
      setError(errorMessage);

      // Add error message
      const errorMsg: ChatMessage = {
        id: crypto.randomUUID(),
        role: 'system',
        content: `Error: ${errorMessage}`,
        timestamp: new Date(),
      };

      setMessages((prev) => [...prev, errorMsg]);

      uiSpan.setAttribute('chat.success', false);
      uiSpan.setAttribute('chat.error', errorMessage);
      uiSpan.setStatus({ code: 2, message: errorMessage });
    } finally {
      uiSpan.end();
      telemetryService.incrementSpanCount();
      setIsLoading(false);
    }
  }, []);

  const clearMessages = useCallback(() => {
    setMessages([
      {
        id: '0',
        role: 'system',
        content:
          'Welcome! This chat demonstrates OpenTelemetry integration. All interactions are traced and sent to the backend for logging.',
        timestamp: new Date(),
      },
    ]);
    setError(null);
  }, []);

  return {
    messages,
    isLoading,
    error,
    sendMessage,
    clearMessages,
  };
}
