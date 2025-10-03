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

  // Example prompt clicks
  setupExamplePrompts();
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
    // Use streaming endpoint for better user experience
    await handleStreamingResponse();
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

async function handleStreamingResponse() {
  const requestData = {
    messages: state.messages,
    model: elements.modelSelect.value || undefined,
    temperature: parseFloat(elements.temperatureSlider.value),
    maxTokens: undefined // Let backend decide
  };

  // Create assistant message container for streaming
  const assistantMessageElement = createStreamingAssistantMessage();
  let accumulatedContent = '';
  let streamingMetadata = null;
  let timeoutId;

  return new Promise((resolve, reject) => {
    // Set up timeout for streaming (30 seconds)
    timeoutId = setTimeout(() => {
      reject(new Error('Streaming request timed out after 30 seconds'));
    }, 30000);

    // Send the request data
    fetch('/v1/chat/completions/stream', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(requestData)
    }).then(response => {
      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }

      const reader = response.body.getReader();
      const decoder = new TextDecoder();

      function readStream() {
        reader.read().then(({ done, value }) => {
          if (done) {
            clearTimeout(timeoutId);
            finalizeStreamingMessage(accumulatedContent, streamingMetadata);
            resolve();
            return;
          }

          const chunk = decoder.decode(value, { stream: true });
          const lines = chunk.split('\n');

          for (const line of lines) {
            if (line.startsWith('data: ')) {
              try {
                const data = JSON.parse(line.substring(6));
                
                if (data.type === 'chunk' && data.content) {
                  accumulatedContent += data.content;
                  updateStreamingMessage(assistantMessageElement, accumulatedContent);
                } else if (data.type === 'complete' && data.metadata) {
                  streamingMetadata = data.metadata;
                }
              } catch (e) {
                console.warn('Failed to parse streaming data:', line);
              }
            }
          }

          readStream();
        }).catch(error => {
          clearTimeout(timeoutId);
          handleStreamingError(error, assistantMessageElement, accumulatedContent);
          reject(error);
        });
      }

      readStream();
    }).catch(error => {
      clearTimeout(timeoutId);
      handleStreamingError(error, assistantMessageElement, accumulatedContent);
      reject(error);
    });
  });
}

function handleStreamingError(error, messageElement, partialContent) {
  console.error('Streaming error:', error);
  
  // Update the message to show error state
  if (messageElement) {
    const contentElement = messageElement.querySelector('p');
    contentElement.innerHTML = `
      <div class="text-red-400">
        <strong>Streaming Error:</strong> ${error.message}
        <br><br>
        <em>Partial response:</em><br>
        ${formatMarkdown(partialContent || 'No content received')}
      </div>
    `;

    // Update metadata to show error
    const metaSpans = messageElement.querySelectorAll('.font-mono');
    metaSpans[0].textContent = 'Error';
    metaSpans[1].textContent = '0';
    metaSpans[2].textContent = '0.000000';

    // Hide TPS metric
    const tpsMetric = messageElement.querySelector('.tps-metric');
    if (tpsMetric) {
      tpsMetric.classList.add('hidden');
    }
  }

  // Remove the loading indicator if it's still showing
  removeLoadingIndicator();
}

// DOM Manipulation
function appendUserMessage(content) {
  const clone = templates.userMessage.content.cloneNode(true);
  clone.querySelector('p').textContent = content;
  elements.chatContainer.appendChild(clone);
  scrollToBottom();
}

