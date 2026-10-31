using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using ObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Component.GUI;
using EObjName = Lumina.Excel.Sheets.EObjName;

namespace Dagobert;

internal sealed partial class AutoPinch
{
    private readonly PinchSchedule _schedule = new();
    private bool _openingBell;
    private bool _startingScheduledRetainers;
    private bool _manualRunPending;
    private bool _bellReady;
    private bool _scheduledCompleted;
    private bool _ownsSuppression;
    private bool _previousSuppression;
    private HashSet<string>? _yesAlreadyRequests;
    private bool _ownsYesAlreadyLock;
    private string _scheduleStatus = "Disabled";
    private string? _taskFailure;
    private long _nextBellAttempt;
    private long _nextCloseAttempt;
    private long _nextAvailabilityCheck;
    private ulong _scheduledCharacter;
    private uint _scheduledTerritory;

    private void UpdateSchedule(IFramework framework)
    {
        try
        {
            var now = Environment.TickCount64;
            _schedule.Configure(Plugin.Configuration.ScheduledPinchEnabled,
                Plugin.Configuration.ScheduledPinchIntervalMinutes, now, Plugin.Configuration.ScheduledPinchRandomExtraMinutes);
            if (_taskFailure != null)
            {
                var failure = _taskFailure;
                _taskFailure = null;
                StopAutomation(failure);
            }
            if (_schedule.Running)
            {
                if (!_schedule.Enabled || !PlayerAvailable() || ECommons.GameHelpers.Player.IsMoving
                    || Svc.PlayerState.ContentId != _scheduledCharacter
                    || Svc.ClientState.TerritoryType != _scheduledTerritory)
                {
                    StopAutomation("Scheduled run interrupted; resume when ready");
                    return;
                }
                if (_taskManager.IsBusy) return;
                if (_openingBell)
                {
                    if (!_bellReady || !RetainerListReady()) { StopAutomation("Summoning bell did not open or its list was closed"); return; }
                    _openingBell = false;
                    _scheduleStatus = "Repricing enabled retainers";
                    _startingScheduledRetainers = true;
                    try { PinchAllRetainers(); }
                    finally { _startingScheduledRetainers = false; }
                    Queue(CloseScheduledRetainerList, "CloseScheduledRetainerList");
                    Queue(() => _scheduledCompleted = true, "ScheduledRunComplete");
                    return;
                }
                if (!_scheduledCompleted) { StopAutomation("Auto Pinch did not finish; check the plugin log"); return; }
                ReleaseAutomationLocks();
                _schedule.Complete(now);
                _scheduleStatus = "Last run completed";
                return;
            }
            if (_manualRunPending && !_taskManager.IsBusy)
            {
                StopAutomation("Auto Pinch timed out or was interrupted; see plugin log");
                return;
            }
            if (!_schedule.IsDue(now) || _taskManager.IsBusy) return;
            if (now < _nextAvailabilityCheck) return;
            _nextAvailabilityCheck = now + 1000;
            if (!PlayerAvailable() || ECommons.GameHelpers.Player.IsMoving) { _scheduleStatus = "Waiting: character is busy or unavailable"; return; }
            // Do not take over a bell session another plugin or the player is already using.
            if (GenericHelpers.IsOccupied()) { _scheduleStatus = "Waiting: finish the current interaction"; return; }
            if (AutoRetainerIPC.Installed)
            {
                if (AutoRetainerIPC.Instance == null) AutoRetainerIPC.Initialize();
                if (AutoRetainerIPC.Suppressed()) { _scheduleStatus = "Waiting: AutoRetainer is suppressed by another operation"; return; }
            }
            if (Plugin.Configuration.EnabledRetainerNames.Contains(Configuration.ALL_DISABLED_SENTINEL))
            {
                _schedule.Pause("No retainers are enabled");
                return;
            }
            if (FindReachableBell() == null) { _scheduleStatus = "Waiting: stand within reach of a summoning bell"; return; }
            AcquireAutomationLocks();
            ClearState();
            _scheduledCharacter = Svc.PlayerState.ContentId;
            _scheduledTerritory = Svc.ClientState.TerritoryType;
            _openingBell = true;
            _bellReady = false;
            _scheduledCompleted = false;
            _schedule.Start();
            _scheduleStatus = "Opening summoning bell";
            _nextBellAttempt = 0;
            _nextCloseAttempt = 0;
            Queue(OpenScheduledBell, "OpenScheduledBell");
            Queue(WaitForRetainerList, "WaitForRetainerList");
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex, "Scheduled Auto Pinch failed");
            StopAutomation("Scheduled Auto Pinch failed; see plugin log");
        }
    }

    private static bool PlayerAvailable() => Svc.Objects.LocalPlayer is { CurrentHp: > 0 } player
        && !player.IsCasting && !Svc.ClientState.IsPvP
        && !Svc.Condition[ConditionFlag.InCombat] && !Svc.Condition[ConditionFlag.BetweenAreas]
        && !Svc.Condition[ConditionFlag.BetweenAreas51] && !Svc.Condition[ConditionFlag.WatchingCutscene]
        && !Svc.Condition[ConditionFlag.WatchingCutscene78]
        && !Svc.Condition[ConditionFlag.Mounted] && !Svc.Condition[ConditionFlag.Crafting];

    private static Dalamud.Game.ClientState.Objects.Types.IGameObject? FindReachableBell()
    {
        var player = Svc.Objects.LocalPlayer;
        if (player == null) return null;
        var name = Svc.Data.GetExcelSheet<EObjName>().GetRow(2000401).Singular.GetText();
        return Svc.Objects.Where(obj => obj.IsTargetable
            && (obj.ObjectKind == ObjectKind.EventObj || obj.ObjectKind == ObjectKind.HousingEventObject)
            && (obj.Name.TextValue == name || obj.Name.TextValue == "リテイナーベル")
            && Vector3.Distance(player.Position, obj.Position) < (obj.ObjectKind == ObjectKind.HousingEventObject ? 6.5f : 4.6f))
            .OrderBy(obj => Vector3.DistanceSquared(player.Position, obj.Position)).FirstOrDefault();
    }

    private unsafe bool? OpenScheduledBell()
    {
        if (RetainerListReady()) return true;
        if (Environment.TickCount64 < _nextBellAttempt || GenericHelpers.IsOccupied()) return false;
        var bell = FindReachableBell();
        if (bell == null) return false;
        Svc.Targets.Target = bell;
        TargetSystem.Instance()->InteractWithObject((GameObject*)bell.Address, false);
        _nextBellAttempt = Environment.TickCount64 + 5000;
        return true;
    }

    private static unsafe bool RetainerListReady() =>
        GenericHelpers.TryGetAddonByName<AtkUnitBase>("RetainerList", out var addon) && GenericHelpers.IsAddonReady(addon);

    private bool? WaitForRetainerList()
    {
        if (!RetainerListReady()) return false;
        _bellReady = true;
        return true;
    }

    private unsafe bool? CloseScheduledRetainerList()
    {
        if (!GenericHelpers.TryGetAddonByName<AtkUnitBase>("RetainerList", out var addon) || !addon->IsVisible)
            return !Svc.Condition[ConditionFlag.OccupiedSummoningBell];
        if (!GenericHelpers.IsAddonReady(addon)) return false;
        if (Environment.TickCount64 < _nextCloseAttempt) return false;
        // Send Quit rather than just hiding the addon, so the bell interaction ends.
        var value = new AtkValue { Type = AtkValueType.Int, Int = -1 };
        addon->FireCallback(1, &value);
        _nextCloseAttempt = Environment.TickCount64 + 500;
        _scheduleStatus = "Finishing scheduled run";
        return false;
    }

    private void AcquireAutomationLocks()
    {
        if (!_ownsSuppression && AutoRetainerIPC.Installed)
        {
            if (AutoRetainerIPC.Instance == null) AutoRetainerIPC.Initialize();
            _previousSuppression = AutoRetainerIPC.Suppressed();
            AutoRetainerIPC.Suppressed(true);
            _ownsSuppression = true;
        }
        if (!_ownsYesAlreadyLock && Svc.PluginInterface.TryGetData<HashSet<string>>("YesAlready.StopRequests", out var requests))
        {
            _yesAlreadyRequests = requests;
            _ownsYesAlreadyLock = requests.Add(Svc.PluginInterface.InternalName);
        }
    }

    private void ReleaseAutomationLocks()
    {
        if (_ownsYesAlreadyLock) _yesAlreadyRequests?.Remove(Svc.PluginInterface.InternalName);
        _ownsYesAlreadyLock = false;
        if (_ownsSuppression)
        {
            _ownsSuppression = false;
            try
            {
                if (AutoRetainerIPC.Installed) AutoRetainerIPC.Suppressed(_previousSuppression);
            }
            catch (Exception ex) { Svc.Log.Error(ex, "Could not restore AutoRetainer suppression"); }
        }
    }

    private void StopAutomation(string reason)
    {
        var notify = !_disposed && (_schedule.Running || _manualRunPending);
        _taskManager.Abort();
        CancelUniversalisPriceRequest();
        RemoveTalkAddonListeners();
        ReleaseAutomationLocks();
        _openingBell = false;
        _manualRunPending = false;
        _schedule.Pause(reason);
        _scheduleStatus = reason;
        Svc.Log.Information("Auto Pinch stopped: {Reason}", reason);
        if (notify && Plugin.Configuration.ShowErrorsInChat)
            Svc.Chat.Print($"[Dagobert] Auto Pinch paused: {reason}");
    }

    private void CompleteManualRun()
    {
        _manualRunPending = false;
        RemoveTalkAddonListeners();
        ReleaseAutomationLocks();
        _schedule.Complete(Environment.TickCount64);
    }

    private Func<bool?> Guard(Func<bool?> action) => () =>
    {
        try { return action(); }
        catch (Exception ex)
        {
            _taskFailure = ex.Message;
            Svc.Log.Error(ex, "Auto Pinch task failed");
            return null;
        }
    };
    private void Queue(Func<bool?> action, string? name = null) => _taskManager.Enqueue(Guard(action), name);
    private void Queue(Action action, string? name = null) => Queue(() => { action(); return true; }, name);
    private void QueueInsert(Func<bool?> action, string? name = null) => _taskManager.Insert(Guard(action), name);
    private void QueueInsert(Action action, string? name = null) => QueueInsert(() => { action(); return true; }, name);

    internal void DrawScheduleSettings()
    {
        var enabled = Plugin.Configuration.ScheduledPinchEnabled;
        if (ImGui.Checkbox("Enable scheduled Auto Pinch", ref enabled))
        {
            Plugin.Configuration.ScheduledPinchEnabled = enabled;
            Plugin.Configuration.Save();
        }
        var minutes = Plugin.Configuration.ScheduledPinchIntervalMinutes;
        if (ImGui.SliderInt("Frequency (minutes)", ref minutes, 1, 10080, "%d", ImGuiSliderFlags.Logarithmic))
        {
            Plugin.Configuration.ScheduledPinchIntervalMinutes = Math.Clamp(minutes, 1, 10080);
            Plugin.Configuration.Save();
        }
        var randomExtra = Plugin.Configuration.ScheduledPinchRandomExtraMinutes;
        if (ImGui.SliderInt("Random extra delay (minutes)", ref randomExtra, 0, 1440, "%d", ImGuiSliderFlags.Logarithmic))
        {
            Plugin.Configuration.ScheduledPinchRandomExtraMinutes = Math.Clamp(randomExtra, 0, 1440);
            Plugin.Configuration.Save();
        }
        ImGui.TextUnformatted("Ctrl+click a slider to type an exact value. 60 minutes = 1 hour.");
        ImGui.TextWrapped($"After each completed run: wait {Plugin.Configuration.ScheduledPinchIntervalMinutes} to {Plugin.Configuration.ScheduledPinchIntervalMinutes + Plugin.Configuration.ScheduledPinchRandomExtraMinutes} minutes. A fresh random extra delay is chosen once per cycle; 0 disables randomness. Run now starts immediately when available.");
        ImGui.TextWrapped("Park within reach of a summoning bell with menus closed. No walking or teleporting is performed. Busy characters wait; cancellation, interruptions, and errors pause scheduling.");
        var status = !_schedule.Enabled ? "Disabled" : _schedule.PauseReason
            ?? (_schedule.Running || _schedule.IsDue(Environment.TickCount64) ? _scheduleStatus : "Waiting for next scheduled run");
        ImGui.TextWrapped($"Status: {status}");
        if (_schedule.Enabled && !_schedule.Running && _schedule.PauseReason == null)
        {
            var remaining = TimeSpan.FromMilliseconds(Math.Max(0, _schedule.NextDue - Environment.TickCount64));
            ImGui.TextUnformatted($"Next run: {remaining.Days}d {remaining:hh\\:mm\\:ss}");
        }
        if (_schedule.Running || _taskManager.IsBusy)
        {
            if (ImGui.Button("Cancel and pause")) StopAutomation("Cancelled by user");
        }
        else if (_schedule.Enabled)
        {
            if (ImGui.Button("Run now")) _schedule.RunNow(Environment.TickCount64);
            ImGui.SameLine();
            if (_schedule.PauseReason == null)
            {
                if (ImGui.Button("Pause")) _schedule.Pause("Paused by user");
            }
            else if (ImGui.Button("Resume")) _schedule.Resume(Environment.TickCount64);
        }
    }
}
