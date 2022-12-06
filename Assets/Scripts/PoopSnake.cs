using System.Collections.Generic;
using UnityEngine;
using Coroutines;

public class PoopSnake : Mole
{
    private Vector2Int currentDir;
    private static Transform BodyParent => Manager.manager.poopSnakeBodyParent;
    private static Transform TailTransform => BodyParent.childCount > 0 ? BodyParent.GetChild(BodyParent.childCount - 1) : null;

    public void Init(Vector2Int direction)
    {
        currentDir = direction; 
        RotateBody(currentDir);
    }

    protected override IEnumerable<Instruction> Main()
    {
        tmpText.text = "Poop Snake: Wandering...";
        yield return null;
        try
        {
            yield return ControlFlow.ExecuteWhile(
                () => Manager.manager.head != null,
                Wander(currentDir, outDir => currentDir = outDir));
        }
        finally
        {
            Die();
        }

        // ReSharper disable once IteratorNeverReturns
    }

    protected override IEnumerable<Instruction> CheckNextPos(Vector2 targetPos)
    {
        if (Manager.manager.IsPosOccupied(targetPos, out var cols))
        {
            Debug.Log($"Poop Snake Enter: {cols[0].tag}");
            if (cols[0].CompareTag("Poop"))
            {
                Destroy(cols[0].gameObject);
                AddTail();
            }
        }
        yield return null;
    }

    protected override IEnumerable<Instruction> MoveTail(Vector3 temp)
    {
        if (TailTransform)
        {
            TailTransform.position = temp;
            TailTransform.SetAsFirstSibling();
        }
        yield return null;
    }

    void AddTail()
    {
        Manager.manager.poopBodyPool.Get().transform.position =
            TailTransform ? TailTransform.position : transform.position - (Vector3)(Vector2)currentDir * 0.5f;
    }

    public void Die()
    {
        while (TailTransform)
        {
            Manager.manager.poopBodyPool.Release(TailTransform.gameObject);
        }
        if (this && gameObject != null) Destroy(gameObject);
        Manager.manager.poopSnake = null;
    }
    
    private void OnTriggerEnter2D(Collider2D col)
    {
        if (col.CompareTag("Body"))
        {
            Manager.manager.SnakeDie(Head.SnakeDieReason.PoopSnake);
        }

        if (col.CompareTag("Head"))
        {
            Manager.manager.PlayAudio(6);
            Manager.manager.AddScore(50);
            Die();
        }
    }
}
