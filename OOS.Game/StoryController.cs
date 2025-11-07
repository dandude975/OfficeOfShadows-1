using OOS.Shared;

namespace OOS.Game
{
    public class StoryController
    {
        public Progress Progress { get; private set; } = Progress.Load();

        public void SetCheckpoint(string id)
        {
            if (Progress.Checkpoint != id)
            {
                Progress.Checkpoint = id;
                Progress.Save();
            }
        }

        public bool AtLeast(string id) => Progress.IsAtOrBeyond(id);

        public void Flag(string key, bool value = true)
        {
            Progress.Flags[key] = value;
            Progress.Save();
        }

        public void ResetProgress()
        {
            Progress.Reset();
            Progress = Progress.Load(); // back to "intro"
        }
    }
}
