using UnityEngine;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 마을 건물 하나의 정보. 건물 표시(<see cref="Building"/>)와 패널 연결(<see cref="VillageManager"/>)이 여기서 읽는다.
    ///
    /// 스프라이트를 비워 두면 색상 박스로 표시되고, 일러스트를 넣으면 그 스프라이트로 바뀐다 —
    /// 아트 교체는 이 에셋만 고치면 되고 코드는 안 건드린다(Hard Rule 1).
    /// </summary>
    [CreateAssetMenu(menuName = "RecycleLife/Village/Building Data", fileName = "BuildingData")]
    public sealed class BuildingData : ScriptableObject
    {
        [SerializeField, Tooltip("건물 이름표에 찍을 이름.")]
        private string displayName = "건물";

        [SerializeField, Tooltip("건물 일러스트. 비우면 아래 색상의 플레이스홀더 박스로 표시한다.")]
        private Sprite sprite;

        [SerializeField, Tooltip("스프라이트가 없을 때 쓰는 박스 색.")]
        private Color color = Color.white;

        [SerializeField, Tooltip("눌렀을 때 열 패널의 ID. UIPanel의 Panel Id와 같아야 한다.")]
        private string panelId;

        public string DisplayName => displayName;
        public Sprite Sprite => sprite;
        public Color Color => color;
        public string PanelId => panelId;
    }
}
