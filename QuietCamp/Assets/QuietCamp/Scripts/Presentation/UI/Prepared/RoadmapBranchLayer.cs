using System.Collections.Generic;
using QuietCamp.Domain;
using UnityEngine;
namespace QuietCamp.Presentation.UI.Prepared
{
    public sealed class RoadmapBranchLayer : MonoBehaviour
    {
        readonly RoadmapBranchGraphic[] _slots=new RoadmapBranchGraphic[8];
        readonly RoadmapBranchData[] _leases=new RoadmapBranchData[8];readonly bool[] _wanted=new bool[8];
        RoadmapGraphic _map;bool _dirty=true;
        public bool Ready {get {foreach(var slot in _slots)if(slot.gameObject.activeSelf&&!slot.Ready)return false;return true;}}
        public void Configure(RoadmapGraphic map)
        {_map=map;for(int i=0;i<8;i++){_slots[i]=QcUi.Stretch((RectTransform)transform,"BranchArt"+i).gameObject.AddComponent<RoadmapBranchGraphic>();_slots[i].material=map.material;_slots[i].gameObject.SetActive(false);}}
        public void Invalidate()=>_dirty=true;
        void LateUpdate()
        {
            if(!_dirty||_map.VisibleArea.height<=0)return;_dirty=false;
            for(int i=0;i<8;i++)_wanted[i]=false;
            var data=_map.Data;var rect=_map.rectTransform.rect;int current=data.RegionAt(rect.yMax-_map.VisibleArea.center.y);
            for(int r=Mathf.Max(0,current-1);r<=Mathf.Min(current+1,data.Definition.regions.Length-1);r++)foreach(var branch in data.Definition.regions[r].branches)
            {
                var at=new Vector2(rect.xMin+branch.x*rect.width,rect.yMax-data.RegionStarts[r]-branch.y);
                if(!_map.BranchVisible(branch)||!_map.InView(at.y,240))continue;
                int slot=System.Array.IndexOf(_leases,branch);if(slot<0){for(int i=0;i<8;i++)if(!_wanted[i]&&!_slots[i].gameObject.activeSelf){slot=i;break;}}
                if(slot<0)continue;_leases[slot]=branch;_wanted[slot]=true;_slots[slot].gameObject.SetActive(true);
                _slots[slot].Configure(branch,_map,data.NodeIndex(branch.anchorNodeId),at,24);
            }
            for(int i=0;i<8;i++)if(!_wanted[i])_slots[i].gameObject.SetActive(false);
        }
    }
}
