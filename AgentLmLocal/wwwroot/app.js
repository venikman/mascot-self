// OpenTelemetry imports from CDN (ESM)
import { WebTracerProvider } from 'https://cdn.jsdelivr.net/npm/@opentelemetry/sdk-trace-web@1.28.0/+esm';
import { Resource } from 'https://cdn.jsdelivr.net/npm/@opentelemetry/resources@1.28.0/+esm';
import { ATTR_SERVICE_NAME, ATTR_SERVICE_VERSION } from 'https://cdn.jsdelivr.net/npm/@opentelemetry/semantic-conventions@1.27.0/+esm';
import { BatchSpanProcessor } from 'https://cdn.jsdelivr.net/npm/@opentelemetry/sdk-trace-base@1.28.0/+esm';
import { OTLPTraceExporter } from 'https://cdn.jsdelivr.net/npm/@opentelemetry/exporter-trace-otlp-http@0.55.0/+esm';
import { trace, context } from 'https://cdn.jsdelivr.net/npm/@opentelemetry/api@1.9.0/+esm';

// Global state
let tracer;
let spanCount = 0;
const messagesDiv = document.getElementById('messages');
const messageInput = document.getElementById('messageInput');
const sendButton = document.getElementById('sendButton');
const chatForm = document.getElementById('chatForm');
const statusIndicator = document.getElementById('statusIndicator');
const statusText = document.getElementById('statusText');
const spanCountEl = document.getElementById('spanCount');

// Initialize OpenTelemetry
function initializeOpenTelemetry() {
    console.log('Initializing OpenTelemetry...');

    // Create resource with service metadata
    const resource = Resource.default().merge(
        new Resource({
            [ATTR_SERVICE_NAME]: 'ai-chat-frontend',
            [ATTR_SERVICE_VERSION]: '1.0.0',
            'deployment.environment': 'development',
            'browser.user_agent': navigator.userAgent,
            'browser.language': navigator.language,
        })
    );

    // Create OTLP exporter pointing to our proxy endpoint
    const exporter = new OTLPTraceExporter({
        url: '/otel/traces', // Our backend proxy endpoint
        headers: {
            'Content-Type': 'application/json',
        },
    });

    // Create tracer provider
    const provider = new WebTracerProvider({
        resource: resource,
    });

    // Add batch span processor with the exporter
    provider.addSpanProcessor(new BatchSpanProcessor(exporter, {
        maxQueueSize: 100,
        maxExportBatchSize: 10,
        scheduledDelayMillis: 500,
    }));

    // Register the provider
    provider.register();

    // Get tracer instance
    tracer = trace.getTracer('ai-chat-frontend', '1.0.0');

    // Update status
    statusIndicator.classList.add('active');
    statusText.textContent = 'OpenTelemetry Active';

    console.log('OpenTelemetry initialized successfully');

    // Create initial span for page load
    const pageLoadSpan = tracer.startSpan('page.load');
    pageLoadSpan.setAttribute('page.url', window.location.href);
    pageLoadSpan.setAttribute('page.title', document.title);
    pageLoadSpan.end();
    updateSpanCount();
}

// Update span count in UI
function updateSpanCount() {
    spanCount++;
    spanCountEl.textContent = spanCount;
}

// Add message to chat UI
function addMessage(content, role = 'user') {
    const messageDiv = document.createElement('div');
    messageDiv.className = `message ${role}`;

    const contentDiv = document.createElement('div');
    contentDiv.className = 'message-content';
    contentDiv.textContent = content;

    const timeDiv = document.createElement('div');
    timeDiv.className = 'message-time';
    timeDiv.textContent = new Date().toLocaleTimeString();

    messageDiv.appendChild(contentDiv);
    messageDiv.appendChild(timeDiv);
    messagesDiv.appendChild(messageDiv);

    // Scroll to bottom
    messagesDiv.scrollTop = messagesDiv.scrollHeight;
}

// Add loading indicator
function addLoadingIndicator() {
    const messageDiv = document.createElement('div');
    messageDiv.className = 'message assistant';
    messageDiv.id = 'loading-indicator';

    const contentDiv = document.createElement('div');
    contentDiv.className = 'message-content';
    contentDiv.innerHTML = '<span class="loading"></span> <span class="loading"></span> <span class="loading"></span>';

    messageDiv.appendChild(contentDiv);
    messagesDiv.appendChild(messageDiv);
    messagesDiv.scrollTop = messagesDiv.scrollHeight;

    return messageDiv;
}

