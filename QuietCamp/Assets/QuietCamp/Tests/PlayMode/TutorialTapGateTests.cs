using NUnit.Framework;
using QuietCamp.Application;

namespace QuietCamp.Tests
{
    public sealed class TutorialTapGateTests
    {
        [Test] public void EarlyPressCannotDismissEvenWhenReleasedAfterTheDelay()
        {
            var gate=new TutorialTapGate(10,12);gate.Press(10.8,1,0,0);
            Assert.IsFalse(gate.Release(11.1,1,0,0));
            gate.Press(11.2,1,0,0);Assert.IsTrue(gate.Release(11.3,1,1,1));
            Assert.IsFalse(gate.Release(11.4,1,1,1));
        }
        [Test] public void SwipesLongHoldsAndOtherPointersCannotDismiss()
        {
            var gate=new TutorialTapGate(0,12);
            gate.Press(1,1,0,0);gate.Move(1,20,0);Assert.IsFalse(gate.Release(1.1,1,0,0));
            gate.Press(2,1,0,0);Assert.IsFalse(gate.Release(2.7,1,0,0));
            gate.Press(3,1,0,0);Assert.IsFalse(gate.Release(3.1,2,0,0));
            gate.Press(4,1,0,0);gate.Cancel();Assert.IsFalse(gate.Release(4.1,1,0,0));
        }
    }
}
