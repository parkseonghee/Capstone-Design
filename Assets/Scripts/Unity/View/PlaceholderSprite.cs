using UnityEngine;

namespace RecycleLife.Unity
{
    /// <summary>
    /// EntityVisualSet에 스프라이트가 없을 때만 쓰는 단색 사각형.
    /// 아트가 들어오기 전에도 보드가 보이게 하기 위한 임시 수단이며,
    /// 한 번만 만들어 모든 뷰가 공유한다(Hard Rule 8).
    /// </summary>
    internal static class PlaceholderSprite
    {
        private static Sprite _square;

        public static Sprite Square
        {
            get
            {
                if (_square != null)
                {
                    return _square;
                }

                var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false)
                {
                    name = "RecycleLife_PlaceholderSquare",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave,
                };

                var pixels = new Color32[8 * 8];
                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = new Color32(255, 255, 255, 255);
                }

                texture.SetPixels32(pixels);
                texture.Apply();

                _square = Sprite.Create(texture, new Rect(0f, 0f, 8f, 8f), new Vector2(0.5f, 0.5f), 8f);
                _square.name = "RecycleLife_PlaceholderSquare";
                _square.hideFlags = HideFlags.HideAndDontSave;

                return _square;
            }
        }
    }
}
