using UnityEngine;

public class ScreenBorderBuilder : MonoBehaviour
{
    public float thickness = 1f;

    private void Start()
    {
        Camera cam = Camera.main;

        Vector2 screenBottomLeft = cam.ScreenToWorldPoint(new Vector2(0, 0));
        Vector2 screenTopRight = cam.ScreenToWorldPoint(new Vector2(Screen.width, Screen.height));

        float left = screenBottomLeft.x;
        float right = screenTopRight.x;
        float bottom = screenBottomLeft.y;
        float top = screenTopRight.y;

        CreateWall(new Vector2(left - thickness / 2, 0), new Vector2(thickness, top - bottom));   
        CreateWall(new Vector2(right + thickness / 2, 0), new Vector2(thickness, top - bottom));  
        CreateWall(new Vector2(0, top + thickness / 2), new Vector2(right - left, thickness));    
        CreateWall(new Vector2(0, bottom - thickness / 2), new Vector2(right - left, thickness)); 
    }

    private void CreateWall(Vector2 pos, Vector2 size)
    {
        GameObject wall = new GameObject("Wall");
        wall.transform.parent = transform;
        wall.transform.position = pos;

        BoxCollider2D col = wall.AddComponent<BoxCollider2D>();
        col.size = size;
        col.isTrigger = false;
    }
}
