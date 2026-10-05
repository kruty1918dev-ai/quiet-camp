using UnityEngine;

namespace QuietCamp.Presentation
{
    /// <summary>Only an explicit player choice rotates the mobile display.</summary>
    public static class ScreenOrientationPolicy
    {
        public static ScreenOrientation Resolve(int choice) => choice switch
        {
            1 => ScreenOrientation.LandscapeLeft,
            2 => ScreenOrientation.LandscapeRight,
            3 => ScreenOrientation.PortraitUpsideDown,
            _ => ScreenOrientation.Portrait
        };

        public static void Apply(int choice)
        {
            if (!UnityEngine.Application.isMobilePlatform) return;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = false;
            Screen.autorotateToLandscapeRight = false;
            Screen.orientation = Resolve(choice);
        }
    }
}
