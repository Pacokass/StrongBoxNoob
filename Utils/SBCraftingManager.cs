using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using static ExileCore.PoEMemory.MemoryObjects.ServerInventory;

namespace StrongboxRolling.Utils
{
    public class SBCraftingManager
    {
        public static Regex Weird = new(@"[^A-Za-z0-9\ ]");
        public string[] prevMods = Array.Empty<string>();
        public StrongboxRolling instance;
        
        // Property to access debug logging setting
        public bool EnableDebugLogging => instance?.Settings?.EnableDebugLogging?.Value ?? false;
        
        // Add a static lock object to prevent multiple threads from writing to the log file simultaneously
        private static readonly object _logLock = new object();
        
        // Add a dictionary to track the last time a message was logged for throttling
        private static readonly Dictionary<string, DateTime> _lastLogTime = new Dictionary<string, DateTime>();
        private static readonly TimeSpan _logThrottleInterval = TimeSpan.FromMilliseconds(500); // Throttle similar messages to once per 500ms
        
        // Add a helper method for logging with throttling
        private void LogDebug(string message, bool throttle = true)
        {
            if (!EnableDebugLogging) return;
            
            // Skip throttled messages that were logged recently
            if (throttle)
            {
                string messageKey = message.Length > 50 ? message.Substring(0, 50) : message; // Use first 50 chars as key
                
                lock (_logLock)
                {
                    if (_lastLogTime.TryGetValue(messageKey, out DateTime lastTime))
                    {
                        if (DateTime.Now - lastTime < _logThrottleInterval)
                        {
                            // Skip this message as it was logged too recently
                            return;
                        }
                    }
                    
                    // Update the last log time for this message
                    _lastLogTime[messageKey] = DateTime.Now;
                }
            }
            
            lock (_logLock)
            {
                try
                {
                    string logFilePath = instance?.Settings?.DebugLogFilePath ?? "./ModDebug.txt";
                    using (StreamWriter writer = File.AppendText(logFilePath))
                    {
                        writer.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [SBCraftingManager] {message}");
                    }
                }
                catch (Exception)
                {
                    // Silently fail if we can't write to the log file
                }
            }
        }
        
