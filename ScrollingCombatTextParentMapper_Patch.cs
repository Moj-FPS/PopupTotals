using System;
using HarmonyLib;
using ProjectM;
using ProjectM.UI;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;

namespace PopupTotals;

[HarmonyPatch]
public class ScrollingCombatTextParentMapperPatch
{
    private static Action<SCTText, ScrollingCombatTextParentMapper.EntryData> _handler;

    private static bool _hooked;
    private static PrefabGUID _sctTypeResourceGain = new(1876501183); // hardcoded for SCT_Type_ResouceGain
    private static World _world;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ScrollingCombatTextParentMapper), nameof(ScrollingCombatTextParentMapper.OnUpdate))]
    private static void OnUpdate_Prefix(ref ScrollingCombatTextParentMapper __instance)
    {
        if (_hooked) return;
        var pcs = __instance.World.GetExistingSystemManaged<PrefabCollectionSystem>();
        if (pcs == null || __instance._Elements == null)
        {
            _hooked = false;
            return;
        }

        _world = __instance.World;
        _handler = OnEntryUpdate;
        __instance._Elements.OnEntryUpdate += _handler;
        _hooked = true;
    }
    
    private static void OnEntryUpdate(SCTText text, ScrollingCombatTextParentMapper.EntryData entry)
    {
        // require ResourceGain SCTs
        if (entry.Type.GuidHash != _sctTypeResourceGain.GuidHash) return;
        var str = entry.SourceTypeText.ToString();
        if (str.Contains('(') && str.Contains(')')) return;
        var prefab = ItemUtils.GetOrRebuild(str);
        if (!ConsoleShared.TryGetLocalCharacterInCurrentWorld(out var character, _world)) return;
        var total = InventoryUtilities.GetItemAmount(_world.EntityManager, character, prefab);
        var newStr = $"{str} ({total})";
        entry.SourceTypeText = new FixedString128Bytes(newStr);
        text.Text.m_text = text.Text.m_text.Replace(str, newStr);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ScrollingCombatTextParentMapper), nameof(ScrollingCombatTextParentMapper.OnDestroy))]
    private static void OnDestroy_Postfix(ref ScrollingCombatTextParentMapper __instance)
    {
        if (_handler == null) return;
        __instance._Elements.OnEntryUpdate -= _handler;
        _hooked = false;
    }
}