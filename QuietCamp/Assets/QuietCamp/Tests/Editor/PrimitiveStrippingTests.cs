using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;

namespace QuietCamp.Tests.Editor
{
    public class PrimitiveStrippingTests
    {
        [TestCase("UnityEngine.CoreModule","UnityEngine.MeshFilter")]
        [TestCase("UnityEngine.CoreModule","UnityEngine.MeshRenderer")]
        [TestCase("UnityEngine.PhysicsModule","UnityEngine.MeshCollider")]
        [TestCase("UnityEngine.PhysicsModule","UnityEngine.BoxCollider")]
        [TestCase("UnityEngine.PhysicsModule","UnityEngine.SphereCollider")]
        [TestCase("UnityEngine.PhysicsModule","UnityEngine.CapsuleCollider")]
        public void DynamicallyCreatedPrimitiveComponentsSurviveEngineStripping(string assembly,string type)
        {
            var xml=XDocument.Load(Path.Combine(Directory.GetCurrentDirectory(),"Assets/QuietCamp/link.xml"));
            Assert.IsTrue(xml.Descendants("assembly").Where(a=>(string)a.Attribute("fullname")==assembly)
                .Elements("type").Any(t=>(string)t.Attribute("fullname")==type&&(string)t.Attribute("preserve")=="all"),
                "CreatePrimitive requires the collider even when presentation immediately removes it: "+type);
        }
    }
}
