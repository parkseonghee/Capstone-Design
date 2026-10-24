using System;
using System.Collections.Generic;
using UnityEngine;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 마을 씬의 진행 담당: 건물 자리(BuildingSlot)에 건물을 만들고, 건물이 눌리면 연결된 기능 화면을 연다.
    /// 돌아다니는 캐릭터가 눌리면 캐릭터 선택 확인창을 연다.
    ///
    /// 어느 자리에 어떤 건물을 둘지, Panel Id마다 어떤 기능 화면을 열지는 전부 인스펙터에서 정한다(Hard Rule 1).
    /// 기능 화면·확인창이 열려 있거나 젤리 연출 중에는 모든 건물과 캐릭터의 터치를 막는다.
    /// 기능 화면은 스스로 닫히므로(닫기 버튼), 여기서는 <see cref="IVillageFeature.IsOpen"/>이 꺼지는 것을 보고 다시 푼다.
    /// </summary>
    public sealed class VillageManager : MonoBehaviour
    {
        [Serializable]
        private sealed class BuildingPlacement
        {
            [Tooltip("이 자리에 지을 건물. 비우면 빈 자리로 둔다.")]
            public BuildingData data;
            [Tooltip("건물을 자식으로 둘 BuildingSlot. 위치가 건물의 발 위치다.")]
            public Transform slot;
        }

        [Serializable]
        private sealed class FeatureLink
        {
            [Tooltip("BuildingData의 Panel Id와 같아야 한다.")]
            public string panelId;
            [Tooltip("열 기능 화면. IVillageFeature를 구현한 컴포넌트(StageMapView, RecipeShopView, TrashBinView 등).")]
            public MonoBehaviour view;
        }

        [Header("건물")]
        [SerializeField] private Building buildingPrefab;
        [SerializeField] private List<BuildingPlacement> placements = new List<BuildingPlacement>();

        [Header("기능 화면")]
        [SerializeField, Tooltip("Panel Id별로 열 기능 화면.")]
        private List<FeatureLink> features = new List<FeatureLink>();

        [Header("캐릭터 선택")]
        [SerializeField, Tooltip("눌러서 고를 수 있는 마을 캐릭터들.")]
        private List<VillageCharacter> characters = new List<VillageCharacter>();
        [SerializeField] private CharacterSelectPopup characterPopup;

        private readonly List<Building> _buildings = new List<Building>();
        private readonly Dictionary<string, IVillageFeature> _features = new Dictionary<string, IVillageFeature>();
        private IVillageFeature _opened;
        private bool _busy;

        private void Awake()
        {
            foreach (FeatureLink link in features)
            {
                var feature = link.view as IVillageFeature;
                if (feature == null)
                {
                    Debug.LogWarning($"{nameof(VillageManager)}: '{link.panelId}'에 연결된 화면이 없거나 IVillageFeature가 아닙니다.", this);
                    continue;
                }

                if (_features.ContainsKey(link.panelId))
                {
                    Debug.LogWarning($"{nameof(VillageManager)}: Panel Id '{link.panelId}'가 중복됩니다.", this);
                    continue;
                }

                _features.Add(link.panelId, feature);
            }
        }

        private void Start()
        {
            CreateBuildings();

            foreach (VillageCharacter character in characters)
            {
                if (character != null)
                {
                    character.Clicked += OnCharacterClicked;
                }
            }
        }

        private void Update()
        {
            if (_opened != null && !_opened.IsOpen)
            {
                _opened = null;
                SetBusy(false);
            }
        }

        private void OnDestroy()
        {
            foreach (Building building in _buildings)
            {
                if (building != null)
                {
                    building.Clicked -= OnBuildingClicked;
                }
            }

            foreach (VillageCharacter character in characters)
            {
                if (character != null)
                {
                    character.Clicked -= OnCharacterClicked;
                }
            }
        }

        private void OnValidate()
        {
            foreach (FeatureLink link in features)
            {
                if (link.view != null && !(link.view is IVillageFeature))
                {
                    Debug.LogWarning($"{nameof(VillageManager)}: {link.view.GetType().Name}는 IVillageFeature가 아닙니다.", this);
                }
            }
        }

        private void CreateBuildings()
        {
            foreach (BuildingPlacement placement in placements)
            {
                if (placement.data == null || placement.slot == null)
                {
                    continue;
                }

                Building building = Instantiate(buildingPrefab, placement.slot);
                building.transform.localPosition = Vector3.zero;
                building.Setup(placement.data);
                building.Clicked += OnBuildingClicked;
                _buildings.Add(building);
            }
        }

        private void OnBuildingClicked(Building building)
        {
            if (_busy)
            {
                return;
            }

            if (!_features.TryGetValue(building.Data.PanelId, out IVillageFeature feature))
            {
                Debug.LogWarning($"{nameof(VillageManager)}: '{building.Data.PanelId}' 기능 화면이 없습니다.", building);
                building.PlayPressed(null);
                return;
            }

            SetBusy(true);
            building.PlayPressed(() =>
            {
                feature.Open();
                if (feature.IsOpen)
                {
                    _opened = feature;
                }
                else
                {
                    SetBusy(false); // 열리지 못했으면(연결 누락 등) 바로 푼다.
                }
            });
        }

        private void OnCharacterClicked(VillageCharacter character)
        {
            if (_busy || characterPopup == null)
            {
                return;
            }

            SetBusy(true);
            characterPopup.Show(character);
            _opened = characterPopup;
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            foreach (Building building in _buildings)
            {
                building.Interactable = !busy;
            }

            foreach (VillageCharacter character in characters)
            {
                if (character != null)
                {
                    character.Interactable = !busy;
                }
            }
        }
    }
}
