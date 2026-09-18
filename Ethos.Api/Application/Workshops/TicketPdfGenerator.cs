using System.Globalization;
using System.Text;
using QRCoder;

namespace Ethos.Api.Application.Workshops;

public record TicketPdfModel(
    string WorkshopTitle,
    string WorkshopDate,
    string WorkshopTime,
    string Venue,
    string AttendeeName,
    string BookingReference,
    string TicketNumber,
    string QrToken,
    string Status = "CONFIRMED / ACTIVE"
);

public static class TicketPdfGenerator
{
    public static byte[] Generate(TicketPdfModel model)
    {
        var sb = new StringBuilder();

        // Page Canvas: A4 = 595.28 x 841.89 points
        sb.AppendLine("q"); // Push graphic state

        // 1. PAGE BACKGROUND (Light Off-White #F8FAFC)
        sb.AppendLine("0.97 0.98 0.99 rg");
        sb.AppendLine("0 0 595.28 841.89 re f");

        // 2. HEADER BANNER (Y: 750 to 842, Height: 92)
        sb.AppendLine("1 1 1 rg"); // Pure White Header
        sb.AppendLine("0 750 595.28 92 re f");

        // Red & Black Accent bar below header (Y: 746, Height: 4)
        sb.AppendLine("0 0 0 rg"); // Black segment
        sb.AppendLine("0 746 70 4 re f");
        sb.AppendLine("1 0.16 0.23 rg"); // Ethos Red #FF2A3A
        sb.AppendLine("70 746 525.28 4 re f");

        // Header Title: ETHOS DANCE STUDIO
        sb.AppendLine("BT");
        sb.AppendLine("/F2 19 Tf");
        sb.AppendLine("0.06 0.09 0.16 rg"); // Deep Slate #0F172A
        sb.AppendLine("36 798 Td");
        sb.AppendLine($"({EscapePdf(model.WorkshopTitle.Length > 0 ? "ETHOS DANCE STUDIO" : "ETHOS DANCE STUDIO")}) Tj");
        sb.AppendLine("ET");

        // Header Subtitle: OFFICIAL WORKSHOP PASS | DIGITAL ADMISSION PASS
        sb.AppendLine("BT");
        sb.AppendLine("/F2 8.5 Tf");
        sb.AppendLine("0.88 0.33 0.22 rg"); // Warm Peach #E05338
        sb.AppendLine("36 784 Td");
        sb.AppendLine($"(OFFICIAL WORKSHOP PASS  |  DIGITAL ADMISSION PASS) Tj");
        sb.AppendLine("ET");

        // Header Right: Status Tag Container (X: 390, Y: 770, W: 169, H: 38)
        sb.AppendLine("0.95 0.96 0.98 rg"); // Fill #F1F5F9
        sb.AppendLine("390 770 169 38 re f");
        sb.AppendLine("0.80 0.84 0.88 RG"); // Border #CBD5E1
        sb.AppendLine("1 w");
        sb.AppendLine("390 770 169 38 re S");

        sb.AppendLine("BT");
        sb.AppendLine("/F2 9 Tf");
        sb.AppendLine("0.06 0.09 0.16 rg");
        sb.AppendLine("408 791 Td");
        sb.AppendLine($"({EscapePdf(model.Status)}) Tj");
        sb.AppendLine("ET");

        sb.AppendLine("BT");
        sb.AppendLine("/F1 7.5 Tf");
        sb.AppendLine("0.39 0.45 0.55 rg");
        sb.AppendLine("408 779 Td");
        sb.AppendLine($"(Ethos Official Entry Pass) Tj");
        sb.AppendLine("ET");

        // 3. MAIN TICKET CARD CONTAINER (X: 36, Y: 140, W: 523.28, H: 586)
        sb.AppendLine("1 1 1 rg"); // White card background
        sb.AppendLine("36 140 523.28 586 re f");
        sb.AppendLine("0.88 0.91 0.94 RG"); // Border #E2E8F0
        sb.AppendLine("1.5 w");
        sb.AppendLine("36 140 523.28 586 re S");

        // Top Card Strip Banner (Y: 672, H: 42)
        sb.AppendLine("0.97 0.98 0.99 rg"); // Light background #F8FAFC
        sb.AppendLine("50 672 495.28 42 re f");

        sb.AppendLine("BT");
        sb.AppendLine("/F2 8.5 Tf");
        sb.AppendLine("0.88 0.33 0.22 rg");
        sb.AppendLine("62 696 Td");
        sb.AppendLine($"(MASTERCLASS & CHOREOGRAPHY INTENSIVE) Tj");
        sb.AppendLine("ET");

        sb.AppendLine("BT");
        sb.AppendLine("/F1 7.5 Tf");
        sb.AppendLine("0.39 0.45 0.55 rg");
        sb.AppendLine("62 684 Td");
        sb.AppendLine($"(DIGITAL ENTRY PASS  |  OFFICIAL RECEIPT) Tj");
        sb.AppendLine("ET");

        // Status Badge (Top Right Pill Box inside Card)
        sb.AppendLine("0.92 0.99 0.96 rg"); // Emerald soft green #ECFDF5
        sb.AppendLine("345 680 185 26 re f");
        sb.AppendLine("0.65 0.95 0.82 RG"); // Emerald border #A7F3D0
        sb.AppendLine("1 w");
        sb.AppendLine("345 680 185 26 re S");

        sb.AppendLine("BT");
        sb.AppendLine("/F2 8.5 Tf");
        sb.AppendLine("0.02 0.59 0.41 rg"); // Emerald text #059669
        sb.AppendLine("360 689 Td");
        sb.AppendLine($"(CONFIRMED / VALID FOR ENTRY) Tj");
        sb.AppendLine("ET");

        // Workshop Title (Large 22pt)
        sb.AppendLine("BT");
        sb.AppendLine("/F2 20 Tf");
        sb.AppendLine("0.06 0.09 0.16 rg");
        sb.AppendLine("60 640 Td");
        sb.AppendLine($"({EscapePdf(model.WorkshopTitle)}) Tj");
        sb.AppendLine("ET");

        // Workshop Subtitle
        sb.AppendLine("BT");
        sb.AppendLine("/F1 9 Tf");
        sb.AppendLine("0.28 0.33 0.41 rg");
        sb.AppendLine("60 622 Td");
        sb.AppendLine($"(Hosted by Ethos Faculty  |  Official Workshop Admission Pass) Tj");
        sb.AppendLine("ET");

        // Divider Line below title
        sb.AppendLine("0.88 0.91 0.94 RG");
        sb.AppendLine("1 w");
        sb.AppendLine("56 608 483.28 0 re S");
        sb.AppendLine("56 608 m 539 608 l S");

        // 4. TWO COLUMN DETAILS & QR CONTAINER
        // Left Column Details
        DrawDetailField(sb, 60, 582, "ATTENDEE NAME", model.AttendeeName, "Primary Ticket Holder", isLarge: true);
        DrawDetailField(sb, 60, 532, "DATE", model.WorkshopDate, "Scheduled Workshop Session");
        DrawDetailField(sb, 60, 482, "SESSION TIME", model.WorkshopTime, "Reporting time: 15 mins prior");
        DrawDetailField(sb, 60, 432, "VENUE", model.Venue, "Main Studio Arena, Hyderabad");
        DrawDetailField(sb, 60, 382, "BOOKING ID", model.BookingReference, "Confirmed Booking Record");
        DrawDetailField(sb, 60, 332, "TICKET NUMBER", model.TicketNumber, "Admit: 1 Person (General Admission)");

        // Right Column: QR Box Container (X: 315, Y: 295, W: 224, H: 298)
        sb.AppendLine("0.97 0.98 0.99 rg"); // Light box bg #F8FAFC
        sb.AppendLine("315 295 224 298 re f");
        sb.AppendLine("0.88 0.91 0.94 RG"); // Border #E2E8F0
        sb.AppendLine("1 w");
        sb.AppendLine("315 295 224 298 re S");

        sb.AppendLine("BT");
        sb.AppendLine("/F2 8.5 Tf");
        sb.AppendLine("0.06 0.09 0.16 rg");
        sb.AppendLine("360 572 Td");
        sb.AppendLine($"(OFFICIAL PASS VERIFICATION) Tj");
        sb.AppendLine("ET");

        // Vector QR Code on Right
        DrawVectorQrCode(sb, model.QrToken, 352, 395, 150);

        sb.AppendLine("BT");
        sb.AppendLine("/F2 10 Tf");
        sb.AppendLine("0.02 0.59 0.41 rg"); // Emerald text
        sb.AppendLine("365 372 Td");
        sb.AppendLine($"(VALID DIGITAL TICKET) Tj");
        sb.AppendLine("ET");

        sb.AppendLine("BT");
        sb.AppendLine("/F1 7.5 Tf");
        sb.AppendLine("0.39 0.45 0.55 rg");
        sb.AppendLine("348 358 Td");
        sb.AppendLine($"(Official Token for Studio Entry) Tj");
        sb.AppendLine("ET");

        sb.AppendLine("BT");
        sb.AppendLine("/F1 7.5 Tf");
        sb.AppendLine("0.39 0.45 0.55 rg");
        sb.AppendLine("338 346 Td");
        sb.AppendLine($"(Scan via Ethos Reception Scanner App) Tj");
        sb.AppendLine("ET");

        // Ticket Number Pill Box at bottom of QR container
        sb.AppendLine("0.88 0.91 0.94 rg");
        sb.AppendLine("330 312 194 20 re f");

        sb.AppendLine("BT");
        sb.AppendLine("/F2 8 Tf");
        sb.AppendLine("0.06 0.09 0.16 rg");
        sb.AppendLine("350 318 Td");
        sb.AppendLine($"({EscapePdf($"PASS # {model.TicketNumber}")}) Tj");
        sb.AppendLine("ET");

        // 5. PERFORATION TEAR LINE (Y: 275)
        sb.AppendLine("0.80 0.84 0.88 RG");
        sb.AppendLine("1 w");
        sb.AppendLine("[4 3] 0 d"); // Dashed pattern
        sb.AppendLine("50 275 m 545 275 l S");
        sb.AppendLine("[] 0 d"); // Restore solid lines

        // Perforation Notch Cutouts (Circles matching #F8FAFC page background)
        sb.AppendLine("0.97 0.98 0.99 rg");
        DrawCircle(sb, 36, 275, 9);
        DrawCircle(sb, 559.28f, 275, 9);

        // 6. ENTRY INSTRUCTIONS CALLOUT BOX (X: 50, Y: 154, W: 495.28, H: 104)
        sb.AppendLine("1 0.98 0.92 rg"); // Soft Amber #FFFBEB
        sb.AppendLine("50 154 495.28 104 re f");
        sb.AppendLine("0.96 0.62 0.04 RG"); // Amber Border #F59E0B
        sb.AppendLine("1 w");
        sb.AppendLine("50 154 495.28 104 re S");

        sb.AppendLine("BT");
        sb.AppendLine("/F2 9.5 Tf");
        sb.AppendLine("0.71 0.33 0.04 rg"); // Amber Dark #B45309
        sb.AppendLine("62 240 Td");
        sb.AppendLine($"([!] ENTRY INSTRUCTIONS & VENUE RULES:) Tj");
        sb.AppendLine("ET");

        sb.AppendLine("BT");
        sb.AppendLine("/F2 9.5 Tf");
        sb.AppendLine("0.47 0.21 0.06 rg");
        sb.AppendLine("62 225 Td");
        sb.AppendLine($"(\"Please keep this ticket PDF pass and present the QR code at entry.\") Tj");
        sb.AppendLine("ET");

        sb.AppendLine("BT");
        sb.AppendLine("/F1 8 Tf");
        sb.AppendLine("0.57 0.25 0.05 rg");
        sb.AppendLine("62 198 Td");
        sb.AppendLine($"(* Non-transferable once scanned   * Carry clean studio shoes or dance barefoot   * Reporting time: 15 mins prior) Tj");
        sb.AppendLine("ET");

        sb.AppendLine("BT");
        sb.AppendLine("/F1 8 Tf");
        sb.AppendLine("0.57 0.25 0.05 rg");
        sb.AppendLine("62 184 Td");
        sb.AppendLine($"(* For questions or support, contact studio desk: +91 8466021834 / +91 9110745710) Tj");
        sb.AppendLine("ET");

        sb.AppendLine("BT");
        sb.AppendLine("/F1 8 Tf");
        sb.AppendLine("0.57 0.25 0.05 rg");
        sb.AppendLine("62 170 Td");
        sb.AppendLine($"(* Support: admissions@ethosdancestudio.com  -  www.ethosdancestudio.com) Tj");
        sb.AppendLine("ET");

        // 7. BOTTOM FOOTER BANNER (Y: 0 to 64)
        sb.AppendLine("0 0 0 rg"); // Black Footer #000000
        sb.AppendLine("0 0 595.28 64 re f");

        sb.AppendLine("1 0.16 0.23 rg"); // Red Top Accent Line #FF2A3A
        sb.AppendLine("0 62 595.28 2 re f");

        sb.AppendLine("BT");
        sb.AppendLine("/F2 8 Tf");
        sb.AppendLine("0.97 0.98 0.99 rg");
        sb.AppendLine("36 40 Td");
        sb.AppendLine($"(Phone: +91 8466021834  |  +91 9110745710) Tj");
        sb.AppendLine("ET");

        sb.AppendLine("BT");
        sb.AppendLine("/F1 8 Tf");
        sb.AppendLine("0.80 0.84 0.88 rg");
        sb.AppendLine("36 26 Td");
        sb.AppendLine($"(Email: ethosdancestudio@gmail.com) Tj");
        sb.AppendLine("ET");

        sb.AppendLine("BT");
        sb.AppendLine("/F2 8 Tf");
        sb.AppendLine("0.97 0.98 0.99 rg");
        sb.AppendLine("400 40 Td");
        sb.AppendLine($"(Instagram: @ethos_dancestudio) Tj");
        sb.AppendLine("ET");

        sb.AppendLine("BT");
        sb.AppendLine("/F1 8 Tf");
        sb.AppendLine("0.80 0.84 0.88 rg");
        sb.AppendLine("340 26 Td");
        sb.AppendLine($"(Venue: Main Studio Arena, Kukatpally, Hyderabad, India) Tj");
        sb.AppendLine("ET");

        sb.AppendLine("BT");
        sb.AppendLine("/F2 7 Tf");
        sb.AppendLine("0.58 0.64 0.72 rg");
        sb.AppendLine("145 10 Td");
        sb.AppendLine($"((C) 2026 ETHOS DANCE STUDIO  |  OFFICIAL WORKSHOP ADMISSION PASS  |  NOT FOR RESALE) Tj");
        sb.AppendLine("ET");

        sb.AppendLine("Q"); // Pop graphic state

        var contentBytes = Encoding.ASCII.GetBytes(sb.ToString());

        // Build Full Valid PDF 1.4 Binary Document
        return BuildPdfDocument(contentBytes);
    }

    private static void DrawDetailField(StringBuilder sb, float x, float labelY, string label, string value, string subtitle, bool isLarge = false)
    {
        // Label
        sb.AppendLine("BT");
        sb.AppendLine("/F2 8 Tf");
        sb.AppendLine("0.88 0.33 0.22 rg"); // Warm Peach #E05338
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0:F2} {1:F2} Td", x, labelY));
        sb.AppendLine($"({EscapePdf(label)}) Tj");
        sb.AppendLine("ET");

        // Value
        sb.AppendLine("BT");
        sb.AppendLine(isLarge ? "/F2 13 Tf" : "/F2 11 Tf");
        sb.AppendLine("0.06 0.09 0.16 rg"); // Deep Slate #0F172A
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0:F2} {1:F2} Td", x, labelY - 14));
        sb.AppendLine($"({EscapePdf(value)}) Tj");
        sb.AppendLine("ET");

