using UnityEngine;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 슬라이드 표시(링·점·화살표)에 쓰는 스프라이트를 코드로 만들어 둔다.
    ///
    /// 아트 파일로 두지 않는 건 <b>배선을 없애기 위해서다</b>. 조작 표시는 어느 씬에 붙여도
    /// 바로 보여야 하는데, 프리팹이나 스프라이트 슬롯을 비워 두면 조용히 안 그려진다.
    /// 나중에 아트가 들어오면 SlideFeedbackView의 스프라이트 슬롯에 꽂으면 이쪽은 안 쓰인다.
    ///
    /// 셋 다 한 번만 만들어 모두가 공유한다(Hard Rule 8).
    /// </summary>
    internal static class SlideGizmoSprites
    {
        private const int RingSize = 128;
        private const int DotSize = 64;
        private const int ArrowSize = 64;

        private static Sprite _ring;
        private static Sprite _dot;
        private static Sprite _arrow;

        /// <summary>가운데가 빈 원. 지름이 "한 칸 나가는 거리"를 뜻한다.</summary>
        public static Sprite Ring
        {
            get
            {
                if (_ring != null)
                {
                    return _ring;
                }

                var pixels = new Color32[RingSize * RingSize];
                float center = (RingSize - 1) * 0.5f;
                float outer = center;
                float inner = center - RingSize * 0.055f;

                for (int y = 0; y < RingSize; y++)
                {
                    for (int x = 0; x < RingSize; x++)
                    {
                        float dx = x - center;
                        float dy = y - center;
                        float r = Mathf.Sqrt(dx * dx + dy * dy);

                        // 안쪽/바깥쪽 경계를 1픽셀에 걸쳐 흐려 계단이 안 보이게 한다.
                        float a = Mathf.Clamp01(outer - r) * Mathf.Clamp01(r - inner);
                        pixels[y * RingSize + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                    }
                }

                _ring = Build(pixels, RingSize, "RecycleLife_SlideRing");
                return _ring;
            }
        }

        /// <summary>꽉 찬 원. 손가락이 지금 있는 자리를 찍는다.</summary>
        public static Sprite Dot
        {
            get
            {
                if (_dot != null)
                {
                    return _dot;
                }

                var pixels = new Color32[DotSize * DotSize];
                float center = (DotSize - 1) * 0.5f;

                for (int y = 0; y < DotSize; y++)
                {
                    for (int x = 0; x < DotSize; x++)
                    {
                        float dx = x - center;
                        float dy = y - center;
                        float r = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(center - r);
                        pixels[y * DotSize + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                    }
                }

                _dot = Build(pixels, DotSize, "RecycleLife_SlideDot");
                return _dot;
            }
        }

        /// <summary>오른쪽(+x)을 가리키는 삼각형. 회전 0도가 오른쪽이 되도록 맞춰 둔다.</summary>
        public static Sprite Arrow
        {
            get
            {
                if (_arrow != null)
                {
                    return _arrow;
                }

                var pixels = new Color32[ArrowSize * ArrowSize];
                float center = (ArrowSize - 1) * 0.5f;

                for (int y = 0; y < ArrowSize; y++)
                {
                    for (int x = 0; x < ArrowSize; x++)
                    {
                        // 꼭짓점이 오른쪽 끝, 밑변이 왼쪽 끝인 이등변삼각형.
                        float t = x / (float)(ArrowSize - 1);           // 0(왼쪽) ~ 1(오른쪽)
                        float halfHeight = (1f - t) * center;           // 오른쪽으로 갈수록 좁아진다
                        float dy = Mathf.Abs(y - center);
                        float a = Mathf.Clamp01(halfHeight - dy);
                        pixels[y * ArrowSize + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                    }
                }

                _arrow = Build(pixels, ArrowSize, "RecycleLife_SlideArrow");
                return _arrow;
            }
        }

        /// <summary>
        /// 한 변이 1 월드 유닛인 스프라이트로 만든다(PPU = 변 길이).
        /// 이러면 쓰는 쪽에서 localScale이 곧 월드 크기가 돼 계산이 단순해진다.
        /// </summary>
        private static Sprite Build(Color32[] pixels, int size, string name)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };

            texture.SetPixels32(pixels);
            texture.Apply();

            var sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
