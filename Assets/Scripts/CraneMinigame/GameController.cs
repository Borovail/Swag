using System;
using UnityEngine;

public abstract class GameController : MonoBehaviour
{
    protected bool autoRestartEnabled = true;
    protected bool roundReported;


    public event Action<bool> RoundFinished;

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
        {
            return;
        }

        roundReported = true;
        RoundFinished?.Invoke(isSuccess);
    }

    protected abstract void ResetRound();

}