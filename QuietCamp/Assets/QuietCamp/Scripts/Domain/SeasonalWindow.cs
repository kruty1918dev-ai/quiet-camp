using System;

namespace QuietCamp.Domain
{
    /// <summary>Meteorological season from the local calendar — the window in
    /// which seasonal side stories (e.g. the frost glade) accept players.</summary>
    public static class SeasonalWindow
    {
        public static string Now() => For(DateTime.Now.Month);
        public static string For(int month) => month switch
        {
            12 or 1 or 2 => "winter",
            3 or 4 or 5 => "spring",
            6 or 7 or 8 => "summer",
            _ => "autumn",
        };
    }
}
