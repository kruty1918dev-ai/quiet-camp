using System.Collections.Generic;
using UnityEngine;

namespace Kruty1918.Atmos
{
    /// <summary>
    /// Applies the Atmos/FoliageSway shader to the renderers of a decor object
    /// and registers the per-renderer data the shader needs: object-space mesh
    /// bounds (for the normalized root→tip mask) and expanded culling bounds
    /// that already include the maximum possible sway.
    ///
    /// Materials are shared per (base color, species) so a whole meadow of
    /// swaying props costs only a handful of materials; species carries the
    /// response parameters (amplitude, frequency, rigid base, flutter) instead
    /// of the old raw amplitude/frequency pair. Use <see cref="Shared"/> for
    /// app-wide deduplication, or construct an instance when you want an
    /// isolated cache you can clear yourself.
    /// </summary>
    public sealed class FoliageSway
    {
        /// <summary>Vegetation response class — one entry per material slot.
        /// Values follow the living-vegetation spec: amplitudes are tip
        /// displacement as a fraction of mesh height, frequencies are Hz.</summary>
        public enum Species
        {
            Generic = 0,
            Trunk = 1,       // heavy, ~0.3° lean, bottom third rigid
            Canopy = 2,      // broadleaf crown mass, slow 0.5–1 % H
            Conifer = 3,     // ~⅓ of broadleaf; tiers keep their silhouette
            Bush = 4,        // 1–1.5 % H, mid frequency
            Grass = 5,       // 2–4° bend, quick
            FlowerStem = 6,  // slower than grass
            FlowerHead = 7,  // stem response plus a small lagged arc
        }

        struct Response
        {
            public float Amp, Freq, FlutterAmp, FlutterFreq, RootLock, PhaseLag;
            public Response(float a, float f, float fa, float ff, float lock_, float lag)
            { Amp = a; Freq = f; FlutterAmp = fa; FlutterFreq = ff; RootLock = lock_; PhaseLag = lag; }
        }

        static Response ResponseFor(Species s)
        {
            switch (s)
            {
                case Species.Trunk:      return new Response(.004f, .12f, 0f, 0f, .35f, 0f);
                case Species.Canopy:     return new Response(.008f, .22f, .0015f, .8f, .35f, 0f);
                case Species.Conifer:    return new Response(.005f, .18f, .0008f, .6f, .3f, 0f);
                case Species.Bush:       return new Response(.012f, .35f, .002f, .9f, .25f, 0f);
                case Species.Grass:      return new Response(.05f, .5f, .004f, 1.4f, .15f, 0f);
                case Species.FlowerStem: return new Response(.03f, .3f, .002f, 1f, .2f, 0f);
                case Species.FlowerHead: return new Response(.035f, .3f, .003f, 1f, .2f, .55f);
                default:                 return new Response(.05f, 1.6f, 0f, 0f, .2f, 0f);
            }
        }

        public static readonly FoliageSway Shared = new FoliageSway();

        static readonly int IdBaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int IdAmp = Shader.PropertyToID("_SwayAmp");
        static readonly int IdFreq = Shader.PropertyToID("_SwayFreq");
        static readonly int IdFlutAmp = Shader.PropertyToID("_FlutterAmp");
        static readonly int IdFlutFreq = Shader.PropertyToID("_FlutterFreq");
        static readonly int IdRootLock = Shader.PropertyToID("_RootLock");
        static readonly int IdPhaseLag = Shader.PropertyToID("_PhaseLag");
        static readonly int IdMeshMinY = Shader.PropertyToID("_MeshMinY");
        static readonly int IdMeshTopY = Shader.PropertyToID("_MeshTopY");

        // Materials are cached per (color, species): props that share a tint
        // but bend differently — a trunk vs its canopy — must not fight over
        // one material's response parameters.
        readonly Dictionary<(Color, Species), Material> _byParams =
            new Dictionary<(Color, Species), Material>();
        readonly MaterialPropertyBlock _mpb = new MaterialPropertyBlock();

        /// <summary>Shared sway material for a base color (created on demand).</summary>
        public Material MaterialFor(Color baseColor, float amplitude = 0.05f, float frequency = 1.6f)
            => MaterialForSpecies(baseColor, Species.Generic);

