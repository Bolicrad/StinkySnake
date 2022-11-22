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
    public BoxCollider2D border;
    public SpriteRenderer spriteRenderer;
    public TMP_Text deathText;
    
    public RectTransform content;
    public ObjectPool<GameObject> textPool;
    public GameObject textPrefab;
    
    public TMP_Text scoreText;
    public Button startButton;
    
    public Head head;
    private Vector3 borderSize;
    public AudioClip[] audioClips;
    public List<StepCommand> realPoopCommands;
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
        realPoopCommands = new List<StepCommand>();
        textPool = new ObjectPool<GameObject>(CreateText, OnGetText, OnReleaseText, OnDestroyText, true, 5, 20);
    }

    #region TextPooling

    private void OnDestroyText(GameObject obj)
    {
        //Do nothing
    }

    private void OnReleaseText(GameObject obj)
    {
        obj.GetComponent<TMP_Text>().text = "";
        obj.transform.SetParent(null);
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
    }

    private void OnGetText(GameObject obj)
    {
        obj.transform.SetParent(content);
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
    }

    private GameObject CreateText()
    {
        return Instantiate(textPrefab);
    }

    #endregion

    #region Text Print

    public void TellPoopEffect(Head.PoopEffectType type,int option) {
        string textContent = "";
        bool isOption = false;
        switch (type) {
            case Head.PoopEffectType.ReduceLength:
                textContent += $"Reduced your length by {option}.";
                PlayAudio(4);
                break;
            case Head.PoopEffectType.ReverseInput:
                isOption = true;
                textContent += $"Input Axis reversed for {option}s";
                PlayAudio(0);
                break;
            case Head.PoopEffectType.LostControl:
                // isOption = true;
                // content += $"Lost Control for {option}s";
                PlayAudio(3);
                return;
            case Head.PoopEffectType.Speedup:
                textContent += $"Speed Level Up to {option}";
                PlayAudio(2);
                break;
            case Head.PoopEffectType.CreateMole:
                textContent += $"Summoned a mole.";
                break;
        }
        if(isOption)PrintToScreen(textPool.Get().GetComponent<TMP_Text>(),textContent,option);
        else PrintToScreen(textPool.Get().GetComponent<TMP_Text>(),textContent);
    }

    private void PrintToScreen(TMP_Text text,string textContent, int time = 3) {
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
        head.bodyParent = transform;
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
        
        foreach (var command in realPoopCommands)
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

    IEnumerator DeleteAllTail() {
        while (transform.childCount > 0) {
            Destroy(transform.GetChild(0).gameObject);
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
            head.food = Instantiate(head.foodPrefab, pos, Quaternion.identity);
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
        if (IsPosOccupied(gridPos, out var col))
        {
            if (col.CompareTag("Poop"))
            {
                return true;
            }
        }
        return false;
    }

    #endregion

    
    public void PlayAudio(int index) {
        // audioSource.clip = audioClips[index];
        // audioSource.Play();
    }
}
