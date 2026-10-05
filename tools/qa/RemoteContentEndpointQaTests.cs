// Injected into an isolated Editor QA project; never included in the game.
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityHTML.Runtime.Content;
namespace QuietCamp.Tests {
 public sealed class RemoteContentEndpointQaTests {
  [UnityTest] public IEnumerator PublishedHttpsDocumentMatchesSourceExactly() {
   Assert.IsTrue(UnityEngine.Application.productName.Contains("QA"));
   var source = new ContentSource { id = "unityhtml-published-readme", timeoutSeconds = 20,
    allowedOrigins = new[] { "https://raw.githubusercontent.com" }, variants = new[] {
     new ContentVariant { language = "en", revision = "v0.2.0", format = ContentFormat.PlainText,
      url = "https://raw.githubusercontent.com/kruty1918dev-ai/com.kruty1918.moyva.unityhtml/108dd7c0381100daba5096c14c1377a985d6103a/README.md" }
    } };
   ContentResult result = null;
   yield return new UnityHtmlContentLoader().Load(source,"en",r=>result=r);
   Assert.NotNull(result); Assert.IsTrue(result.Succeeded,result.Error);
   Assert.AreEqual(ContentOrigin.Network,result.Document.Origin);
   Assert.AreEqual(File.ReadAllText("/home/oleks/Документи/GitHub/unityhtml/README.md"),result.Document.OriginalBody);
   Debug.Log("[RemoteContentQA] Published HTTPS source matched the original body exactly.");
  }
 }
}