        public SBCraftingManager(StrongboxRolling ins)
        {
            instance = ins;
        }
        public IEnumerator CraftBox(LabelOnGround sbLabel)
        {
            if (sbLabel is null)
            {
                yield return instance.wait2ms;
            }
            if (instance.GameController.Player.GetComponent<Actor>().isMoving)
            {
                instance.FullWork = true;
                yield break;
            }
            SBType boxType = StaticHelpers.GetStrongboxType(sbLabel);

            // Debug logging
            LogDebug($"--- Starting CraftBox for {boxType} strongbox ---");

            string[] labelsBefore = StaticHelpers.FindAllLabels(sbLabel);
            if (GetWisFromInv().Any() && labelsBefore.Where(x => x.ToLower().Contains("unidentified")).Any())
            {
                LogDebug("Strongbox is unidentified, using Wisdom Scroll");
                prevMods = StaticHelpers.FindAllLabels(sbLabel);
                CraftWithItem(GetWisFromInv().First());
                if (!WaitForChange(labelsBefore))
                {
                    LogDebug("No change after using Wisdom Scroll");
                    yield return true;
                }
                else
                {
                    LogDebug("Strongbox identified");
                }
            }
            
            sbLabel.ItemOnGround.TryGetComponent<ObjectMagicProperties>(out ObjectMagicProperties magicPropsC);
            if (magicPropsC is null)
            {
                LogDebug("No ObjectMagicProperties component found, stopping crafting");
                yield return true;
            }
            
            // Check if we're using "Upgrade to rare only" for Regular strongboxes
            bool upgradeToRareOnly = boxType == SBType.Regular && 
                                    instance.Settings.UseModSelectionSystem.Value && 
                                    instance.Settings.RegularUpgradeToRareOnly != null && 
                                    instance.Settings.RegularUpgradeToRareOnly.Value;
            
            if (upgradeToRareOnly)
            {
                LogDebug("Using 'Upgrade to rare only' mode for Regular strongbox");
                LogDebug($"Current rarity: {magicPropsC.Rarity}");
                
                // Check if the strongbox is already rare
                if (magicPropsC.Rarity == MonsterRarity.Rare)
                {
                    LogDebug("Strongbox is already rare, stopping crafting");
                    instance.pickItCoroutine.Pause();
                    instance.FullWork = true;
                    yield return true;
                }
                else
                {
                    // If it's magic, use a scouring first to make it normal
                    if (magicPropsC.Rarity == MonsterRarity.Magic && GetScoursFromInv().Any())
                    {
                        LogDebug("Strongbox is magic, using Orb of Scouring first");
                        CraftWithItem(GetScoursFromInv().First());
                        yield return instance.wait100ms;
                        
                        // Refresh the component after scouring
                        sbLabel.ItemOnGround.TryGetComponent<ObjectMagicProperties>(out magicPropsC);
                        if (magicPropsC == null)
                        {
                            LogDebug("Failed to get ObjectMagicProperties after scouring");
                            yield return true;
                        }
                        
                        LogDebug($"Rarity after scouring: {magicPropsC.Rarity}");
                    }
                    
                    // Now it should be normal, use an alch to make it rare
                    if (magicPropsC.Rarity == MonsterRarity.White && GetAlchsFromInv().Any())
                    {
                        LogDebug("Using Orb of Alchemy to make strongbox rare");
                        CraftWithItem(GetAlchsFromInv().First());
                        yield return instance.wait100ms;
                        
                        // Refresh the component after alching
                        sbLabel.ItemOnGround.TryGetComponent<ObjectMagicProperties>(out magicPropsC);
                        if (magicPropsC == null)
                        {
                            LogDebug("Failed to get ObjectMagicProperties after alching");
                            yield return true;
                        }
                        
                        LogDebug($"Rarity after alching: {magicPropsC.Rarity}");
                        
                        // Check if it's rare now
                        if (magicPropsC.Rarity == MonsterRarity.Rare)
                        {
                            LogDebug("Strongbox is now rare, stopping crafting");
                            instance.pickItCoroutine.Pause();
                            instance.FullWork = true;
                            yield return true;
                        }
                        else
                        {
                            LogDebug("Failed to make strongbox rare with Orb of Alchemy");
                        }
                    }
                    else
                    {
                        if (magicPropsC.Rarity != MonsterRarity.White)
                        {
                            LogDebug("Strongbox is not normal rarity, cannot use Orb of Alchemy");
                        }
                        else if (!GetAlchsFromInv().Any())
                        {
                            LogDebug("No Orb of Alchemy available");
                        }
                    }
                }
            }
            else
            {
                // Check if the strongbox already has the desired mods
                bool hasDesiredMods = CheckMods();
                if (hasDesiredMods)
                {
                    LogDebug("Strongbox already has desired mods, stopping crafting");
                    yield return true;
                }
                else
                {
                    LogDebug("Strongbox does not have desired mods, continuing crafting");
                }

                // Apply the appropriate crafting method
                if (!instance.Settings.BoxCraftingUseAltsAugs || CheckBoxTypeAlchOverride(boxType))
                {
                    LogDebug("Using Scour/Alch crafting method");
                    ScourAlchStep(magicPropsC, sbLabel);
                }
                else if (instance.Settings.BoxCraftingUseAltsAugs)
                {
                    LogDebug("Using Transmute/Alt/Aug crafting method");
                    AlterStep(magicPropsC, sbLabel);
                }
                
                // Check again after crafting
                hasDesiredMods = CheckMods();
                if (hasDesiredMods)
                {
                    LogDebug("Strongbox now has desired mods after crafting, stopping");
                    yield return true;
                }
                else
                {
                    LogDebug("Strongbox still does not have desired mods after crafting");
                }
            }
        }
        public bool ScourAlchStep(ObjectMagicProperties magicPropsC, LabelOnGround sbLabel)
        {
            try
            {
                if (magicPropsC.Mods.Count() > 0)
                {
                    if (GetScoursFromInv().Any())
                    {
                        LogDebug("Using Scouring Orb");
                        CraftWithItem(GetScoursFromInv().First());
                        return true;
                    }
                    else
                    {
                        LogDebug("No Scouring Orbs available");
                    }
                }
                else if (magicPropsC.Mods.Count == 0 &&
                    CheckBoxTypeEngOverride(StaticHelpers.GetStrongboxType(sbLabel)) &&
                    GetEngFromInv().Any() &&
                    !HasMaxQuality(sbLabel))
                {
                    // Check if we should use fast apply mode for Engineer's Orbs
                    if (instance.Settings.UseFastApplyForEngineerOrbs.Value && 
                        magicPropsC.Rarity == MonsterRarity.White)
                    {
                        LogDebug("Using Fast Apply mode for Engineer's Orbs");
                        return FastApplyEngineerOrbs(sbLabel);
                    }
                    else
                    {
                        LogDebug("Using Engineer's Orb");
                        CraftWithItem(GetEngFromInv().FirstOrDefault());
                    }
                }
                else if (GetAlchsFromInv().Any())
                {
                    LogDebug("Using Orb of Alchemy");
                    prevMods = StaticHelpers.FindAllLabels(sbLabel);
                    CraftWithItem(GetAlchsFromInv().First());
                    if (!WaitForChange(prevMods))
                    {
                        LogDebug("No change after using Orb of Alchemy");
                        return false;
                    }
                    else
                    {
                        LogDebug("Applied Orb of Alchemy successfully");
                    }
                }
                else
                {
                    LogDebug("No Orb of Alchemy available");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                LogDebug($"Error in ScourAlchStep: {ex.ToString()}");
            }
            return false;
        }
        public bool HasMaxQuality(LabelOnGround sbLabel)
        {
            string[] labels = StaticHelpers.FindAllLabels(sbLabel);
            foreach (string label in labels)
            {
                // Check for various ways the quality might be displayed
                if (label.Contains("<augmented>{+20%}") || 
                    label.Contains("Quality: +20%") || 
                    label.Contains("Quality: 20%"))
                {
                    return true;
                }
            }
            return false;
        }
        public bool AlterStep(ObjectMagicProperties magicPropsC, LabelOnGround sbLabel)
        {
            try
            {
                if (magicPropsC.Mods.Count() > 2 && GetScoursFromInv().Any())
                {
                    LogDebug("Too many mods, using Scouring Orb");
                    CraftWithItem(GetScoursFromInv().First());
                    return true;
                }
                else if (magicPropsC.Mods.Count() is 0 && GetTransmutesFromInv().Any())
                {
                    LogDebug("No mods, using Orb of Transmutation");
                    prevMods = StaticHelpers.FindAllLabels(sbLabel);
                    CraftWithItem(GetTransmutesFromInv().First());
                    if (!WaitForChange(prevMods))
                    {
                        LogDebug("No change after using Orb of Transmutation");
                        return false;
                    }
                    else
                    {
                        LogDebug("Applied Orb of Transmutation successfully");
                    }
                    return true;
                }
                else if (magicPropsC.Mods.Count() == 1 && instance.Settings.BoxCraftingUseAltsAugs && StaticHelpers.FindAllLabels(sbLabel).Where(x => x.ToLower().Contains("suffix")).Any())
                {
                    if (GetAugsFromInv().Any())
                    {
                        LogDebug("One mod (suffix), using Orb of Augmentation");
                        prevMods = StaticHelpers.FindAllLabels(sbLabel);
                        CraftWithItem(GetAugsFromInv().First());
                        if (!WaitForChange(prevMods))
                        {
                            LogDebug("No change after using Orb of Augmentation");
                            return false;
                        }
                        else
                        {
                            LogDebug("Applied Orb of Augmentation successfully");
                        }
                        return true;
                    }
                    else
                    {
                        LogDebug("No Orb of Augmentation available");
                    }
                }
                else if (GetAltsFromInv().Any())
                {
                    LogDebug("Using Orb of Alteration");
                    prevMods = StaticHelpers.FindAllLabels(sbLabel);
                    CraftWithItem(GetAltsFromInv().First());
                    if (!WaitForChange(prevMods))
                    {
                        LogDebug("No change after using Orb of Alteration");
                        return false;
                    }
                    else
                    {
                        LogDebug("Applied Orb of Alteration successfully");
                    }
                    return true;
                }
                else
                {
                    LogDebug("No appropriate currency available");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                LogDebug($"Error in AlterStep: {ex.ToString()}");
            }
            return false;
        }
        public bool CheckBoxTypeAlchOverride(SBType sb)
        {
            if (sb is SBType.Arcanist && instance.Settings.UseAlchScourForArcanist)
            {
                return true;
            }
            if (sb is SBType.Diviner && instance.Settings.UseAlchScourForDiviner)
            {
                return true;
            }
            if (sb is SBType.Cartographer && instance.Settings.UseAlchScourForCartog)
            {
                return true;
            }
            return false;
        }
        public bool CheckBoxTypeEngOverride(SBType sb)
        {
            if (sb is SBType.Arcanist && instance.Settings.UseEngForArcanist)
            {
                return true;
            }
            if (sb is SBType.Diviner && instance.Settings.UseEngForDiviner)
            {
                return true;
            }
            if (sb is SBType.Cartographer && instance.Settings.UseEngForCartog)
            {
                return true;
            }
            return false;
        }
        public bool WaitForChange(string[] labelsBefore)
        {
            int maxWait = 200;
            int totalWait = 0;
            string[] labels = StaticHelpers.FindAllLabels(instance.GetClosestChest());
            while (!StaticHelpers.LabelsChanged(labelsBefore, labels) && totalWait < maxWait)
            {
                int delay = 10;
                Task.Delay(delay).Wait();
                totalWait += delay;
                labels = StaticHelpers.FindAllLabels(instance.GetClosestChest());
            }
            if (totalWait >= maxWait)
            {
                return false;
            }
            return true;
        }
        public void CraftWithItem(InventSlotItem e)
        {
            if (instance.FullWork)
            {
                return;
            }

            LabelOnGround toCraft = instance.GetClosestChest();
            string[] labels = StaticHelpers.FindAllLabels(toCraft);
            List<string> toLog = new();

            toLog.Add(@$"{DateTime.Now.ToString("yyyy-mm-dd_T")}");
            toLog.Add(@$"{e.Item.RenderName}");
            toLog.AddRange(labels);
            if (!e.Item.Metadata.ToLower().Contains("ident") && labels.Where(x => x.ToLower().Contains("unidentified")).Any())
            {
                return;
            }
            string allMods = string.Join(" ", toLog);

            File.AppendAllLines(@"./craftingLog.txt", toLog);
            if (CheckMods() && e.Item.Metadata != "Metadata/Items/Currency/CurrencyAddModToMagic")
            {
                return;
            }
            if (!instance.GameController.Window.IsForeground()) return;
            if (!IngameState.pTheGame.IngameState.IngameUi.InventoryPanel.IsVisibleLocal)
            {
                SendKeys.SendWait("i");

                Task.Delay(instance.Settings.BoxCraftingMidStepDelay).Wait();
            }
            Mouse.MoveCursorToPosition(instance.GetPos(e));
            Task.Delay(instance.Settings.BoxCraftingMidStepDelay).Wait();
            if (!StaticHelpers.WaitForMouseIcon(MouseActionType.Free, instance.GameController.IngameState.IngameUi.Cursor))
            {
                return;
            }
            Mouse.RightClick(instance.Settings.BoxCraftingMidStepDelay);
            if (!StaticHelpers.WaitForMouseIcon(MouseActionType.UseItem, instance.GameController.IngameState.IngameUi.Cursor))
            {
                return;
            }

            Task.Delay(instance.Settings.BoxCraftingMidStepDelay).Wait();
            //Mouse.SetCursorPos(GetPos(toCraft));
            Mouse.LinearSmoothMove(instance.GetPos(toCraft));
            bool? isTargeted = toCraft.ItemOnGround.GetComponent<Targetable>()?.isTargeted;
            int limit = 200;
            int i = 0;
            while (isTargeted is null || !isTargeted.Value)
            {
                Task.Delay(1).Wait();
                if (i >= limit)
                {
                    return;
                }
                i++;
                isTargeted = toCraft.ItemOnGround.GetComponent<Targetable>()?.isTargeted;
            }
            if (isTargeted is not null && isTargeted.Value)
            {

                Task.Delay(instance.Settings.BoxCraftingMidStepDelay).Wait();
                if (!StaticHelpers.WaitForMouseIcon(MouseActionType.UseItem, instance.GameController.IngameState.IngameUi.Cursor))
                {
                    return;
                }
                Mouse.LeftClick(instance.Settings.BoxCraftingMidStepDelay);
                StaticHelpers.WaitForMouseIcon(MouseActionType.Free, instance.GameController.IngameState.IngameUi.Cursor);
            }
            else
            {

            }

            Task.Delay(instance.Settings.BoxCraftingStepDelay).Wait();
            Task.Delay(100).Wait();

        }
        public bool CheckMods()
        {
            try
            {
                LabelOnGround chest = instance.GetClosestChest();
                SBType sbType = StaticHelpers.GetStrongboxType(chest);
                string[] allMods = StaticHelpers.FindAllLabels(chest);
                
                // Debug logging
                LogDebug($"--- Checking Strongbox of type {sbType} ---");
                LogDebug($"Found {allMods.Length} mods:");
                foreach (string mod in allMods)
                {
                    LogDebug($"- {mod}");
                }
                
                // Get the ObjectMagicProperties component to check rarity
                chest.ItemOnGround.TryGetComponent<ObjectMagicProperties>(out ObjectMagicProperties magicPropsC);
                
                if (magicPropsC != null)
                {
                    LogDebug($"Rarity from ObjectMagicProperties: {magicPropsC.Rarity}");
                }
                else
                {
                    LogDebug("ObjectMagicProperties component not found");
                }
                
                // Special case for Regular strongboxes with "Upgrade to rare only" option
                if (sbType == SBType.Regular && 
                    instance.Settings.UseModSelectionSystem.Value && 
                    instance.Settings.RegularUpgradeToRareOnly != null && 
                    instance.Settings.RegularUpgradeToRareOnly.Value)
                {
                    // Check if the strongbox is already rare using the ObjectMagicProperties component
                    bool isRare = false;
                    
                    if (magicPropsC != null && magicPropsC.Rarity == MonsterRarity.Rare)
                    {
                        isRare = true;
                    }
                    
                    if (isRare)
                    {
                        LogDebug("Regular strongbox is already rare, keeping it.");
                        instance.pickItCoroutine.Pause();
                        instance.FullWork = true;
                        return true;
                    }
                    
                    LogDebug("Regular strongbox is not rare, continuing crafting.");
                    return false;
                }
                
                // Use the mod selection system if enabled
                if (instance.Settings.UseModSelectionSystem.Value)
                {
                    // Debug logging
                    LogDebug("Using mod selection system");
                    
                    // Create a combined string of all mods for better matching
                    string combinedMods = string.Join(" ", allMods.Select(x => Weird.Replace(x, "").ToLower()));
                    LogDebug($"Combined mods: {combinedMods}");
                    
                    // Check if the strongbox meets our criteria using the mod selection system
                    bool isDesirable = instance.ModManager.CheckStrongboxMods(new[] { combinedMods }, sbType);
                    
                    LogDebug($"Is desirable: {isDesirable}");
                    
                    if (isDesirable)
                    {
                        LogDebug($"Strongbox has desirable mods for type: {sbType}, keeping it.");
                        instance.pickItCoroutine.Pause();
                        instance.FullWork = true;
                        return true;
                    }
                    
                    LogDebug("Strongbox does not meet criteria, continuing crafting.");
                    return false;
                }
                // Use the original regex-based system if mod selection is disabled
                else
                {
                    // Debug logging
                    LogDebug("Using regex-based system");
                    
                    Regex goodMods;
                    
                    if (sbType is SBType.Diviner)
                    {
                        goodMods = new Regex(@$"{instance.Settings.DivinerRegex}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
                    }
                    else if (sbType is SBType.Arcanist)
                    {
                        goodMods = new Regex(@$"{instance.Settings.ArcanistRegex}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
                    }
                    else if (sbType is SBType.Cartographer)
                    {
                        goodMods = new Regex(@$"{instance.Settings.CartogRegex}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
                    }
                    else
                    {
                        goodMods = new Regex(@$"{instance.Settings.ModsRegex}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
                    }

                    allMods = allMods.Select(x => Weird.Replace(x, "").ToLower()).ToArray();
                    string added = String.Join(" ", allMods);
                    
                    foreach (string s in allMods)
                    {
                        if (goodMods.IsMatch(s))
                        {
                            LogDebug($"Found matching mod: {s}, keeping strongbox.");
                            instance.pickItCoroutine.Pause();
                            instance.FullWork = true;

                            return true;
                        }
                    }

                    if (goodMods.IsMatch(added))
                    {
                        LogDebug("Grouped mods matched where separate did not, keeping strongbox.");
                        if (magicPropsC != null && magicPropsC.Mods.Count == 1)
                        {
                            if (GetAugsFromInv().Any())
                            {
                                CraftWithItem(GetAugsFromInv().First());
                            }
                        }
                        instance.pickItCoroutine.Pause();
                        instance.FullWork = true;
                        return true;
                    }
                    
                    LogDebug("No matching mods found, continuing crafting.");
                }
            }
            catch (Exception ex)
            {
                instance.LogError(ex.ToString());
                LogDebug($"Error in CheckMods: {ex.ToString()}");
            }
            return false;
        }
        
        // New method to check if a strongbox is ready without side effects
        public bool IsStrongboxReady(LabelOnGround chest)
        {
            try
            {
                if (chest == null || chest.ItemOnGround == null)
                {
                    return false;
                }
                
                SBType sbType = StaticHelpers.GetStrongboxType(chest);
                string[] allMods = StaticHelpers.FindAllLabels(chest);
                
                if (EnableDebugLogging)
                {
                    LogDebug($"Checking strongbox of type {sbType}", true); // Throttle this common message
                }
                
                // Get the ObjectMagicProperties component to check rarity
                chest.ItemOnGround.TryGetComponent<ObjectMagicProperties>(out ObjectMagicProperties magicPropsC);
                
                // Special case for Regular strongboxes with "Upgrade to rare only" option
                if (sbType == SBType.Regular && 
                    instance.Settings.UseModSelectionSystem.Value && 
                    instance.Settings.RegularUpgradeToRareOnly != null && 
                    instance.Settings.RegularUpgradeToRareOnly.Value)
                {
                    // Check if the strongbox is already rare using the ObjectMagicProperties component
                    if (magicPropsC != null && magicPropsC.Rarity == MonsterRarity.Rare)
                    {
                        if (EnableDebugLogging)
                        {
                            LogDebug($"Regular strongbox is already rare (Upgrade to rare only mode) - READY", false); // Don't throttle important state changes
                        }
                        return true;
                    }
                    
                    if (EnableDebugLogging)
                    {
                        LogDebug($"Regular strongbox is not rare yet (Upgrade to rare only mode) - NOT READY", true); // Throttle this repetitive message
                    }
                    return false;
                }
                
                // Use the mod selection system if enabled
                if (instance.Settings.UseModSelectionSystem.Value)
                {
                    // Create a combined string of all mods for better matching
                    string combinedMods = string.Join(" ", allMods.Select(x => Weird.Replace(x, "").ToLower()));
                    
                    if (EnableDebugLogging)
                    {
                        LogDebug($"Using mod selection system for {sbType}", true); // Throttle this common message
                        LogDebug($"Combined mods: {combinedMods}", true); // Throttle this verbose message
                    }
                    
                    // Check if the strongbox meets our criteria using the mod selection system
                    bool isDesirable = instance.ModManager.CheckStrongboxMods(new[] { combinedMods }, sbType);
                    
                    if (EnableDebugLogging)
                    {
                        LogDebug($"Mod selection result: {(isDesirable ? "READY" : "NOT READY")}", false); // Don't throttle important results
                    }
                    
                    return isDesirable;
                }
                // Use the original regex-based system if mod selection is disabled
                else
                {
                    Regex goodMods;
                    
                    if (sbType is SBType.Diviner)
                    {
                        goodMods = new Regex(@$"{instance.Settings.DivinerRegex}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
                    }
                    else if (sbType is SBType.Arcanist)
                    {
                        goodMods = new Regex(@$"{instance.Settings.ArcanistRegex}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
                    }
                    else if (sbType is SBType.Cartographer)
                    {
                        goodMods = new Regex(@$"{instance.Settings.CartogRegex}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
                    }
                    else
                    {
                        goodMods = new Regex(@$"{instance.Settings.ModsRegex}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
                    }

                    if (EnableDebugLogging)
                    {
                        LogDebug($"Using regex system for {sbType}", true); // Throttle this common message
                        LogDebug($"Regex pattern: {goodMods}", true); // Throttle this verbose message
                    }

                    allMods = allMods.Select(x => Weird.Replace(x, "").ToLower()).ToArray();
                    string added = String.Join(" ", allMods);
                    
                    if (EnableDebugLogging)
                    {
                        LogDebug($"Combined mods: {added}", true); // Throttle this verbose message
                    }
                    
                    foreach (string s in allMods)
                    {
                        if (goodMods.IsMatch(s))
                        {
                            if (EnableDebugLogging)
                            {
                                LogDebug($"Found matching mod: {s} - READY", false); // Don't throttle important matches
                            }
                            return true;
                        }
                    }

                    if (goodMods.IsMatch(added))
                    {
                        if (EnableDebugLogging)
                        {
                            LogDebug($"Found match in combined mods - READY", false); // Don't throttle important matches
                        }
                        return true;
                    }
                    
                    if (EnableDebugLogging)
                    {
                        LogDebug($"No matching mods found - NOT READY", true); // Throttle this repetitive message
                    }
                }
            }
            catch (Exception ex)
            {
                instance.LogError(ex.ToString());
                if (EnableDebugLogging)
                {
                    LogDebug($"Error in IsStrongboxReady: {ex.Message}", false); // Don't throttle error messages
                }
            }
            return false;
        }

        public ServerInventory.InventSlotItem[] GetInvWithMD(string metadataToFind)
        {
            try
            {
                if (instance.InventoryItems is not null && instance.InventoryItems.InventorySlotItems.Any())
                {
                    return instance.InventoryItems.InventorySlotItems.Where(x => (bool)(x.Item?.Metadata?.Contains(metadataToFind))).OrderBy(x => x.PosX).ThenBy(x => x.PosY).ToArray();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            return Array.Empty<InventSlotItem>();
        }
        public InventSlotItem[] GetScoursFromInv()
        {
            return GetInvWithMD(ItemCodes.ScourCode);
        }
        public InventSlotItem[] GetAlchsFromInv()
        {
            return GetInvWithMD(ItemCodes.AlchCode);
        }
        public InventSlotItem[] GetTransmutesFromInv()
        {
            return GetInvWithMD(ItemCodes.TransmuteCode);
        }
        public InventSlotItem[] GetAugsFromInv()
        {
            return GetInvWithMD(ItemCodes.AugCode);
        }
        public InventSlotItem[] GetAltsFromInv()
        {
            return GetInvWithMD(ItemCodes.AltCode);
        }
        public InventSlotItem[] GetWisFromInv()
        {
            return GetInvWithMD(ItemCodes.WisdomCode);
        }
        public InventSlotItem[] GetEngFromInv()
        {
            return GetInvWithMD(ItemCodes.EngineerCode);
        }

        // New method for fast applying Engineer's Orbs
        public bool FastApplyEngineerOrbs(LabelOnGround sbLabel)
        {
            try
            {
                LogDebug("Starting Fast Apply for Engineer's Orbs");
                
                // Get the first Engineer's Orb from inventory
                var engineerOrb = GetEngFromInv().FirstOrDefault();
                if (engineerOrb == null)
                {
                    LogDebug("No Engineer's Orbs available for fast apply");
                    return false;
                }
                
                LogDebug("Found Engineer's Orb in inventory");
                
                // Check if the strongbox is normal rarity
                sbLabel.ItemOnGround.TryGetComponent<ObjectMagicProperties>(out ObjectMagicProperties magicPropsC);
                if (magicPropsC == null || magicPropsC.Rarity != MonsterRarity.White)
                {
                    LogDebug("Strongbox is not normal rarity, cannot use Engineer's Orb");
                    return false;
                }
                
                // Check if the strongbox already has max quality
                if (HasMaxQuality(sbLabel))
                {
                    LogDebug("Strongbox already has max quality, skipping Engineer's Orb");
                    return false;
                }
                
                // Maximum number of applications
                int maxApplications = 4;
                int applications = 0;
                
                // Open inventory if not already open
                if (!IngameState.pTheGame.IngameState.IngameUi.InventoryPanel.IsVisibleLocal)
                {
                    LogDebug("Opening inventory");
                    SendKeys.SendWait("i");
                    Task.Delay(instance.Settings.BoxCraftingMidStepDelay).Wait();
                }
                
                // First application - similar to regular CraftWithItem
                LogDebug("Starting first application");
                
                // Move cursor to the Engineer's Orb
                LogDebug("Moving cursor to Engineer's Orb");
                Mouse.MoveCursorToPosition(instance.GetPos(engineerOrb));
                Task.Delay(instance.Settings.BoxCraftingMidStepDelay).Wait();
                if (!StaticHelpers.WaitForMouseIcon(MouseActionType.Free, instance.GameController.IngameState.IngameUi.Cursor))
                {
                    LogDebug("Failed to get free mouse cursor");
                    return false;
                }
                
                // Right-click the Engineer's Orb
                LogDebug("Right-clicking Engineer's Orb");
                Mouse.RightClick(instance.Settings.BoxCraftingMidStepDelay);
                if (!StaticHelpers.WaitForMouseIcon(MouseActionType.UseItem, instance.GameController.IngameState.IngameUi.Cursor))
                {
                    LogDebug("Failed to get use item cursor");
                    return false;
                }
                
                // Move cursor to the strongbox
                LogDebug("Moving cursor to strongbox");
                Mouse.LinearSmoothMove(instance.GetPos(sbLabel));
                Task.Delay(instance.Settings.BoxCraftingMidStepDelay).Wait();
                
                // Wait for targeting
                LogDebug("Waiting for targeting");
                bool? isTargeted = sbLabel.ItemOnGround.GetComponent<Targetable>()?.isTargeted;
                int limit = 200;
                int i = 0;
                while (isTargeted is null || !isTargeted.Value)
                {
                    Task.Delay(1).Wait();
                    if (i >= limit)
                    {
                        LogDebug("Failed to target strongbox");
                        return false;
                    }
                    i++;
                    isTargeted = sbLabel.ItemOnGround.GetComponent<Targetable>()?.isTargeted;
                }
                
                LogDebug("Strongbox targeted successfully");
                
                // Apply the Engineer's Orbs
                if (isTargeted is not null && isTargeted.Value)
                {
                    Task.Delay(instance.Settings.BoxCraftingMidStepDelay).Wait();
                    if (!StaticHelpers.WaitForMouseIcon(MouseActionType.UseItem, instance.GameController.IngameState.IngameUi.Cursor))
                    {
                        LogDebug("Lost use item cursor");
                        return false;
                    }
                    
                    try
                    {
                        // Start holding Shift key and keep it held for all applications
                        LogDebug("Starting to hold Shift key");
                        Mouse.StartHoldingShift();
                        
                        // Apply Engineer's Orbs up to the maximum number or until conditions are met
                        for (int appIndex = 0; appIndex < maxApplications; appIndex++)
                        {
                            // Store the current labels to check for changes
                            string[] labelsBefore = StaticHelpers.FindAllLabels(sbLabel);
                            
                            // Left-click while Shift is held to apply the orb
                            LogDebug($"Left-clicking to apply Engineer's Orb #{appIndex+1}");
                            Mouse.LeftClickWhileShiftHeld(0);
                            applications++;
                            
                            // Wait for the specified delay between applications
                            LogDebug($"Waiting {instance.Settings.FastApplyDelay.Value}ms before checking result");
                            Task.Delay(instance.Settings.FastApplyDelay.Value).Wait();
                            
                            // Wait for the labels to change, indicating the orb was applied
                            if (!WaitForChange(labelsBefore))
                            {
                                LogDebug("No change detected after applying Engineer's Orb");
                                break;
                            }
                            
                            LogDebug($"Engineer's Orb #{appIndex+1} applied successfully");
                            
                            // Check if we should stop
                            if (HasMaxQuality(sbLabel))
                            {
                                LogDebug("Reached max quality (20%), stopping fast apply");
                                break;
                            }
                            
                            // Check if the strongbox is still normal rarity
                            sbLabel.ItemOnGround.TryGetComponent<ObjectMagicProperties>(out magicPropsC);
                            if (magicPropsC == null || magicPropsC.Rarity != MonsterRarity.White)
                            {
                                LogDebug("Strongbox is no longer normal rarity, stopping fast apply");
                                break;
                            }
                            
                            // Make sure we're still targeting the strongbox
                            isTargeted = sbLabel.ItemOnGround.GetComponent<Targetable>()?.isTargeted;
                            if (isTargeted is null || !isTargeted.Value)
                            {
                                LogDebug("Lost targeting, stopping fast apply");
                                break;
                            }
                            
                            // Make sure we still have the use item cursor
                            if (!StaticHelpers.WaitForMouseIcon(MouseActionType.UseItem, instance.GameController.IngameState.IngameUi.Cursor))
                            {
                                LogDebug("Lost use item cursor, stopping fast apply");
                                break;
                            }
                            
                            // If this isn't the last application, wait before the next one
                            if (appIndex < maxApplications - 1)
                            {
                                LogDebug($"Waiting {instance.Settings.FastApplyDelay.Value}ms before next application");
                                Task.Delay(instance.Settings.FastApplyDelay.Value).Wait();
                            }
                        }
                    }
                    finally
                    {
                        // Always release Shift key at the end
                        LogDebug("Releasing Shift key");
                        Mouse.StopHoldingShift();
                    }
                }
                
                // Wait for cursor to return to free state
                StaticHelpers.WaitForMouseIcon(MouseActionType.Free, instance.GameController.IngameState.IngameUi.Cursor);
                
                LogDebug($"Fast apply completed with {applications} Engineer's Orbs applied");
                return applications > 0;
            }
            catch (Exception ex)
            {
                // Make sure to release Shift key in case of exception
                try { Mouse.StopHoldingShift(); } catch { }
                
                Console.WriteLine(ex.ToString());
                LogDebug($"Error in FastApplyEngineerOrbs: {ex.ToString()}");
                return false;
            }
        }
    }
}
