using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace SelectNextLootItem;

public sealed unsafe class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static IAddonLifecycle AddonLifecycle { get; private set; } = null!;
    [PluginService] internal static IPluginLog Logger { get; private set; } = null!;

    public Plugin()
    {
        RegisterAddonListeners();
    }

    public void Dispose()
    {
        UnregisterAddonListeners();
    }

    private static void RegisterAddonListeners()
    {
        AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "NeedGreed", OnNeedGreedSetup);
        AddonLifecycle.RegisterListener(AddonEvent.PostReceiveEvent, "NeedGreed", OnNeedGreedEvent);
        Logger.Debug("Registered AddonLifecycle listeners for NeedGreed.");
    }

    private static void UnregisterAddonListeners()
    {
        AddonLifecycle.UnregisterListener(OnNeedGreedSetup, OnNeedGreedEvent);
        Logger.Debug("Unregistered AddonLifecycle listeners for NeedGreed.");
    }

    private static void OnNeedGreedSetup(AddonEvent type, AddonArgs args)
    {
        // Find first item that hasn't been rolled on, and select it.
        var addonNeedGreed = GetAddon<AddonNeedGreed>(args);
        if (addonNeedGreed == null)
        {
            Logger.Debug("NeedGreed setup received null addon.");
            return;
        }

        for (var index = 0; index < addonNeedGreed->NumItems; index++)
        {
            if (IsUnrolledItem(addonNeedGreed, index))
            {
                Logger.Debug($"NeedGreed setup selecting first unrolled item at index {index}.");
                SelectItem(addonNeedGreed, index);
                break;
            }
        }
    }

    private static void OnNeedGreedEvent(AddonEvent type, AddonArgs args)
    {
        if (args is not AddonReceiveEventArgs eventArgs)
        {
            Logger.Debug("NeedGreed event ignored because args were not AddonReceiveEventArgs.");
            return;
        }

        var eventType = (AtkEventType)eventArgs.AtkEventType;
        var buttonType = (ButtonType)eventArgs.EventParam;
        var addon = GetAddon<AddonNeedGreed>(eventArgs);
        if (addon == null || eventType is not AtkEventType.ButtonClick)
        {
            Logger.Debug($"NeedGreed event ignored. Addon null: {addon == null}, EventType: {eventType}.");
            return;
        }

        var selectedItemIndex = addon->SelectedItemIndex;
        Logger.Debug($"NeedGreed button click detected. Button: {buttonType}, SelectedIndex: {selectedItemIndex}.");

        switch (buttonType)
        {
            // Fall through unconditionally
            case ButtonType.Need:
            case ButtonType.Greed:

            // Don't select next item if we are passing on an item that we already rolled on
            case ButtonType.Pass when IsUnrolledItem(addon, selectedItemIndex):
                Logger.Debug($"Advancing to next item from index {selectedItemIndex} after {buttonType}.");
                SelectNextItem(addon, selectedItemIndex);
                break;
        }
    }

    private static bool IsUnrolledItem(AddonNeedGreed* addon, int index)
        => addon->Items[index] is { Roll: 0, ItemId: not 0 };

    private static void SelectNextItem(AddonNeedGreed* addon, int selectedItemIndex)
    {
        var nextIndex = selectedItemIndex + 1;
        if (nextIndex < addon->NumItems)
        {
            Logger.Debug($"Selecting next item at index {nextIndex}.");
            SelectItem(addon, nextIndex);
        }
        else
        {
            Logger.Debug("No next item to select.");
        }
    }

    private static void SelectItem(AddonNeedGreed* addon, int index)
    {
        Logger.Debug($"Sending ListItemClick for index {index}.");
        var eventData = new AtkEventData();
        eventData.ListItemData.SelectedIndex = index;
        addon->ReceiveEvent(AtkEventType.ListItemClick, 0, null, &eventData);
    }

    internal static T* GetAddon<T>(AddonArgs args) where T : unmanaged
        => (T*)args.Addon.Address;

    // There are other button types such as "Greed Only" and "Loot Recipient"
    private enum ButtonType : uint
    {
        Need = 0,
        Greed = 1,
        Pass = 2,
    }
}
