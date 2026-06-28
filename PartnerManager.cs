using System;
using System.Linq;
using Rage;
using Rage.Native;
using LSPD_First_Response.Mod.API;

namespace PolicePartner
{
    public enum PartnerState
    {
        None,
        Following,
        TrafficStop_PassengerSide,
        WatchingSuspect,
        CoveringThreat,
        Dismissed
    }

    public class PartnerManager
    {
        public Ped Partner { get; private set; }
        public PartnerState State { get; private set; } = PartnerState.None;
        public bool HasPartner => Partner != null && Partner.Exists() && Partner.IsAlive;

        public bool SpawnFemale { get; set; } = false;
        public string ChosenModel { get; set; } = "";

        private Vehicle _stopVehicle;
        private Ped _watchedSuspect;
        private const int FollowDistance = 2;

        private static readonly string[] MaleModels =
        {
            "s_m_y_cop_01", "s_m_y_hwaycop_01", "s_m_y_sheriff_01",
            "s_m_y_ranger_01", "s_m_y_swat_01"
        };
        private static readonly string[] FemaleModels =
        {
            "s_f_y_cop_01", "s_f_y_sheriff_01"
        };

        public bool SpawnPartner()
        {
            DismissPartner(false);

            string modelName = ResolveModel();
            var model = new Model(modelName);

            if (!model.IsValid)
            {
                Game.LogTrivial("[PolicePartner] Model " + modelName + " not valid.");
                Game.DisplayNotification("~r~[Police Partner]~w~ Model not found: " + modelName);
                return false;
            }

            model.LoadAndWait();

            Vector3 spawnPos = GetSpawnPosition();
            Partner = new Ped(model, spawnPos, Game.LocalPlayer.Character.Heading);

            if (!Partner.Exists())
            {
                Game.LogTrivial("[PolicePartner] Failed to create Ped.");
                return false;
            }

            Partner.IsPersistent = true;
            Partner.BlockPermanentEvents = true;
            Partner.Tasks.Clear();

            // Give weapon using correct RPH API
            if (Config.AutoEquipWeapon)
            {
                WeaponHash weaponHash = (WeaponHash)Game.GetHashKey(Config.DefaultWeapon);
                Partner.Inventory.GiveNewWeapon(weaponHash, 120, true);
            }

            SetState(PartnerState.Following);
            Game.DisplayNotification("~b~[Police Partner]~w~ Partner is on duty. Press ~y~" + Config.FollowKey + "~w~ to regroup.");
            Game.LogTrivial("[PolicePartner] Partner spawned: " + modelName);
            return true;
        }

        public void DismissPartner(bool permanent)
        {
            if (!HasPartner) return;

            Partner.Tasks.Clear();
            Partner.Dismiss();
            Partner.Delete();
            Partner = null;
            _stopVehicle = null;
            _watchedSuspect = null;
            SetState(PartnerState.None);

            if (permanent)
                Game.DisplayNotification("~b~[Police Partner]~w~ Partner dismissed.");
        }

        public void ForceFollow()
        {
            if (!HasPartner) return;
            SetState(PartnerState.Following);
            Partner.Tasks.FollowNavigationMeshToPosition(
                Game.LocalPlayer.Character.Position,
                Game.LocalPlayer.Character.Heading,
                3.0f);
        }

        public void Process()
        {
            if (!HasPartner) return;

            switch (State)
            {
                case PartnerState.Following:
                    ProcessFollowing();
                    break;
                case PartnerState.TrafficStop_PassengerSide:
                    ProcessPassengerSide();
                    break;
                case PartnerState.WatchingSuspect:
                    ProcessWatchSuspect();
                    break;
                case PartnerState.CoveringThreat:
                    ProcessCoverThreat();
                    break;
            }

            CheckForTrafficStop();
        }

        private void ProcessFollowing()
        {
            Ped player = Game.LocalPlayer.Character;
            float dist = Vector3.Distance(Partner.Position, player.Position);

            if (dist > FollowDistance + 1f)
            {
                Partner.Tasks.FollowNavigationMeshToPosition(
                    player.Position,
                    player.Heading,
                    2.5f,
                    0.5f);
            }
            else
            {
                Partner.Tasks.AchieveHeading(player.Heading);
            }
        }

