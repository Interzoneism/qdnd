namespace QDND.Combat.Persistence
{
    /// <summary>
    /// Interface for subsystems that can export/import their state for save/load.
    /// </summary>
    /// <typeparam name="T">The snapshot type for this subsystem</typeparam>
    public interface IStateExportable<T>
    {
        /// <summary>Export current state to a snapshot.</summary>
        T ExportState();

        /// <summary>Restore state from a snapshot.</summary>
        void ImportState(T snapshot);
    }
}