        /// <summary>Shared sway material for a base color and species.</summary>
        public Material MaterialForSpecies(Color baseColor, Species species)
        {
            var key = (baseColor, species);
            if (!_byParams.TryGetValue(key, out var mat) || mat == null)
            {
                mat = AtmosShaders.NewMaterial(AtmosShaders.FoliageSway, "FoliageSway");
                if (mat == null) return null;
                var r = ResponseFor(species);
                mat.SetColor(IdBaseColor, baseColor);
                mat.SetFloat(IdAmp, r.Amp);
                mat.SetFloat(IdFreq, r.Freq);
                mat.SetFloat(IdFlutAmp, r.FlutterAmp);
                mat.SetFloat(IdFlutFreq, r.FlutterFreq);
                mat.SetFloat(IdRootLock, r.RootLock);
                mat.SetFloat(IdPhaseLag, r.PhaseLag);
                _byParams[key] = mat;
            }
            return mat;
        }

        /// <summary>
        /// Swaps every renderer under <paramref name="root"/> to the sway shader,
        /// preserving each renderer's _BaseColor/_Color. Returns swapped count.
        /// Kept for compatibility — equivalent to ApplySpecies(Generic).
        /// </summary>
        public int Apply(GameObject root, float amplitude = 0.05f, float frequency = 1.6f)
            => ApplySpecies(root, Species.Generic);

        /// <summary>Same-material-species variant of Apply for simple props.</summary>
        public int ApplySpecies(GameObject root, Species species)
        {
            if (root == null) return 0;
            var swapped = 0;
            foreach (var r in root.GetComponentsInChildren<Renderer>())
                swapped += ApplyRenderer(r, null, species);
            return swapped;
        }

        /// <summary>
        /// Per-material-slot species: trunk and canopy of a multi-slot mesh
        /// get their own responses while each slot keeps its original tint.
        /// If a renderer has more slots than <paramref name="speciesPerSlot"/>,
        /// the last species repeats; extra slots are never left unswapped.
        /// </summary>
        public int ApplySlots(GameObject root, Species[] speciesPerSlot)
        {
            if (root == null || speciesPerSlot == null || speciesPerSlot.Length == 0) return 0;
            var swapped = 0;
            foreach (var r in root.GetComponentsInChildren<Renderer>())
                swapped += ApplyRenderer(r, speciesPerSlot, Species.Generic);
            return swapped;
        }

        int ApplyRenderer(Renderer r, Species[] perSlot, Species single)
        {
            var mats = r.sharedMaterials;
            var changed = false;
            for (var i = 0; i < mats.Length; i++)
            {
                var src = mats[i];
                var c = src != null
                    ? (src.HasProperty("_BaseColor") ? src.GetColor("_BaseColor")
                       : src.HasProperty("_Color") ? src.GetColor("_Color")
                       : Color.white)
                    : Color.white;
                var species = perSlot != null
                    ? perSlot[Mathf.Min(i, perSlot.Length - 1)] : single;
                var mat = MaterialForSpecies(c, species);
                if (mat == null) break;
                mats[i] = mat;
                changed = true;
            }
            if (!changed) return 0;
            r.sharedMaterials = mats;
            RegisterMeshData(r);
            return 1;
        }

        /// <summary>
        /// One-time per-renderer registration: the normalized height mask is
        /// derived from the mesh's own object-space bounds (a grass blade and
        /// a pine share the code), and culling bounds grow to cover the full
        /// possible sway so a bent crown never pops out of the frustum.
        /// </summary>
        void RegisterMeshData(Renderer r)
        {
            var b = r.localBounds;
            float meshH = Mathf.Max(1e-4f, b.size.y);
            _mpb.SetFloat(IdMeshMinY, b.min.y);
            _mpb.SetFloat(IdMeshTopY, b.max.y);
            r.SetPropertyBlock(_mpb);
            // Max sway ≈ largest species amplitude plus flutter, with margin.
            float reach = meshH * (.05f + .004f) * 1.6f;
            b.Expand(new Vector3(reach, 0f, reach));
            r.localBounds = b;
        }

        /// <summary>Drops cached materials (they remain assigned; new ones are created).</summary>
        public void Clear() => _byParams.Clear();
    }
}
