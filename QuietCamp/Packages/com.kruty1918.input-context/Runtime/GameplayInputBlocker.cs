using Kruty1918.InputRouting.API;
using UnityEngine;

namespace Kruty1918.InputRouting.Runtime
{
    [DisallowMultipleComponent]
    public sealed class GameplayInputBlocker : MonoBehaviour
    {
        [SerializeField] private GameplayInputKind _blockedInput = GameplayInputKind.AllPointer;

        public GameplayInputKind BlockedInput => _blockedInput;
    }
}
