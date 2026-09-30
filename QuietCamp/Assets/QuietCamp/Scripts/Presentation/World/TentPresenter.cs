using DG.Tweening;
using QuietCamp.Domain;
using UnityEngine;
namespace QuietCamp.Presentation.World
{
    /// <summary>
    /// Visual wrapper for one committed tent: model root stays at the committed
    /// logical pose (rotation = 90*q, position = footprint centre) while a
    /// dedicated child carries select-lift and commit tweens. Logic never reads
    /// back tween state.
    /// </summary>
    public sealed class TentPresenter
    {
        readonly GameObject _root;
        readonly LevelData _level;
        readonly Transform _liftNode;
        readonly Transform _doorMarker;
        Placement _placement;
        Tween _moveTween, _rotateTween;

        public string GuestId { get; }
        public GameObject Root => _root;
        public Placement Placement => _placement;

        public TentPresenter(GameObject root, LevelData level, string guestId)
        {
            _root = root;
            _level = level;
            GuestId = guestId;
            _liftNode = new GameObject("LiftNode").transform;
            var visual = root.transform.Find("VisualCenter");
            _liftNode.SetParent(root.transform, false);
            if (visual != null) visual.SetParent(_liftNode, true);
            var door = root.transform.Find("DoorMarker");
            if (door != null)
            {
                _doorMarker = door;
                var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                marker.name = "DoorDot";
                Object.Destroy(marker.GetComponent<Collider>());
                marker.transform.SetParent(_doorMarker, false);
                marker.transform.localScale = Vector3.one * 0.14f;
                var r = marker.GetComponent<Renderer>();
                r.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                r.material.SetColor("_BaseColor", new Color(1f, 0.85f, 0.3f));
            }
        }

        /// <summary>Applies a committed placement; animates unless instant.</summary>
        public void ApplyPlacement(Placement p, bool instant)
        {
            _placement = p?.Copy();
            var targetPos = BoardMath.TentCenter(_level, p.x, p.z);
            var targetYaw = BoardMath.TentYaw(p.rotation);
            if (instant)
            {
                _root.transform.localPosition = targetPos;
                _root.transform.localEulerAngles = new Vector3(0f, targetYaw, 0f);
                return;
            }
            _moveTween?.Kill();
            _rotateTween?.Kill();
            _moveTween = _root.transform.DOLocalMove(targetPos, 0.16f).SetEase(Ease.OutQuad);
            _rotateTween = _root.transform.DOLocalRotate(new Vector3(0f, targetYaw, 0f), 0.16f)
                .SetEase(Ease.OutQuad);
        }

        /// <summary>Lifts the visual node while the tent is selected/dragged.</summary>
        public void SetLifted(bool lifted, bool reducedMotion)
        {
            _liftNode.DOKill();
            var y = lifted ? BoardMath.SelectedLift : 0f;
            if (reducedMotion) { _liftNode.localPosition = new Vector3(0f, y, 0f); return; }
            _liftNode.DOLocalMoveY(y, 0.1f).SetEase(Ease.OutQuad);
        }

        public void SetDoorMarkerVisible(bool visible)
        {
            if (_doorMarker != null) _doorMarker.gameObject.SetActive(visible);
        }

        public void Dispose()
        {
            _moveTween?.Kill();
            _rotateTween?.Kill();
            _liftNode.DOKill();
            if (_root != null) Object.Destroy(_root);
        }
    }
}
