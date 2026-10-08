using Newtonsoft.Json.Linq;

namespace Kruty1918.LevelGen
{
    /// <summary>
    /// One pipeline step. Steps mutate <see cref="GenContext.Level"/>; they may
    /// throw <see cref="GenFailedException"/> to fail the current attempt so
    /// the retry loop rolls a new seed. Steps read their parameters from a
    /// JObject so recipes stay declarative.
    /// </summary>
    public interface IGenStep
    {
        void Apply(GenContext ctx, JObject p);
    }

    /// <summary>Per-attempt state shared by all steps.</summary>
    public sealed class GenContext
    {
        public GenLevel Level;
        public GenRng Rng;

        /// <summary>Named values steps can stash for later steps (e.g. a path's cells).</summary>
        public readonly System.Collections.Generic.Dictionary<string, object> Vars =
            new System.Collections.Generic.Dictionary<string, object>();
    }

    /// <summary>Thrown by a step (usually "require") to reject this attempt.</summary>
    public sealed class GenFailedException : System.Exception
    {
        public GenFailedException(string message) : base(message) { }
    }

    /// <summary>Thrown for malformed recipes (unknown step type, bad params).</summary>
    public sealed class GenRecipeException : System.Exception
    {
        public GenRecipeException(string message) : base(message) { }
    }
}
