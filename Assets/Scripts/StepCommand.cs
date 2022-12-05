using UnityEngine;

public class StepCommand:MyCommand
{
    protected int stepRemain;
    
    public void Init(int step)
    {
        commander = Manager.manager.head;
        executed = false;
        stepRemain = step;
        OnInit();
    }
    public void Reset(int step)
    {
        stepRemain = step;
        OnReset();
    }
    
    public void Step()
    {
        stepRemain--;
        if (stepRemain <= 0)
        {
            Execute();
            executed = true;
        }
        else OnStep();
    }
    
    protected virtual void OnStep()
    {
        //Do nothing
    }

    protected virtual void OnReset()
    {
        //Do nothing
    }
}


public class CmdCreatePoop : StepCommand
{
    protected override void OnInit()
    {
        base.OnInit();
        OnReset();
        OnStep();
    }

    protected override void OnReset()
    {
        Manager.manager.PlayAudio(1);
    }

    protected override void OnStep()
    {
        tmpText.text = stepRemain > 0 ? $"Digesting. {stepRemain} steps to shit" : "";
    }

    protected override void Execute()
    {
        Debug.Log($"Step Command Executed: CreatePoop");
        commander.CreatePoop();
        base.Execute();
    }
}

public class CmdLostControl : StepCommand
{
    protected override void OnInit()
    {
        base.OnInit();
        OnReset();
        OnStep();
    }

    protected override void OnReset()
    {
        commander.lostControl = true;
        Manager.manager.PlayAudio(3);
    }

    protected override void OnStep()
    {
        if (commander.lostControl == false) commander.lostControl = true;
        tmpText.text = stepRemain > 0 ? $"Lost Control for {stepRemain} steps" : "";
    }

    protected override void Execute()
    {
        Debug.Log("Step Command Executed: Stop Losing Control");
        commander.lostControl = false;
        base.Execute();
    }
}
