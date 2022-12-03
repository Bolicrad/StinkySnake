using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;
// ReSharper disable Unity.InefficientPropertyAccess

public class Manager : MonoBehaviour
{
    public static Manager manager;
    
    public GameObject headPrefab;
    public GameObject molePrefab;
    public Transform moleParent;
    
    public GameObject bodyPrefab;
    public Transform bodyParent;
    public ObjectPool<GameObject> bodyPool;

    public GameObject poopPrefab;
    public Transform poopParent;
    public ObjectPool<GameObject> poopPool;

    public GameObject foodPrefab;
    
    
    
    
    public BoxCollider2D border;
    public SpriteRenderer spriteRenderer;
    public TMP_Text deathText;
    
    public RectTransform content;
    public ObjectPool<GameObject> textPool;
    public GameObject textPrefab;
    
    public Transform recycleBin;
    
    public TMP_Text scoreText;
    public Button startButton;
    
    public Head head;
    private Vector3 borderSize;
    public AudioClip[] audioClips;
    public List<StepCommand> realStepCommands;
    public List<TimerCommand> realTimerCommands;
    public AudioSource audioSource;
    private Coroutine ledHandler;
    public Vector2Int gridMax;

    private void Awake()
    {
        manager = this;
        borderSize = Camera.main!.ScreenToWorldPoint(new Vector3(Screen.width, Screen.height));
        border.size = new Vector2(borderSize.y * 2 - 1.5f, borderSize.y * 2 - 1.5f);
        spriteRenderer.size = new Vector2(borderSize.y * 2 - 0.5f, borderSize.y * 2 - 0.5f);
        transform.position = new Vector3(borderSize.y - borderSize.x, 0);
        gridMax = new Vector2Int(
            (int)(spriteRenderer.size.x / 0.5f) / 2,
            (int)(spriteRenderer.size.y / 0.5f) / 2);
        realStepCommands = new List<StepCommand>();
        realTimerCommands = new List<TimerCommand>();
        
        textPool = new ObjectPool<GameObject>(CreateText, OnGetText, OnReleaseText, OnDestroyText, true, 5, 20);
        bodyPool = new ObjectPool<GameObject>(CreateBody, OnGetBody, OnReleaseBody, OnDestroyBody, true, 10, 50);
        poopPool = new ObjectPool<GameObject>(CreatePoop, OnGetPoop, OnReleasePoop, OnDestroyPoop, true, 25, 100);
    }

    #region Poop Pooling

    private void OnDestroyPoop(GameObject obj)
    {
        Debug.Log($"Poop pool is full, poop {obj.GetInstanceID()} is destroyed.");
        DestroyImmediate(obj);
    }

    private void OnReleasePoop(GameObject obj)
    {
        obj.transform.parent = recycleBin;
        obj.SetActive(false);
    }

    private void OnGetPoop(GameObject obj)
    {
        obj.transform.parent = poopParent;
        obj.SetActive(true);
    }

    private GameObject CreatePoop()
    {
        return Instantiate(poopPrefab);
    }

    #endregion
    
    #region Body Pooling

        private void OnDestroyBody(GameObject obj)
        {
            Debug.Log($"Body Pool is full, Body {obj.GetInstanceID()} is destroyed.");
            DestroyImmediate(obj);
        }
    
        private void OnReleaseBody(GameObject obj)
        {
            obj.transform.SetParent(recycleBin);
            obj.SetActive(false);
        }
    
        private void OnGetBody(GameObject obj)
        {
            obj.transform.SetParent(bodyParent);
            obj.SetActive(true);
        }
    
        private GameObject CreateBody()
        {
            return Instantiate(bodyPrefab);
        }

    #endregion
    
    #region TextPooling

    private void OnDestroyText(GameObject obj)
    {
        Debug.Log($"Text pool is full, Tmp_Text {obj.GetInstanceID()} is destroyed.");
        DestroyImmediate(obj);
    }

    private void OnReleaseText(GameObject obj)
    {
        obj.GetComponent<TMP_Text>().text = "";
        obj.transform.SetParent(recycleBin);
        obj.SetActive(false);
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
    }

    private void OnGetText(GameObject obj)
    {
        obj.transform.SetParent(content);
        obj.SetActive(true);
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
    }

    private GameObject CreateText()
    {
        return Instantiate(textPrefab);
    }

    #endregion

    #region Text Print

    public void TellPoopEffect(Head.PoopEffectType type,int option) {
        var textContent = "";
        switch (type)
        {
            case Head.PoopEffectType.ReduceLength:
                textContent += $"Reduced your length by {option}.";
                PlayAudio(4);
                break;
            case Head.PoopEffectType.Speedup:
                textContent += $"Speed Level Up to {option}";
                PlayAudio(2);
                break;
            case Head.PoopEffectType.CreateMole:
                textContent += $"Summoned a mole.";
                break;
            default:
                return;
        }
        PrintToScreen(textPool.Get().GetComponent<TMP_Text>(),textContent);
    }

