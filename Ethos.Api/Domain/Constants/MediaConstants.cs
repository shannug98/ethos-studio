namespace Ethos.Api.Domain.Constants;

public static class MediaConstants
{
    public static class MediaTypes
    {
        public const string Image = "Image";
        public const string Video = "Video";

        public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
        {
            Image, Video
        };
    }

    public static class Sections
    {
        public const string HomepageScrolling = "HomepageScrolling";
        public const string HomepageReels    = "HomepageReels";    // Portrait 9:16 dance reels — video only
        public const string GalleryImages    = "GalleryImages";
        public const string GalleryVideos    = "GalleryVideos";
        public const string GallerySlideshow = "GallerySlideshow";
        public const string Workshop         = "Workshop";
        public const string Events           = "Events";
        public const string AboutEthos       = "AboutEthos";
        public const string Founders         = "Founders";
        public const string Trainers         = "Trainers";
        public const string Draft            = "Draft";

        public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
        {
            HomepageScrolling, HomepageReels, GalleryImages, GalleryVideos, GallerySlideshow, Workshop, Events, AboutEthos, Founders, Trainers, Draft
        };

        /// <summary>Sections that accept Image uploads.</summary>
        public static readonly HashSet<string> AllowedForImages = new(StringComparer.OrdinalIgnoreCase)
        {
            HomepageScrolling, GalleryImages, GallerySlideshow, Workshop, Events, AboutEthos, Founders, Trainers, Draft
        };

        /// <summary>Sections that accept Video uploads.</summary>
        public static readonly HashSet<string> AllowedForVideos = new(StringComparer.OrdinalIgnoreCase)
        {
            HomepageScrolling, HomepageReels, GalleryVideos, Workshop, Events, AboutEthos, Founders, Trainers, Draft
        };
    }

    public static class LayoutTypes
    {
        public const string Portrait = "Portrait";
        public const string Landscape = "Landscape";
        public const string Square = "Square";
        public const string Featured = "Featured";

        public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
        {
            Portrait, Landscape, Square, Featured
        };
    }

    public static class Categories
    {
        public const string Workshop = "Workshop";
        public const string InStudio = "In Studio";
        public const string Events = "Events";
        public const string Performances = "Performances";
        public const string BehindTheScenes = "Behind the Scenes";
        public const string Other = "Other";

        // Legacy / Fallback aliases
        public const string General = "General";
        public const string Workshops = "Workshops";
        public const string Community = "Community";
        public const string Studio = "Studio";

        public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
        {
            Workshop, InStudio, Events, Performances, BehindTheScenes, Other,
            General, Workshops, Community, Studio
        };
    }

    public static class Visibility
    {
        public const string Public = "Public";
        public const string Private = "Private";
    }

    public static class ApprovalStatus
    {
        public const string Pending = "Pending";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
    }

    public static bool TryNormalizeMediaType(string? raw, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        var clean = raw.Trim();
        if (clean.Equals("image", StringComparison.OrdinalIgnoreCase))
        {
            normalized = MediaTypes.Image;
            return true;
        }
        if (clean.Equals("video", StringComparison.OrdinalIgnoreCase))
        {
            normalized = MediaTypes.Video;
            return true;
        }
        return false;
    }

    public static bool TryNormalizeSection(string? raw, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        var clean = raw.Trim();
        if (clean.Equals("Founder", StringComparison.OrdinalIgnoreCase)) { normalized = Sections.Founders; return true; }
        if (clean.Equals("WeAreEthos", StringComparison.OrdinalIgnoreCase)) { normalized = Sections.AboutEthos; return true; }
        if (clean.Equals("Hero", StringComparison.OrdinalIgnoreCase) || clean.Equals("HeroBanner", StringComparison.OrdinalIgnoreCase)) { normalized = Sections.HomepageScrolling; return true; }
        if (clean.Equals("Reels", StringComparison.OrdinalIgnoreCase)) { normalized = Sections.HomepageReels; return true; }
        if (clean.Equals("Gallery", StringComparison.OrdinalIgnoreCase)) { normalized = Sections.GalleryImages; return true; }

        foreach (var s in Sections.All)
        {
            if (s.Equals(clean, StringComparison.OrdinalIgnoreCase))
            {
                normalized = s;
                return true;
            }
        }
        return false;
    }

    public static bool TryNormalizeLayoutType(string? raw, out string normalized)
    {
        normalized = LayoutTypes.Square;
        if (string.IsNullOrWhiteSpace(raw)) return true;

        var clean = raw.Trim().ToLowerInvariant();
        if (clean == "portrait" || clean == "portrait_3_4") { normalized = LayoutTypes.Portrait; return true; }
        if (clean == "landscape" || clean == "landscape_16_9") { normalized = LayoutTypes.Landscape; return true; }
        if (clean == "square" || clean == "square_1_1") { normalized = LayoutTypes.Square; return true; }
        if (clean == "featured") { normalized = LayoutTypes.Featured; return true; }

        return false;
    }

    public static bool TryNormalizeCategory(string? raw, out string normalized)
    {
        normalized = Categories.Workshop;
        if (string.IsNullOrWhiteSpace(raw)) return true;

        var clean = raw.Trim().Replace(" ", "").Replace("-", "");
        foreach (var c in Categories.All)
        {
            var canonicalClean = c.Replace(" ", "").Replace("-", "");
            if (canonicalClean.Equals(clean, StringComparison.OrdinalIgnoreCase))
            {
                normalized = c;
                return true;
            }
        }

        return false;
    }
}
