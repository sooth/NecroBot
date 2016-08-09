#region using directives

using PoGo.NecroBot.Logic.Common;
using PoGo.NecroBot.Logic.Event;
using PoGo.NecroBot.Logic.Logging;
using PoGo.NecroBot.Logic.State;
using PoGo.NecroBot.Logic.Utils;
using POGOProtos.Inventory.Item;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System;

#endregion

namespace PoGo.NecroBot.Logic.Tasks
{
    public class RecycleItemsTask
    {
        private static int _diff;
        private static Random rnd = new Random();

        public static async Task Execute(ISession session, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var items = await session.Inventory.GetItems();
            
            var currentTotalItems = await session.Inventory.GetTotalItemCount();
            if ((session.Profile.PlayerData.MaxItemStorage * session.LogicSettings.RecycleInventoryAtUsagePercentage/100.0f) > currentTotalItems)
                return;

            if (session.LogicSettings.TotalAmountOfPokeballsToKeep != 0)
                await OptimizedRecycleBalls(session, cancellationToken, items);

            if (!session.LogicSettings.VerboseRecycling)
                Logger.Write(session.Translation.GetTranslation(TranslationString.RecyclingQuietly), LogLevel.Recycling);

            if (session.LogicSettings.TotalAmountOfPotionsToKeep>=0)
                await OptimizedRecyclePotions(session, cancellationToken, items);

            if (session.LogicSettings.TotalAmountOfRevivesToKeep>=0)
                await OptimizedRecycleRevives(session, cancellationToken, items);

            if (session.LogicSettings.TotalAmountOfBerriesToKeep >= 0)
                await OptimizedRecycleBerries(session, cancellationToken, items);

            await session.Inventory.RefreshCachedInventory();
            currentTotalItems = await session.Inventory.GetTotalItemCount();
            if((session.Profile.PlayerData.MaxItemStorage * session.LogicSettings.RecycleInventoryAtUsagePercentage / 100.0f) > currentTotalItems)
            {
                return;
            }
            //recycles other misc items
            var ritems = await session.Inventory.GetItemsToRecycle(session);

            foreach (var item in ritems)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await session.Client.Inventory.RecycleItem(item.ItemId, item.Count);

                if (session.LogicSettings.VerboseRecycling)
                    session.EventDispatcher.Send(new ItemRecycledEvent { Id = item.ItemId, Count = item.Count });
                if (session.LogicSettings.DelayBetweenRecycleActions)
                    DelayingUtils.Delay(session.LogicSettings.DelayBetweenPlayerActions, 500);
            }

            await session.Inventory.RefreshCachedInventory();
        }

        private static int GetItemCount(IEnumerable<ItemData> inventory, ItemId item)
        {
            return inventory.Where(s => s.ItemId == item).Select(i => i.Count).FirstOrDefault();
        }

        private static async Task<int> RecycleItems(ISession session, CancellationToken cancellationToken, int itemCount, int Diff, ItemId item)
        {
            int itemsToRecycle = 0;
            int itemsToKeep = itemCount - Diff;
            if (itemsToKeep < 0)
                itemsToKeep = 0;
            itemsToRecycle = itemCount - itemsToKeep;
            if (itemsToRecycle != 0)
            {
                Diff -= itemsToRecycle;
                cancellationToken.ThrowIfCancellationRequested();
                await session.Client.Inventory.RecycleItem(item, itemsToRecycle);
                if (session.LogicSettings.VerboseRecycling)
                    session.EventDispatcher.Send(new ItemRecycledEvent { Id = item, Count = itemsToRecycle });
                if (session.LogicSettings.DelayBetweenRecycleActions)
                    DelayingUtils.Delay(session.LogicSettings.DelayBetweenPlayerActions, 500);
            }
            return Diff;
        }