    public void PrintToScreen(TMP_Text text,string textContent, int time = 3) {
        StartCoroutine(PostLed(text, textContent, time));
    }

    private IEnumerator PostLed(TMP_Text text, string textContent, int time) {

        text.text = textContent;
        yield return new WaitForSeconds(time);
        textPool.Release(text.gameObject);
    }

    #endregion

    #region Game Life Cycle

    public void StartGame() {
        head = Instantiate(headPrefab).GetComponent<Head>();
        head.transform.position = transform.position;
        startButton.gameObject.SetActive(false);
        deathText.text = "Poop Effects:";
        Time.timeScale = 1;
    }
    public void SnakeDie(Head.SnakeDieReason reason) {
        var highScore = PlayerPrefs.GetInt("HighScore", 0);
        if (head.score > highScore) {
            highScore = head.score;
            PlayerPrefs.SetInt("HighScore", highScore);
        }
        Time.timeScale = 0;

        var text = reason switch
        {
            Head.SnakeDieReason.HitSelf => "You ate your body.",
            Head.SnakeDieReason.HitWall => "You hit the boundary.",
            Head.SnakeDieReason.Length0 => "Your body fell into the void.",
            Head.SnakeDieReason.PoopSnake => "Another Snake ate you.",
            _ => ""
        };
        scoreText.text = $"Score: {head.score} \nHigh Score: {highScore}";
        deathText.text = text;
        PlayAudio(5);
        startButton.gameObject.SetActive(true);
        StopAllCoroutines();
        StartCoroutine(DeleteAllTail());
        
        //Destroy all moles
        if (moleParent.childCount > 0)
        {
            for (int i = moleParent.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(moleParent.GetChild(i).gameObject);
            }
        }
        
        GridPrinter.gridPrinter.drawingAim = false;
        
        Destroy(head.food);
        head.gameObject.SetActive(false);
        
        foreach (var command in realStepCommands)
        {
            command.executed = true;
        }

        foreach (var command in realTimerCommands)
        {
            command.executed = true;
        }
        foreach (var tmpText in content.GetComponentsInChildren<TMP_Text>())
        {
            textPool.Release(tmpText.gameObject);
        }
        
        Destroy(head.gameObject);
    }

    #endregion

    #region Create/Destroy

    private IEnumerator DeleteAllTail() {
        while (bodyParent.childCount > 0) {
            bodyPool.Release(bodyParent.GetChild(0).gameObject);
            yield return null;
        }
    }
    public void CreateMole()
    {
        //Find a proper place to instantiate a mole.

        var pos = GetUnoccupiedPos();

        Instantiate(molePrefab, pos, Quaternion.identity).transform.parent = moleParent;
    }
    public void CreateFood()
    {

        var pos = GetUnoccupiedPos();
        
        if (!head.food)
        {
            head.food = Instantiate(foodPrefab, pos, Quaternion.identity);
        }
        else head.food.transform.position = pos;
    }

    #endregion

    #region Grid Management

    public Vector2 GetUnoccupiedPos()
    {
        var fixedPos = GetRandomPos();

        while (IsPosOccupied(fixedPos, out var col))
        {
            if (col!= null) Debug.Log($"Occupied by {col.tag}");
            fixedPos = GetRandomPos();
        }

        return fixedPos;
    }

    public Vector2 GridToWorldPos(Vector2Int gridPos)
    {
        return GridPrinter.GridToWorldPoint(gridPos, transform.position);
    }

    public Vector2Int WorldToGridPoint(Vector2 pos)
    {
        return GridPrinter.WorldToGridPoint(pos, transform.position);
    }

    public Vector2 GetRandomPos()
    {
        var gridPos = GridPrinter.GetRandomGridPos(gridMax);
        return GridPrinter.GridToWorldPoint(gridPos, transform.position);
    }

    public bool IsPosOccupied(Vector2 pos, out Collider2D col)
    {
        // ReSharper disable once Unity.PreferNonAllocApi
        col = null;
        var colliders = Physics2D.OverlapPointAll(pos);
        if (colliders.Length > 0)
        {
            foreach (Collider2D other in colliders)
            {
                col = other;
            }
            return true;
        }
        return false;
    }

    public bool IsPosInRange(Vector2 pos)
    {
        return IsGridPosInRange(GridPrinter.WorldToGridPoint(pos, transform.position));
    }

    public bool IsGridPosInRange(Vector2Int gridPos)
    {
        return Mathf.Abs(gridPos.x) <= gridMax.x && Mathf.Abs(gridPos.y) <= gridMax.y;
    }

    #endregion
    
    #region Todo: Match3 Poop Snake Generation

    public void Match3Poop(Vector2 pos)
    {


    }
    
    public bool IsValidPoop(Vector2Int gridPos)
    {
        if (!IsGridPosInRange(gridPos)) return false;
        return IsPosOccupied(gridPos, out var col) && col.CompareTag("Poop");
    }

    #endregion

    
    public void PlayAudio(int index) {
        audioSource.clip = audioClips[index];
        audioSource.Play();
    }
}
