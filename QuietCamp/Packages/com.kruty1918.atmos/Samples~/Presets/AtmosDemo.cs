using Kruty1918.Atmos;
using UnityEngine;

/// <summary>
/// Drop on any GameObject to see Atmos in action: a day→evening→night sky
/// cycle, a swaying patch of grass primitives, and a campfire you can toggle.
/// The sky JSON presets next to this file load via Resources or TextAsset.
/// </summary>
public class AtmosDemo : MonoBehaviour
{
    SkyController _sky;
    FireVisual _fire;

    void Start()
    {
        _sky = gameObject.AddComponent<SkyController>();
        _sky.targetCamera = Camera.main;
        _sky.skies.Add(new SkyController.NamedSky { name = "day", spec = SkySpec.Day });
        _sky.skies.Add(new SkyController.NamedSky { name = "evening", spec = SkySpec.Evening });
        _sky.skies.Add(new SkyController.NamedSky { name = "night", spec = SkySpec.Night });
        _sky.Set("day");

        // A small patch of swaying grass made from primitives.
        for (var i = 0; i < 8; i++)
        {
            var blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blade.transform.SetParent(transform, false);
            blade.transform.localPosition = new Vector3(i * 0.4f - 1.4f, 0.4f, -1f);
            blade.transform.localScale = new Vector3(0.06f, 0.8f, 0.06f);
            FoliageSway.Shared.Apply(blade);
        }

        _fire = Campfire.Create(transform);
        _fire.SetBurning(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) _sky.TransitionTo("day", 3f);
        if (Input.GetKeyDown(KeyCode.Alpha2)) { _sky.TransitionTo("evening", 3f); _fire.SetBurning(true); }
        if (Input.GetKeyDown(KeyCode.Alpha3)) _sky.TransitionTo("night", 3f);
    }
}
