using System;
using System.Collections.Generic;
using QuietCamp.Domain;
using UnityEngine;

namespace QuietCamp.Presentation.World
{
    /// <summary>Event-driven cosmetic packing. A finite search never moves tents, reserves
    /// gameplay cells or falls back to an unsafe nearest point. Unplaced items go indoors.</summary>
    public static class TentBelongingsLayout
    {
        public const int OutdoorItems=2;
        public static readonly Vector2[] Sizes={new Vector2(.30f,.28f),new Vector2(.24f,.24f)};
        public static List<Rect> Reservations(LevelData level,IReadOnlyList<Placement> placements)
        {
            var result=new List<Rect>();var occupied=new HashSet<Cell>();
            void CellRect(Cell c){var centre=BoardMath.CellCenterWorld(level,c);result.Add(new Rect(centre.x-.52f,centre.z-.52f,1.04f,1.04f));}
            foreach(var b in level.blocked??Array.Empty<int[]>())if(b!=null&&b.Length==2){var c=new Cell(b[0],b[1]);occupied.Add(c);CellRect(c);}
            foreach(var p in placements)
            {
                foreach(var c in RuleEvaluator.Footprint(p)){occupied.Add(c);CellRect(c);}
                CellRect(RuleEvaluator.Door(p));
            }
            if(level.entry?.Length==2)
            {
                var entry=new Cell(level.entry[0],level.entry[1]);CellRect(entry);
                foreach(var p in placements)
                    foreach(var c in CampWalkability.Path(level,occupied,entry,RuleEvaluator.Door(p))??new List<Cell>())CellRect(c);
                foreach(var point in level.accessPoints??Array.Empty<AccessPointData>())
                {
                    var goal=new Cell(point.x,point.z);CellRect(goal);
                    foreach(var c in CampWalkability.Path(level,occupied,entry,goal)??new List<Cell>())CellRect(c);
                }
            }
            foreach(var c in level.exteriorWalkable??Array.Empty<int[]>())if(c?.Length==2)CellRect(new Cell(c[0],c[1]));
            return result;
        }
        public static Rect[] Find(LevelData level,Placement placement,Rect body,List<Rect> occupied)
        {
            var result=new Rect[OutdoorItems];
            // The model is narrower than its 2x2 gameplay footprint. Search
            // beyond both, otherwise every candidate hits its own reservation
            // and a roomy clearing always sends everything indoors.
            var centre=BoardMath.TentCenter(level,placement.x,placement.z);
            body=Rect.MinMaxRect(Mathf.Min(body.xMin,centre.x-1),Mathf.Min(body.yMin,centre.z-1),
                Mathf.Max(body.xMax,centre.x+1),Mathf.Max(body.yMax,centre.z+1));
            var field=new Rect(-level.width*.5f+.04f,-level.height*.5f+.04f,level.width-.08f,level.height-.08f);
            var front=placement.rotation==0?Vector2.up:placement.rotation==1?Vector2.right:placement.rotation==2?Vector2.down:Vector2.left;
            var sides=new[]{Vector2.left,Vector2.right,Vector2.down,Vector2.up};
            int seed=0;foreach(char c in placement.guestId??"")seed=unchecked(seed*31+c);
            for(int item=0;item<OutdoorItems;item++)
            {
                var size=Sizes[item];bool placed=false;
                for(int side=0;side<4&&!placed;side++)
                {
                    var normal=sides[((seed&3)+side)%4];if(normal==front)continue;
                    var tangent=new Vector2(-normal.y,normal.x);
                    for(int step=0;step<5&&!placed;step++)
                    {
                        float lateral=step==0?0:(step%2==1?-1:1)*((step+1)/2)*.38f;
                        var at=body.center+Vector2.Scale(normal,body.size*.5f+size*.5f+Vector2.one*.13f)+tangent*lateral;
                        var candidate=new Rect(at-size*.5f,size);
                        if(!Safe(level,field,candidate,occupied))continue;
                        result[item]=candidate;occupied.Add(new Rect(candidate.position-Vector2.one*.045f,candidate.size+Vector2.one*.09f));placed=true;
                    }
                }
            }
            return result;
        }
        public static bool Safe(LevelData level,Rect field,Rect item,IReadOnlyList<Rect> occupied)
        {
            if(item.width<=0||item.height<=0||item.xMin<field.xMin||item.xMax>field.xMax||item.yMin<field.yMin||item.yMax>field.yMax)return false;
            foreach(var obstacle in occupied)if(item.Overlaps(obstacle))return false;
            for(int i=0;i<4;i++)if(ShorelineGeometry.Contains(level.environment?.shore,new Vector3((i&1)==0?item.xMin:item.xMax,0,(i&2)==0?item.yMin:item.yMax),.12f))return false;
            return true;
        }
    }
}
