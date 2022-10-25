using System.Collections;
using UnityEngine;
// ReSharper disable Unity.InefficientPropertyAccess

public class Head : MonoBehaviour
{
    public enum PoopEffectType
    {
        ReduceLength,
        ReverseInput,
        LostControl,
        Speedup
    }

    public enum SnakeDieReason
    {
        HitSelf, 
        HitWall, 
        Length0, 
        PoopSnake
    };
    private static readonly System.Random Rng = new System.Random();
    private Vector2 now; // actual head direction
    private Vector2 next;
    private int angle;
    private Transform TailTransform => bodyParent.childCount > 0 ? bodyParent.GetChild(bodyParent.childCount - 1) : null;
    public Transform bodyParent;
    public GameObject bodyPrefab;
    public GameObject foodPrefab;
    public GameObject poopPrefab;
    public GameObject food;
    private Vector3 tmp;
    private bool digesting;
    private int poopCount;
    public int digestMoveNumber = 3;
    private int poopDamage = 1;
    private bool canInput = true;
    public float unitScale = 0.5f;
    public float timer;
    public float defaultTimerGap = 0.1f;
    private int speedLevel = 1;
    public int debuffTime = 1;
    private bool lostControl;
    private bool reverseInput;
    private Coroutine lostControlHandler;
    private Coroutine reverseInputHandler;
    public int score;

    public void Start()
    {
        for (var i = 0; i < 3; ++i)
        {
            CreateBody(transform.position - (i + 1) * new Vector3(0, unitScale, 0));
        }
        now = Vector2.up;
        next = Vector2.up;
        angle = 0;
        Manager.manager.CreateFood();
    }
    
    private void CreatePoop(Vector3 position)
    {
        Instantiate(poopPrefab, position, Quaternion.identity);
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

    private IEnumerator LostControlDebuff(int time)
    {
        lostControl = true;
        yield return new WaitForSeconds(time);
        lostControl = false;
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
        
        transform.position = unitScale * now + (Vector2)transform.position;

        if (digesting)
        {
            if (poopCount > 0)
            {
                poopCount--;
            }
            else
            {
                CreatePoop(TailTransform ? TailTransform.position : tmp);
                digesting = false;
            }
        }
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
            if (now == Vector2.up || now == Vector2.down) {
                input = Input.GetAxis("Horizontal");
                if (Mathf.Abs(input) > 0) {
                    now = (reverseInput?input<0:input>0) ? Vector2.right : Vector2.left;
                    angle = (reverseInput ? input < 0 : input > 0) ? -90 : 90;
                    canInput = false;
                }
            }
            if (now == Vector2.left || now == Vector2.right) {
                input = Input.GetAxis("Vertical");
                if (Mathf.Abs(input) > 0) {
                    now = (reverseInput ? input < 0 : input > 0) ? Vector2.up : Vector2.down;
                    angle = (reverseInput ? input < 0 : input > 0) ? 0 : 180;
                    canInput = false;
                }
            }
        }

        timer += Time.deltaTime;


        if (timer > defaultTimerGap / speedLevel)
        {
            MoveBody();
            canInput = true;
            timer = 0;
        }
    }
    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log("Enter: "+other.tag);
        if (other.tag.Equals("Food"))
        {
            score += speedLevel;
            Manager.manager.scoreText.text = $"Score: {score}";
            Manager.manager.PlayAudio(1);
            Destroy(food);
            food = null;
            CreateBody(TailTransform ? TailTransform.position : transform.position);
            digesting = true;
            poopCount = digestMoveNumber;
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
                        option = debuffTime;
                        if (lostControlHandler != null)
                        {
                            StopCoroutine(lostControlHandler);
                            lostControlHandler = null;
                        }
                        lostControlHandler = StartCoroutine(LostControlDebuff(debuffTime));
                        break;
                    }
                case PoopEffectType.Speedup:
                    {
                        speedLevel++;
                        option = speedLevel;
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

    private void OnTriggerExit2D(Collider2D other)
    {
        Debug.Log("Exit: "+other.tag);
        if (other.tag.Equals("Boundary")&&transform.gameObject.activeInHierarchy)
        {
            Manager.manager.SnakeDie(SnakeDieReason.HitWall);
        }
    }
}