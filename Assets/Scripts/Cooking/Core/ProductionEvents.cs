namespace Cook.Core
{
    public readonly struct ProductionStarted
    {
        public ProductionStarted(ProductionSession session) { Session = session; }
        public ProductionSession Session { get; }
    }

    public readonly struct ProductionChanged
    {
        public ProductionChanged(ProductionSession session) { Session = session; }
        public ProductionSession Session { get; }
    }

    public readonly struct ProductionFinished
    {
        public ProductionFinished(ProductionResult result) { Result = result; }
        public ProductionResult Result { get; }
    }

    public readonly struct StationChanged
    {
        public StationChanged(ProductionStation station) { Station = station; }
        public ProductionStation Station { get; }
    }

    public readonly struct OperationPerformed
    {
        public OperationPerformed(ProductionOperationData operation) { Operation = operation; }
        public ProductionOperationData Operation { get; }
    }

    public readonly struct CookInputPressed { }
    public readonly struct CookInputReleased { }

    public readonly struct OperationInputProgress
    {
        public OperationInputProgress(ProductionInteractionMode mode, float progress)
        { Mode = mode; Progress = progress; }
        public ProductionInteractionMode Mode { get; }
        public float Progress { get; }
    }
}
