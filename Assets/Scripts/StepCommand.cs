public class StepCommand
{
    protected int stepRemain;
    protected readonly Head commander;
    public bool executed;

    protected StepCommand()
    {
        commander = Manager.manager.head;
    }

    public void Init(int step)
    {
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
    protected override void Execute()
    {
        commander.CreatePoop();
    }
}

public class CmdLostControl : StepCommand
{
    protected override void OnInit()
    {
        OnStep();
    }

    protected override void OnStep()
    {
        Manager.manager.effectTexts[(int)Head.PoopEffectType.LostControl].text =
            stepRemain > 0 ? $"Lost Control for {stepRemain} steps" : "";
    }

    protected override void Execute()
    {
        commander.lostControl = false;
        Manager.manager.effectTexts[(int)Head.PoopEffectType.LostControl].text = "";
    }
    
}
