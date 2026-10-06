using System;

namespace QuietCamp.Domain
{
    /// <summary>A named run of campaign positions (1-based, inclusive) — the
    /// world framing layer over the ordered level list. Act 1 teaches safe
    /// places; act 2 joins them into a living network of routes.</summary>
    [Serializable] public sealed class DistrictDefinition
    {
        public string id;
        public int act, from, to;
        public string TitleKey => "district." + id + ".title";
        public string IntroKey => "district." + id + ".intro";
    }
}
