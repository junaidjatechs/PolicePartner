using System;
using System.Windows.Forms;
using Rage;
using Rage.Attributes;

[assembly: Plugin("Police Partner", Description = "Spawn and control a Police Partner during LSPDFR patrols.", Author = "Junaid", PrefersSingleInstance = true)]

namespace PolicePartner
{
    public class EntryPoint
    {
        private static PartnerManager _partnerManager;
        private static PartnerMenu _partnerMenu;

        public static void Main()
        {
            Game.LogTrivial("[PolicePartner] Plugin loaded.");
            Config.Load();

            _partnerManager = new PartnerManager();
            _partnerMenu = new PartnerMenu(_partnerManager);

            GameFiber.StartNew(MainLoop);

            // Keep plugin alive
            while (true)
            {
                GameFiber.Yield();
            }
        }

        public static void OnUnload(bool isTerminating)
        {
            Game.LogTrivial("[PolicePartner] Plugin unloading...");
            _partnerManager?.DismissPartner(true);
            _partnerMenu?.Close();
        }

        private static void MainLoop()
        {
            while (true)
            {
                try
                {
                    // Open menu on configured key
                    if (Game.IsKeyDown(Config.MenuKey))
                    {
                        _partnerMenu.Toggle();
                    }

                    // Follow key — partner catches up to player
                    if (Game.IsKeyDown(Config.FollowKey))
                    {
                        _partnerManager?.ForceFollow();
                    }

                    _partnerManager?.Process();
                    _partnerMenu?.Process();
                }
                catch (Exception ex)
                {
                    Game.LogTrivial($"[PolicePartner] Error in main loop: {ex}");
                }

                GameFiber.Yield();
            }
        }
    }
}
