using Newtonsoft.Json.Linq;

namespace Kruty1918.LevelKit
{
    /// <summary>
    /// One placed or declared object in a level: a guest, spawn point,
    /// witness pose, pickup, enemy, etc. Kind is a free-form string scoped
    /// by the project's profile; Id is stable text. X/Z/Rotation cover the
    /// usual grid placement; X/Z are -1 when the entity has no position.
    /// Any extra fields live in Data and survive a roundtrip untouched.
    /// </summary>
    public sealed class LevelEntity
    {
        public string Id = "";
        public string Kind = "";
        public int X = -1;
        public int Z = -1;
        public int Rotation;
        public JObject Data = new JObject();

        public bool HasPosition => X >= 0 && Z >= 0;

        public T Get<T>(string key, T fallback = default)
        {
            var token = Data != null ? Data[key] : null;
            if (token == null || token.Type == JTokenType.Null) return fallback;
            try { return token.ToObject<T>(); }
            catch { return fallback; }
        }

        public void Set(string key, JToken value)
        {
            Data ??= new JObject();
            Data[key] = value;
        }
    }
}
