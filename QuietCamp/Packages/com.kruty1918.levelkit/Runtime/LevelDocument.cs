using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Kruty1918.LevelKit
{
    /// <summary>
    /// The universal in-memory representation of one level file.
    ///
    /// Grid + named cell layers + typed entities + verbatim extra props.
    /// Nothing game-specific lives here: cell semantics ("blocked" vs
    /// "shade") and entity kinds ("guest" vs "enemy") come from the game's
    /// LevelProfile, while every unknown JSON field is preserved in Props
    /// so a parse -> write roundtrip is lossless.
    /// </summary>
    public sealed class LevelDocument
    {
        public string Id = "";
        public int Order;
        public int SchemaVersion = 1;
        public int Width;
        public int Height;
        public readonly List<LevelLayer> Layers = new List<LevelLayer>();
        public readonly List<LevelEntity> Entities = new List<LevelEntity>();
        public JObject Props = new JObject();

        public bool HasGrid => Width > 0 && Height > 0;

        public LevelLayer LayerOrNull(string name)
            => Layers.FirstOrDefault(l => l.Name == name);

        /// <summary>Returns the layer, creating it when absent.</summary>
        public LevelLayer Layer(string name)
        {
            var layer = LayerOrNull(name);
            if (layer == null)
            {
                layer = new LevelLayer(name);
                Layers.Add(layer);
            }
            return layer;
        }

        public IEnumerable<LevelEntity> EntitiesOf(string kind)
            => Entities.Where(e => e.Kind == kind);

        public LevelEntity EntityOrNull(string id)
            => Entities.FirstOrDefault(e => e.Id == id);

        public bool InsideGrid(CellPos pos)
            => !HasGrid || (pos.X >= 0 && pos.X < Width && pos.Z >= 0 && pos.Z < Height);

        public T Prop<T>(string key, T fallback = default)
        {
            var token = Props != null ? Props[key] : null;
            if (token == null || token.Type == JTokenType.Null) return fallback;
            try { return token.ToObject<T>(); }
            catch { return fallback; }
        }

        public void SetProp(string key, JToken value)
        {
            Props ??= new JObject();
            Props[key] = value;
        }
    }
}
