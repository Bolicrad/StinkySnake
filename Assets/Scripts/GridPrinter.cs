using System;
using UnityEngine;
using Random = UnityEngine.Random;
public class GridPrinter : MonoBehaviour
{
    public Material lineMaterial;
    private static readonly Color LineColor = Color.white;
    private static readonly Color OverlapColor = Color.red;
    private static readonly Color SelectedColor = Color.blue;
    private static readonly Color AvailableColor = Color.green;
    
    void OnPostRender()
    {
        DrawGrid();
        
        //Draw Mouse Grid
        Vector2Int mousePos = GetMouseGridPos();
        if (Mathf.Abs(mousePos.x) > Manager.manager.gridMax.x || Mathf.Abs(mousePos.y) > Manager.manager.gridMax.y) return;
        DrawSquare(GridToWorldPoint(mousePos, Manager.manager.transform.position), OverlapColor);
    }
    
    public static Vector2Int WorldToGridPoint(Vector2 worldPos, Vector2 center)
    {
        var pos = worldPos - center;
        return new Vector2Int(
            (int)((worldPos.x > center.x ? pos.x + 0.25f : pos.x - 0.25f) / 0.5f),
            (int)((worldPos.y > center.y ? pos.y + 0.25f : pos.y - 0.25f) / 0.5f));
    }
    
    public static Vector2 GridToWorldPoint(Vector2Int gridPos, Vector2 center)
    {
        return new Vector2(
            gridPos.x * 0.5f + center.x,
            gridPos.y * 0.5f + center.y * 0.5f);
    }
    
    public static Vector2Int GetRandomGridPos(Vector2Int range)
    {
        var x = Random.Range(-range.x + 1, range.x);
        var y = Random.Range(-range.y + 1, range.y);

        return new Vector2Int(x, y);
    }

    public static Vector2Int GetMouseGridPos()
    {
        return WorldToGridPoint(Camera.main!.ScreenToWorldPoint(Input.mousePosition), Manager.manager.transform.position);
    }

    void DrawLine(Vector2 start, Vector2 end, Color color)
    {
        GL.Begin(GL.LINES);
        lineMaterial.SetPass(0);
        GL.Color(color);
        GL.Vertex3(start.x,start.y,0);
        GL.Vertex3(end.x,end.y,0);
        GL.End();
    }
    
    void DrawSquare(Vector2 center, float width, float height, Color color)
    {
        Vector2 leftUp = new Vector2(center.x - width / 2f, center.y + height / 2f);
        Vector2 rightUp = new Vector2(center.x + width / 2f, center.y + height / 2f);
        Vector2 leftDown = new Vector2(center.x - width / 2f, center.y - height / 2f);
        Vector2 rightDown = new Vector2(center.x + width / 2f, center.y - height / 2f);
 
        DrawLine(rightUp, leftUp, color);
        DrawLine(leftUp, leftDown, color);
        DrawLine(leftDown, rightDown, color);
        DrawLine(rightDown, rightUp, color);
    }
    
    void DrawSquare(Vector2 center, Color color)
    {
        DrawSquare(center, 0.5f, 0.5f, color);
    }
    
    void DrawGrid()
    {
        Vector2 cameraPos = Manager.manager.transform.position;
        var size = Manager.manager.spriteRenderer.size;
        var rightUp = cameraPos + size / 2;
        var leftDown = cameraPos - size / 2;

        float x = cameraPos.x + 0.25f;
        float y = cameraPos.y + 0.25f;
        
        //Draw Vertical Lines;
        while (x < rightUp.x)
        {
            DrawLine(new Vector2(x, rightUp.y), new Vector2(x, leftDown.y), LineColor);
            DrawLine(new Vector2(2 * cameraPos.x - x, rightUp.y), new Vector2(2 * cameraPos.x - x, leftDown.y),
                LineColor);
            x += 0.5f;
        }
        
        //Draw Horizontal Lines;
        while (y < rightUp.y)
        {
            DrawLine(new Vector2(leftDown.x, y), new Vector2(rightUp.x, y), LineColor);
            DrawLine(new Vector2(leftDown.x, 2 * cameraPos.y - y), new Vector2(rightUp.x, 2 * cameraPos.y - y),
                LineColor);
            y += 0.5f;
        }
    }

    private void Start()
    {

    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Debug.Log(GetMouseGridPos());
        }
    }
    
}
