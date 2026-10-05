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
        Tween _moveTween, _rotateTween, _liftTween, _scaleTween;

        public string GuestId { get; }
        public GameObject Root => _root;
        public Placement Placement => _placement;

        public TentPresenter(GameObject root, LevelData level, string guestId)
        {
            _root = root;
            TentCloth.Apply(root);
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
                var marker = new GameObject("DoorDot",typeof(MeshFilter),typeof(MeshRenderer));
                marker.transform.SetParent(_doorMarker, false);
                marker.transform.localScale = Vector3.one * 0.14f;
                // A small bevelled canvas mat sits on the ground; the cue receives real light.
                var vertices=new Vector3[25];var triangles=new int[108];vertices[0]=new Vector3(0,.08f,0);
                for(int i=0;i<12;i++)
                {
                    float angle=i*Mathf.PI/6;
                    vertices[1+i]=new Vector3(Mathf.Cos(angle)*.43f,.08f,Mathf.Sin(angle)*.43f);
                    vertices[13+i]=new Vector3(Mathf.Cos(angle)*.5f,0,Mathf.Sin(angle)*.5f);
                    int next=(i+1)%12,at=i*9;
                    triangles[at]=0;triangles[at+1]=1+next;triangles[at+2]=1+i;
                    triangles[at+3]=1+i;triangles[at+4]=13+next;triangles[at+5]=13+i;
                    triangles[at+6]=1+i;triangles[at+7]=1+next;triangles[at+8]=13+next;
                }
                var mesh=new Mesh{name="Doorway canvas mat"};mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();
                marker.GetComponent<MeshFilter>().sharedMesh=mesh;marker.AddComponent<OwnedCampMesh>().Mesh=mesh;
                var r = marker.GetComponent<Renderer>();
                var material=new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
                material.SetColor("_BaseColor", new Color(.94f,.78f,.43f));
                r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=true;
                r.sharedMaterial=material;marker.AddComponent<OwnedCampMaterial>().Material=material;
            }
            TentStoryVisual.Attach(root,level,guestId);
        }

        /// <summary>Keep the small doorway cue readable; the tent and its collider keep their authored size.</summary>
        public void BindViewport(Kruty1918.GameplayViewport.GameplayViewport viewport)
        {
            var dot = _doorMarker != null ? _doorMarker.Find("DoorDot") : null;
            if (dot == null || viewport == null) return;
            var scale = _root.GetComponent<Kruty1918.GameplayViewport.ViewportVisualScale>();
            if (scale == null) scale = _root.AddComponent<Kruty1918.GameplayViewport.ViewportVisualScale>();
            scale.Viewport = viewport; scale.Visual = dot;
            scale.ReferenceLocalScale = Vector3.one * .14f; scale.ReferenceWorldSize = .14f;
            scale.SizeFraction = .019f; scale.MinimumMultiplier = 1f; scale.MaximumMultiplier = 1.7f;
        }

        /// <summary>Applies a committed placement; animates unless instant.</summary>
        public void ApplyPlacement(Placement p, bool instant, float durationScale = 1f)
        {
            if (p == null) return;
            if (!instant && _placement != null && _placement.x == p.x
                && _placement.z == p.z && _placement.rotation == p.rotation) return;
            _placement = p?.Copy();
            var targetPos = BoardMath.TentCenter(_level, p.x, p.z);
            var targetYaw = BoardMath.TentYaw(p.rotation);
            _moveTween?.Kill(); _rotateTween?.Kill();
            if (instant)
            {
                _root.transform.localPosition = targetPos;
                _root.transform.localEulerAngles = new Vector3(0f, targetYaw, 0f);
                return;
            }
            var d = CampMotion.Change * Mathf.Max(0.01f, durationScale);
            _moveTween = _root.transform.DOLocalMove(targetPos, d).SetEase(CampMotion.Settle)
                .SetUpdate(true).SetLink(_root);
            _rotateTween = _root.transform.DOLocalRotate(new Vector3(0f, targetYaw, 0f), d)
                .SetEase(CampMotion.Settle).SetUpdate(true).SetLink(_root);
        }

        /// <summary>Lifts the visual node while the tent is selected/dragged.</summary>
        public void SetLifted(bool lifted, bool reducedMotion, float durationScale = 1f)
        {
            _liftTween?.Kill();
            var y = lifted ? BoardMath.SelectedLift : 0f;
            if (reducedMotion) { _liftNode.localPosition = new Vector3(0f, y, 0f); return; }
            _liftTween = _liftNode.DOLocalMoveY(y, CampMotion.Release * Mathf.Max(0.01f, durationScale))
                .SetEase(CampMotion.Settle).SetUpdate(true).SetLink(_root);
        }

        public void Appear(bool reducedMotion, float durationScale)
        {
            _scaleTween?.Kill();
            _liftNode.localScale = Vector3.one;
            if (reducedMotion) return;
            _liftNode.localScale = Vector3.one * .82f;
            _scaleTween = _liftNode.DOScale(1f, CampMotion.Enter * durationScale)
                .SetEase(CampMotion.Settle).SetUpdate(true).SetLink(_root);
        }

        public void Pulse(bool reducedMotion, float durationScale)
        {
            _scaleTween?.Kill();
            _liftNode.localScale = Vector3.one;
            if (reducedMotion) return;
            _scaleTween = DOTween.Sequence().SetUpdate(true).SetLink(_root)
                .Append(_liftNode.DOScale(1.025f, CampMotion.Press * durationScale).SetEase(Ease.OutSine))
                .Append(_liftNode.DOScale(1f, CampMotion.Release * durationScale).SetEase(CampMotion.Settle));
        }

        public void Disappear(bool reducedMotion, float durationScale)
        {
            _root.GetComponent<TentStoryVisual>()?.BeginRemoval();
            if (reducedMotion) { Dispose(); return; }
            _moveTween?.Kill(); _rotateTween?.Kill(); _liftTween?.Kill(); _scaleTween?.Kill();
            foreach (var collider in _root.GetComponentsInChildren<Collider>()) collider.enabled = false;
            _scaleTween = _liftNode.DOScale(0f, CampMotion.Exit * durationScale)
                .SetEase(Ease.InOutSine).SetUpdate(true).SetLink(_root)
                .OnComplete(() => { if (_root != null) Object.Destroy(_root); });
        }

        public void SetHeldCard(bool held)
        {
            _root.GetComponent<TentStoryVisual>()?.SetHeld(held);
            // Packing can hide the interior while a commit settles. Restore
            // those renderers too, otherwise they stay disabled after a drag.
            foreach(var renderer in _liftNode.GetComponentsInChildren<Renderer>(true))renderer.enabled=!held;
            var glow=_root.transform.Find("TentInteriorGlow");if(glow!=null)glow.gameObject.SetActive(!held);
            SetDoorMarkerVisible(!held);
        }

        public void SetDoorMarkerVisible(bool visible)
        {
            if (_doorMarker != null) _doorMarker.gameObject.SetActive(visible);
        }

        public void Dispose()
        {
            if(_root!=null)_root.GetComponent<TentStoryVisual>()?.BeginRemoval();
            _moveTween?.Kill();
            _rotateTween?.Kill();
            _liftTween?.Kill(); _scaleTween?.Kill();
            if (_root != null) Object.Destroy(_root);
        }
    }
}
