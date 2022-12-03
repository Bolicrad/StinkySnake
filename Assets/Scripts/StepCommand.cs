using UnityEngine;

public class StepCommand:MyCommand
{
    protected int stepRemain;
    
    public static bool CheckDuplicate()
    {
        return true;
    }
    
    public void Init(int step)
    {
        OnInit();
        stepRemain = step;
    }
    public void Reset(int step)
    {
        stepRemain = step;
    }
    
    public void Step()
    {
        stepRemain--;
        if (stepRemain <= 0)
        {
            Execute();
        }
        else OnStep();
    }
    
    protected virtual void OnStep()
    {
        //Do nothing
    }
}


public class CmdCreatePoop : StepCommand
{
    public new static bool CheckDuplicate()
    {
        return true;
    }

    protected override void OnInit()
    {
        base.OnInit();
        Manager.manager.PlayAudio(1);
        OnStep();
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
        commander.lostControl = true;
        Manager.manager.PlayAudio(3);
        OnStep();
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
