using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// ReSharper disable Unity.InefficientPropertyAccess

public class Head : MonoBehaviour
{
    public enum PoopEffectType
    {
        ReduceLength,
        ReverseInput,
        LostControl,
        Speedup,
        CreateMole,
    }

    public enum SnakeDieReason
    {
        HitSelf, 
        HitWall, 
        Length0, 
        PoopSnake
    };
    private static readonly System.Random Rng = new System.Random();
    private Vector2Int now; // actual head direction
    private int angle;
    private Transform TailTransform => bodyParent.childCount > 0 ? bodyParent.GetChild(bodyParent.childCount - 1) : null;
    public Transform bodyParent;
    public Transform poopParent;
    public GameObject bodyPrefab;
    public GameObject foodPrefab;
    public GameObject poopPrefab;
    public GameObject food;
    private Vector3 tmp;
    private bool digesting;
    private int poopCount;

    private int DigestMoveNumber => 2 + (bodyParent.childCount / speedLevel) / 4;

    private int poopDamage = 1;
    public bool lostControl;
    private int lostControlPower = 3;

    private bool canInput = true;
    public float unitScale = 0.5f;
    public float timer;
    public float defaultTimerGap = 0.1f;
    private int speedLevel = 1;
    public int debuffTime = 1;
    
    private bool reverseInput;
    private Coroutine lostControlHandler;
    private Coroutine reverseInputHandler;
    public int score;

    private static List<StepCommand> PoopCommands => Manager.manager.realPoopCommands;

    public void Start()
    {
        poopParent = GameObject.Find("PoopParent").transform;
        for (var i = 0; i < 3; ++i)
        {
            CreateBody(transform.position - (i + 1) * new Vector3(0, unitScale, 0));
        }
        now = Vector2Int.up;
        angle = 0;
        Manager.manager.CreateFood();
    }
    
    private void CreatePoop(Vector3 position)
    {
        Instantiate(poopPrefab, position, Quaternion.identity).transform.parent = poopParent;
    }

    public void CreatePoop()
    {
        var position = TailTransform ? TailTransform.position : transform.position - (Vector3)(Vector2)now * unitScale;
        CreatePoop(position);
    }

    private void CreateBody(Vector3 position)
    {
        var newBody = Instantiate(bodyPrefab, position, Quaternion.identity);
        newBody.transform.SetParent(bodyParent);
    }

    private IEnumerator DeleteBody(int num)
    {
        for (var i = 0; i < num; i++)
        {
            yield return null;
            RemoveBody();
        }
    }
    
    private IEnumerator ReverseInputDebuff(int time)
    {
        reverseInput = true;
        yield return new WaitForSeconds(time);
        reverseInput = false;
    }

    private void RemoveBody()
    {
        if (TailTransform)
        {
            Destroy(TailTransform.gameObject);
        }
        else
        {
            Manager.manager.SnakeDie(SnakeDieReason.Length0);
        }
    }

    private void MoveBody()
    {
        tmp = transform.position;
        Vector2 nextPos = unitScale * (Vector2)now + (Vector2)transform.position;
        
        //Judge If the snake run out of the range (if so die with the hit wall reason)
        if (!Manager.manager.IsPosInRange(nextPos))
        {
            Manager.manager.SnakeDie(SnakeDieReason.HitWall);
            return;
        }

        transform.position = nextPos;

        //Step Command Logics
        foreach (var poopCommand in PoopCommands.Where(poopCommand => !poopCommand.executed))
        {
            poopCommand.Step();
        }
        
        // if (digesting)
        // {
        //     if (poopCount > 0)
        //     {
        //         poopCount--;
        //     }
        //     else
        //     {
        //         CreatePoop(TailTransform ? TailTransform.position : tmp);
        //         digesting = false;
        //     }
        // }
        

        if (TailTransform)
        {
            TailTransform.position = tmp;
            TailTransform.SetAsFirstSibling();
        }
        transform.eulerAngles = angle * Vector3.forward;
    }

