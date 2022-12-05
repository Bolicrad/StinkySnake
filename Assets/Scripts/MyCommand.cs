using TMPro;

public class MyCommand
{
    protected Head commander;
    public bool executed;
    protected TMP_Text tmpText;
    
    protected virtual void OnInit()
    {
        tmpText = Manager.manager.textPool.Get().GetComponent<TMP_Text>();
    }
    
    protected virtual void Execute()
    {
        Manager.manager.textPool.Release(tmpText.gameObject);
    }

}
