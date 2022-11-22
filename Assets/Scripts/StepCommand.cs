using System;

public class StepCommand
{
    protected int stepRemain;
    protected readonly Head commander;
    public bool executed;

    protected StepCommand(Head head)
    {
        commander = head;
    }

    public void Init(int step)
    {
        stepRemain = step;
        executed = false;
    }
    
    public void Step()
    {
        stepRemain--;
        if(stepRemain <= 0)Execute();
        else OnStep();
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


public class CmdCreatePoop:StepCommand
{
    protected override void Execute()
    {
        commander.CreatePoop();
        executed = true;
    }
    public CmdCreatePoop(Head head) : base(head) { }
}
