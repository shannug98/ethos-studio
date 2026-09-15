export const echoKnowledge = [
  {
    keywords: ["hi", "hello", "hey", "good morning", "good evening"],
    response:
      "Hi! I’m ECHO, your Ethos Studio Assistant. I can help you explore workshops, classes, trainers, bookings, and studio information."
  },
  {
    keywords: ["what is ethos", "about ethos", "tell me about ethos"],
    response:
      "Ethos is a dance studio where you can explore dance classes, workshops, events, and creative experiences for different skill levels."
  },
  {
    keywords: ["what can you do", "help", "how can you help"],
    response:
      "I can help you find workshops, explore classes, learn about trainers, understand bookings, and find the studio location."
  },
  {
    keywords: ["class", "classes", "dance classes"],
    response:
      "You can explore our available dance classes from the Classes section. You can choose a class based on your preferred dance style and experience level."
  },
  {
    keywords: ["workshop", "workshops", "available workshops"],
    response:
      "You can explore the latest workshops in the Workshops section. Select a workshop to view its date, location, trainer, price, and registration details."
  },
  {
    keywords: ["trainer", "trainers", "teacher", "teachers"],
    response:
      "You can meet our trainers through the Trainers or Meet the Team section. Each trainer profile can provide information about their dance style and experience."
  },
  {
    keywords: ["location", "address", "where are you", "studio location"],
    response:
      "You can find the Ethos studio address and map from the Studio Location section."
  },
  {
    keywords: ["payment", "pay", "upi", "razorpay"],
    response:
      "Workshop payments are completed securely through the payment page. Please make sure your name, WhatsApp number, and email address are correct before paying."
  },
  {
    keywords: ["booking", "register", "registration"],
    response:
      "To register, open the workshop you are interested in, select the number of tickets, enter your contact details, and complete the payment."
  },
  {
    keywords: ["contact", "phone", "call", "whatsapp", "human", "team"],
    response:
      "You can contact the Ethos team directly using WhatsApp or phone. I can show you the contact options below."
  }
];

export function findBasicEchoAnswer(message) {
  const normalizedMessage = String(message || "")
    .toLowerCase()
    .trim()
    .replace(/[?!.,]/g, "");

  const matchedAnswer = echoKnowledge.find((item) =>
    item.keywords.some((keyword) => normalizedMessage.includes(keyword))
  );

  return matchedAnswer?.response || null;
}
