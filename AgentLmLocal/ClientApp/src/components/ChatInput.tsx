import { useState, useRef, useEffect, type FormEvent } from 'react';

interface ChatInputProps {
  onSend: (message: string) => void;
  isLoading: boolean;
}

export function ChatInput({ onSend, isLoading }: ChatInputProps) {
  const [input, setInput] = useState('');
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (!isLoading && inputRef.current) {
      inputRef.current.focus();
    }
  }, [isLoading]);

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault();
    if (!input.trim() || isLoading) return;

    onSend(input);
    setInput('');
  };

  return (
    <form className="input-form" onSubmit={handleSubmit}>
      <input
        ref={inputRef}
        type="text"
        value={input}
        onChange={(e) => setInput(e.target.value)}
        placeholder="Type your message..."
        autoComplete="off"
        disabled={isLoading}
        required
      />
      <button type="submit" disabled={isLoading || !input.trim()}>
        {isLoading ? 'Sending...' : 'Send'}
      </button>
    </form>
  );
}