// Remove loading indicator
function removeLoadingIndicator() {
    const loadingDiv = document.getElementById('loading-indicator');
    if (loadingDiv) {
        loadingDiv.remove();
    }
}

// Send chat message with OpenTelemetry tracing
async function sendMessage(message) {
    // Create parent span for the entire chat interaction
    const chatSpan = tracer.startSpan('chat.interaction');
    chatSpan.setAttribute('chat.message', message);
    chatSpan.setAttribute('chat.message.length', message.length);

    const chatContext = trace.setSpan(context.active(), chatSpan);

    try {
        // Add user message to UI
        addMessage(message, 'user');

        // Show loading indicator
        const loadingIndicator = addLoadingIndicator();

        // Create span for API request
        const apiSpan = tracer.startSpan('chat.api.request', {}, chatContext);
        apiSpan.setAttribute('http.method', 'POST');
        apiSpan.setAttribute('http.url', '/chat');

        const requestStartTime = performance.now();

        try {
            const response = await fetch('/chat', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({ message }),
            });

            const requestDuration = performance.now() - requestStartTime;
            apiSpan.setAttribute('http.status_code', response.status);
            apiSpan.setAttribute('http.response_time_ms', requestDuration);

            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }

            const data = await response.json();

            // Create span for UI update
            const uiSpan = tracer.startSpan('chat.ui.update', {}, chatContext);
            removeLoadingIndicator();
            addMessage(data.message, 'assistant');
            uiSpan.setAttribute('chat.response.length', data.message.length);
            uiSpan.end();
            updateSpanCount();

            apiSpan.setStatus({ code: 1 }); // OK
            apiSpan.end();
            updateSpanCount();

            chatSpan.setStatus({ code: 1 }); // OK
            chatSpan.setAttribute('chat.success', true);
        } catch (error) {
            removeLoadingIndicator();
            addMessage('Error: Failed to get response from AI', 'error');

            apiSpan.setStatus({ code: 2, message: error.message }); // ERROR
            apiSpan.recordException(error);
            apiSpan.end();
            updateSpanCount();

            chatSpan.setStatus({ code: 2, message: error.message }); // ERROR
            chatSpan.setAttribute('chat.success', false);
            chatSpan.setAttribute('chat.error', error.message);

            statusIndicator.classList.add('error');
            statusIndicator.classList.remove('active');
            statusText.textContent = 'Error occurred';

            setTimeout(() => {
                statusIndicator.classList.remove('error');
                statusIndicator.classList.add('active');
                statusText.textContent = 'OpenTelemetry Active';
            }, 3000);
        }
    } finally {
        chatSpan.end();
        updateSpanCount();
    }
}

// Handle form submission
chatForm.addEventListener('submit', async (e) => {
    e.preventDefault();

    const message = messageInput.value.trim();
    if (!message) return;

    // Create span for user input
    const inputSpan = tracer.startSpan('user.input');
    inputSpan.setAttribute('input.length', message.length);
    inputSpan.setAttribute('input.timestamp', Date.now());
    inputSpan.end();
    updateSpanCount();

    // Clear input and disable button
    messageInput.value = '';
    sendButton.disabled = true;

    // Send message
    await sendMessage(message);

    // Re-enable button and focus input
    sendButton.disabled = false;
    messageInput.focus();
});

// Track user interactions
messageInput.addEventListener('focus', () => {
    const span = tracer.startSpan('user.focus.input');
    span.end();
    updateSpanCount();
});

document.addEventListener('visibilitychange', () => {
    const span = tracer.startSpan('page.visibility.change');
    span.setAttribute('page.hidden', document.hidden);
    span.end();
    updateSpanCount();
});

// Handle errors
window.addEventListener('error', (event) => {
    const span = tracer.startSpan('error.unhandled');
    span.setAttribute('error.message', event.message);
    span.setAttribute('error.filename', event.filename);
    span.setAttribute('error.lineno', event.lineno);
    span.setAttribute('error.colno', event.colno);
    span.setStatus({ code: 2, message: event.message });
    span.end();
    updateSpanCount();
});

// Initialize on page load
document.addEventListener('DOMContentLoaded', () => {
    console.log('DOM Content Loaded');
    initializeOpenTelemetry();
    messageInput.focus();
});

console.log('app.js loaded');
