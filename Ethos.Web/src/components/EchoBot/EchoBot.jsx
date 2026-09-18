import { useState, useRef, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { apiClient } from "../../services/apiClient";
import { workshopsApi } from "../../services/workshopsApi";
import { findBasicEchoAnswer } from "./echoKnowledge";
import "./EchoBot.css";

const emojis = ["😀", "😊", "😂", "😍", "🔥", "💃", "🕺", "❤️", "👍", "🙏"];

const starterSuggestions = [
  "What workshops are available?",
  "Tell me about dance classes & fees",
  "Where is Ethos located?",
  "How do payments and tickets work?"
];

export default function EchoBot() {
  const navigate = useNavigate();
  const [botState, setBotState] = useState("closed"); // "closed" | "preview" | "chat"
  const [message, setMessage] = useState("");
  const [messages, setMessages] = useState([]);
  const [isTyping, setIsTyping] = useState(false);
  const [conversationId, setConversationId] = useState("");
  const [showEmojiPicker, setShowEmojiPicker] = useState(false);

  const messagesEndRef = useRef(null);

  const scrollToBottom = () => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
  };

  useEffect(() => {
    if (botState === "chat" && messages.length > 0) {
      scrollToBottom();
    }
  }, [messages, isTyping, botState]);

  const shouldShowContactActions = (text) => {
    if (!text) return false;
    const normalizedText = text.toLowerCase();

    return [
      "contact",
      "whatsapp",
      "call",
      "human",
      "team",
      "support",
      "couldn’t connect",
      "could not connect"
    ].some((keyword) => normalizedText.includes(keyword));
  };

  const sendQuery = async (queryText) => {
    const trimmedMessage = queryText.trim();
    if (!trimmedMessage || isTyping) return;

    const userMessage = {
      id: crypto.randomUUID ? crypto.randomUUID() : `user-${Date.now()}`,
      sender: "user",
      text: trimmedMessage
    };

    setMessages((currentMessages) => [...currentMessages, userMessage]);
    setMessage("");
    setShowEmojiPicker(false);
    setIsTyping(true);

    // 1. Workshop-specific handling
    const workshopQuestion =
      /workshop|workshops|available workshops|upcoming workshop/i.test(trimmedMessage);

    if (workshopQuestion) {
      try {
        const res = await workshopsApi.getApprovedWorkshops();
        const workshops = Array.isArray(res) ? res : res?.items || [];

        const workshopAnswer =
          workshops.length > 0
            ? `I found ${workshops.length} available workshop${
                workshops.length === 1 ? "" : "s"
              }. You can open the Workshops page to view the details.`
            : "There are no workshops available at the moment.";

        setTimeout(() => {
          setMessages((currentMessages) => [
            ...currentMessages,
            {
              id: crypto.randomUUID ? crypto.randomUUID() : `echo-${Date.now()}`,
              sender: "echo",
              text: workshopAnswer,
              action: {
                label: "View Workshops",
                path: "/workshops"
              }
            }
          ]);
          setIsTyping(false);
        }, 350);
        return;
      } catch (wsErr) {
        console.warn("ECHO: Workshop API unavailable:", wsErr);
        setTimeout(() => {
          setMessages((currentMessages) => [
            ...currentMessages,
            {
              id: crypto.randomUUID ? crypto.randomUUID() : `echo-${Date.now()}`,
              sender: "echo",
              text:
                "I couldn’t load the live workshop list right now. Please open the Workshops page to view the latest details.",
              action: {
                label: "View Workshops",
                path: "/workshops"
              }
            }
          ]);
          setIsTyping(false);
        }, 350);
        return;
      }
    }

    // 2. Local basic answer layer
    const basicAnswer = findBasicEchoAnswer(trimmedMessage);

    if (basicAnswer) {
      setTimeout(() => {
        setMessages((currentMessages) => [
          ...currentMessages,
          {
            id: crypto.randomUUID ? crypto.randomUUID() : `echo-${Date.now()}`,
            sender: "echo",
            text: basicAnswer
          }
        ]);
        setIsTyping(false);
      }, 450);
      return;
    }

    // 3. Fallback to studio backend chat API if query is advanced
    try {
      const response = await apiClient.post("/api/echo/chat", {
        message: trimmedMessage,
        conversationId: conversationId || undefined
      });

      if (response?.conversationId) {
        setConversationId(response.conversationId);
      }

      setMessages((currentMessages) => [
        ...currentMessages,
        {
          id: crypto.randomUUID ? crypto.randomUUID() : `echo-${Date.now()}`,
          sender: "echo",
          text:
            response?.message ||
            "I’m still learning that. You can contact the Ethos team for help.",
          actions: response?.actions || []
        }
      ]);
    } catch (error) {
      console.error("ECHO request failed:", error);

      setMessages((currentMessages) => [
        ...currentMessages,
        {
          id: crypto.randomUUID ? crypto.randomUUID() : `echo-${Date.now()}`,
          sender: "echo",
          text:
            "I couldn’t connect to the studio service right now. I can still help with basic questions, or you can contact the Ethos team directly."
        }
      ]);
    } finally {
      setIsTyping(false);
    }
  };

  const handleSendMessage = () => {
    sendQuery(message);
  };

  const handleActionClick = (action) => {
    if (!action) return;

    if (action.type === "NAVIGATE") {
      navigate(action.value);
    } else if (action.type === "WHATSAPP") {
      window.open(action.value, "_blank", "noopener,noreferrer");
    } else if (action.type === "CALL") {
      window.location.href = `tel:${action.value}`;
    } else if (action.type === "URL") {
      window.open(action.value, "_blank", "noopener,noreferrer");
    }
  };

  const renderMessageText = (text) => {
    if (!text) return null;
    return text.split("\n").map((line, idx) => {
      const parts = line.split(/(\*\*.*?\*\*)/g);
      return (
        <p key={idx} className="echo-msg-paragraph">
          {parts.map((part, pIdx) => {
            if (part.startsWith("**") && part.endsWith("**")) {
              return <strong key={pIdx}>{part.slice(2, -2)}</strong>;
            }
            return part;
          })}
        </p>
      );
    });
  };

  return (
    <div className={`echo-widget ${botState === "chat" ? "echo-widget-open" : ""}`}>
      {botState === "chat" && (
        <section className="echo-panel" aria-label="ECHO AI Assistant">
          {/* HEADER */}
          <header className="echo-header">
            <button
              type="button"
              className="echo-header-icon"
              aria-label="New chat session"
              title="Reset conversation"
              onClick={() => {
                setMessages([]);
                setConversationId("");
              }}
            >
              <span />
              <span />
              <span />
            </button>

            <div className="echo-header-title">ECHO</div>

            <div className="echo-header-actions">
              <button
                type="button"
                className="echo-header-icon dots-icon"
                aria-label="Options"
                onClick={() => {
                  window.open("https://wa.me/918341701113", "_blank", "noopener,noreferrer");
                }}
                title="Connect on WhatsApp"
              >
                <span />
                <span />
                <span />
              </button>

              <button
                type="button"
                className="echo-header-icon close-icon"
                aria-label="Close ECHO"
                onClick={() => setBotState("closed")}
              >
                <span />
                <span />
              </button>
            </div>
          </header>

          {/* MAIN CONTENT AREA */}
          <main className="echo-content">
            {messages.length === 0 ? (
              <div className="echo-welcome-view">
                <div className="echo-avatar-area">
                  <div className="echo-avatar-ring">
                    <div className="echo-avatar">
                      <div className="echo-antenna" />
                      <div className="echo-face">
                        <span />
                        <span />
                      </div>
                    </div>

                    <div className="echo-hi-bubble">HI!</div>
                  </div>
                </div>

                <h2 className="echo-heading">What can I help with?</h2>

                {/* SUGGESTION CHIPS */}
                <div className="echo-starter-chips">
                  {starterSuggestions.map((suggestion, idx) => (
                    <button
                      key={idx}
                      type="button"
                      className="echo-starter-chip"
                      onClick={() => sendQuery(suggestion)}
                    >
                      {suggestion} ↗
                    </button>
                  ))}
                </div>
              </div>
            ) : (
              <div className="echo-messages-container">
                {messages.map((msg) => (
                  <div
                    key={msg.id}
                    className={`echo-msg-row echo-msg-row-${msg.sender}`}
                  >
                    {msg.sender === "echo" && (
                      <div className="echo-msg-avatar-badge">
                        <div className="echo-mini-avatar">
                          <span />
                          <span />
                        </div>
                      </div>
                    )}

                    <div className="echo-msg-bubble">
                      <div className="echo-msg-text">
                        {renderMessageText(msg.text)}
                      </div>

                      {msg.action && (
                        <div className="echo-action-buttons">
                          <button
                            type="button"
                            className="echo-action-btn echo-action-button"
                            onClick={() => navigate(msg.action.path)}
                          >
                            {msg.action.label} ↗
                          </button>
                        </div>
                      )}

                      {msg.actions && msg.actions.length > 0 && (
                        <div className="echo-action-buttons">
                          {msg.actions.map((act, aIdx) => (
                            <button
                              key={aIdx}
                              type="button"
                              className="echo-action-btn echo-action-button"
                              onClick={() => handleActionClick(act)}
                            >
                              {act.label} ↗
                            </button>
                          ))}
                        </div>
                      )}

                      {msg.sender === "echo" && shouldShowContactActions(msg.text) && (
                        <div className="echo-contact-actions">
                          <button
                            type="button"
                            className="echo-contact-btn echo-wa-btn"
                            onClick={() =>
                              window.open(
                                "https://wa.me/918341701113?text=Hi%20Ethos%2C%20I%20need%20assistance.",
                                "_blank",
                                "noopener,noreferrer"
                              )
                            }
                          >
                            Chat on WhatsApp ↗
                          </button>

                          <button
                            type="button"
                            className="echo-contact-btn echo-call-btn"
                            onClick={() => {
                              window.location.href = "tel:+918341701113";
                            }}
                          >
                            Call Studio ↗
                          </button>
                        </div>
                      )}
                    </div>
                  </div>
                ))}

                {isTyping && (
                  <div className="echo-msg-row echo-msg-row-echo">
                    <div className="echo-msg-avatar-badge">
                      <div className="echo-mini-avatar">
                        <span />
                        <span />
                      </div>
                    </div>
                    <div className="echo-msg-bubble echo-typing-bubble">
                      <div className="echo-typing">
                        <span />
                        <span />
                        <span />
                      </div>
                    </div>
                  </div>
                )}

                <div ref={messagesEndRef} />
              </div>
            )}

            {/* INPUT BOX */}
            <div className="echo-message-box">
              <textarea
                value={message}
                onChange={(event) => setMessage(event.target.value)}
                onKeyDown={(event) => {
                  if (event.key === "Enter" && !event.shiftKey) {
                    event.preventDefault();
                    handleSendMessage();
                  }
                }}
                placeholder="Message AI Assistant..."
                aria-label="Message ECHO"
                rows={1}
              />

              <div className="echo-input-footer">
                {/* EMOJI PICKER */}
                <div className="echo-emoji-wrapper">
                  <button
                    type="button"
                    className="echo-smiley-button"
                    aria-label="Open emoji picker"
                    onClick={() => setShowEmojiPicker((curr) => !curr)}
                  >
                    ☺
                  </button>

                  {showEmojiPicker && (
                    <div className="echo-emoji-picker">
                      {emojis.map((emoji) => (
                        <button
                          key={emoji}
                          type="button"
                          className="echo-emoji-option"
                          onClick={() => {
                            setMessage((curr) => `${curr}${emoji}`);
                            setShowEmojiPicker(false);
                          }}
                        >
                          {emoji}
                        </button>
                      ))}
                    </div>
                  )}
                </div>

                <div className="echo-input-actions">
                  <button
                    type="button"
                    className="echo-phone-button"
                    aria-label="Contact ECHO by phone"
                    onClick={() => {
                      window.location.href = "tel:+918341701113";
                    }}
                  >
                    <svg viewBox="0 0 24 24" aria-hidden="true">
                      <path d="M7.2 3.8l2.4-.6c.7-.2 1.4.2 1.7.9l1.1 2.8c.2.6.1 1.2-.4 1.6L10.5 10c1 2.1 2.7 3.8 4.8 4.8l1.5-1.5c.4-.4 1-.6 1.6-.4l2.8 1.1c.7.3 1.1 1 .9 1.7l-.6 2.4c-.2.8-.9 1.3-1.7 1.3C11.1 19.4 4.6 12.9 4.6 4.2c0-.8.5-1.5 1.3-1.7z" />
                    </svg>
                  </button>

                  <button
                    type="button"
                    className={`echo-send-button ${
                      message.trim() && !isTyping ? "echo-send-active" : ""
                    }`}
                    aria-label="Send message"
                    disabled={!message.trim() || isTyping}
                    onClick={handleSendMessage}
                  >
                    <svg viewBox="0 0 24 24" aria-hidden="true">
                      <path d="M4 4l16 8-16 8 3.5-8L4 4zm3.8 8h8.1" />
                    </svg>
                  </button>
                </div>
              </div>
            </div>
          </main>
        </section>
      )}

      {/* 1. CLOSED STATE: COMPACT CIRCULAR LAUNCHER BUTTON */}
      {botState === "closed" && (
        <button
          type="button"
          className="echo-launcher-closed"
          onClick={() => setBotState("preview")}
          aria-label="Open ECHO AI Assistant"
          title="Open ECHO AI Assistant"
        >
          <div className="echo-launcher-avatar">
            <div className="echo-launcher-face">
              <span />
              <span />
            </div>
            <div className="echo-online-dot" />
          </div>
        </button>
      )}

      {/* 2. PREVIEW STATE: SLID-OUT CAPSULE BAR WITH ECHO ACTION AND CLOSE BUTTON */}
      {botState === "preview" && (
        <div className="echo-launcher-preview-wrapper">
          <div
            className="echo-launcher-preview"
            onClick={() => setBotState("chat")}
            role="button"
            tabIndex={0}
            aria-label="Open ECHO Chat"
          >
            <div className="echo-launcher-avatar">
              <div className="echo-launcher-face">
                <span />
                <span />
              </div>
              <div className="echo-online-dot" />
            </div>

            <div className="echo-launcher-text">
              <strong>ECHO</strong>
              <span>AI Studio Assistant</span>
            </div>

            <span className="echo-launcher-arrow">↗</span>
          </div>

          <button
            type="button"
            className="echo-preview-close"
            onClick={(e) => {
              e.stopPropagation();
              setBotState("closed");
            }}
            aria-label="Close AI Bot"
            title="Close"
          >
            ×
          </button>
        </div>
      )}

      {/* 3. CHAT STATE: FLOATING CLOSE BUTTON */}
      {botState === "chat" && (
        <button
          type="button"
          className="echo-floating-close"
          onClick={() => setBotState("closed")}
          aria-label="Close ECHO"
        >
          ×
        </button>
      )}
    </div>
  );
}