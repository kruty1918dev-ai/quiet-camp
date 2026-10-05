using System;
using System.Collections.Generic;
using QuietCamp.Domain;
using UnityEngine;

namespace QuietCamp.Presentation.World
{
    /// <summary>Readable, seeded remnants and repaired objects. One coloured mesh;
    /// no text, physics objects, extra lights or gameplay rules.</summary>
    public static class CampStoryComposer
    {
        public static Mesh Compose(LevelData level, bool cared = false)
        {
            var descriptor=EnvironmentCompositionData.For(level);
            var motifs=descriptor.storyMotifs??Array.Empty<string>();
            var vertices=new List<Vector3>();var colors=new List<Color>();var indices=new List<int>();
            var wood=new Color(.39f,.31f,.22f);var stone=new Color(.43f,.45f,.38f);
            var cloth=new Color(.43f,.49f,.34f);var fresh=new Color(.66f,.49f,.30f);
            bool Has(string id)=>Array.IndexOf(motifs,id)>=0;
            void Box(Vector3 position,Vector3 size,Color color,float lean=0)
            {
                int first=vertices.Count;EnvironmentComposer.AppendBox(vertices,indices,position,size);
                for(int i=first;i<vertices.Count;i++)
                {
                    if(lean!=0)vertices[i]=position+Quaternion.Euler(0,0,lean)*(vertices[i]-position);
                    colors.Add(color);
                }
            }
            void Marker(bool repaired)
            {
                float tilt=repaired?0:-8;
                Box(new Vector3(-1,.53f,.45f),new Vector3(.13f,1.06f,.15f),wood,tilt);
                Box(new Vector3(-1,.90f,.45f),new Vector3(.68f,.18f,.13f),wood,tilt);
                if(repaired)
                {
                    Box(new Vector3(-.93f,.94f,.37f),new Vector3(.42f,.10f,.05f),fresh);
                    Box(new Vector3(-1,.62f,.45f),new Vector3(.18f,.045f,.18f),new Color(.72f,.66f,.48f));
                }
                else Box(new Vector3(-1.3f,.04f,.63f),new Vector3(.30f,.08f,.13f),wood,-5);
            }
            if(Has("marker"))Marker(false);
            if(Has("repaired-marker"))Marker(true);
            if(Has("bench"))
            {
                for(int board=0;board<3;board++)Box(new Vector3(.55f,.34f,-.3f+board*.14f),new Vector3(1.36f,.08f,.12f),wood);
                Box(new Vector3(0,.15f,-.18f),new Vector3(.12f,.3f,.40f),wood);
                Box(new Vector3(1.12f,.15f,-.18f),new Vector3(.12f,.3f,.40f),wood);
                Box(new Vector3(.55f,.59f,-.49f),new Vector3(1.36f,.13f,.08f),wood,-3);
            }
            if(Has("bag"))
            {
                Box(new Vector3(.45f,.21f,.65f),new Vector3(.46f,.42f,.30f),cloth,-7);
                Box(new Vector3(.45f,.39f,.65f),new Vector3(.49f,.10f,.34f),cloth);
                foreach(float x in new[]{.32f,.58f})Box(new Vector3(x,.21f,.47f),new Vector3(.035f,.40f,.04f),wood);
                Box(new Vector3(.45f,.51f,.63f),new Vector3(.19f,.04f,.06f),wood);
            }
            if(Has("lantern"))
            {
                var at=new Vector3(.98f,0,.64f);var frame=new Color(.28f,.32f,.28f);
                Box(at+Vector3.up*.04f,new Vector3(.23f,.08f,.23f),frame);
                Box(at+Vector3.up*.21f,new Vector3(.14f,.26f,.14f),new Color(.91f,.64f,.25f));
                foreach(float x in new[]{-.10f,.10f})foreach(float z in new[]{-.10f,.10f})
                    Box(at+new Vector3(x,.21f,z),new Vector3(.027f,.29f,.027f),frame);
                Box(at+Vector3.up*.39f,new Vector3(.23f,.08f,.23f),frame);
                Box(at+Vector3.up*.47f,new Vector3(.10f,.025f,.04f),frame);
            }
            if(Has("ruin")||Has("wall")||Has("settlement")||Has("chimney"))
            {
                // The same small foundation survives across the seasonal arc.
                Box(new Vector3(0,.045f,-1.6f),new Vector3(2.9f,.09f,2.5f),stone);
                Box(new Vector3(-1.1f,.46f,-1.6f),new Vector3(.31f,.92f,2.4f),stone);
                Box(new Vector3(-.3f,.24f,-2.73f),new Vector3(1.27f,.48f,.31f),stone);
                Box(new Vector3(.62f,.75f,-2.73f),new Vector3(.27f,1.5f,.31f),stone);
                Box(new Vector3(1.6f,.52f,-2.73f),new Vector3(.27f,1.04f,.31f),stone);
                Box(new Vector3(1.14f,1.20f,-2.73f),new Vector3(1.33f,.17f,.32f),wood,-6);
                Box(new Vector3(.57f,.10f,-.3f),new Vector3(.75f,.19f,.38f),stone);
                Box(new Vector3(-.5f,.1f,-.8f),new Vector3(.35f,.2f,.4f),stone,8);
                if(Has("bench"))
                {
                    // A broken bus-stop roof makes the former human space recognizable.
                    Box(new Vector3(1.17f,1.9f,.2f),new Vector3(2.15f,.11f,1.08f),wood,-4);
                    Box(new Vector3(.24f,.92f,.47f),new Vector3(.10f,1.84f,.1f),wood);
                    Box(new Vector3(2.1f,.69f,.47f),new Vector3(.10f,1.38f,.1f),wood,-8);
                }
            }
            if(Has("flowers"))
            {
                // Reused growing boxes beside the last chapter's greenhouse frame.
                for(int row=0;row<2;row++)
                {
                    var at=new Vector3(.35f,0,.85f+row*.45f);
                    Box(at+Vector3.up*.055f,new Vector3(.86f,.11f,.30f),wood);
                    for(int seedling=0;seedling<4;seedling++)
                    {
                        var p=at+new Vector3(-.3f+seedling*.2f,.15f,0);
                        Box(p,new Vector3(.025f,.13f,.025f),new Color(.29f,.44f,.22f));
                        Box(p+Vector3.up*.085f,new Vector3(.09f,.045f,.09f),
                            seedling%2==0?new Color(.79f,.78f,.60f):new Color(.64f,.28f,.20f));
                    }
                }
                foreach(float x in new[]{-.1f,1.2f})foreach(float z in new[]{.61f,1.65f})
                    Box(new Vector3(x,.56f,z),new Vector3(.055f,1.12f,.055f),fresh);
                foreach(float z in new[]{.61f,1.65f})
                {
                    Box(new Vector3(.55f,1.12f,z),new Vector3(1.35f,.055f,.055f),fresh);
                    Box(new Vector3(.55f,1.39f,z),new Vector3(.055f,.055f,.055f),fresh);
                    // Sloping roof frame; open panes let nature and sunlight through.
                    Box(new Vector3(.22f,1.25f,z),new Vector3(.055f,.73f,.055f),fresh,-65);
                    Box(new Vector3(.88f,1.25f,z),new Vector3(.055f,.73f,.055f),fresh,65);
                }
                Box(new Vector3(.55f,1.39f,1.13f),new Vector3(.055f,.055f,1.12f),fresh);
            }
            if(Has("lighthouse"))
            {
                Box(new Vector3(-.8f,1.35f,-1.4f),new Vector3(.94f,2.7f,.94f),stone);
                Box(new Vector3(-.8f,2.76f,-1.4f),new Vector3(1.20f,.12f,1.20f),wood);
                Box(new Vector3(-.8f,3.02f,-1.4f),new Vector3(.64f,.44f,.64f),cared && Has("beacon-ready") ? new Color(1.2f,.85f,.38f) : new Color(.26f,.34f,.34f));
                Box(new Vector3(-.8f,3.30f,-1.4f),new Vector3(.95f,.14f,.95f),wood);
            }
            if(Has("pier"))
            {
                for(int plank=0;plank<5;plank++)Box(new Vector3(.65f,.14f,-1.45f+plank*.24f),new Vector3(1.08f,.075f,.18f),wood);
                foreach(float x in new[]{.18f,1.12f})Box(new Vector3(x,.38f,-1.8f),new Vector3(.10f,.76f,.10f),wood);
            }
            if(cared && Has("bench"))
            {
                foreach(float x in new[]{.35f,.70f})
                {
                    Box(new Vector3(x,.43f,-.18f),new Vector3(.10f,.14f,.10f),new Color(.67f,.45f,.29f));
                    Box(new Vector3(x+.06f,.43f,-.18f),new Vector3(.035f,.075f,.055f),new Color(.67f,.45f,.29f));
                }
            }
            if(vertices.Count==0)return null;
            var mesh=new Mesh{name="Quiet remnants: "+string.Join(", ",motifs)};
            mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            return mesh;
        }
    }
}
