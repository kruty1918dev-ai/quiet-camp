using System;
using System.Linq;
using QuietCamp.Infrastructure;
using UnityEditor;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;

namespace QuietCamp.Editor.LevelAuthoring
{
    public sealed partial class LevelAuthoringWindow
    {
        float _modelScale=1,_modelAge=.4f,_modelDensity=.75f,_cutOffset;
        int _ageKind,_detailBackend,_cutAxis;
        string _variantLabel="Мій варіант";
        GameObject _modelEditObject;
        LevelAuthoringAsset _modelEditOriginal;
        Mesh _modelEditMesh;
        AuthoredModelVariant _modelPreview;
        bool _compareOriginal;
        ModelAuthoringWorkshop.DetailJob _detailJob;
        LevelAuthoringAsset _detailOriginal;
        static readonly string[] AgeNames={"Мох","Вицвіле дерево","Окиснення металу","Бруд біля основи"};
        static readonly string[] AgeKinds={"moss","fade","rust","dirt"};

        LevelAuthoringAsset SelectedModel()
        {
            var go=Selection.activeGameObject;
            var marker=go==null?null:go.GetComponentInParent<LevelAuthoringAsset>();
            return marker!=null && marker.packedModel && _workspace!=null && marker.transform.IsChildOf(_workspace.Scenery)?marker:null;
        }
        CampArtDefinition.Model SourceModel(LevelAuthoringAsset marker) => _workspace.FrozenModel(marker.stableId);
        void DrawModelWorkshop()
        {
            var marker=_modelEditOriginal??SelectedModel();
            EditorGUILayout.HelpBox("Оберіть декоративний об’єкт у Scene View. Зміни створюють окремий варіант; оригінал і решта екземплярів зберігаються.",MessageType.Info);
            if(marker==null){EditorGUILayout.LabelField("Оберіть дерево, пропс або частину літака.");return;}
            var model=SourceModel(marker);var bounds=AuthoringPropCatalog.ModelBounds(model);
            EditorGUILayout.LabelField("Об’єкт",LevelAuthoringPalette.Get(marker.stableId).label);
            EditorGUILayout.LabelField("Габарити моделі, м",$"{bounds.size.x:F2} × {bounds.size.y:F2} × {bounds.size.z:F2}");
            var actual=Vector3.Scale(bounds.size,marker.transform.lossyScale);
            EditorGUILayout.LabelField("Габарити екземпляра, м",$"{actual.x:F2} × {actual.y:F2} × {actual.z:F2}");
            EditorGUILayout.LabelField("Трикутники",(model.colors.Length/3).ToString());
            _variantLabel=EditorGUILayout.TextField("Назва варіанта",_variantLabel);
            using(new EditorGUI.DisabledScope(_modelEditObject!=null || _detailJob!=null))
            {
                EditorGUILayout.Space();EditorGUILayout.LabelField("Масштаб",EditorStyles.boldLabel);
                _modelScale=EditorGUILayout.FloatField("Множник геометрії",_modelScale);
                EditorGUILayout.LabelField("Після зміни, м",$"{bounds.size.x*_modelScale:F2} × {bounds.size.y*_modelScale:F2} × {bounds.size.z*_modelScale:F2}");
                if(GUILayout.Button("Переглянути правильний розмір"))Try(()=>ApplyModelRecipe(marker,"scale"));
                EditorGUILayout.Space();EditorGUILayout.LabelField("Деталізація",EditorStyles.boldLabel);
                _modelDensity=EditorGUILayout.Slider("Зберегти деталей",_modelDensity,.2f,1);
                _detailBackend=EditorGUILayout.Popup("Алгоритм",_detailBackend,new[]{"Захист стилю QuietCamp","UnityMeshSimplifier"});
                if(GUILayout.Button("Переглянути спрощений варіант"))Try(()=>ApplyModelRecipe(marker,"simplify"));
                EditorGUILayout.Space();EditorGUILayout.LabelField("Старіння",EditorStyles.boldLabel);
                _ageKind=EditorGUILayout.Popup("Матеріал старіння",_ageKind,AgeNames);_modelAge=EditorGUILayout.Slider("Інтенсивність",_modelAge,0,1);
                if(GUILayout.Button("Переглянути старіння"))Try(()=>ApplyModelRecipe(marker,"age"));
                if(GUILayout.Button("Малювати старіння пензлем Polybrush"))Try(()=>BeginModelSession(marker,false));
                EditorGUILayout.Space();EditorGUILayout.LabelField("Розділення й форма",EditorStyles.boldLabel);
                if(GUILayout.Button("Редагувати копію в ProBuilder"))Try(()=>BeginModelSession(marker,true));
                _cutAxis=EditorGUILayout.Popup("Площина розрізу",_cutAxis,new[]{"X","Y","Z"});_cutOffset=EditorGUILayout.FloatField("Зсув площини, м",_cutOffset);
                EditorGUILayout.HelpBox("EzySlice перевіряє опуклість і переносить кольори на нові вершини. Порожнистий літак розділяйте інструментами граней ProBuilder.",MessageType.None);
                if(GUILayout.Button("Розрізати опуклу модель на дві частини"))Try(()=>SplitModel(marker));
            }
            if(_detailJob!=null)
            {
                EditorGUILayout.HelpBox("Обчислення в окремому потоці. Сцена залишається доступною для огляду.",MessageType.Info);
                if(GUILayout.Button("Скасувати обчислення")){_detailJob.Dispose();_detailJob=null;_detailOriginal=null;}
            }
            if(_modelEditObject!=null)
            {
                bool compare=EditorGUILayout.Toggle("Показати оригінал для порівняння",_compareOriginal);
                if(compare!=_compareOriginal){_compareOriginal=compare;_modelEditObject.SetActive(!compare);_modelEditOriginal.GetComponent<MeshRenderer>().enabled=compare;SceneView.RepaintAll();}
                EditorGUILayout.HelpBox("Редагуйте вибрану копію. Після завершення натисніть «Зберегти». Для відділених граней ProBuilder можна вибрати отриману частину перед збереженням.",MessageType.Info);
                if(GUILayout.Button("Зберегти вибрану геометрію як варіант"))Try(CommitModelSession);
                if(GUILayout.Button("Скасувати редагування копії"))CancelModelSession();
            }
        }
        void ApplyModelRecipe(LevelAuthoringAsset marker,string operation)
        {
            var entry=LevelAuthoringPalette.Get(marker.stableId);var r=new ModelAuthoringRecipe{label=_variantLabel,category=entry.category,operation=operation,size=_modelScale,amount=_modelAge,density=_modelDensity,seed=Document.level.seed,
                backend=operation=="age"?AgeKinds[_ageKind]:_detailBackend==0?"protected":"fast"};
            if(operation=="simplify") {_detailOriginal=marker;_detailJob=new ModelAuthoringWorkshop.DetailJob(SourceModel(marker),r);return;}
            var preview=ModelAuthoringWorkshop.Create(SourceModel(marker),r,false);
            PreviewVariant(marker,preview);
        }
        void PreviewVariant(LevelAuthoringAsset marker,AuthoredModelVariant preview)
        {
            BeginModelSession(marker,false,false);_modelPreview=preview;
            LevelAuthoringPalette.PopulateMesh(_modelEditMesh,preview.model);
            _message=$"Попередній перегляд: {SourceModel(marker).colors.Length/3} → {preview.model.colors.Length/3} трикутників. Збережіть або скасуйте.";
        }
        void PollModelJob()
        {
            if(_detailJob==null || !_detailJob.IsDone)return;
            var job=_detailJob;var marker=_detailOriginal;_detailJob=null;_detailOriginal=null;
            try{if(marker!=null)PreviewVariant(marker,job.Finish());}catch(Exception e){_message=e.Message;}finally{job.Dispose();Repaint();}
        }
        void CommitVariant(LevelAuthoringAsset marker,AuthoredModelVariant variant)
        {
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Варіант моделі");
            _workspace.ReplaceModel(marker,variant);var d=_workspace.Capture(false);Undo.RecordObject(_state,"Варіант моделі");_state.Write(d);_workspace.ApplyDocument(d);
            Undo.CollapseUndoOperations(group);_displayDocument=null;_dirty=true;_message="Варіант збережено в палітрі. Ctrl+Z повертає попередню модель.";Repaint();
        }
        void SplitModel(LevelAuthoringAsset marker)
        {
            var source=SourceModel(marker);var axes=new[]{Vector3.right,Vector3.up,Vector3.forward};
            var variants=ModelAuthoringWorkshop.Slice(source,axes[_cutAxis],_cutOffset,_variantLabel);
            var snapshot=_workspace.Capture(false);var pose=snapshot.art.instances.Single(i=>i.id==marker.instanceId);
            CommitVariant(marker,variants[0]);var copy=LevelAuthoringDocument.Copy(pose);copy.id="authored/"+Guid.NewGuid().ToString("N");copy.modelId=variants[1].model.id;
            copy.x+=.12f;_workspace.Spawn(copy);_state.Write(_workspace.Capture(false));_dirty=true;
        }
        void BeginModelSession(LevelAuthoringAsset marker,bool probuilder,bool openTool=true)
        {
            CancelModelSession();PlacementToolIntegration.Adapter?.Stop();_modelEditOriginal=marker;
            _modelEditObject=new GameObject("Редагована копія — "+marker.name);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(_modelEditObject,_workspace.Scene);
            _modelEditObject.transform.SetParent(_workspace.Root.transform,false);_modelEditObject.transform.position=marker.transform.position;_modelEditObject.transform.rotation=marker.transform.rotation;_modelEditObject.transform.localScale=marker.transform.lossyScale;
            _modelEditMesh=UnityEngine.Object.Instantiate(marker.GetComponent<MeshFilter>().sharedMesh);_modelEditObject.AddComponent<MeshFilter>().sharedMesh=_modelEditMesh;
            var renderer=_modelEditObject.AddComponent<MeshRenderer>();renderer.sharedMaterials=marker.GetComponent<MeshRenderer>().sharedMaterials;
            marker.GetComponent<MeshRenderer>().enabled=false;Selection.activeGameObject=_modelEditObject;
            if(probuilder)
            {
                var mesh=_modelEditObject.AddComponent<ProBuilderMesh>();new MeshImporter(_modelEditMesh,renderer.sharedMaterials,mesh).Import();
                var context=UnityEngine.Assemblies.CurrentAssemblies.GetLoadedAssemblies().Select(a=>a.GetType("UnityEditor.ProBuilder.PositionToolContext")).FirstOrDefault(t=>t!=null);
                if(context==null)throw new InvalidOperationException("Не знайдено контекст ProBuilder 6.1.");
                UnityEditor.EditorTools.ToolManager.SetActiveContext(context);SceneView.lastActiveSceneView?.Focus();
            }
            else if(openTool)EditorApplication.ExecuteMenuItem("Tools/Polybrush/Polybrush Window");
        }
        void CommitModelSession()
        {
            var selected=Selection.activeGameObject;var filter=selected==null?null:selected.GetComponent<MeshFilter>();
            if(filter==null)throw new InvalidOperationException("Оберіть відредаговану геометрію.");
            if(selected.scene!=_workspace.Scene || selected.GetComponentInParent<LevelAuthoringAsset>()!=null)throw new InvalidOperationException("Зберігайте саме робочу копію або відділені від неї грані.");
            var source=SourceModel(_modelEditOriginal);
            var model=ModelAuthoringWorkshop.ReadMesh(filter.sharedMesh,source.sourceModelId??source.id);
            model.shading=SourceModel(_modelEditOriginal).shading;
            var entry=LevelAuthoringPalette.Get(_modelEditOriginal.stableId);
            var recipe=_modelPreview?.recipe??new ModelAuthoringRecipe{sourceId=_modelEditOriginal.stableId,operation="external",label=_variantLabel,category=entry.category,backend="brush-or-probuilder"};
            var variant=ModelAuthoringWorkshop.Register(model,recipe);
            var marker=_modelEditOriginal;CancelModelSession();CommitVariant(marker,variant);Selection.activeGameObject=marker.gameObject;
        }
        void CancelModelSession()
        {
            if(_detailJob!=null){_detailJob.Dispose();_detailJob=null;_detailOriginal=null;}
            if(_modelEditOriginal!=null)_modelEditOriginal.GetComponent<MeshRenderer>().enabled=true;
            if(_modelEditObject!=null)UnityEngine.Object.DestroyImmediate(_modelEditObject);
            if(_modelEditMesh!=null)UnityEngine.Object.DestroyImmediate(_modelEditMesh);
            _modelEditObject=null;_modelEditOriginal=null;_modelEditMesh=null;
            _modelPreview=null;
            _compareOriginal=false;
        }
        void DrawModelGuide()
        {
            if(_tab!=6)return;var marker=_modelEditOriginal??SelectedModel();if(marker==null)return;
            var b=AuthoringPropCatalog.ModelBounds(SourceModel(marker));var old=Handles.matrix;Handles.matrix=marker.transform.localToWorldMatrix;
            Handles.color=new Color(.4f,.8f,1,.8f);Handles.DrawWireCube(b.center,b.size);
            var normal=new[]{Vector3.right,Vector3.up,Vector3.forward}[_cutAxis];var centre=normal*_cutOffset;
            var a=_cutAxis==0?Vector3.up:Vector3.right;var c=_cutAxis==2?Vector3.up:Vector3.forward;float size=b.size.magnitude*.55f;
            Handles.DrawSolidRectangleWithOutline(new[]{centre-a*size-c*size,centre+a*size-c*size,centre+a*size+c*size,centre-a*size+c*size},new Color(.2f,.75f,.9f,.06f),new Color(.2f,.75f,.9f,.65f));
            Handles.matrix=Matrix4x4.identity;var foot=marker.transform.TransformPoint(new Vector3(b.max.x+.8f,b.min.y,b.center.z));
            Handles.color=new Color(1,.86f,.45f,.9f);Handles.DrawLine(foot,foot+Vector3.up*1.75f);Handles.DrawWireDisc(foot+Vector3.up*1.62f,SceneView.lastActiveSceneView?.camera?.transform.forward??Vector3.forward,.12f);Handles.Label(foot+Vector3.up*1.95f,"Людина — 1,75 м");Handles.matrix=old;
        }
    }
}
