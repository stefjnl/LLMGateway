// Chat Application State
const state = {
  messages: [],
  totalTokens: 0,
  totalCost: 0,
  isLoading: false,
  healthStatus: 'unknown'
};

// DOM Elements
const elements = {
  chatContainer: document.getElementById('chat-container'),
  messageInput: document.getElementById('message-input'),
  sendButton: document.getElementById('send-btn'),
  clearButton: document.getElementById('clear-btn'),
  modelSelect: document.getElementById('model-select'),
  temperatureSlider: document.getElementById('temperature'),
  tempValue: document.getElementById('temp-value'),
  totalTokens: document.getElementById('total-tokens'),
  totalCost: document.getElementById('total-cost'),
  healthStatus: document.getElementById('health-status')
};

// Templates
const templates = {
  userMessage: document.getElementById('user-message-template'),
  assistantMessage: document.getElementById('assistant-message-template'),
  loading: document.getElementById('loading-template')
};

// Initialize the application
function init() {
  setupEventListeners();
  setupTemperatureSlider();
  startHealthCheckPolling();
  updateUI();
}

// Event Listeners
function setupEventListeners() {
  // Send message on button click
  elements.sendButton.addEventListener('click', handleSendMessage);

  // Send message on Enter (but allow Shift+Enter for new lines)
  elements.messageInput.addEventListener('keydown', (e) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSendMessage();
    }
  });

  // Clear conversation
  elements.clearButton.addEventListener('click', handleClearConversation);

  // Update temperature display
  elements.temperatureSlider.addEventListener('input', updateTemperatureDisplay);
}

// Temperature Slider
function setupTemperatureSlider() {
  updateTemperatureDisplay();
}

function updateTemperatureDisplay() {
  const value = parseFloat(elements.temperatureSlider.value);
  elements.tempValue.textContent = value.toFixed(1);
}

// Message Handling
async function handleSendMessage() {
  const content = elements.messageInput.value.trim();

  if (!content || state.isLoading) {
    return;
  }

  // Add user message to conversation
  state.messages.push({ role: 'user', content });
  appendUserMessage(content);

  // Clear input and disable controls
  elements.messageInput.value = '';
  setLoadingState(true);

  try {
    const response = await sendChatRequest();

    if (!response.ok) {
      const errorData = await response.json();
      throw new Error(JSON.stringify(errorData));
    }

    const result = await response.json();

    // Add assistant response to conversation
    state.messages.push({ role: 'assistant', content: result.content });

    // Update totals
    state.totalTokens += result.tokensUsed;
    state.totalCost += result.estimatedCostUsd;

    // Display response
    appendAssistantMessage(result);

  } catch (error) {
    handleApiError(error);
  } finally {
    setLoadingState(false);
    scrollToBottom();
  }
}

// API Communication
async function sendChatRequest() {
  const requestData = {
    messages: state.messages,
    model: elements.modelSelect.value || undefined,
    temperature: parseFloat(elements.temperatureSlider.value),
    maxTokens: undefined // Let backend decide
  };

  return fetch('/v1/chat/completions', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(requestData)
  });
}

// DOM Manipulation
function appendUserMessage(content) {
  const clone = templates.userMessage.content.cloneNode(true);
  clone.querySelector('p').textContent = content;
  elements.chatContainer.appendChild(clone);
  scrollToBottom();
}

function appendAssistantMessage(result) {
  // Remove loading indicator if present
  removeLoadingIndicator();

  const clone = templates.assistantMessage.content.cloneNode(true);

  // Set message content
  clone.querySelector('p').textContent = result.content;

  // Set metadata
  const metaSpans = clone.querySelectorAll('.font-mono');
  metaSpans[0].textContent = result.model;
  metaSpans[1].textContent = result.tokensUsed.toLocaleString();
  metaSpans[2].textContent = result.estimatedCostUsd.toFixed(6);

  elements.chatContainer.appendChild(clone);
  scrollToBottom();
}

function showLoadingIndicator() {
  const clone = templates.loading.content.cloneNode(true);
  clone.id = 'loading-indicator';
  elements.chatContainer.appendChild(clone);
  scrollToBottom();
}

function removeLoadingIndicator() {
  const indicator = document.getElementById('loading-indicator');
  if (indicator) {
    indicator.remove();
  }
}

