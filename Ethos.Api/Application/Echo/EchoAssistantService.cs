using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Ethos.Api.Application.Packages;
using Ethos.Api.Application.Workshops;
using Ethos.Api.Contracts.Echo;

namespace Ethos.Api.Application.Echo;

public class EchoAssistantService : IEchoAssistantService
{
    private readonly IWorkshopService _workshopService;
    private readonly IPackageService _packageService;
    private const string StudioPhone = "+91 83417 01113";
    private const string StudioPhoneClean = "+918341701113";
    private const string StudioWhatsAppNumber = "918341701113";

    public EchoAssistantService(
        IWorkshopService workshopService,
        IPackageService packageService)
    {
        _workshopService = workshopService;
        _packageService = packageService;
    }

    public async Task<EchoChatResponse> ProcessMessageAsync(
        EchoChatRequest request,
        CancellationToken cancellationToken = default)
    {
        var rawInput = request.Message?.Trim() ?? string.Empty;
        var normalized = rawInput.ToLowerInvariant();
        var convId = string.IsNullOrWhiteSpace(request.ConversationId)
            ? Guid.NewGuid().ToString("N")
            : request.ConversationId;

        // 1. GREETINGS & INTRO
        if (IsGreeting(normalized))
        {
            return new EchoChatResponse
            {
                ConversationId = convId,
                Message = "Hi! I’m ECHO, your Ethos Studio Assistant. 💃 I can help you explore upcoming workshops, dance classes, packages, trainers, bookings, payments, and studio information. What can I help you with today?",
                Actions = new List<EchoAction>
                {
                    new() { Label = "View Workshops", Type = "NAVIGATE", Value = "/workshops" },
                    new() { Label = "Explore Classes", Type = "NAVIGATE", Value = "/classes" },
                    new() { Label = "Studio Location", Type = "NAVIGATE", Value = "/workshops" },
                    new() { Label = "Chat on WhatsApp", Type = "WHATSAPP", Value = BuildWhatsAppUrl("Hi ECHO, I would like to know more about Ethos Dance Studio.") }
                }
            };
        }

        // 2. WORKSHOPS INQUIRY
        if (IsWorkshopQuery(normalized))
        {
            try
            {
                var workshops = await _workshopService.GetApprovedWorkshopsAsync();
                if (workshops != null && workshops.Count > 0)
                {
                    var upcoming = workshops
                        .OrderBy(w => w.WorkshopDate)
                        .Take(3)
                        .ToList();

                    var lines = upcoming.Select(w =>
                    {
                        var dateStr = w.WorkshopDate.ToString("d MMM yyyy");
                        return $"• **{w.Title}** ({w.DanceStyle}) — {dateStr} at {w.StartTime:hh\\:mm}, starting at ₹{w.StartingPrice}.";
                    });

                    var responseText = $"Here are the upcoming workshops currently open for registration:\n\n" +
                                       string.Join("\n", lines) +
                                       "\n\nAll prices are all-inclusive with zero hidden fees. Which one would you like to explore?";

                    var actions = new List<EchoAction>();
                    foreach (var w in upcoming.Take(2))
                    {
                        var slug = CreateSlug(w.Title);
                        actions.Add(new EchoAction
                        {
                            Label = $"View {w.Title}",
                            Type = "NAVIGATE",
                            Value = $"/workshops/{slug}"
                        });
                    }

                    actions.Add(new EchoAction { Label = "View All Workshops", Type = "NAVIGATE", Value = "/workshops" });

                    return new EchoChatResponse
                    {
                        ConversationId = convId,
                        Message = responseText,
                        Actions = actions
                    };
                }
            }
            catch
            {
                // fallback handled below
            }

            return new EchoChatResponse
            {
                ConversationId = convId,
                Message = "We have exciting dance workshops lined up including Hip-Hop, Contemporary, and community events like Micless with Merakee! You can view all upcoming workshops and secure your passes directly online.",
                Actions = new List<EchoAction>
                {
                    new() { Label = "View Workshops", Type = "NAVIGATE", Value = "/workshops" },
                    new() { Label = "Chat on WhatsApp", Type = "WHATSAPP", Value = BuildWhatsAppUrl("Hi ECHO, what workshops are happening this month?") }
                }
            };
        }

        // 3. CLASSES & PACKAGES
        if (IsClassOrPackageQuery(normalized))
        {
            try
            {
                var packages = await _packageService.GetActivePackagesAsync();
                var summary = "Ethos offers training across Hip-Hop, Contemporary, Jazz, and Freestyle for all levels — from complete beginners to advanced performers.\n\n";

                if (packages != null && packages.Count > 0)
                {
                    var pkgLines = packages.Take(3).Select(p => $"• **{p.Name}**: {(p.ClassLimit.HasValue ? $"{p.ClassLimit.Value} classes" : "Flexible sessions")} for ₹{p.Price} ({p.DurationDays} days validity).");
                    summary += "Current popular packages:\n" + string.Join("\n", pkgLines) + "\n\n";
                }
                else
                {
                    summary += "We offer flexible monthly and multi-class packages tailored for your schedule.\n\n";
                }

                summary += "Beginners are completely welcome! Would you like to check out the schedule?";

                return new EchoChatResponse
                {
                    ConversationId = convId,
                    Message = summary,
                    Actions = new List<EchoAction>
                    {
                        new() { Label = "Explore Classes", Type = "NAVIGATE", Value = "/classes" },
                        new() { Label = "Chat on WhatsApp", Type = "WHATSAPP", Value = BuildWhatsAppUrl("Hi ECHO, I want to know about beginner dance batches and fees.") }
                    }
                };
            }
            catch
            {
                return new EchoChatResponse
                {
                    ConversationId = convId,
                    Message = "Ethos offers daily and weekend dance batches in Hip-Hop, Contemporary, Jazz, and Freestyle with flexible class packages for all experience levels.",
                    Actions = new List<EchoAction>
                    {
                        new() { Label = "Explore Classes", Type = "NAVIGATE", Value = "/classes" }
                    }
                };
            }
        }

        // 4. TRAINERS & ARTISTS
        if (IsTrainerQuery(normalized))
        {
            return new EchoChatResponse
            {
                ConversationId = convId,
                Message = "Our faculty includes certified resident choreographers and acclaimed guest artists. Whether you're learning foundational grooves or refining competitive performance technique, our mentors personalize feedback for every dancer.",
                Actions = new List<EchoAction>
                {
                    new() { Label = "Meet Faculty", Type = "NAVIGATE", Value = "/classes" },
                    new() { Label = "Apply as Trainer", Type = "NAVIGATE", Value = "/trainer/application" },
                    new() { Label = "Chat on WhatsApp", Type = "WHATSAPP", Value = BuildWhatsAppUrl("Hi ECHO, who are the trainers teaching at Ethos?") }
                }
            };
        }

        // 5. BOOKINGS & REGISTRATION GUIDANCE
        if (IsBookingQuery(normalized))
        {
            return new EchoChatResponse
            {
                ConversationId = convId,
                Message = "Booking a workshop at Ethos is quick and instant:\n1. Choose your workshop from the Workshops page.\n2. Pick how many tickets you need (early tier pricing applies automatically).\n3. Enter your contact details and complete 100% secure payment via Razorpay.\n4. Your digital entry pass with a QR code is issued immediately!",
                Actions = new List<EchoAction>
                {
                    new() { Label = "Browse Workshops", Type = "NAVIGATE", Value = "/workshops" },
                    new() { Label = "Student Portal", Type = "NAVIGATE", Value = "/student/login" }
                }
            };
        }

        // 6. PAYMENTS, REFUNDS & RECEIPT SUPPORT
        if (IsPaymentQuery(normalized))
        {
            return new EchoChatResponse
            {
                ConversationId = convId,
                Message = "Here is our payment & billing guide:\n• **Payment Methods**: We accept UPI (Google Pay, PhonePe, Paytm), Debit/Credit Cards, and NetBanking via Razorpay.\n• **Transparent Pricing**: The displayed ticket price is final and includes all platform fees and taxes.\n• **Pass & Receipt**: Instant QR confirmation pass is shown right upon successful payment.\n• **Payment Issue or Deduction?**: If money was deducted without confirmation, banks usually auto-reconcile within 3–5 business days. Our team can also verify your payment ID manually.",
                Actions = new List<EchoAction>
                {
                    new() { Label = "Payment Support on WhatsApp", Type = "WHATSAPP", Value = BuildWhatsAppUrl("Hi Ethos Support, I have a payment confirmation question.") },
                    new() { Label = "Call Support", Type = "CALL", Value = StudioPhoneClean }
                }
            };
        }

        // 7. LOCATION, TIMINGS & CONTACT
        if (IsLocationOrContactQuery(normalized))
        {
            return new EchoChatResponse
            {
                ConversationId = convId,
                Message = $"Ethos Dance Studio locations & contact:\n\n📍 **Main Studio**: Lake Shore Mall, IDA Kukatpally, Hyderabad, Telangana 500072\n📍 **Studio 2**: Nizampet Road, Hyderabad\n⏰ **Hours**: Monday – Sunday: 7:00 AM – 9:30 PM\n📞 **Phone**: {StudioPhone}\n💬 **WhatsApp**: {StudioPhone}\n\nBoth locations feature dedicated parking and premium spring-flooring dance halls.",
                Actions = new List<EchoAction>
                {
                    new() { Label = "Chat on WhatsApp", Type = "WHATSAPP", Value = BuildWhatsAppUrl("Hi ECHO, I would like directions or studio hours for Ethos.") },
                    new() { Label = "Call Studio", Type = "CALL", Value = StudioPhoneClean },
                    new() { Label = "View Workshops", Type = "NAVIGATE", Value = "/workshops" }
                }
            };
        }

        // 8. HELPFUL FALLBACK / HUMAN HANDOFF
        return new EchoChatResponse
        {
            ConversationId = convId,
            Message = "I’m not completely sure about that. As your virtual assistant, I can help you explore workshops, check class packages, guide your bookings, or connect you with the Ethos team.",
            Actions = new List<EchoAction>
            {
                new() { Label = "Chat on WhatsApp", Type = "WHATSAPP", Value = BuildWhatsAppUrl($"Hi Ethos, I asked ECHO: \"{rawInput}\"") },
                new() { Label = "Call Studio", Type = "CALL", Value = StudioPhoneClean },
                new() { Label = "View Workshops", Type = "NAVIGATE", Value = "/workshops" },
                new() { Label = "Explore Classes", Type = "NAVIGATE", Value = "/classes" }
            }
        };
    }

