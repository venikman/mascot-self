import type { ChatMessage as ChatMessageType } from '../types';

interface ChatMessageProps {
  message: ChatMessageType;
}

export function ChatMessage({ message }: ChatMessageProps) {
  const messageClass = message.role === 'system' ? 'system-message' : message.role;

  return (
    <div className={`message ${messageClass}`}>
      <div className="message-content">{message.content}</div>
      <div className="message-time">{message.timestamp.toLocaleTimeString()}</div>
    </div>
  );
}
