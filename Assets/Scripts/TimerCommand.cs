using UnityEngine;

public class TimerCommand : MyCommand
{
    protected float timeRemain;

    public void Init(float time)
    {
        commander = Manager.manager.head;
        executed = false;
        timeRemain = time;
        OnInit();
    }

    public void Reset(float time)
    {
        timeRemain = time;
        OnReset();
    }

    public void Update(float delta)
    {
        timeRemain -= delta;
        if (timeRemain <= 0f)
        {
            Execute();
            executed = true;
        }
        else OnUpdate();
    }

    protected virtual void OnUpdate()
    {
        //Do nothing
    }

    protected virtual void OnReset()
    {
        //Do nothing
    }
}

public class CmdReverseInput : TimerCommand
{
    protected override void OnInit()
    {
        base.OnInit();
        OnReset();
        OnUpdate();
    }

    protected override void OnReset()
    {
        commander.reverseInput = true;
        Manager.manager.PlayAudio(0);
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
