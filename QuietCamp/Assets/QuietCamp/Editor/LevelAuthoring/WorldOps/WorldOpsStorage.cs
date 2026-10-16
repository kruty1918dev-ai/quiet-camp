using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace QuietCamp.Editor.LevelAuthoring.WorldOps
{
    public static class WorldOpsStorage
    {
        public static string Hash(byte[] value){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(value)).Replace("-","").ToLowerInvariant();}
        public static string Hash(string value)=>Hash(Encoding.UTF8.GetBytes(value));
        public static string FileHash(string path)=>File.Exists(path)?Hash(File.ReadAllBytes(path)):"absent";
        public static string Digest(object value)=>Hash(JsonConvert.SerializeObject(value));
        public static void Write(string path,byte[] bytes)
        {
            path=Path.GetFullPath(path);Directory.CreateDirectory(Path.GetDirectoryName(path));string pending=path+".worldops-pending";
            using(var stream=new FileStream(pending,FileMode.Create,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
            if(File.Exists(path))File.Replace(pending,path,null);else File.Move(pending,path);
        }
        public static void Write(string path,string value)=>Write(path,new UTF8Encoding(false).GetBytes(value));
        public static void Json(string path,object value)=>Write(path,JsonConvert.SerializeObject(value,Formatting.Indented));
        public static T Read<T>(string path)=>JsonConvert.DeserializeObject<T>(File.ReadAllText(path));
        public static void Check(Dictionary<string,string> sources)
        {foreach(var pair in sources)if(FileHash(pair.Key)!=pair.Value)throw new IOException("Source changed: "+pair.Key);}
        public static string SafeChild(string root,string relative)
        {
            string prefix=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            string full=Path.GetFullPath(Path.Combine(prefix,relative));
            if(!full.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Path escapes its owner: "+relative);
            return full;
        }
    }
    [Serializable] public sealed class WorldWrite
    {
        public string path,beforeHash,afterHash,backup,prepared;
    }
    [Serializable] public sealed class WorldJournal
    {
        public int version=1;
        public string id,state="prepared",error;
        public List<WorldWrite> writes=new List<WorldWrite>();
        public Dictionary<string,string> preconditions=new Dictionary<string,string>();
    }
    public static class WorldOpsTransaction
    {
        public static WorldJournal Prepare(string directory,Dictionary<string,string> writes,Dictionary<string,string> preconditions,IEnumerable<string> roots)
        {
            var journal=new WorldJournal{id=Guid.NewGuid().ToString("N"),preconditions=new Dictionary<string,string>(preconditions)};
            Directory.CreateDirectory(directory);int index=0;
            var allowed=roots.Select(r=>Path.GetFullPath(r).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar).ToArray();
            foreach(var pair in writes.OrderBy(p=>p.Key,StringComparer.Ordinal))
            {
                string path=Path.GetFullPath(pair.Key);
                if(!allowed.Any(r=>path.StartsWith(r,StringComparison.OrdinalIgnoreCase)))throw new IOException("Write outside declared projects: "+path);
                var item=new WorldWrite{path=path,beforeHash=WorldOpsStorage.FileHash(path),afterHash=WorldOpsStorage.Hash(pair.Value),
                    backup=Path.Combine(directory,index+".before"),prepared=Path.Combine(directory,index+".after")};index++;
                if(item.beforeHash==item.afterHash)continue;
                if(File.Exists(path))WorldOpsStorage.Write(item.backup,File.ReadAllBytes(path));
                WorldOpsStorage.Write(item.prepared,pair.Value);journal.writes.Add(item);
            }
            WorldOpsStorage.Json(Path.Combine(directory,"journal.json"),journal);return journal;
        }
        public static void Commit(string directory,Action<int> afterWrite=null)
        {
            string path=Path.Combine(directory,"journal.json");var journal=WorldOpsStorage.Read<WorldJournal>(path);
            if(journal.state=="committed")return;
            if(journal.state!="prepared"&&journal.state!="committing")throw new IOException("Transaction is not commit-ready.");
            // Verify the entire group first. A resumed half-write accepts exactly its own bytes.
            foreach(var item in journal.writes)
            {
                string current=WorldOpsStorage.FileHash(item.path);
                if(current!=item.beforeHash&&!(journal.state=="committing"&&current==item.afterHash))throw new IOException("Conflicting target: "+item.path);
                if(WorldOpsStorage.FileHash(item.prepared)!=item.afterHash)throw new IOException("Damaged staged write: "+item.path);
                if(item.beforeHash!="absent"&&WorldOpsStorage.FileHash(item.backup)!=item.beforeHash)throw new IOException("Damaged backup: "+item.path);
            }
            foreach(var pair in journal.preconditions)
            {
                var own=journal.writes.FirstOrDefault(w=>string.Equals(w.path,Path.GetFullPath(pair.Key),StringComparison.OrdinalIgnoreCase));
                string current=WorldOpsStorage.FileHash(pair.Key);
                if(current!=pair.Value&&!(journal.state=="committing"&&own!=null&&current==own.afterHash))throw new IOException("Source changed: "+pair.Key);
            }
            journal.state="committing";WorldOpsStorage.Json(path,journal);int index=0;
            foreach(var item in journal.writes)
            {
                if(WorldOpsStorage.FileHash(item.path)!=item.afterHash)WorldOpsStorage.Write(item.path,File.ReadAllBytes(item.prepared));
                afterWrite?.Invoke(++index);
            }
            journal.state="committed";WorldOpsStorage.Json(path,journal);
        }
        public static void Rollback(string directory)
        {
            string path=Path.Combine(directory,"journal.json");var journal=WorldOpsStorage.Read<WorldJournal>(path);
            if(journal.state=="rolled-back")return;
            // No changes at all if even one target contains later user work.
            foreach(var item in journal.writes)
            {
                var hash=WorldOpsStorage.FileHash(item.path);
                if(hash!=item.beforeHash&&hash!=item.afterHash)throw new IOException("Rollback would replace later work: "+item.path);
                if(item.beforeHash!="absent"&&WorldOpsStorage.FileHash(item.backup)!=item.beforeHash)throw new IOException("Damaged backup: "+item.path);
            }
            journal.state="rolling-back";WorldOpsStorage.Json(path,journal);
            foreach(var item in journal.writes)
            {
                if(item.beforeHash=="absent"){if(File.Exists(item.path))File.Delete(item.path);}
                else WorldOpsStorage.Write(item.path,File.ReadAllBytes(item.backup));
            }
            journal.state="rolled-back";WorldOpsStorage.Json(path,journal);
        }
    }
}
