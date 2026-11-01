using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Network.Structures;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using Lumina.Excel.Sheets;
using System;
using System.Linq;

namespace Dagobert
{
  internal unsafe sealed class MarketBoardHandler : IDisposable
  {
    private const int UnknownRequestId = -1;
    private const int ListingsPerBatch = 10;

    private readonly Lumina.Excel.ExcelSheet<Item> _items;
    private bool _newRequest;
    private bool _useHq;
    private int _lastRequestId = UnknownRequestId;
    private int _pendingNoMatchRequestId = UnknownRequestId;
    private long _pendingNoMatchTimeoutAt;
    private long _pendingNoOfferingsTimeoutAt;

    private PriceIdentity _expectedIdentity;
    private long _generation;

    public void BeginRequest(PriceIdentity identity, long generation)
    {
      CancelRequest();
      _expectedIdentity = identity;
      _generation = generation;
      _newRequest = true;
      _useHq = Plugin.Configuration.HQ && identity.IsHq;
      _pendingNoOfferingsTimeoutAt = Environment.TickCount64 + GetMarketBoardResultTimeoutMs();
    }

    public void CancelRequest()
    {
      _newRequest = false;
      ClearPendingNoMatch();
      ClearPendingNoOfferings();
    }

    public event EventHandler<NewPriceEventArgs>? NewPriceReceived;

    public MarketBoardHandler()
    {
      _items = Svc.Data.GetExcelSheet<Item>();

      Plugin.MarketBoard.OfferingsReceived += MarketBoardOnOfferingsReceived;
      Svc.Framework.Update += FrameworkOnUpdate;

      Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "ItemSearchResult", ItemSearchResultPostSetup);
    }

    public void Dispose()
    {
      Plugin.MarketBoard.OfferingsReceived -= MarketBoardOnOfferingsReceived;
      Svc.Framework.Update -= FrameworkOnUpdate;
      Plugin.AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, "ItemSearchResult", ItemSearchResultPostSetup);
    }

    private void MarketBoardOnOfferingsReceived(IMarketBoardCurrentOfferings currentOfferings)
    {
      if (!_newRequest)
        return;

      if (currentOfferings.RequestId == _lastRequestId)
        return;

      // Network batches carry item IDs: never attach another item's response to this request.
      // Empty batches have no identity, so let the bounded timeout handle them.
      if (currentOfferings.ItemListings.Count == 0
          || currentOfferings.ItemListings.Any(listing => listing.ItemId != _expectedIdentity.ItemId))
        return;

      ClearPendingNoOfferings();
      var skipNq = _useHq && _items.Single(j => j.RowId == currentOfferings.ItemListings[0].ItemId).CanBeHq;
      var i = 0;
      while (i < currentOfferings.ItemListings.Count)
      {
        if (skipNq && !currentOfferings.ItemListings[i].IsHq)
          i++;
        else
          break;
      }

      // offerings arrive in batches of 10; if no match in this batch, wait for the next
      if (i >= currentOfferings.ItemListings.Count)
      {
        if (currentOfferings.ItemListings.Count < ListingsPerBatch)
        {
          CompletePriceRequest(-1, currentOfferings.RequestId);
          return;
        }

        _pendingNoMatchRequestId = currentOfferings.RequestId;
        _pendingNoMatchTimeoutAt = Environment.TickCount64 + GetMarketBoardResultTimeoutMs();
        return;
      }

      ClearPendingNoMatch();

      int price;

      if (!Plugin.Configuration.UndercutSelf && IsOwnRetainer(currentOfferings.ItemListings[i].RetainerId))
        price = (int)currentOfferings.ItemListings[i].PricePerUnit;
      else if (Plugin.Configuration.UndercutMode == UndercutMode.FixedAmount)
        price = Math.Max((int)currentOfferings.ItemListings[i].PricePerUnit - Plugin.Configuration.UndercutAmount, 1);
      else
        price = Math.Max((100 - Plugin.Configuration.UndercutAmount) * (int)currentOfferings.ItemListings[i].PricePerUnit / 100, 1);

      CompletePriceRequest(price, currentOfferings.RequestId);
    }

    private void FrameworkOnUpdate(object _)
    {
      if (!_newRequest)
        return;

      var now = Environment.TickCount64;
      if (_pendingNoMatchRequestId >= 0 && now >= _pendingNoMatchTimeoutAt)
      {
        Svc.Log.Debug("No matching market board listing received before timeout");
        CompletePriceRequest(-1, _pendingNoMatchRequestId);
        return;
      }

      if (_pendingNoOfferingsTimeoutAt > 0 && now >= _pendingNoOfferingsTimeoutAt)
      {
        Svc.Log.Debug("No market board offerings received before timeout");
        CompletePriceRequest(-1, UnknownRequestId);
      }
    }

    private void ItemSearchResultPostSetup(AddonEvent type, AddonArgs args)
    {
      // Only an explicit BeginRequest arms a lookup; unrelated result windows cannot.
      if (!_newRequest) return;
      _pendingNoOfferingsTimeoutAt = Environment.TickCount64 + GetMarketBoardResultTimeoutMs();
    }

    public void PopulateRetainerCache()
    {
      bool changed = false;
      var retainerManager = RetainerManager.Instance();

      for (uint i = 0; i < retainerManager->GetRetainerCount(); ++i)
      {
        if (!Plugin.Configuration.SeenRetainers.Contains(retainerManager->GetRetainerBySortedIndex(i)->RetainerId))
        {
          Plugin.Configuration.SeenRetainers.Add(retainerManager->GetRetainerBySortedIndex(i)->RetainerId);
          changed = true;
        }
        
      }

      if (changed)
        Plugin.Configuration.Save();
    }

    private static bool IsOwnRetainer(ulong retainerId) => Plugin.Configuration.SeenRetainers.Contains(retainerId);

    private static int GetMarketBoardResultTimeoutMs() => Math.Max(Plugin.Configuration.MarketBoardKeepOpenMS, 1000);

    private void CompletePriceRequest(int price, int requestId)
    {
      ClearPendingNoMatch();
      ClearPendingNoOfferings();
      if (requestId >= 0)
        _lastRequestId = requestId;
      _newRequest = false;
      NewPriceReceived?.Invoke(this, new NewPriceEventArgs(price, _expectedIdentity, _generation));
    }

    private void ClearPendingNoMatch()
    {
      _pendingNoMatchRequestId = UnknownRequestId;
      _pendingNoMatchTimeoutAt = 0;
    }

    private void ClearPendingNoOfferings()
    {
      _pendingNoOfferingsTimeoutAt = 0;
    }
  }
}