    private void Update()
    {
        if (canInput && !lostControl)
        {
            float input;
            if (now == Vector2Int.up || now == Vector2Int.down) {
                input = Input.GetAxis("Horizontal");
                if (Mathf.Abs(input) > 0)
                {
                    now = (reverseInput ? input < 0 : input > 0) ? Vector2Int.right : Vector2Int.left;
                    angle = (reverseInput ? input < 0 : input > 0) ? -90 : 90;
                    canInput = false;
                }
            }
            if (now == Vector2Int.left || now == Vector2Int.right) {
                input = Input.GetAxis("Vertical");
                if (Mathf.Abs(input) > 0)
                {
                    now = (reverseInput ? input < 0 : input > 0) ? Vector2Int.up : Vector2Int.down;
                    angle = (reverseInput ? input < 0 : input > 0) ? 0 : 180;
                    canInput = false;
                }
            }
        }

        timer += Time.deltaTime;
        
        if (timer > defaultTimerGap / Mathf.Sqrt(speedLevel))
        {
            MoveBody();
            canInput = true;
            timer = 0;
        }
    }
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.tag.Equals("Food"))
        {
            score += speedLevel;
            Manager.manager.scoreText.text = $"Score: {score}";
            Manager.manager.PlayAudio(1);
            
            Destroy(food);
            food = null;
            
            //Create a new body
            CreateBody(TailTransform ? TailTransform.position : transform.position - (Vector3)(Vector2)now * unitScale);
            
            //Set up a poop command
            // digesting = true;
            // poopCount = digestMoveNumber;

            AddStepCommand<CmdCreatePoop>(DigestMoveNumber);

            Manager.manager.CreateFood();
        }
        if (other.tag.Equals("Poop"))
        {
            Destroy(other.gameObject);
            var damageType = (PoopEffectType)Rng.Next(System.Enum.GetNames(typeof(PoopEffectType)).Length);
            int option = 0;
            switch (damageType)
            {
                case PoopEffectType.ReduceLength:
                {
                    option = poopDamage;
                    StartCoroutine(DeleteBody(poopDamage));
                    poopDamage++;
                    break;
                }
                case PoopEffectType.ReverseInput:
                {
                    option = debuffTime;
                    if (reverseInputHandler != null)
                    {
                        StopCoroutine(reverseInputHandler);
                        reverseInputHandler = null;
                    }
                    reverseInputHandler = StartCoroutine(ReverseInputDebuff(debuffTime));
                    break;
                }
                case PoopEffectType.LostControl:
                {
                    lostControl = true;
                    AddStepCommand<CmdLostControl>(lostControlPower);
                    lostControlPower++;
    
                    //legacy version
                    {
                        // option = debuffTime;
                        // if (lostControlHandler != null)
                        // {
                        //     StopCoroutine(lostControlHandler);
                        //     lostControlHandler = null;
                        // }
                        // lostControlHandler = StartCoroutine(LostControlDebuff(debuffTime));
                    }
                    break;
                }
                case PoopEffectType.Speedup:
                {
                    speedLevel++;
                    option = speedLevel;
                    break;
                }
                case PoopEffectType.CreateMole:
                {
                    Manager.manager.CreateMole();
                    break;
                }
            }
            Manager.manager.TellPoopEffect(damageType, option);
        }
        if (other.tag.Equals("Body"))
        {
            Manager.manager.SnakeDie(SnakeDieReason.HitSelf);
        }
    }

    private static void AddStepCommand<T>(int step) where T: StepCommand, new()
    {
        var newCommand = PoopCommands.FirstOrDefault(poopCommand => poopCommand.executed && poopCommand.GetType() == typeof(T));
        if (newCommand == null)
        {
            newCommand = new T();
            PoopCommands.Add(newCommand);
        }
        newCommand.Init(step);
    }

}