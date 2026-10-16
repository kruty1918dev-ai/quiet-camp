using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace QuietCamp.Editor.LevelAuthoring.WorldOps
{
    public static class WorldOpsVisibility
    {
        public static List<ValidationFinding> Project(Camera camera,CampArtDefinition art,Rect gameplay,WorldCriteria criteria)
        {
            var result=new List<ValidationFinding>();
            void Add(string code,string obj,string message,float measured=0,float limit=0)=>result.Add(new ValidationFinding{code=code,levelId=art.levelId,objectId=obj,message=message,measured=measured,limit=limit});
            var centre=camera.WorldToViewportPoint(Vector3.zero);float error=new Vector2((centre.x-gameplay.center.x)*camera.pixelWidth,(centre.y-gameplay.center.y)*camera.pixelHeight).magnitude;
            if(error>criteria.maximumCentreError)Add("camera.field-centre",null,"Playable field is not centred in the available gameplay viewport.",error,criteria.maximumCentreError);
            var cell=camera.WorldToScreenPoint(Vector3.right)-camera.WorldToScreenPoint(Vector3.zero);
            if(cell.magnitude<criteria.minimumCellPixels)Add("camera.cell-scale",null,"Cells are too small at the gameplay resolution.",cell.magnitude,criteria.minimumCellPixels);
            var p=art.instances.FirstOrDefault(x=>x.id==art.primaryLandmarkInstanceId);var m=art.models.FirstOrDefault(x=>x.id==p?.modelId);
            if(p!=null&&m!=null)
            {
                var vertices=WorldOpsGeometry.Vertices(p,m).Select(camera.WorldToViewportPoint).ToArray();
                int outside=vertices.Count(v=>v.z<=0||v.x<gameplay.xMin||v.x>gameplay.xMax||v.y<gameplay.yMin||v.y>gameplay.yMax);
                if(outside>0)Add("landmark.clipped",p.id,"Transformed primary geometry is clipped by the frame or the gameplay UI boundary.",outside,0);
            }
            var plane=new Plane(Vector3.up,Vector3.zero);
            foreach(var corner in new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up})
            {
                var ray=camera.ViewportPointToRay(corner);
                if(!plane.Raycast(ray,out float distance)){Add("camera.ground-edge",null,"The frame leaves the decorated ground.");break;}
                var point=ray.GetPoint(distance);
                if(Mathf.Abs(point.x)>art.surroundExtent-1||Mathf.Abs(point.z)>art.surroundExtent-1){Add("camera.ground-edge",null,"The frame leaves the decorated ground.");break;}
            }
            return result;
        }
        // Diagnostic render only. Beauty PNGs always come from ScreenCapture/Game View.
        public static void Capture(string path,WorldCriteria criteria)
        {
            var camera=Camera.main;var view=UnityEngine.Object.FindAnyObjectByType<CampArtView>();
            if(camera==null||view?.Definition==null)throw new InvalidOperationException("The real gameplay camera is required.");
            var art=view.Definition;var viewport=camera.GetComponent<Kruty1918.GameplayViewport.GameplayViewport>();
            var rect=viewport!=null?viewport.Frame.NormalizedGameplay:new Rect(0,0,1,1);
            var findings=Project(camera,art,rect,criteria);
            var primary=art.instances.SingleOrDefault(i=>i.id==art.primaryLandmarkInstanceId);var model=art.models.SingleOrDefault(m=>m.id==primary?.modelId);
            if(primary==null||model==null){WorldOpsStorage.Json(path+".diagnostics.json",new{kind="diagnostic-only",findings,mask=false});return;}
            var shader=Shader.Find("Hidden/QuietCamp/WorldOpsMask");if(shader==null)throw new InvalidOperationException("WorldOps diagnostic shader was not imported.");
            var material=new Material(shader){hideFlags=HideFlags.HideAndDontSave};
            var mesh=new Mesh{indexFormat=IndexFormat.UInt32,name="WorldOps primary diagnostic"};
            mesh.vertices=WorldOpsGeometry.Vertices(primary,model);mesh.triangles=Enumerable.Range(0,mesh.vertexCount).ToArray();mesh.RecalculateBounds();
            int width=Screen.width,height=Screen.height,remaining=2,visible=0,potential=0;string error=null;
            void Finish()
            {
                if(--remaining>0)return;
                if(error==null&&potential>0&&visible/(float)potential<.75f)findings.Add(new ValidationFinding{code="landmark.occluded",levelId=art.levelId,objectId=primary.id,message="Less than 75% of the projected primary silhouette survives scene depth.",measured=visible/(float)potential,limit=.75f});
                if(error!=null)findings.Add(new ValidationFinding{code="mask.readback",levelId=art.levelId,message=error});
                WorldOpsStorage.Json(path+".diagnostics.json",new{kind="diagnostic-id-and-depth-not-gameplay",width,height,findings,visible,potential,asyncReadback=SystemInfo.supportsAsyncGPUReadback,depthRangeMetres=512});
                UnityEngine.Object.DestroyImmediate(mesh);UnityEngine.Object.DestroyImmediate(material);
            }
            void Render(bool isolated)
            {
                var rt=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear){antiAliasing=1};rt.Create();
                using(var cmd=new CommandBuffer{name="WorldOps ID/depth diagnostics"})
                {
                    cmd.SetRenderTarget(rt);cmd.ClearRenderTarget(true,true,Color.black);
                    cmd.SetViewProjectionMatrices(camera.worldToCameraMatrix,GL.GetGPUProjectionMatrix(camera.projectionMatrix,true));
                    if(!isolated)
                    {
                        cmd.SetGlobalFloat("_WorldOpsId",0);
                        foreach(var renderer in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r=>r.enabled&&r.gameObject.activeInHierarchy&&(camera.cullingMask&(1<<r.gameObject.layer))!=0))
                        {
                            var filter=renderer.GetComponent<MeshFilter>();if(filter?.sharedMesh==null)continue;
                            for(int sub=0;sub<filter.sharedMesh.subMeshCount;sub++)cmd.DrawRenderer(renderer,material,sub,0);
                        }
                    }
                    cmd.SetGlobalFloat("_WorldOpsId",1);cmd.DrawMesh(mesh,Matrix4x4.identity,material,0,0);Graphics.ExecuteCommandBuffer(cmd);
                }
                void Save(byte[] pixels)
                {
                    var texture=new Texture2D(width,height,TextureFormat.RGBA32,false,true);
                    try{texture.LoadRawTextureData(pixels);texture.Apply();var colors=texture.GetPixels32();int count=colors.Count(p=>p.r>127);if(isolated)potential=count;else visible=count;File.WriteAllBytes(path+(isolated?".isolated.png":".id-depth.png"),texture.EncodeToPNG());}
                    catch(Exception e){error=e.Message;}
                    finally{UnityEngine.Object.DestroyImmediate(texture);rt.Release();UnityEngine.Object.DestroyImmediate(rt);Finish();}
                }
                if(SystemInfo.supportsAsyncGPUReadback)AsyncGPUReadback.Request(rt,0,TextureFormat.RGBA32,r=>{if(r.hasError){error="GPU readback failed.";rt.Release();UnityEngine.Object.DestroyImmediate(rt);Finish();}else Save(r.GetData<byte>().ToArray());});
                else
                {
                    var old=RenderTexture.active;var texture=new Texture2D(width,height,TextureFormat.RGBA32,false,true);
                    try{RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();Save(texture.GetRawTextureData<byte>().ToArray());}
                    finally{RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(texture);}
                }
            }
            Render(false);Render(true);
        }
    }
}
