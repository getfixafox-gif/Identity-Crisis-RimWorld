namespace IdentityCrisis
{
    public sealed class TransformationResult
    {
        public bool Success { get; private set; }
        // Partial is a failed operation after mutation began, not a successful conversion.
        public bool Partial { get; private set; }
        public string Error { get; private set; }
        public AssimilationReport Report { get; private set; }

        internal TransformationResult(bool success, bool partial, string error, AssimilationReport report)
        {
            Success = success;
            Partial = partial;
            Error = error;
            Report = report;
        }
    }
}
