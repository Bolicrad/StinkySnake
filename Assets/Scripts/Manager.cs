using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
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
    public List<TMP_Text> effectTexts;
    public TMP_Text scoreText;
    public Button startButton;
    private Head head;
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
    }

    public void StartGame() {
        head = Instantiate(headPrefab).GetComponent<Head>();
        head.transform.position = transform.position;
        head.bodyParent = transform;
        startButton.gameObject.SetActive(false);
        deathText.text = string.Empty;
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
        foreach (var tmpText in effectTexts)
        {
            tmpText.text = string.Empty;
        }
        Destroy(head.gameObject);
    }

    public void TellPoopEffect(Head.PoopEffectType type,int option) {
        string content = "";
        bool isOption = false;
        switch (type) {
            case Head.PoopEffectType.ReduceLength:
                content += $"Reduced your length by {option}.";
                PlayAudio(4);
                break;
            case Head.PoopEffectType.ReverseInput:
                isOption = true;
                content += $"Input Axis reversed for {option}s";
                PlayAudio(0);
                break;
            case Head.PoopEffectType.LostControl:
                // isOption = true;
                // content += $"Lost Control for {option}s";
                PlayAudio(3);
                return;
            case Head.PoopEffectType.Speedup:
                content += $"Speed Level Up to {option}";
                PlayAudio(2);
                break;
            case Head.PoopEffectType.CreateMole:
                content += $"Summoned a mole.";
                break;
        }
        if(isOption)PrintToScreen(effectTexts[(int)type],content,option);
        else PrintToScreen(effectTexts[(int)type],content);
    }

    private void PrintToScreen(TMP_Text text,string content, int time = 3) {
        if (ledHandler != null)
        {
            StopCoroutine(ledHandler);
            ledHandler = null;
        }

        ledHandler = StartCoroutine(PostLed(text, content, time));
    }

    private IEnumerator PostLed(TMP_Text text, string content, int time) {

        text.text = content;
        yield return new WaitForSeconds(time);
        text.text = "";
    }

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

    public void PlayAudio(int index) {
        // audioSource.clip = audioClips[index];
        // audioSource.Play();
    }
}