        private static async Task OptimizedRecycleBalls(ISession session, CancellationToken cancellationToken, IEnumerable<ItemData> items)
        {
            int Diff = 0;

            var pokeBallsCount = GetItemCount(items, ItemId.ItemPokeBall);
            var greatBallsCount = GetItemCount(items, ItemId.ItemGreatBall);
            var ultraBallsCount = GetItemCount(items, ItemId.ItemUltraBall);
            var masterBallsCount = GetItemCount(items, ItemId.ItemMasterBall);

            if(session.LogicSettings.DetailedCountsBeforeRecycling)
                Logger.Write(session.Translation.GetTranslation(TranslationString.CurrentPokeballInv,
                    pokeBallsCount, greatBallsCount, ultraBallsCount,
                    masterBallsCount));

            if(pokeBallsCount > session.LogicSettings.TotalAmountOfPokeballsToKeep)
            {
                Diff = pokeBallsCount - session.LogicSettings.TotalAmountOfPokeballsToKeep;
                if(Diff > 0)
                {
                    Diff = await RecycleItems(session, cancellationToken, pokeBallsCount, Diff, ItemId.ItemPokeBall);
                }
            }
            if(greatBallsCount > session.LogicSettings.TotalAmountOfGreatballsToKeep)
            {
                Diff = greatBallsCount - session.LogicSettings.TotalAmountOfGreatballsToKeep;
                if(Diff > 0)
                {
                    Diff = await RecycleItems(session, cancellationToken, greatBallsCount, Diff, ItemId.ItemGreatBall);
                }
            }
            if(ultraBallsCount > session.LogicSettings.TotalAmountOfUltraballsToKeep)
            {
                Diff = ultraBallsCount - session.LogicSettings.TotalAmountOfUltraballsToKeep;
                if(Diff > 0)
                {
                    Diff = await RecycleItems(session, cancellationToken, ultraBallsCount, Diff, ItemId.ItemUltraBall);
                }
            }
            if(masterBallsCount > session.LogicSettings.TotalAmountOfMasterballsToKeep)
            {
                Diff = masterBallsCount - session.LogicSettings.TotalAmountOfMasterballsToKeep;
                if(Diff > 0)
                {
                    Diff = await RecycleItems(session, cancellationToken, ultraBallsCount, Diff, ItemId.ItemMasterBall);
                }
            }
        }

        private static async Task OptimizedRecyclePotions(ISession session, CancellationToken cancellationToken, IEnumerable<ItemData> items)
        {
            int Diff = 0;

            var potionCount = GetItemCount(items, ItemId.ItemPotion);
            var superPotionCount = GetItemCount(items, ItemId.ItemSuperPotion);
            var hyperPotionsCount = GetItemCount(items, ItemId.ItemHyperPotion);
            var maxPotionCount = GetItemCount(items, ItemId.ItemMaxPotion);

            var currentAmountOfPotions = potionCount + superPotionCount + hyperPotionsCount + maxPotionCount;

            if(session.LogicSettings.DetailedCountsBeforeRecycling)
                Logger.Write(session.Translation.GetTranslation(TranslationString.CurrentPotionInv,
                    potionCount, superPotionCount, hyperPotionsCount, maxPotionCount));
            
            int totalPotionsCount = potionCount + superPotionCount + hyperPotionsCount + maxPotionCount;
            if(totalPotionsCount > session.LogicSettings.TotalAmountOfPotionsToKeep)
            {
                Diff = totalPotionsCount - session.LogicSettings.TotalAmountOfPotionsToKeep;
                if(Diff > 0)
                {
                    Diff = await RecycleItems(session, cancellationToken, potionCount, Diff, ItemId.ItemPotion);
                }

                if(Diff > 0)
                {
                    Diff = await RecycleItems(session, cancellationToken, superPotionCount, Diff, ItemId.ItemSuperPotion);
                }

                if(Diff > 0)
                {
                    Diff = await RecycleItems(session, cancellationToken, hyperPotionsCount, Diff, ItemId.ItemHyperPotion);
                }

                if(Diff > 0)
                {
                    Diff = await RecycleItems(session, cancellationToken, maxPotionCount, Diff, ItemId.ItemMaxPotion);
                }
            }
        }