    private static bool IsGreeting(string text)
    {
        var greetings = new[] { "hi", "hello", "hey", "hola", "namaste", "good morning", "good evening", "who are you", "what can you do", "help", "start" };
        return greetings.Any(g => text == g || text.StartsWith(g + " ") || text.EndsWith(" " + g));
    }

    private static bool IsWorkshopQuery(string text)
    {
        var terms = new[] { "workshop", "workshops", "event", "events", "hip-hop", "hip hop", "contemporary", "jazz", "merakee", "micless", "sing-along", "cypher", "weekend", "ticket", "tickets" };
        return terms.Any(text.Contains);
    }

    private static bool IsClassOrPackageQuery(string text)
    {
        var terms = new[] { "class", "classes", "package", "packages", "fee", "fees", "cost", "price", "monthly", "beginner", "beginners", "batches", "batch", "schedule" };
        return terms.Any(text.Contains);
    }

    private static bool IsTrainerQuery(string text)
    {
        var terms = new[] { "trainer", "trainers", "faculty", "instructor", "instructors", "teacher", "teachers", "choreographer", "artist", "who teaches" };
        return terms.Any(text.Contains);
    }

    private static bool IsBookingQuery(string text)
    {
        var terms = new[] { "how to book", "how to register", "book", "booking", "register", "registration", "seats", "capacity", "two people", "wrong phone", "my booking" };
        return terms.Any(text.Contains);
    }

    private static bool IsPaymentQuery(string text)
    {
        var terms = new[] { "pay", "payment", "upi", "gpay", "phonepe", "razorpay", "failed", "refund", "receipt", "deducted", "tax", "taxes", "platform fee" };
        return terms.Any(text.Contains);
    }

    private static bool IsLocationOrContactQuery(string text)
    {
        var terms = new[] { "where", "location", "address", "reach", "kukatpally", "nizampet", "contact", "phone", "number", "whatsapp", "call", "timing", "timings", "hours", "parking", "open" };
        return terms.Any(text.Contains);
    }

    private static string BuildWhatsAppUrl(string prompt)
    {
        return $"https://wa.me/{StudioWhatsAppNumber}?text={Uri.EscapeDataString(prompt)}";
    }

    private static string CreateSlug(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var chars = normalized
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .ToArray();
        var clean = new string(chars);
        clean = Regex.Replace(clean, @"[^a-z0-9\s-]", "");
        clean = Regex.Replace(clean, @"\s+", "-");
        clean = Regex.Replace(clean, @"-+", "-");
        return clean.Trim('-');
    }
}
