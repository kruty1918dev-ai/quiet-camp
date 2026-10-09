using System;
using System.Collections.Generic;
using QuietCamp.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace QuietCamp.Presentation.UI.Prepared
{
    /// <summary>Fixed native control pool on top of art, under the scroll mask. Never one control per catalog item.</summary>
    public sealed class RoadmapNodeLayer : MonoBehaviour
    {
        sealed class Slot
        {
            public RectTransform Rect;
            public Button Button;
            public RoadmapControlGraphic Background;
            public Image Status;
            public TextMeshProUGUI Label;
            public string Id,LevelId;
            public RoadmapBranchData Branch;
            public bool Wanted;
            public CanvasGroup Reveal;
        }
        readonly List<Slot> _pool=new List<Slot>(24);
        readonly TextMeshProUGUI[] _regionLabels=new TextMeshProUGUI[3];
        readonly Dictionary<string,string> _numbers=new Dictionary<string,string>(StringComparer.Ordinal);
        readonly HashSet<string> _requested=new HashSet<string>(StringComparer.Ordinal);
        RoadmapGraphic _map;GameServices _services;
        Sprite _check,_lock,_leaf;TMP_FontAsset _font;
        bool _dirty=true;
        public int ActiveControls {get;private set;}
        public bool Prepared {get;private set;}
        public void Configure(RoadmapGraphic map,GameServices services)
        {
            _map=map;_services=services;
            foreach(var node in map.Data.Nodes)_numbers.Add(node.id,node.order.ToString());
            _font=Resources.Load<TMP_FontAsset>("Fonts/DejaVuSans SDF");
            _check=Resources.Load<Sprite>("QuietCamp/UI/Icons/check");_lock=Resources.Load<Sprite>("QuietCamp/UI/Icons/lock");
            _leaf=Resources.Load<Sprite>("QuietCamp/UI/Atlas/orb_leaf");
            for(int i=0;i<24;i++)
            {
                var rect=new GameObject("RoadmapControl",typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(transform,false);
                rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(.5f,.5f);
                var image=rect.gameObject.AddComponent<RoadmapControlGraphic>();
                var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;
                var slot=new Slot{Rect=rect,Button=button,Background=image,Reveal=rect.gameObject.AddComponent<CanvasGroup>()};
                slot.Label=Label(rect,"NodeLabel");slot.Label.rectTransform.offsetMin=new Vector2(12,8);slot.Label.rectTransform.offsetMax=new Vector2(-12,-8);
                var icon=QcUi.Stretch(rect,"NodeStatus");icon.anchorMin=icon.anchorMax=Vector2.one;icon.sizeDelta=new Vector2(24,24);icon.anchoredPosition=new Vector2(-10,-10);
                slot.Status=icon.gameObject.AddComponent<Image>();slot.Status.preserveAspect=true;slot.Status.raycastTarget=false;
                button.onClick.AddListener(()=>{if(!_map.InputEnabled)return;if(slot.Branch!=null)_map.SelectBranch(slot.Branch);else if(slot.LevelId!=null)_map.SelectLevel(slot.LevelId);});
                _pool.Add(slot);rect.gameObject.SetActive(false);
            }
            for(int i=0;i<3;i++){_regionLabels[i]=Label((RectTransform)transform,"RegionTitle"+i);_regionLabels[i].rectTransform.anchorMin=_regionLabels[i].rectTransform.anchorMax=new Vector2(.5f,1);_regionLabels[i].rectTransform.sizeDelta=new Vector2(500,70);_regionLabels[i].fontSize=26;_regionLabels[i].color=new Color(.95f,.92f,.78f,.94f);_regionLabels[i].gameObject.SetActive(false);}
        }
        TextMeshProUGUI Label(RectTransform parent,string name)
        {var r=QcUi.Stretch(parent,name);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=_font;t.alignment=TextAlignmentOptions.Center;t.raycastTarget=false;t.textWrappingMode=TextWrappingModes.Normal;return t;}
        public void Invalidate()=>_dirty=true;
        string T(string key)=>_services.Localization?.T(key)??key;
        void LateUpdate()
        {
            if(!_dirty||_map?.Data==null)return;_dirty=false;ActiveControls=0;
            using var timing=_map.Work.Measure(RoadmapWorkMetrics.Controls);
            UnityEngine.Profiling.Profiler.BeginSample("Roadmap.Controls.Update");
            try
            {
                foreach(var slot in _pool)slot.Wanted=false;
                var data=_map.Data;var rect=_map.rectTransform.rect;int current=data.RegionAt(rect.yMax-_map.VisibleArea.center.y);
                int first=Mathf.Max(0,current-1),last=Mathf.Min(data.Definition.regions.Length-1,current+1);
                // Reserve all visible identities before recycling. A newly arriving node must
                // not steal a cached control needed later in this same update.
                _requested.Clear();
                for(int r=first;r<=last;r++)
                {
                    var region=data.Definition.regions[r];
                    foreach(var node in region.nodePositions)
                    {int index=data.NodeIndex(node.id);if(_map.InView(_map.ControlCentre(index).y,100)&&_map.Reveal(index)!=RoadmapReveal.Hidden)_requested.Add(node.id);}
                    foreach(var branch in region.branches)
                    {int anchor=data.NodeIndex(branch.anchorNodeId);float y=_map.Position(branch.x,data.RegionStarts[r]+branch.y).y;
                     if(anchor>=0&&_map.BranchVisible(branch)&&_map.InView(y,140))_requested.Add(branch.id);}
                }
                int labelIndex=0;
                for(int r=first;r<=last;r++)
                {
                    var region=data.Definition.regions[r];int regionNode=data.NodeIndex(region.nodePositions[0].id);
                    var title=_regionLabels[labelIndex++];bool known=_map.Reveal(regionNode)==RoadmapReveal.Revealed;
                    title.gameObject.SetActive(known);if(known){title.text=T(region.titleKey);title.rectTransform.anchoredPosition=new Vector2(0,-data.RegionStarts[r]-75);}
                    foreach(var node in region.nodePositions)
                    {
                        int index=data.NodeIndex(node.id);var at=_map.ControlCentre(index);
                        if(!_map.InView(at.y,100)||_map.Reveal(index)==RoadmapReveal.Hidden)continue;
                        var slot=Acquire(node.id);if(slot==null)continue;
                        bool done=_services.Progression.IsCompleted(node.levelId),locked=_map.Reveal(index)!=RoadmapReveal.Revealed;
                        bool currentNode=_map.IsWorldDiorama&&index==_map.Frontier&&!done;
                        Bind(slot,node.id,node.levelId,null,_numbers[node.id],at,new Vector2(96,96),currentNode?new Color(.18f,.34f,.27f):locked?new Color(.69f,.74f,.60f,.8f):done?new Color(.65f,.76f,.59f):new Color(.93f,.90f,.75f),done?_check:locked?_lock:null);
                    }
                    foreach(var branch in region.branches)
                    {
                        int anchor=data.NodeIndex(branch.anchorNodeId);var at=_map.Position(branch.x,data.RegionStarts[r]+branch.y);
                        if(anchor<0||!_map.BranchVisible(branch)||!_map.InView(at.y,140))continue;
                        if(_map.IsWorldDiorama)at+=new Vector2(0,-70);
                        var slot=Acquire(branch.id);if(slot==null)continue;
                        Bind(slot,branch.id,null,branch,T(branch.titleKey),at,(_map.IsWorldDiorama?new Vector2(220,88):new Vector2(290,132)),new Color(.88f,.85f,.69f),data.BranchAvailable(branch,_services.Progression)?_leaf:_lock);
                    }
                }
                for(int i=labelIndex;i<3;i++)_regionLabels[i].gameObject.SetActive(false);
                foreach(var slot in _pool){slot.Rect.gameObject.SetActive(slot.Wanted);if(slot.Wanted)ActiveControls++;}
                Prepared=true;
            }
            finally{UnityEngine.Profiling.Profiler.EndSample();}
        }
        Slot Acquire(string id)
        {
            foreach(var slot in _pool)if(slot.Id==id){slot.Wanted=true;return slot;}
            foreach(var slot in _pool)if(!slot.Wanted&&!_requested.Contains(slot.Id)&&!slot.Rect.gameObject.activeSelf){slot.Id=id;slot.Wanted=true;return slot;}
            // Retire offscreen controls before recycling; never steal a currently requested identity.
            foreach(var slot in _pool)if(!slot.Wanted&&!_requested.Contains(slot.Id)){slot.Id=id;slot.Wanted=true;return slot;}
            Debug.LogError("Roadmap control budget exhausted");return null;
        }
        void Bind(Slot slot,string id,string levelId,RoadmapBranchData branch,string text,Vector2 position,Vector2 size,Color color,Sprite icon)
        {
            slot.Id=id;slot.LevelId=levelId;slot.Branch=branch;
            slot.Rect.name=branch==null?"<button #level-"+(_map.Data.LevelIndex(levelId))+">":"<button #branch-"+branch.id+">";slot.Rect.anchoredPosition=position-new Vector2(_map.rectTransform.rect.xMin,_map.rectTransform.rect.yMax);slot.Rect.sizeDelta=size;
            if(slot.Label.text!=text)slot.Label.text=text;slot.Label.fontSize=(branch==null?(_map.IsWorldDiorama?18:38):(_map.IsWorldDiorama?20:25))*(_services.Settings?.textScale??1);
            slot.Label.enableAutoSizing=true;slot.Label.fontSizeMax=slot.Label.fontSize;slot.Label.fontSizeMin=(_map.IsWorldDiorama?16:22)*(_services.Settings?.textScale??1);
            slot.Label.textWrappingMode=branch==null?TextWrappingModes.NoWrap:TextWrappingModes.Normal;
            int index=branch==null?_map.Data.LevelIndex(levelId):_map.Data.NodeIndex(branch.anchorNodeId);
            bool current=_map.IsWorldDiorama&&branch==null&&index==_map.Frontier&&!_services.Progression.IsCompleted(levelId);
            slot.Label.color=current?new Color(.97f,.94f,.82f):new Color(.13f,.25f,.21f);slot.Background.Configure(branch!=null,_map.IsWorldDiorama,current);
            // Existing world stays readable; newly revealed controls fade with the fog retreat.
            slot.Reveal.alpha=index<=_map.Frontier?_map.RevealOpacity(index):.65f*_map.RevealOpacity(Mathf.Max(0,index-2));
            slot.Reveal.interactable=index<=_map.Frontier?slot.Reveal.alpha>.95f:slot.Reveal.alpha>.60f;
            slot.Status.rectTransform.anchoredPosition=_map.IsWorldDiorama&&branch==null?new Vector2(-27,-27):new Vector2(-10,-10);slot.Background.color=color;slot.Status.sprite=icon;slot.Status.enabled=icon!=null;
        }
    }
}