        private void ProcessPassengerSide()
        {
            if (_stopVehicle == null || !_stopVehicle.Exists())
            {
                SetState(PartnerState.Following);
                return;
            }

            Vector3 targetPos = _stopVehicle.GetOffsetPosition(new Vector3(2.5f, 0f, 0f));
            float dist = Vector3.Distance(Partner.Position, targetPos);

            if (dist > 1.5f)
            {
                Partner.Tasks.FollowNavigationMeshToPosition(targetPos, _stopVehicle.Heading, 2.0f, 0.3f);
            }
            else
            {
                Partner.Tasks.AchieveHeading(_stopVehicle.Heading + 90f);

                _watchedSuspect = FindNearestPedToVehicle(_stopVehicle);
                if (_watchedSuspect != null)
                    SetState(PartnerState.WatchingSuspect);
            }
        }

        private void ProcessWatchSuspect()
        {
            if (_watchedSuspect == null || !_watchedSuspect.Exists())
            {
                SetState(_stopVehicle != null ? PartnerState.TrafficStop_PassengerSide : PartnerState.Following);
                return;
            }

            NativeFunction.Natives.TASK_TURN_PED_TO_FACE_ENTITY(Partner, _watchedSuspect, 1000);

            if (_watchedSuspect.IsInCombat || _watchedSuspect.IsFleeing)
                SetState(PartnerState.CoveringThreat);
        }

        private void ProcessCoverThreat()
        {
            if (_watchedSuspect == null || !_watchedSuspect.Exists() ||
                (!_watchedSuspect.IsInCombat && !_watchedSuspect.IsFleeing))
            {
                Partner.Tasks.Clear();
                SetState(_watchedSuspect != null ? PartnerState.WatchingSuspect : PartnerState.Following);
                return;
            }

            Partner.Tasks.AimWeaponAt(_watchedSuspect, -1);
            NativeFunction.Natives.SET_PED_COMBAT_ATTRIBUTES(Partner, 46, true);
            NativeFunction.Natives.SET_PED_COMBAT_ATTRIBUTES(Partner, 5, true);
        }

        private void CheckForTrafficStop()
        {
            bool inStop = Functions.IsPlayerPerformingPullover();

            if (inStop && State == PartnerState.Following)
            {
                LHandle pullover = Functions.GetCurrentPullover();
                if (pullover != null)
                {
                    Ped suspect = Functions.GetPulloverSuspect(pullover);
                    _stopVehicle = suspect != null && suspect.Exists() ? suspect.CurrentVehicle : null;
                    if (_stopVehicle != null && _stopVehicle.Exists())
                    {
                        SetState(PartnerState.TrafficStop_PassengerSide);
                        Game.DisplaySubtitle("~b~Partner~w~ moving to passenger side.", 3000);
                    }
                }
            }
            else if (!inStop && (State == PartnerState.TrafficStop_PassengerSide ||
                                  State == PartnerState.WatchingSuspect ||
                                  State == PartnerState.CoveringThreat))
            {
                _stopVehicle = null;
                _watchedSuspect = null;
                Partner.Tasks.Clear();
                SetState(PartnerState.Following);
                Game.DisplaySubtitle("~b~Partner~w~ regrouping.", 2000);
            }
        }

        private string ResolveModel()
        {
            if (!string.IsNullOrWhiteSpace(ChosenModel))
                return ChosenModel;
            return SpawnFemale ? FemaleModels[0] : MaleModels[0];
        }

        private Vector3 GetSpawnPosition()
        {
            Ped player = Game.LocalPlayer.Character;
            return player.Position + player.ForwardVector * Config.SpawnDistance;
        }

        private Ped FindNearestPedToVehicle(Vehicle v)
        {
            // Use RelationshipGroup to exclude cops instead of IsPolice
            RelationshipGroup copGroup = new RelationshipGroup("COP");
            return World.GetAllPeds()
                .Where(p => p.Exists()
                            && p != Partner
                            && p != Game.LocalPlayer.Character
                            && p.RelationshipGroup != copGroup
                            && Vector3.Distance(p.Position, v.Position) < 10f)
                .OrderBy(p => Vector3.Distance(p.Position, v.Position))
                .FirstOrDefault();
        }

        private void SetState(PartnerState newState)
        {
            State = newState;
            Game.LogTrivial("[PolicePartner] State -> " + newState);
        }
    }
}
