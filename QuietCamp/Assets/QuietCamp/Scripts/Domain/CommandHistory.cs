using System.Collections.Generic;
using System.Linq;
namespace QuietCamp.Domain
{
    public sealed class CommandHistory
    {
        readonly Stack<Placement[]> undo=new Stack<Placement[]>();
        readonly Stack<Placement[]> redo=new Stack<Placement[]>();
        public Placement[] Current { get; private set; }=new Placement[0];
        public bool CanUndo=>undo.Count>0;
        public bool CanRedo=>redo.Count>0;
        static Placement[] Copy(IEnumerable<Placement> a)=>a.Select(p=>p.Copy()).ToArray();
        public bool Commit(LevelData level,IEnumerable<Placement> next)
        {
            var copy=Copy(next);
            if(!RuleEvaluator.Evaluate(level,copy,false).CanCommit)return false;
            undo.Push(Copy(Current));redo.Clear();Current=copy;return true;
        }
        public bool Undo(){if(undo.Count==0)return false;redo.Push(Copy(Current));Current=undo.Pop();return true;}
        public bool Redo(){if(redo.Count==0)return false;undo.Push(Copy(Current));Current=redo.Pop();return true;}
        public void Restore(IEnumerable<Placement> saved){Current=Copy(saved);undo.Clear();redo.Clear();}
    }
}
