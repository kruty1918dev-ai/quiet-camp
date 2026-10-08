using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Kruty1918.LevelGen
{
    /// <summary>
    /// A placed thing: an obstacle, coin, enemy, guest, spawn point — anything
    /// a game maps to gameplay objects. <see cref="Kind"/> is a free-form id
    /// the game interprets; <see cref="Props"/> carries arbitrary JSON data
    /// (colors, speeds, rule flags) through to the level file.
    /// </summary>
    [Serializable]
    public class GenEntity
    {
        public string kind;
        public int x, y;
        public int rotation;
        public JObject props;

        public GenEntity() { props = new JObject(); }
        public GenEntity(string kind, int x, int y, int rotation = 0, JObject props = null)
        {
            this.kind = kind; this.x = x; this.y = y; this.rotation = rotation;
            this.props = props ?? new JObject();
        }

        public GenCell Cell => new GenCell(x, y);
        public GenEntity Clone() => new GenEntity(kind, x, y, rotation, props?.DeepClone() as JObject);
    }
}
