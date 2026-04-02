using UnityEngine;
using UnityEngine.UI;

public class LoopingBG : MonoBehaviour
{
    [SerializeField] private RawImage bgImage;
    [SerializeField] private float scrollSpeed = 0.5f;
    [SerializeField] float x = 1, y = 1;
    private void Update()
    {
        bgImage.uvRect = new Rect(bgImage.uvRect.position + new Vector2(x,y) * Time.deltaTime * scrollSpeed, bgImage.uvRect.size);
        //clear memory x,y to prevent overflow
        if (bgImage.uvRect.position.x > 1000 || bgImage.uvRect.position.y > 1000)
        {
            bgImage.uvRect = new Rect(Vector2.zero, bgImage.uvRect.size);
        }
    }
}
