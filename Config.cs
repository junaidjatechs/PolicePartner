using System;
using System.IO;
using System.Windows.Forms;
using Rage;

namespace PolicePartner
{
    public static class Config
    {
        public static Keys MenuKey { get; private set; } = Keys.F6;
        public static Keys FollowKey { get; private set; } = Keys.Y;
        public static float SpawnDistance { get; private set; } = 3.0f;
        public static bool AutoEquipWeapon { get; private set; } = true;
        public static string DefaultWeapon { get; private set; } = "WEAPON_PISTOL";

        private static readonly string IniPath = @"Plugins\PolicePartner\PolicePartner.ini";

        public static void Load()
        {
            try
            {
                if (!File.Exists(IniPath))
                {
                    WriteDefaults();
                    return;
                }

                var ini = new InitializationFile(IniPath);

                MenuKey = ParseKey(ini.ReadString("Keys", "MenuKey", "F6"), Keys.F6);
                FollowKey = ParseKey(ini.ReadString("Keys", "FollowKey", "Y"), Keys.Y);
                SpawnDistance = ini.ReadSingle("Settings", "SpawnDistance", 3.0f);
                AutoEquipWeapon = ini.ReadBoolean("Settings", "AutoEquipWeapon", true);
                DefaultWeapon = ini.ReadString("Settings", "DefaultWeapon", "WEAPON_PISTOL");

                Game.LogTrivial("[PolicePartner] Config loaded.");
            }
            catch (Exception ex)
            {
                Game.LogTrivial($"[PolicePartner] Config load error: {ex.Message}. Using defaults.");
            }
        }

        private static void WriteDefaults()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(IniPath)!);
            File.WriteAllText(IniPath,
@"[Keys]
; Key to open the Police Partner menu
MenuKey=F6
; Key to force partner to follow/regroup with player
FollowKey=Y

[Settings]
; Distance (metres) in front of player where partner spawns
SpawnDistance=3.0
; Give partner a weapon on spawn
AutoEquipWeapon=true
; Weapon hash to give (use WEAPON_* names)
DefaultWeapon=WEAPON_PISTOL
");
            Game.LogTrivial("[PolicePartner] Default config written.");
        }

        private static Keys ParseKey(string value, Keys fallback)
        {
            return Enum.TryParse(value, true, out Keys key) ? key : fallback;
        }
    }
}
