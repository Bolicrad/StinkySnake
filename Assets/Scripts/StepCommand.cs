using TMPro;
using UnityEngine;

public class StepCommand
{
    protected int stepRemain;
    protected Head commander;
    public bool executed;

    protected StepCommand()
    {
        commander = Manager.manager.head;
    }

    public void Init(int step)
    {
        commander = Manager.manager.head;
        stepRemain = step;
        executed = false;
        OnInit();
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

    protected virtual void OnInit()
    {
        
    }

    protected virtual void OnStep()
    {
        //Do nothing
    }

    protected virtual void Execute()
    {
        //Do nothing
    }
}


public class CmdCreatePoop : StepCommand
{
    private TMP_Text tmpText;

    protected override void OnInit()
    {
        tmpText = Manager.manager.textPool.Get().GetComponent<TMP_Text>();
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
        Manager.manager.textPool.Release(tmpText.gameObject);
    }
}

public class CmdLostControl : StepCommand
{
    private TMP_Text tmpText;
    protected override void OnInit()
    {
        tmpText = Manager.manager.textPool.Get().GetComponent<TMP_Text>();
        OnStep();
    }

    protected override void OnStep()
    {
        tmpText.text = stepRemain > 0 ? $"Lost Control for {stepRemain} steps" : "";
    }

    protected override void Execute()
    {
        Debug.Log("Step Command Executed: Stop Lost Control");
        commander.lostControl = false;
        Manager.manager.textPool.Release(tmpText.gameObject);
    }
}
