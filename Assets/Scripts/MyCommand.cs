using TMPro;

public class MyCommand
{
    protected Head commander;
    public bool executed;
    protected TMP_Text tmpText;
    
    protected virtual void OnInit()
    {
        commander = Manager.manager.head;
        executed = false;
        tmpText = Manager.manager.textPool.Get().GetComponent<TMP_Text>();
    }
    
    protected virtual void Execute()
    {
        executed = true;
        Manager.manager.textPool.Release(tmpText.gameObject);
    }

}
