using Kruty1918.Atmos;
using NUnit.Framework;
using UnityEngine;

public class AtmosTests
{
    [Test]
    public void SkySpec_Lerp_BlendsColorsAndScalars()
    {
        var a = SkySpec.Day;
        var b = SkySpec.Evening;
        var mid = SkySpec.Lerp(a, b, 0.5f);
        Assert.AreEqual((a.zenith.r + b.zenith.r) / 2f, mid.zenith.r, 1e-4f);
        Assert.AreEqual((a.stars + b.stars) / 2f, mid.stars, 1e-4f);
        Assert.AreEqual((a.sunSize + b.sunSize) / 2f, mid.sunSize, 1e-4f);
    }

    [Test]
    public void SkySpec_Lerp_Endpoints()
    {
        var mid = SkySpec.Lerp(SkySpec.Day, SkySpec.Evening, 0f);
        Assert.AreEqual(SkySpec.Day.zenith, mid.zenith);
        mid = SkySpec.Lerp(SkySpec.Day, SkySpec.Evening, 1f);
        Assert.AreEqual(SkySpec.Evening.zenith, mid.zenith);
    }

    [Test]
    public void SkySpec_Json_RoundTrips()
    {
        var spec = SkySpec.Evening;
        var clone = SkySpec.FromJson(spec.ToJson());
        Assert.AreEqual(spec.zenith, clone.zenith);
        Assert.AreEqual(spec.stars, clone.stars, 1e-4f);
        Assert.AreEqual(spec.sunDir, clone.sunDir);
    }

    [Test]
    public void SkySpec_ApplyTo_WritesAllProperties()
    {
        var shader = Shader.Find(AtmosShaders.SkyGradient);
        Assert.NotNull(shader, "Atmos/SkyGradient shader missing");
        var mat = new Material(shader);
        var spec = SkySpec.Night;
        spec.ApplyTo(mat);
        AssertColorClose(spec.zenith, mat.GetColor("_ZenithColor"));
        Assert.AreEqual(spec.stars, mat.GetFloat("_Stars"), 1e-4f);
        Object.DestroyImmediate(mat);
    }

    static void AssertColorClose(Color expected, Color actual)
    {
        Assert.AreEqual(expected.r, actual.r, 1e-3f);
        Assert.AreEqual(expected.g, actual.g, 1e-3f);
        Assert.AreEqual(expected.b, actual.b, 1e-3f);
    }

    [Test]
    public void Sky_CreateMaterial_ReturnsConfiguredMaterial()
    {
        var mat = Sky.CreateMaterial(SkySpec.Evening);
        Assert.NotNull(mat);
        AssertColorClose(SkySpec.Evening.horizon, mat.GetColor("_HorizonColor"));
        Object.DestroyImmediate(mat);
    }

    [Test]
    public void FoliageSway_Apply_SwapsRendererMaterials()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            var swapped = FoliageSway.Shared.Apply(go);
            Assert.AreEqual(1, swapped);
            var mat = go.GetComponent<Renderer>().sharedMaterial;
            Assert.AreEqual("Atmos/FoliageSway", mat.shader.name);
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void Campfire_Create_BuildsFullFxHierarchy()
    {
        var parent = new GameObject("FirePit");
        try
        {
            var visual = Campfire.Create(parent.transform, CampfireSpec.Cozy);
            Assert.NotNull(visual);
            var root = visual.transform;
            Assert.NotNull(root.Find("FireGlow"));
            Assert.NotNull(root.Find("Flame0"));
            Assert.NotNull(root.Find("FireLight"));
            var light = root.Find("FireLight").GetComponent<Light>();
            Assert.AreEqual(3f, light.range, 1e-4f);
            Assert.AreEqual(1.2f, light.intensity, 1e-4f);
        }
        finally { Object.DestroyImmediate(parent); }
    }

    [Test]
    public void CampfireSpec_Bonfire_ScalesUp()
    {
        Assert.Greater(CampfireSpec.Bonfire.lightRange, CampfireSpec.Cozy.lightRange);
        Assert.Greater(CampfireSpec.Bonfire.emberCount, CampfireSpec.Cozy.emberCount);
    }
}