        private static async Task OptimizedRecycleRevives(ISession session, CancellationToken cancellationToken, IEnumerable<ItemData> items)
        {
            int Diff = 0;

            var reviveCount = GetItemCount(items, ItemId.ItemRevive);
            var maxReviveCount = GetItemCount(items, ItemId.ItemMaxPotion);

            var totalRevivesCount = reviveCount + maxReviveCount;

            if(session.LogicSettings.DetailedCountsBeforeRecycling)
                Logger.Write(session.Translation.GetTranslation(TranslationString.CurrentReviveInv,
                    reviveCount, maxReviveCount));

            if(totalRevivesCount > session.LogicSettings.TotalAmountOfRevivesToKeep)
            {
                Diff = totalRevivesCount - session.LogicSettings.TotalAmountOfRevivesToKeep;
                if(Diff > 0)
                {
                    Diff = await RecycleItems(session, cancellationToken, reviveCount, Diff, ItemId.ItemRevive);
                }

                if(Diff > 0)
                {
                    Diff = await RecycleItems(session, cancellationToken, maxReviveCount, Diff, ItemId.ItemMaxRevive);
                }
            }
        }

        private static async Task OptimizedRecycleBerries(ISession session, CancellationToken cancellationToken, IEnumerable<ItemData> items)
        {
            int Diff = 0;

            var razz = GetItemCount(items, ItemId.ItemRazzBerry);
            var bluk = GetItemCount(items, ItemId.ItemBlukBerry);
            var nanab = GetItemCount(items, ItemId.ItemNanabBerry);
            var pinap = GetItemCount(items, ItemId.ItemWeparBerry);
            var wepar = GetItemCount(items, ItemId.ItemPinapBerry);

            int totalBerryCount = razz + bluk + nanab + pinap + wepar;

            var currentAmountOfIncense = GetItemCount(items, ItemId.ItemIncenseOrdinary) +
                GetItemCount(items, ItemId.ItemIncenseSpicy) +
                GetItemCount(items, ItemId.ItemIncenseCool) +
                GetItemCount(items, ItemId.ItemIncenseFloral);
            var currentAmountOfLuckyEggs = GetItemCount(items, ItemId.ItemLuckyEgg);
            var currentAmountOfLures = GetItemCount(items, ItemId.ItemTroyDisk);

            if(session.LogicSettings.DetailedCountsBeforeRecycling)
                Logger.Write(session.Translation.GetTranslation(TranslationString.CurrentMiscItemInv,
                    totalBerryCount, currentAmountOfIncense, currentAmountOfLuckyEggs, currentAmountOfLures));
            
            
            if(totalBerryCount > session.LogicSettings.TotalAmountOfBerriesToKeep)
            {
                Diff = totalBerryCount - session.LogicSettings.TotalAmountOfBerriesToKeep;
                if(Diff > 0)
                {
                    Diff = await RecycleItems(session, cancellationToken, razz, Diff, ItemId.ItemRazzBerry);
                }

                if(Diff > 0)
                {
                    Diff = await RecycleItems(session, cancellationToken, bluk, Diff, ItemId.ItemBlukBerry);
                }

                if(Diff > 0)
                {
                    Diff = await RecycleItems(session, cancellationToken, nanab, Diff, ItemId.ItemNanabBerry);
                }

                if(Diff > 0)
                {
                    Diff = await RecycleItems(session, cancellationToken, pinap, Diff, ItemId.ItemPinapBerry);
                }

                if(Diff > 0)
                {
                    Diff = await RecycleItems(session, cancellationToken, wepar, Diff, ItemId.ItemWeparBerry);
                }
            }
        }
    }
}
