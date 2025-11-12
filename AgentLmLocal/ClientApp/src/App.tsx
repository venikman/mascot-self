import { TelemetryStatus } from './components/TelemetryStatus';
import { ChatContainer } from './components/ChatContainer';
import { useOpenTelemetry } from './hooks/useOpenTelemetry';
import './App.css';

function App() {
  const telemetryStatus = useOpenTelemetry();

  return (
    <div className="container">
      <header>
        <h1>AI Chat with OpenTelemetry</h1>
        <p className="subtitle">Frontend telemetry collected and sent to backend</p>
      </header>

      <TelemetryStatus status={telemetryStatus} />

      <ChatContainer />
    </div>
  );
}

export default App;
