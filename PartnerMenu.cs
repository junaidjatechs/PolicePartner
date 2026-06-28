using System.Collections.Generic;
using Rage;
using RAGENativeUI;
using RAGENativeUI.Elements;

namespace PolicePartner
{
    public class PartnerMenu
    {
        private readonly PartnerManager _manager;

        private readonly MenuPool _menuPool;
        private readonly UIMenu _mainMenu;

        private readonly UIMenuListItem _genderItem;
        private readonly UIMenuListItem _modelItem;
        private readonly UIMenuItem     _statusItem;
        private readonly UIMenuItem     _spawnItem;
        private readonly UIMenuItem     _dismissItem;
        private readonly UIMenuItem     _followItem;

        // Use string lists — avoids dynamic/CSharpArgumentInfo issues
        private static readonly List<string> MaleModelLabels = new List<string>
        {
            "Auto (Match Dept.)",
            "s_m_y_cop_01 - LSPD Officer",
            "s_m_y_hwaycop_01 - Highway Patrol",
            "s_m_y_sheriff_01 - Sheriff",
            "s_m_y_ranger_01 - Park Ranger",
            "s_m_y_swat_01 - SWAT"
        };

        private static readonly List<string> FemaleModelLabels = new List<string>
        {
            "Auto (Match Dept.)",
            "s_f_y_cop_01 - LSPD Officer",
            "s_f_y_sheriff_01 - Sheriff"
        };

        private static readonly Dictionary<string, string> ModelMap = new Dictionary<string, string>
        {
            ["Auto (Match Dept.)"]               = "",
            ["s_m_y_cop_01 - LSPD Officer"]      = "s_m_y_cop_01",
            ["s_m_y_hwaycop_01 - Highway Patrol"] = "s_m_y_hwaycop_01",
            ["s_m_y_sheriff_01 - Sheriff"]        = "s_m_y_sheriff_01",
            ["s_m_y_ranger_01 - Park Ranger"]     = "s_m_y_ranger_01",
            ["s_m_y_swat_01 - SWAT"]              = "s_m_y_swat_01",
            ["s_f_y_cop_01 - LSPD Officer"]       = "s_f_y_cop_01",
            ["s_f_y_sheriff_01 - Sheriff"]        = "s_f_y_sheriff_01"
        };

        private static readonly List<string> GenderLabels = new List<string> { "Male", "Female" };

        public PartnerMenu(PartnerManager manager)
        {
            _manager  = manager;
            _menuPool = new MenuPool();

            _mainMenu = new UIMenu("~b~Police Partner", "~w~Manage your patrol partner");
            _menuPool.Add(_mainMenu);

            // Gender — use string overload to avoid dynamic
            _genderItem = new UIMenuListItem("Gender", "Select partner gender.", GenderLabels);
            _genderItem.OnListChanged += (sender, index) =>
            {
                var newList = index == 1 ? FemaleModelLabels : MaleModelLabels;
                _modelItem.Collection.Clear();
                foreach (var m in newList)
                    _modelItem.Collection.Add(m);
                _modelItem.Index = 0;
            };
            _mainMenu.AddItem(_genderItem);

            // Model
            _modelItem = new UIMenuListItem("Uniform / Model", "Choose partner appearance.", MaleModelLabels);
            _mainMenu.AddItem(_modelItem);

            // Status (disabled, info only)
            _statusItem = new UIMenuItem("Status", "~r~No partner active");
            _statusItem.Enabled = false;
            _mainMenu.AddItem(_statusItem);

            // Divider
            var divider = new UIMenuItem("──────────────");
            divider.Enabled = false;
            _mainMenu.AddItem(divider);

            // Actions
            _spawnItem   = new UIMenuItem("~g~Spawn Partner",    "Spawn or respawn your partner next to you.");
            _dismissItem = new UIMenuItem("~r~Dismiss Partner",  "Remove your partner from the world.");
            _followItem  = new UIMenuItem("~y~Regroup (Follow)", "Order partner to come to your position.");

            _mainMenu.AddItem(_spawnItem);
            _mainMenu.AddItem(_dismissItem);
            _mainMenu.AddItem(_followItem);

            _mainMenu.OnItemSelect += OnItemSelect;
            _menuPool.RefreshIndex();
        }

        public void Toggle()
        {
            RefreshStatus();
            _mainMenu.Visible = !_mainMenu.Visible;
        }

        public void Close()
        {
            _mainMenu.Visible = false;
        }

        public void Process()
        {
            _menuPool.ProcessMenus();
            if (_mainMenu.Visible)
                RefreshStatus();
        }

        private void OnItemSelect(UIMenu sender, UIMenuItem selectedItem, int index)
        {
            if (selectedItem == _spawnItem)        OnSpawn();
            else if (selectedItem == _dismissItem) OnDismiss();
            else if (selectedItem == _followItem)  OnRegroup();
        }

        private void OnSpawn()
        {
            _manager.SpawnFemale = _genderItem.Index == 1;
            _manager.ChosenModel = ResolveSelectedModel();
            _manager.SpawnPartner();
            _mainMenu.Visible = false;
        }

        private void OnDismiss()
        {
            _manager.DismissPartner(true);
            _mainMenu.Visible = false;
        }

        private void OnRegroup()
        {
            _manager.ForceFollow();
            _mainMenu.Visible = false;
        }

        private string ResolveSelectedModel()
        {
            string label = _modelItem.Collection.Count > _modelItem.Index
                ? _modelItem.Collection[_modelItem.Index].ToString()
                : "";
            return ModelMap.TryGetValue(label, out string model) ? model : "";
        }

        private void RefreshStatus()
        {
            _statusItem.Description = _manager.HasPartner
                ? "~g~Active~w~ - State: ~b~" + _manager.State
                : "~r~No partner active";
        }
    }
}