// Markdown rendering for AI responses
function formatMarkdown(text) {
  return text
    .replace(/### (.*?)(\n|$)/g, '<h3 class="text-base font-semibold mt-4 mb-2 text-white">$1</h3>')
    .replace(/\*\*(.*?)\*\*/g, '<strong class="font-semibold text-white">$1</strong>')
    .replace(/\n\n/g, '</p><p class="mb-3">')
    .replace(/^(.+)$/gm, '<p class="mb-3">$1</p>');
}

function appendAssistantMessage(result) {
  const clone = templates.assistantMessage.content.cloneNode(true);

  // Set message content with markdown formatting
  clone.querySelector('p').innerHTML = formatMarkdown(result.content);

  // Set metadata
  const metaSpans = clone.querySelectorAll('.font-mono');
  metaSpans[0].textContent = result.model;
  metaSpans[1].textContent = result.tokensUsed.toLocaleString();
  metaSpans[2].textContent = result.estimatedCostUsd.toFixed(6);

  elements.chatContainer.appendChild(clone);
  scrollToBottom();
}

function createStreamingAssistantMessage() {
  const clone = templates.assistantMessage.content.cloneNode(true);
  
  // Clear initial content and metadata
  clone.querySelector('p').innerHTML = '';
  const metaSpans = clone.querySelectorAll('.font-mono');
  metaSpans[0].textContent = '...';
  metaSpans[1].textContent = '0';
  metaSpans[2].textContent = '0.000000';
  
  // Hide TPS metric initially
  const tpsMetric = clone.querySelector('.tps-metric');
  if (tpsMetric) {
    tpsMetric.classList.add('hidden');
  }

  elements.chatContainer.appendChild(clone);
  scrollToBottom();
  
  return clone;
}

function updateStreamingMessage(messageElement, content) {
  const contentElement = messageElement.querySelector('p');
  contentElement.innerHTML = formatMarkdown(content);
  scrollToBottom();
}

function finalizeStreamingMessage(content, metadata) {
  // Find the last assistant message (the streaming one)
  const assistantMessages = elements.chatContainer.querySelectorAll('.flex.justify-start');
  const lastAssistantMessage = assistantMessages[assistantMessages.length - 1];
  
  if (!lastAssistantMessage || !metadata) return;

  // Update final content
  const contentElement = lastAssistantMessage.querySelector('p');
  contentElement.innerHTML = formatMarkdown(content);

  // Update metadata
  const metaSpans = lastAssistantMessage.querySelectorAll('.font-mono');
  metaSpans[0].textContent = metadata.model;
  metaSpans[1].textContent = metadata.totalTokens.toLocaleString();
  metaSpans[2].textContent = metadata.estimatedCostUsd.toFixed(6);

  // Show and update TPS metric if available
  const tpsMetric = lastAssistantMessage.querySelector('.tps-metric');
  if (tpsMetric && metadata.averageTokensPerSecond > 0) {
    const tpsSpan = tpsMetric.querySelector('.font-mono');
    tpsSpan.textContent = metadata.averageTokensPerSecond.toFixed(1);
    tpsMetric.classList.remove('hidden');
  }

  // Update state totals
  state.messages.push({ role: 'assistant', content });
  state.totalTokens += metadata.totalTokens;
  state.totalCost += metadata.estimatedCostUsd;
  updateUI();
  
  scrollToBottom();
}

function showLoadingIndicator() {
  // Remove any existing loading indicator first
  removeLoadingIndicator();

  const clone = templates.loading.content.cloneNode(true);
  clone.id = 'loading-indicator';
  elements.chatContainer.appendChild(clone);
  scrollToBottom();
}

function removeLoadingIndicator() {
  // Remove by ID first (most common case)
  const indicator = document.getElementById('loading-indicator');
  if (indicator) {
    indicator.remove();
    return;
  }
  
  // Fallback: remove any loading indicators that might not have the ID
  const loadingIndicators = elements.chatContainer.querySelectorAll('[id="loading-indicator"]');
  loadingIndicators.forEach(indicator => indicator.remove());
  
  // Additional fallback: remove any elements containing "Thinking..." text
  const allElements = elements.chatContainer.querySelectorAll('*');
  allElements.forEach(element => {
    if (element.textContent && element.textContent.includes('Thinking...')) {
      const parentMessage = element.closest('.flex.justify-start');
      if (parentMessage) {
        parentMessage.remove();
      }
    }
  });
}

function setLoadingState(loading) {
  state.isLoading = loading;

  elements.sendButton.disabled = loading;
  elements.messageInput.disabled = loading;
  elements.clearButton.disabled = loading;

  if (loading) {
    showLoadingIndicator();
  } else {
    // Ensure loading indicator is removed with a small delay
    // to prevent race conditions with message appending
    setTimeout(() => {
      removeLoadingIndicator();
    }, 50);
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
    elements.chatContainer.scrollTo({
      top: elements.chatContainer.scrollHeight,
      behavior: 'smooth'
    });
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

// Example Prompts
function setupExamplePrompts() {
  const examplePrompts = document.querySelectorAll('.example-prompt');
  examplePrompts.forEach(prompt => {
    prompt.addEventListener('click', () => {
      const promptText = prompt.querySelector('p').textContent;
      elements.messageInput.value = promptText;
      elements.messageInput.focus();
      // Remove welcome message after clicking a prompt
      removeWelcomeMessage();
    });
  });
}

function removeWelcomeMessage() {
  const welcomeSection = document.querySelector('.welcome-section');
  if (welcomeSection) {
    welcomeSection.remove();
  }
}

// Initialize when DOM is ready
if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', init);
} else {
  init();
}