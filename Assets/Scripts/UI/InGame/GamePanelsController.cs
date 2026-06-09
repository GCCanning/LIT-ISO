using IsoCore.Foundation;
using UnityEngine;

namespace LitIso.UI.InGame
{
    /// <summary>
    /// Routes all in-game panel keys to one canonical tabbed Character/System panel.
    /// Default keys: I inventory, C crafting, K/Tab status, M remains map overlay.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GamePanelsController : MonoBehaviour
    {
        CharacterPanelView _panel;
        IInventoryViewModel _inventory;
        ICraftingViewModel _crafting;
        ICharacterSheetViewModel _character;
        FoundationProgression _progression;
        FoundationQoLService _qol;

        public bool AnyOpen => _panel != null && _panel.IsOpen;

        void Awake()
        {
            _inventory ??= new PlaceholderInventoryViewModel();
            _crafting ??= new PlaceholderCraftingViewModel();
            _character ??= new PlaceholderCharacterSheetViewModel();

            _panel = gameObject.AddComponent<CharacterPanelView>();
            _panel.Closed += RefreshCoordinatorState;
            RebindPanel();
            RefreshCoordinatorState();
        }

        public void BindInventory(IInventoryViewModel model)
        {
            _inventory = model ?? new PlaceholderInventoryViewModel();
            RebindPanel();
        }

        public void BindCrafting(ICraftingViewModel model)
        {
            _crafting = model ?? new PlaceholderCraftingViewModel();
            RebindPanel();
        }

        public void BindCharacter(ICharacterSheetViewModel model)
        {
            _character = model ?? new PlaceholderCharacterSheetViewModel();
            RebindPanel();
        }

        public void BindProgression(FoundationProgression progression, FoundationQoLService qol)
        {
            _progression = progression;
            _qol = qol;
            RebindPanel();
        }

        public void OpenInventory() => Open(CharacterPanelTab.Inventory);
        public void OpenCrafting() => Open(CharacterPanelTab.Crafting);
        public void OpenCharacterSheet() => Open(CharacterPanelTab.Status);
        public void OpenSkills() => Open(CharacterPanelTab.Skills);
        public void OpenQuests() => Open(CharacterPanelTab.Quests);
        public void OpenSystem() => Open(CharacterPanelTab.System);
        public void OpenMapTab() => Open(CharacterPanelTab.Map);

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape) && AnyOpen)
            {
                _panel.Hide();
                FoundationUiCoordinator.Active?.ConsumeInputThisFrame();
                RefreshCoordinatorState();
                return;
            }

            var ui = FoundationUiCoordinator.Active;
            if (ui != null && ui.InputConsumedThisFrame)
                return;

            if (Input.GetKeyDown(KeyCode.I)) OpenInventory();
            if (Input.GetKeyDown(KeyCode.C)) OpenCrafting();
            if (Input.GetKeyDown(KeyCode.K) || Input.GetKeyDown(KeyCode.Tab)) OpenCharacterSheet();
        }

        void Open(CharacterPanelTab tab)
        {
            if (_panel == null)
                return;

            _panel.Show(tab);
            FoundationUiCoordinator.Active?.ConsumeInputThisFrame();
            RefreshCoordinatorState();
        }

        void RebindPanel()
        {
            if (_panel == null)
                return;

            _panel.Init(_inventory, _crafting, _character, _progression, _qol);
            RefreshCoordinatorState();
        }

        void RefreshCoordinatorState()
        {
            FoundationUiCoordinator.Active?.SetModalOpen("panels", AnyOpen);
        }
    }
}
