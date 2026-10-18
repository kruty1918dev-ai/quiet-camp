using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace QuietCamp.Editor
{
    /// <summary>A reversible editor pointer override, including Unity's temporary default settings.</summary>
    internal sealed class EditorPointerInputScope : IDisposable
    {
        InputSettings _previous,_working;
        bool _ownsPrevious;
        public EditorPointerInputScope()
        {
            var source=InputSystem.settings;
            // InputManager destroys HideAndDontSave settings when replacing them.
            // Preserve a value snapshot when there is no persistent settings asset.
            _ownsPrevious=source.hideFlags==HideFlags.HideAndDontSave;
            _previous=_ownsPrevious?UnityEngine.Object.Instantiate(source):source;
            if(_ownsPrevious)_previous.hideFlags=HideFlags.HideAndDontSave;
            _working=UnityEngine.Object.Instantiate(source);_working.hideFlags=HideFlags.HideAndDontSave;
            _working.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            _working.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings=_working;
        }
        public void Dispose()
        {
            if(_working!=null&&InputSystem.settings==_working&&_previous!=null)
            {InputSystem.settings=_previous;_ownsPrevious=false;}
            if(_working!=null&&InputSystem.settings!=_working)UnityEngine.Object.DestroyImmediate(_working);
            if(_ownsPrevious&&_previous!=null&&InputSystem.settings!=_previous)UnityEngine.Object.DestroyImmediate(_previous);
            _previous=null;_working=null;_ownsPrevious=false;
        }
    }
}
