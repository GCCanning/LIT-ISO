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
        ISkillWebViewModel _skillWeb;
        FoundationProgression _progression;
        FoundationQoLService _qol;

        public bool AnyOpen => _panel != null && _panel.IsOpen;

        void Awake()
        {
            _inventory ??= new PlaceholderInventoryViewModel();
            _crafting ??= new PlaceholderCraftingViewModel();
            _character ??= new PlaceholderCharacterSheetViewModel();
            _skillWeb ??= new PlaceholderSkillWebViewModel();

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

        public void BindSkillWeb(ISkillWebViewModel model)
        {
            _skillWeb = model ?? new PlaceholderSkillWebViewModel();
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
        public void OpenCharacterSheet() => Open(CharacterPanelTab.Character);
        public void OpenSkills() => Open(CharacterPanelTab.Skills);
        public void OpenSpells() => Open(CharacterPanelTab.Spells);
        public void OpenQuests() => Open(CharacterPanelTab.Journal);
        public void OpenSystem() => Open(CharacterPanelTab.System);
        public void OpenMapTab() => Open(CharacterPanelTab.Map);

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape) && AnyOpen)
            {
                // inventory overlays (context menu / drag) swallow Escape before
                // the panel itself closes
                if (_panel.ConsumeEscape())
                {
                    FoundationUiCoordinator.Active?.ConsumeInputThisFrame();
                    return;
                }
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
            if (Input.GetKeyDown(KeyCode.T)) OpenSkills();
            if (Input.GetKeyDown(KeyCode.J)) OpenQuests();
            // F8: preview the Day-7 Class Assignment ceremony (placeholder data
            // until the Foundation trial-scoring runtime lands)
            if (Input.GetKeyDown(KeyCode.F8))
                ClassAssignmentView.Show(new PlaceholderClassAssignmentViewModel());
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

            _panel.Init(_inventory, _crafting, _character, _progression, _qol, _skillWeb);
            RefreshCoordinatorState();
        }

        void RefreshCoordinatorState()
        {
            FoundationUiCoordinator.Active?.SetModalOpen("panels", AnyOpen);
        }
    }
}
