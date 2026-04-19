using System;
using UnityEngine;

public abstract class GameController : MonoBehaviour
{
    public enum Difficulty { Easy, Medium, Hard, Insane }
    public enum ControlScheme { Spacebar, Mouse }

    protected bool autoRestartEnabled = true;
    protected bool roundReported;

    public event Action<bool> RoundFinished;

    public virtual ControlScheme RequiredControls => ControlScheme.Mouse;
    public virtual string ControlDescription => string.Empty;

    public virtual void ApplyDifficulty(Difficulty difficulty) { }

    public void BeginManagedRound()
    {
        autoRestartEnabled = false;
        ResetRound();
    }

    public void EndManagedRound()
    {
        autoRestartEnabled = false;
    }

    protected void ReportRoundFinished(bool isSuccess)
    {
        if (roundReported)
            return;

        roundReported = true;
        RoundFinished?.Invoke(isSuccess);
    }

    protected abstract void ResetRound();
}