        // Subtitle
        if (!string.IsNullOrEmpty(subtitle))
        {
            sb.AppendLine("BT");
            sb.AppendLine("/F1 7.5 Tf");
            sb.AppendLine("0.39 0.45 0.55 rg"); // Slate Muted #64748B
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0:F2} {1:F2} Td", x, labelY - 25));
            sb.AppendLine($"({EscapePdf(subtitle)}) Tj");
            sb.AppendLine("ET");
        }
    }

    private static void DrawCircle(StringBuilder sb, float cx, float cy, float r)
    {
        // Draw circle using 4 Bezier curves in PDF
        float k = r * 0.552284749831f;
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0:F2} {1:F2} m", cx + r, cy));
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0:F2} {1:F2} {2:F2} {3:F2} {4:F2} {5:F2} c", cx + r, cy + k, cx + k, cy + r, cx, cy + r));
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0:F2} {1:F2} {2:F2} {3:F2} {4:F2} {5:F2} c", cx - k, cy + r, cx - r, cy + k, cx - r, cy));
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0:F2} {1:F2} {2:F2} {3:F2} {4:F2} {5:F2} c", cx - r, cy - k, cx - k, cy - r, cx, cy - r));
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0:F2} {1:F2} {2:F2} {3:F2} {4:F2} {5:F2} c", cx + k, cy - r, cx + r, cy - k, cx + r, cy));
        sb.AppendLine("f");
    }

    private static void DrawVectorQrCode(StringBuilder sb, string rawToken, float startX, float startY, float targetSize)
    {
        // QR Code Background Box (White)
        sb.AppendLine("1 1 1 rg");
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0:F2} {1:F2} {2:F2} {3:F2} re f", startX - 5, startY - 5, targetSize + 10, targetSize + 10));
        sb.AppendLine("0.80 0.84 0.88 RG");
        sb.AppendLine("1 w");
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0:F2} {1:F2} {2:F2} {3:F2} re S", startX - 5, startY - 5, targetSize + 10, targetSize + 10));

        using var qrGenerator = new QRCodeGenerator();
        var safeToken = string.IsNullOrWhiteSpace(rawToken) ? "ETHOS-SAMPLE-TICKET" : rawToken;
        using var qrCodeData = qrGenerator.CreateQrCode(safeToken, QRCodeGenerator.ECCLevel.M);

        var matrix = qrCodeData.ModuleMatrix;
        int moduleCount = matrix.Count;
        if (moduleCount == 0) return;

        float moduleSize = targetSize / moduleCount;

        sb.AppendLine("0 0 0 rg"); // Black modules

        for (int row = 0; row < moduleCount; row++)
        {
            var bitArray = matrix[row];
            for (int col = 0; col < moduleCount; col++)
            {
                if (bitArray[col])
                {
                    float x = startX + (col * moduleSize);
                    float y = startY + ((moduleCount - 1 - row) * moduleSize);
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0:F2} {1:F2} {2:F2} {3:F2} re f", x, y, moduleSize, moduleSize));
                }
            }
        }
    }

    private static string EscapePdf(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        var sanitized = input
            .Replace("–", "-")
            .Replace("—", "-")
            .Replace("“", "\"")
            .Replace("”", "\"")
            .Replace("’", "'")
            .Replace("\\", "\\\\")
            .Replace("(", "\\(")
            .Replace(")", "\\)")
            .Replace("\r", "")
            .Replace("\n", " ");

        var sb = new StringBuilder();
        foreach (var ch in sanitized)
        {
            if (ch <= 127)
            {
                sb.Append(ch);
            }
            else
            {
                sb.Append(' ');
            }
        }
        return sb.ToString();
    }

    private static byte[] BuildPdfDocument(byte[] contentBytes)
    {
        using var ms = new MemoryStream();
        using var writer = new StreamWriter(ms, Encoding.ASCII, leaveOpen: true);

        var offsets = new List<long>();

        void WriteObjHeader(int objIndex)
        {
            writer.Flush();
            offsets.Add(ms.Position);
            writer.WriteLine($"{objIndex} 0 obj");
        }

        writer.WriteLine("%PDF-1.4");
        writer.WriteLine("%\u00e2\u00e3\u00cf\u00d3");

        // 1 0 obj: Catalog
        WriteObjHeader(1);
        writer.WriteLine("<< /Type /Catalog /Pages 2 0 R >>");
        writer.WriteLine("endobj");

        // 2 0 obj: Pages
        WriteObjHeader(2);
        writer.WriteLine("<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        writer.WriteLine("endobj");

        // 3 0 obj: Page
        WriteObjHeader(3);
        writer.WriteLine("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595.28 841.89] /Contents 4 0 R /Resources << /Font << /F1 5 0 R /F2 6 0 R >> >> >>");
        writer.WriteLine("endobj");

        // 4 0 obj: Content Stream
        WriteObjHeader(4);
        writer.WriteLine($"<< /Length {contentBytes.Length} >>");
        writer.WriteLine("stream");
        writer.Flush();
        ms.Write(contentBytes, 0, contentBytes.Length);
        writer.WriteLine();
        writer.WriteLine("endstream");
        writer.WriteLine("endobj");

        // 5 0 obj: Font F1 (Helvetica)
        WriteObjHeader(5);
        writer.WriteLine("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
        writer.WriteLine("endobj");

        // 6 0 obj: Font F2 (Helvetica-Bold)
        WriteObjHeader(6);
        writer.WriteLine("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>");
        writer.WriteLine("endobj");

        // xref Table
        writer.Flush();
        long startXref = ms.Position;
        writer.WriteLine("xref");
        writer.WriteLine($"0 {offsets.Count + 1}");
        writer.WriteLine("0000000000 65535 f ");
        foreach (var offset in offsets)
        {
            writer.WriteLine($"{offset:D10} 00000 n ");
        }

        // Trailer
        writer.WriteLine("trailer");
        writer.WriteLine($"<< /Size {offsets.Count + 1} /Root 1 0 R >>");
        writer.WriteLine("startxref");
        writer.WriteLine(startXref);
        writer.WriteLine("%%EOF");
        writer.Flush();

        return ms.ToArray();
    }
}
