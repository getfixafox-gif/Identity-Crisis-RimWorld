namespace IdentityCrisis
{
    // Values are saved in the save file; append new states rather than reordering them.
    public enum PodLifecycleState
    {
        Idle = 0,
        Waiting = 1,
        Working = 2,
        Paused = 3,
        Completed = 4,
        Error = 5
    }
}
