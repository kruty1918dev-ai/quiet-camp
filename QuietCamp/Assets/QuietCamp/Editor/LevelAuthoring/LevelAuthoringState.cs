using Newtonsoft.Json;
using UnityEngine;

namespace QuietCamp.Editor.LevelAuthoring
{
    // Transient Undo target. No configuration asset is created; JSON owns the draft.
    public sealed class LevelAuthoringState : ScriptableObject
    {
        [TextArea] public string json;
        public LevelAuthoringDocument Read() => JsonConvert.DeserializeObject<LevelAuthoringDocument>(json);
        public void Write(LevelAuthoringDocument document) => json = JsonConvert.SerializeObject(document);
    }
}
