using System.IO;

namespace QuietCamp.Editor.LevelAuthoring
{
    // Callers stage all bytes and keep originals for rollback of the whole operation.
    internal static class AuthoringFileTransaction
    {
        public static void CommitPrepared(string path)
        {
            var prepared=path+".pending";
            if(!File.Exists(path)){File.Move(prepared,path);return;}
            try{File.Replace(prepared,path,null);}
            catch(IOException)
            {
                // Some Windows readers allow writing but deny deletion/replacement.
                // Retain the caller's rollback and use the already validated bytes.
                File.WriteAllBytes(path,File.ReadAllBytes(prepared));File.Delete(prepared);
            }
        }
    }
}
