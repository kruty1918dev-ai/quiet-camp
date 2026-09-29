namespace Kruty1918.SaveSystem
{
    /// <summary>
    /// Optional ordering contract for save modules that need an explicit place in
    /// the deterministic gameplay save/load pipeline. Lower values execute first.
    /// The same order is used for capture and restore so file order and runtime
    /// registration order cannot silently change dependency semantics.
    /// </summary>
    public interface ISaveModuleExecutionOrder
    {
        int SaveLoadOrder { get; }
    }
}
