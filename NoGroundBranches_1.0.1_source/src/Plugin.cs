using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace NoGroundBranches
{
    [BepInPlugin(GUID, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string GUID = "choico007.NoGroundBranches";
        public const string Name = "NoGroundBranches";
        public const string Version = "1.0.0";

        internal static ConfigEntry<bool> ModEnabled;
        internal static ConfigEntry<string> PrefabNames;
        internal static ConfigEntry<bool> RemoveExisting;
        internal static ConfigEntry<bool> LogPrefabNames;
        internal static BepInEx.Logging.ManualLogSource Log;

        void Awake()
        {
            Log = Logger;
            ModEnabled = Config.Bind("General", "Enabled", true, "모드 활성화 여부");
            PrefabNames = Config.Bind("General", "PrefabNames", "Pickable_Branch",
                "생성을 막을 프리팹 이름 (쉼표로 구분). 예: Pickable_Branch,Branch");
            RemoveExisting = Config.Bind("General", "RemoveExisting", true,
                "이미 월드에 생성되어 있는 가지도 제거할지 여부");
            LogPrefabNames = Config.Bind("Debug", "LogPrefabNames", false,
                "true면 월드 생성 목록의 모든 Pickable 프리팹 이름을 로그에 출력 (이름 확인용)");

            new Harmony(GUID).PatchAll();
        }

        internal static bool IsTarget(string rawName)
        {
            if (string.IsNullOrEmpty(rawName)) return false;
            string n = rawName.Replace("(Clone)", "").Trim();
            return PrefabNames.Value
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Any(s => string.Equals(s, n, StringComparison.OrdinalIgnoreCase));
        }
    }

    // 새 청크(zone)가 생성될 때 가지가 스폰되지 않도록 vegetation 목록에서 비활성화
    [HarmonyPatch(typeof(ZoneSystem), "Start")]
    static class ZoneSystem_Start_Patch
    {
        static void Postfix(ZoneSystem __instance)
        {
            if (!Plugin.ModEnabled.Value) return;
            foreach (var veg in __instance.m_vegetation)
            {
                if (veg.m_prefab == null) continue;
                if (Plugin.LogPrefabNames.Value)
                    Plugin.Log.LogInfo("Vegetation prefab: " + veg.m_prefab.name);
                if (Plugin.IsTarget(veg.m_prefab.name))
                {
                    veg.m_enable = false;
                    Plugin.Log.LogInfo("Disabled spawn: " + veg.m_prefab.name);
                }
            }
        }
    }

    // 이미 생성되어 있던 가지 제거
    [HarmonyPatch(typeof(Pickable), "Start")]
    static class Pickable_Start_Patch
    {
        static void Postfix(Pickable __instance)
        {
            if (!Plugin.ModEnabled.Value || !Plugin.RemoveExisting.Value) return;
            if (!Plugin.IsTarget(__instance.gameObject.name)) return;

            var nv = __instance.GetComponent<ZNetView>();
            if (nv != null && nv.IsValid())
            {
                nv.ClaimOwnership();
                nv.Destroy();
            }
        }
    }
}
