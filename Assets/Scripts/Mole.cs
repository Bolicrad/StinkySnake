using System;
using System.Collections.Generic;
using UnityEngine;
using Coroutines;
using TMPro;
using Coroutine = Coroutines.Coroutine;
using Random = UnityEngine.Random;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

public class Mole : MonoBehaviour
{
    private Coroutine main;
    private TMP_Text tmpText;
    // Start is called before the first frame update
    void Start()
    {
        tmpText = Manager.manager.textPool.Get().GetComponent<TMP_Text>();
        main = new Coroutine(Main());
    }

    // Update is called once per frame
    void Update()
    {
        main.Update();
    }

    private void OnDestroy()
    {
        Manager.manager.textPool.Release(tmpText.gameObject);
    }

    IEnumerable<Instruction> Main()
    {
        Vector2Int dir = Vector2Int.up;
        tmpText.text = "Summoned a Mole. Beat it!";
        try
        {
            Transform target = null;
            while (target == null)
            {
                yield return ControlFlow.ExecuteWhileRunning(
                    FindFood(targetFound => target = targetFound),
                    Wander(dir, finalDir => dir = finalDir));
                if (target != null)
                {
                    tmpText.text = "Mole: Dashing to food.";
                    yield return ControlFlow.Call(DashToTarget(target));
                }
            }
        }
        finally
        {
            tmpText.text = "Mole: Escaped.";
            if(gameObject)Destroy(gameObject);
        }
    }

    IEnumerable<Instruction> FindFood(Action<Transform> targetFound)
    {
        tmpText.text = "Mole: Searching for food.";
        GameObject foodObj = GameObject.FindWithTag("Food");
        while (foodObj!=null &&
               Manager.manager.WorldToGridPoint(transform.position).x !=
               Manager.manager.WorldToGridPoint(foodObj.transform.position).x 
               &&
               Manager.manager.WorldToGridPoint(transform.position).y !=
               Manager.manager.WorldToGridPoint(foodObj.transform.position).y
              )
        {
            yield return null;
        }
        if(foodObj!=null) targetFound(foodObj.transform);
    }

    IEnumerable<Instruction> Wander(Vector2Int startDir, Action<Vector2Int> finalDir)
    {
        Vector2Int dir = startDir;
        try
        {
            int stepCount = 0;
            while (true)
            {
                yield return Utils.WaitForSeconds(0.3f);
            
                Vector2Int newDir = dir;
                if (stepCount >= Random.Range(3,6))
                {
                    //Time to change a direction
                    newDir = GetNewDir(dir);
                }

                Vector2 nextPos = GetNextPos(newDir);
                while (!Manager.manager.IsPosInRange(nextPos))
                {
                    newDir = GetNewDir(newDir);
                    nextPos = GetNextPos(newDir);
                }
                if (newDir != dir)
                {
                    RotateBody(newDir);
                    dir = newDir;
                    stepCount = 0;
                }
                else
                {
                    stepCount++;
                }
                yield return ControlFlow.Call(MoveBodyTo(GetNextPos(dir)));
            }
        }
        finally
        {
            finalDir(dir);
        }
    }

    IEnumerable<Instruction> DashToTarget(Transform target)
    {
        Vector2Int dir = (Manager.manager.WorldToGridPoint(target.position)
                          - Manager.manager.WorldToGridPoint(transform.position));
        if (dir.x == 0) dir.y = Math.Sign(dir.y);
        if (dir.y == 0) dir.x = Math.Sign(dir.x);
        RotateBody(dir);

        while (target != null &&
               Manager.manager.WorldToGridPoint(transform.position)
               != Manager.manager.WorldToGridPoint(target.position))
        {
            yield return Utils.WaitForSeconds(0.15f);
            yield return ControlFlow.Call(MoveBodyTo(GetNextPos(dir)));
        }
        
        if (target != null)
        {
            //Mole successfully ate the food
            Manager.manager.CreateFood();
        }
        else
        {
            //Player Ate the food, go to revenge
            while (Manager.manager.IsPosInRange(transform.position))
            {
                yield return Utils.WaitForSeconds(0.15f);
                yield return ControlFlow.Call(MoveBodyTo(GetNextPos(dir)));
            }
        }
    }
    
    IEnumerable<Instruction> MoveBodyTo(Vector2 targetPos)
    {
        if (Manager.manager.IsPosOccupied(targetPos, out var cols))
        {
            Debug.Log($"Mole Enter: {cols[0].tag}");

            if (cols[0].CompareTag("Head"))
            {
                Manager.manager.AddScore(20);
                Destroy(gameObject);
            }

            if (cols[0].CompareTag("Body"))
            {
                Manager.manager.SnakeDie(Head.SnakeDieReason.PoopSnake);
            }

            if (cols[0].CompareTag("Poop"))
            {
                var poopShift = Manager.manager.GetUnoccupiedPos();
                GridPrinter.gridPrinter.aimPos = poopShift;
                GridPrinter.gridPrinter.drawingAim = true;
                yield return Utils.WaitForSeconds(0.3f);
                cols[0].transform.position = poopShift;
                GridPrinter.gridPrinter.drawingAim = false;
            }
        }

        if (!Manager.manager.IsPosInRange(targetPos))
        {
            //Mole Dashed out of border, Die
            Destroy(gameObject);
        }

        yield return null;
        transform.position = targetPos;
    }

    Vector2 GetNextPos(Vector2Int dir)
    {
        return (Vector2)dir * 0.5f + (Vector2)transform.position;
    }

    Vector2Int GetNewDir(Vector2Int dir)
    {
        var newDir = dir;
        if (dir.x == 0)
        {
            newDir = Random.Range(0, 2) > 0 ? Vector2Int.left : Vector2Int.right;
        }
        if (dir.y == 0)
        {
            newDir = Random.Range(0, 2) > 0 ? Vector2Int.up : Vector2Int.down;
        }
        return newDir;
    }

    void RotateBody(Vector2Int dir)
    {
        int angle = 0;
        if (dir == Vector2Int.up) angle = 0;
        if (dir == Vector2Int.left) angle = 90;
        if (dir == Vector2Int.down) angle = 180;
        if (dir == Vector2Int.right) angle = -90;
        transform.eulerAngles = angle * Vector3.forward;
    }
}
