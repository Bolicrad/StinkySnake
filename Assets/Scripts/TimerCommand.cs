using UnityEngine;

public class TimerCommand : MyCommand
{
    protected float timeRemain;

    public void Init(float time)
    {
        OnInit();
        timeRemain = time;
    }

    public void Reset(float time)
    {
        timeRemain = time;
    }

    public void Update(float delta)
    {
        timeRemain -= delta;
        if (timeRemain <= 0f)
        {
            Execute();
        }
        else OnUpdate();
    }

    protected virtual void OnUpdate()
    {
        //Do nothing
    }
}

public class CmdReverseInput : TimerCommand
{
    protected override void OnInit()
    {
        base.OnInit();
        commander.reverseInput = true;
        Manager.manager.PlayAudio(0);
        OnUpdate();
    }

    protected override void OnUpdate()
    {
        if (commander.reverseInput == false) commander.reverseInput = true;
        tmpText.text = $"Input axis reversed: {timeRemain:0.00}s.";
    }

    protected override void Execute()
    {
        Debug.Log("Timer Command Executed: Stop Reversing Input");
        commander.reverseInput = false;
        base.Execute();
    }
}
