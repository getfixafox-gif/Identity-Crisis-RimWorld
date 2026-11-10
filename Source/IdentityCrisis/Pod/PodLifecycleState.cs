namespace IdentityCrisis
{
    // Values are saved; append new states rather than reordering them.
    public enum PodLifecycleState
    {
        Idle = 0,
        Waiting = 1,
        Working = 2,
        Paused = 3,
        Completing = 4,
        Completed = 5,
        Error = 6
    }
}
