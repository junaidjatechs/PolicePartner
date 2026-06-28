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
        // ── Public state ────────────────────────────────────────────────────
        public Ped Partner { get; private set; }
        public PartnerState State { get; private set; } = PartnerState.None;
        public bool HasPartner => Partner != null && Partner.Exists() && Partner.IsAlive;

        // ── Spawn options (set by menu before spawning) ──────────────────────
        public bool SpawnFemale { get; set; } = false;
        public string ChosenModel { get; set; } = ""; // empty = auto-pick

        // ── Private tracking ────────────────────────────────────────────────
        private Vehicle _stopVehicle;       // vehicle involved in current traffic stop
        private Ped _watchedSuspect;        // ped being watched / detained
        private int _stateTimer;            // generic countdown (ms)
        private const int FollowDistance = 2;  // metres behind player

        // Passenger-side models available in LSPDFR
        private static readonly string[] MaleModels =
        {
            "s_m_y_cop_01", "s_m_y_hwaycop_01", "s_m_y_sheriff_01",
            "s_m_y_ranger_01", "s_m_y_swat_01"
        };
        private static readonly string[] FemaleModels =
        {
            "s_f_y_cop_01", "s_f_y_sheriff_01"
        };

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>Spawn or respawn the partner near the player.</summary>
        public bool SpawnPartner()
        {
            DismissPartner(false);

            string modelName = ResolveModel();
            var model = new Model(modelName);

            if (!model.IsValid)
            {
                Game.LogTrivial($"[PolicePartner] Model {modelName} not valid.");
                Game.DisplayNotification($"~r~[Police Partner]~w~ Model not found: {modelName}");
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

            // Basic cop setup
            Partner.IsPersistent = true;
            Partner.BlockPermanentEvents = true;
            Partner.Tasks.Clear();

            // Give weapon
            if (Config.AutoEquipWeapon)
            {
                var weapon = (WeaponHash)Game.GetHashKey(Config.DefaultWeapon);
                Partner.GiveNewWeapon(weapon, 120, true);
            }

            // Register with LSPDFR so it counts as a cop
            Functions.SetCopAsBuddyCop(Partner);

            SetState(PartnerState.Following);
            Game.DisplayNotification("~b~[Police Partner]~w~ Partner is on duty. Press ~y~" + Config.FollowKey + "~w~ to regroup.");
            Game.LogTrivial($"[PolicePartner] Partner spawned: {modelName}");
            return true;
        }

        /// <summary>Dismiss partner cleanly.</summary>
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

        /// <summary>Force partner to run back to player (Y key).</summary>
        public void ForceFollow()
        {
            if (!HasPartner) return;
            SetState(PartnerState.Following);
            Partner.Tasks.FollowNavigationMeshToPosition(
                Game.LocalPlayer.Character.Position,
                Game.LocalPlayer.Character.Heading,
                3.0f);
        }

        /// <summary>Called every tick from main loop.</summary>
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

        // ── State processors ─────────────────────────────────────────────────

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
                // Stand beside player, face same direction
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

            // Passenger side offset: right of vehicle
            Vector3 targetPos = _stopVehicle.GetOffsetPosition(new Vector3(2.5f, 0f, 0f));

            float dist = Vector3.Distance(Partner.Position, targetPos);
            if (dist > 1.5f)
            {
                Partner.Tasks.FollowNavigationMeshToPosition(targetPos, _stopVehicle.Heading, 2.0f, 0.3f);
            }
            else
            {
                // Face the vehicle
                Partner.Tasks.AchieveHeading(_stopVehicle.Heading + 90f);

                // Auto-transition to watching if there's a suspect
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

            // Face suspect and keep eyes on them
            NativeFunction.Natives.TASK_TURN_PED_TO_FACE_ENTITY(Partner, _watchedSuspect, 1000);

            // If suspect becomes hostile or runs — draw weapon
            if (_watchedSuspect.IsInCombat || _watchedSuspect.IsFleeing)
            {
                SetState(PartnerState.CoveringThreat);
            }
        }

        private void ProcessCoverThreat()
        {
            if (_watchedSuspect == null || !_watchedSuspect.Exists() ||
                (!_watchedSuspect.IsInCombat && !_watchedSuspect.IsFleeing))
            {
                // Threat resolved — holster and go back to watching
                Partner.Tasks.Clear();
                SetState(_watchedSuspect != null ? PartnerState.WatchingSuspect : PartnerState.Following);
                return;
            }

            // Draw weapon and aim at suspect
            Partner.Tasks.AimWeaponAt(_watchedSuspect, -1);
            NativeFunction.Natives.SET_PED_COMBAT_ATTRIBUTES(Partner, 46, true); // use cover
            NativeFunction.Natives.SET_PED_COMBAT_ATTRIBUTES(Partner, 5, true);  // can fight armed
        }

        // ── Traffic stop detection ───────────────────────────────────────────

        private void CheckForTrafficStop()
        {
            // LSPDFR API: check if player is in a traffic stop
            bool inStop = Functions.IsPlayerPerformingPullover();

            if (inStop && State == PartnerState.Following)
            {
                LHandle pullover = Functions.GetCurrentPullover();
                if (pullover != null)
                {
                    _stopVehicle = Functions.GetPulloverSuspect(pullover)?.CurrentVehicle;
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
                // Stop ended — regroup
                _stopVehicle = null;
                _watchedSuspect = null;
                Partner.Tasks.Clear();
                SetState(PartnerState.Following);
                Game.DisplaySubtitle("~b~Partner~w~ regrouping.", 2000);
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private string ResolveModel()
        {
            if (!string.IsNullOrWhiteSpace(ChosenModel))
                return ChosenModel;

            var pool = SpawnFemale ? FemaleModels : MaleModels;
            // Match player's department by checking their model name
            string playerModel = NativeFunction.Natives.GET_ENTITY_MODEL<int>(Game.LocalPlayer.Character).ToString();
            // Default: first in pool
            return pool[0];
        }

        private Vector3 GetSpawnPosition()
        {
            Ped player = Game.LocalPlayer.Character;
            return player.Position + player.ForwardVector * Config.SpawnDistance;
        }

        private Ped FindNearestPedToVehicle(Vehicle v)
        {
            return World.GetAllPeds()
                .Where(p => p.Exists() && p != Partner && p != Game.LocalPlayer.Character
                            && !p.IsPolice
                            && Vector3.Distance(p.Position, v.Position) < 10f)
                .OrderBy(p => Vector3.Distance(p.Position, v.Position))
                .FirstOrDefault();
        }

        private void SetState(PartnerState newState)
        {
            State = newState;
            Game.LogTrivial($"[PolicePartner] State → {newState}");
        }
    }
}
