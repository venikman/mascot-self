import { useEffect, useRef } from 'react';
import { ChatMessage } from './ChatMessage';
import { ChatInput } from './ChatInput';
import { useChat } from '../hooks/useChat';

export function ChatContainer() {
  const { messages, isLoading, sendMessage } = useChat();
  const messagesEndRef = useRef<HTMLDivElement>(null);

  // Auto-scroll to bottom when new messages arrive
  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages]);

  return (
    <div className="chat-container">
      <div className="messages">
        {messages.map((message) => (
          <ChatMessage key={message.id} message={message} />
        ))}
        {isLoading && (
          <div className="message assistant">
            <div className="message-content">
              <span className="loading"></span>
              <span className="loading"></span>
              <span className="loading"></span>
            </div>
          </div>
        )}
        <div ref={messagesEndRef} />
      </div>

      <ChatInput onSend={sendMessage} isLoading={isLoading} />
    </div>
  );
}
