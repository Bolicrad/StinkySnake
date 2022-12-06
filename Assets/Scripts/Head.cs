using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// ReSharper disable Unity.InefficientPropertyAccess

public class Head : MonoBehaviour
{
    #region Enums

    private enum PoopEffectType
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
    }

    #endregion
    
    #region Parameters
    
    private static readonly System.Random Rng = new System.Random();
    private Vector2Int now; // actual head direction
    private int angle;
    private static Transform TailTransform => BodyParent.childCount > 0 ? BodyParent.GetChild(BodyParent.childCount - 1) : null;
    private static Transform BodyParent => Manager.manager.bodyParent;

    public GameObject food;
    private Vector3 tmp;
    private bool digesting;
    private int poopCount;

    private int DigestMoveNumber => 2 + (BodyParent.childCount / speedLevel) / 4;

    private int reduceLengthAmount = 1;
    
    public bool lostControl;
    private int lostControlPower = 3;
    
    public bool reverseInput;
    private const float ReverseTime = 3f;

    private bool canInput = true;
    public float unitScale = 0.5f;
    private float timer;
    public float defaultTimerGap = 0.1f;
    private int speedLevel = 1;

    private static List<StepCommand> StepCommands => Manager.manager.realStepCommands;
    private static List<TimerCommand> TimerCommands => Manager.manager.realTimerCommands;
    #endregion
    
    #region Create/Destroy

    private IEnumerator CreatePoop(Vector3 position)
    {
        Manager.manager.poopPool.Get().transform.position = position;
        yield return new WaitForSeconds(defaultTimerGap / Mathf.Sqrt(speedLevel) + Time.deltaTime);
        Manager.manager.Match3Poop(position);
    }

    public void CreatePoop()
    {
        var position = TailTransform ? TailTransform.position : transform.position - (Vector3)(Vector2)now * unitScale;
        StartCoroutine(CreatePoop(position));
    }

    private void CreateBody(Vector3 position)
    {
        Manager.manager.bodyPool.Get().transform.position = position;
    }

    private IEnumerator DeleteBody(int num)
    {
        for (var i = 0; i < num; i++)
        {
            yield return null;
            RemoveBody();
        }
    }

    private void RemoveBody()
    {
        if (TailTransform)
        {
            Manager.manager.bodyPool.Release(TailTransform.gameObject);
        }
        else
        {
            Manager.manager.SnakeDie(SnakeDieReason.Length0);
        }
    }

    #endregion

    #region Game Cycle

    public void Start()
    {
        // for (var i = 0; i < 3; ++i)
        // {
        //     CreateBody(transform.position - (i + 1) * new Vector3(0, unitScale, 0));
        // }
        now = Vector2Int.up;
        angle = 0;
        Manager.manager.CreateFood();
    }

    private void Update()
    {
        //Input
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
            Step();
            canInput = true;
            timer = 0;
        }
        
        foreach (var timerCommand in TimerCommands.Where(command=>!command.executed))
        {
            timerCommand.Update(Time.deltaTime);
        }
        
    }
    private void Step()
    {
        //Calculate target position
        tmp = transform.position;
        Vector2 nextPos = unitScale * (Vector2)now + (Vector2)transform.position;
        
        //Judge If the snake run out of the range (if so die with the hit wall reason)
        if (!Manager.manager.IsPosInRange(nextPos))
        {
            Manager.manager.SnakeDie(SnakeDieReason.HitWall);
            return;
        }
        
        //Step Command Logics
        foreach (var poopCommand in StepCommands.Where(command => !command.executed))
        {
            poopCommand.Step();
        }

        //Move
        transform.position = nextPos;
        if (TailTransform)
        {
            TailTransform.position = tmp;
            TailTransform.SetAsFirstSibling();
        }
        transform.eulerAngles = angle * Vector3.forward;
    }

    #endregion

    #region Collision
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Food"))
        {
            Manager.manager.AddScore(speedLevel);
            Manager.manager.CreateFood();
            
            //Create a new body
            CreateBody(TailTransform ? TailTransform.position : transform.position - (Vector3)(Vector2)now * unitScale);
            
            AddStepCommand<CmdCreatePoop>(DigestMoveNumber);
        }
        if (other.CompareTag("Body"))
        {
            Manager.manager.SnakeDie(SnakeDieReason.HitSelf);
        }
        if (other.CompareTag("Poop"))
        {
            Manager.manager.poopPool.Release(other.gameObject);
            DealPoopEffect();
        }
        
        if (other.CompareTag("Mole"))
        {
            Manager.manager.PlayAudio(6);
            Manager.manager.AddScore(20);
            Destroy(other.gameObject);
        }

        if (other.CompareTag("Body_PoopSnake"))
        {
            Manager.manager.PlayAudio(6);
            Manager.manager.AddScore(50);
            Manager.manager.poopSnake.Die();
        }
    }

    private void DealPoopEffect(int type = -1)
    {
        PoopEffectType damageType;
        if (type >= 0)
        {
            damageType = (PoopEffectType)type;
        }
        else
        {
            damageType = (PoopEffectType)Rng.Next(System.Enum.GetNames(typeof(PoopEffectType)).Length);
        }
        switch (damageType)
        {
            case PoopEffectType.ReverseInput:
            {
                AddTimerCommand<CmdReverseInput>(ReverseTime);
                break;
            }
            case PoopEffectType.LostControl:
            {
                AddStepCommand<CmdLostControl>(lostControlPower++);
                break;
            }
            case PoopEffectType.ReduceLength:
            {
                StartCoroutine(DeleteBody(reduceLengthAmount));
                Manager.manager.PlayAudio(4);
                Manager.manager.PrintToScreenOneTime($"Reduced your length by {reduceLengthAmount++}.");
                
                break;
            }
            case PoopEffectType.Speedup:
            {
                Manager.manager.PlayAudio(2);
                Manager.manager.PrintToScreenOneTime($"Speed Level Up to {++speedLevel}");
                
                break;
            }
            case PoopEffectType.CreateMole:
            {
                Manager.manager.CreateMole();
                break;
            }
        }
    }

    #endregion
    
    #region Command

    private static void AddStepCommand<T>(int step) where T: StepCommand, new()
    {
        StepCommand newCommand;
        if (typeof(T) != typeof(CmdCreatePoop))
        {
            newCommand = StepCommands.FirstOrDefault(command => command.GetType() == typeof(T) && !command.executed);
            if (newCommand != null)
            {
                newCommand.Reset(step);
                return;
            }
        }

        newCommand = StepCommands.FirstOrDefault(command => command.GetType() == typeof(T) && command.executed);
        if (newCommand == null)
        {
            newCommand = new T();
            StepCommands.Add(newCommand);
        }
        newCommand.Init(step);
    }

    private static void AddTimerCommand<T>(float time) where T : TimerCommand, new()
    {
        var newCommand = TimerCommands.FirstOrDefault(command => command.GetType() == typeof(T) && !command.executed);
        if (newCommand != null)
        {
            newCommand.Reset(time);
            return;
        }
        newCommand = TimerCommands.FirstOrDefault(command => command.GetType() == typeof(T) && command.executed);
        if (newCommand == null)
        {
            newCommand = new T();
            TimerCommands.Add(newCommand);
        }
        newCommand.Init(time);
    }


    #endregion

}