function setLoadingState(loading) {
  state.isLoading = loading;

  elements.sendButton.disabled = loading;
  elements.messageInput.disabled = loading;
  elements.clearButton.disabled = loading;

  if (loading) {
    showLoadingIndicator();
  } else {
    removeLoadingIndicator();
  }
}

// Error Handling
function handleApiError(error) {
  let errorData;

  try {
    errorData = JSON.parse(error.message);
  } catch {
    errorData = {
      title: 'Network Error',
      detail: 'Unable to connect to the server. Please check your connection and try again.',
      correlationId: null
    };
  }

  showErrorToast(errorData);
}

function showErrorToast(problemDetails) {
  const toast = document.createElement('div');
  toast.className = 'error-toast';

  toast.innerHTML = `
    <div class="error-title">${problemDetails.title || 'Error'}</div>
    <div class="error-detail">${problemDetails.detail || 'Something went wrong'}</div>
    ${problemDetails.correlationId ? `<div class="error-correlation-id">ID: ${problemDetails.correlationId}</div>` : ''}
  `;

  document.body.appendChild(toast);

  // Auto-remove after 5 seconds
  setTimeout(() => {
    toast.classList.add('fade-out');
    setTimeout(() => toast.remove(), 300);
  }, 5000);
}

// Health Monitoring
async function checkHealthStatus() {
  try {
    const response = await fetch('/health/ready');
    const isHealthy = response.ok;

    updateHealthIndicator(isHealthy);
    state.healthStatus = isHealthy ? 'healthy' : 'unhealthy';

  } catch (error) {
    updateHealthIndicator(false);
    state.healthStatus = 'unhealthy';
  }
}

function updateHealthIndicator(isHealthy) {
  const indicator = elements.healthStatus.querySelector('.w-3');
  const statusText = elements.healthStatus.querySelector('span');

  if (isHealthy) {
    indicator.className = 'w-3 h-3 rounded-full bg-green-500 animate-pulse';
    statusText.textContent = 'Healthy';
    statusText.className = 'text-sm text-white';
  } else {
    indicator.className = 'w-3 h-3 rounded-full bg-red-500';
    statusText.textContent = 'Unhealthy';
    statusText.className = 'text-sm text-red-400';
  }
}

function startHealthCheckPolling() {
  // Initial check
  checkHealthStatus();

  // Poll every 30 seconds
  setInterval(checkHealthStatus, 30000);
}

// Conversation Management
function handleClearConversation() {
  if (state.messages.length === 0) {
    return;
  }

  // Reset state
  state.messages = [];
  state.totalTokens = 0;
  state.totalCost = 0;

  // Clear UI
  elements.chatContainer.innerHTML = '';

  // Add back welcome message
  addWelcomeMessage();

  // Update display
  updateUI();
}

function addWelcomeMessage() {
  const welcomeDiv = document.createElement('div');
  welcomeDiv.className = 'text-center py-8';
  welcomeDiv.innerHTML = `
    <div class="max-w-2xl mx-auto">
      <h2 class="text-2xl font-bold text-white mb-4">Welcome to LLM Gateway</h2>
      <p class="text-gray-300 mb-6">Start a conversation with our intelligent routing system. Messages are automatically routed to the best available LLM model.</p>
      <div class="bg-slate-800/50 backdrop-blur-sm rounded-lg p-4 border border-white/10">
        <p class="text-sm text-gray-400">💡 <strong>Tip:</strong> Try asking about current events, technical questions, or creative writing tasks!</p>
      </div>
    </div>
  `;
  elements.chatContainer.appendChild(welcomeDiv);
}

// UI Updates
function updateUI() {
  elements.totalTokens.textContent = state.totalTokens.toLocaleString();
  elements.totalCost.textContent = state.totalCost.toFixed(6);
  elements.clearButton.disabled = state.messages.length === 0;
}

// Utilities
function scrollToBottom() {
  setTimeout(() => {
    elements.chatContainer.scrollTop = elements.chatContainer.scrollHeight;
  }, 100);
}

// Keyboard Navigation
document.addEventListener('keydown', (e) => {
  // Ctrl/Cmd + / to focus message input
  if ((e.ctrlKey || e.metaKey) && e.key === '/') {
    e.preventDefault();
    elements.messageInput.focus();
  }

  // Escape to clear input
  if (e.key === 'Escape' && document.activeElement === elements.messageInput) {
    elements.messageInput.value = '';
  }
});

// Initialize when DOM is ready
if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', init);
} else {
  init();
}