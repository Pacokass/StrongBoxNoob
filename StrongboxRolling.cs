using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Nodes;
using ExileCore.Shared.Cache;
using ImGuiNET;
using Newtonsoft.Json;
using Random_Features.Libs;
using StrongboxRolling.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

using static ExileCore.PoEMemory.MemoryObjects.ServerInventory;
using Color = SharpDX.Color;
using Input = ExileCore.Input;
using RectangleF = SharpDX.RectangleF;
using Stack = ExileCore.PoEMemory.Components.Stack;
using Vector2 = SharpDX.Vector2;

namespace StrongboxRolling
{
    public class StrongboxRolling : BaseSettingsPlugin<StrongboxRollingSettings>
    {
        internal const string StrongboxRollingRuleDirectory = "StrongboxRolling Rules";
        internal const string StrongboxModsDirectory = "StrongboxRolling Mods";
        internal readonly List<Entity> _entities = new List<Entity>();
        internal readonly Stopwatch _pickUpTimer = Stopwatch.StartNew();
        internal readonly Stopwatch DebugTimer = Stopwatch.StartNew();
        internal readonly WaitTime toPick = new WaitTime(1);
        internal readonly WaitTime wait1ms = new WaitTime(1);
        internal readonly WaitTime wait2ms = new WaitTime(2);
        internal readonly WaitTime wait3ms = new WaitTime(3);
        internal readonly WaitTime wait100ms = new WaitTime(100);
        internal readonly WaitTime waitForNextTry = new WaitTime(1);
        internal Vector2 _clickWindowOffset;
        internal HashSet<string> _magicRules;
        internal HashSet<string> _normalRules;
        internal HashSet<string> _rareRules;
        internal HashSet<string> _uniqueRules;
        internal HashSet<string> _ignoreRules;
        internal Dictionary<string, int> _weightsRules = new Dictionary<string, int>();
        internal WaitTime _workCoroutine;
        public DateTime buildDate;
        internal uint coroutineCounter;
        internal Vector2 cursorBeforePickIt;
        internal bool FullWork = true;
        internal Element LastLabelClick;
        public string MagicRuleFile;
        internal WaitTime mainWorkCoroutine = new WaitTime(5);
        public string NormalRuleFile;
        internal Coroutine pickItCoroutine;
        public string RareRuleFile;
        internal WaitTime tryToPick = new WaitTime(7);
        public string UniqueRuleFile;
        internal WaitTime waitPlayerMove = new WaitTime(10);
        internal List<string> _customItems = new List<string>();
        internal SBCraftingManager CraftingManager;
        internal StashCraftingManager StashCraftingManager;
        internal ModSelectionManager ModManager;
        public int[,] inventorySlots { get; set; } = new int[0, 0];
        public ServerInventory InventoryItems { get; set; }
        public static StrongboxRolling Controller { get; set; }


        public FRSetManagerPublishInformation FullRareSetManagerData = new FRSetManagerPublishInformation();

        // Add new fields for caching and drawing
        internal List<Tuple<RectangleF, Color, int>> StrongboxBorderDrawList = new List<Tuple<RectangleF, Color, int>>();
        private FrameCache<List<LabelOnGround>> StrongboxLabelCache;
        private Dictionary<LabelOnGround, (bool isReady, DateTime lastCheck)> StrongboxModCheckCache = new Dictionary<LabelOnGround, (bool isReady, DateTime lastCheck)>();
        private const int ModCheckCacheDurationMs = 1000; // Cache mod check results for 1 second

        public StrongboxRolling()
        {
            Name = "StrongboxRolling";
        }

        public string PluginVersion { get; set; }
        internal List<string> PickitFiles { get; set; }

        public override bool Initialise()
        {
            Controller = this;
            
            // Initialize the mod selection manager
            string modsDirectory = Path.Combine(DirectoryFullName, StrongboxModsDirectory);
            if (!Directory.Exists(modsDirectory))
            {
                Directory.CreateDirectory(modsDirectory);
            }
            string modsFilePath = Path.Combine(modsDirectory, "strongbox_mods.json");
            ModManager = new ModSelectionManager(modsFilePath);
            
            // Connect debug setting to ModManager
            ModManager.EnableDebugLogging = Settings.EnableDebugLogging.Value;
            ModManager.DebugLogFilePath = Settings.DebugLogFilePath;
            
            // Clear the debug log file on startup
            if (File.Exists(Settings.DebugLogFilePath))
            {
                ClearDebugLogFile();
            }
            
            // Ensure RegularUpgradeToRareOnly is initialized
            if (Settings.RegularUpgradeToRareOnly == null)
            {
                Settings.RegularUpgradeToRareOnly = new ExileCore.Shared.Nodes.ToggleNode(false);
            }
            
            // Sync the required mods settings with the ModManager
            ModManager.SetRequiredDesiredMods(SBType.Regular, Settings.RegularRequiredMods.Value);
            ModManager.SetRequiredDesiredMods(SBType.Arcanist, Settings.ArcanistRequiredMods.Value);
            ModManager.SetRequiredDesiredMods(SBType.Diviner, Settings.DivinerRequiredMods.Value);
            ModManager.SetRequiredDesiredMods(SBType.Cartographer, Settings.CartographerRequiredMods.Value);
            
            // Initialize the strongbox label cache
            StrongboxLabelCache = new FrameCache<List<LabelOnGround>>(UpdateStrongboxLabelList);
            
            CraftingManager = new(this);
            StashCraftingManager = new(this);
            pickItCoroutine = new Coroutine(MainWorkCoroutine(), this, "StrongboxRolling");
            Core.ParallelRunner.Run(pickItCoroutine);
            pickItCoroutine.Pause();
            DebugTimer.Reset();

            _workCoroutine = new WaitTime(Settings.ExtraDelay);

            //LoadCustomItems();
            return true;
        }

        public override void DrawSettings()
        {
            // General Settings
            Settings.Enable.Value = ImGuiExtension.Checkbox("Enable Plugin", Settings.Enable);
            Settings.CraftBoxKey = ImGuiExtension.HotkeySelector("Craft box Key: " + Settings.CraftBoxKey.Value.ToString(), Settings.CraftBoxKey);
            Settings.CancelKey = ImGuiExtension.HotkeySelector("Cancel Key: " + Settings.CancelKey.Value.ToString(), Settings.CancelKey);
            Settings.BoxCraftingUseAltsAugs.Value = ImGuiExtension.Checkbox("Use Transmute/Alt/Aug instead of Alch/Scour", Settings.BoxCraftingUseAltsAugs);
            Settings.BoxCraftingMidStepDelay.Value = ImGuiExtension.IntSlider("Mid-step delay", Settings.BoxCraftingMidStepDelay);
            Settings.BoxCraftingStepDelay.Value = ImGuiExtension.IntSlider("Step delay", Settings.BoxCraftingStepDelay);
            
            // Debug Settings
            if (ImGui.CollapsingHeader("Debug Settings"))
            {
                bool enableDebug = Settings.EnableDebugLogging.Value;
                if (ImGui.Checkbox("Enable Debug Logging", ref enableDebug))
                {
                    Settings.EnableDebugLogging.Value = enableDebug;
                    ModManager.EnableDebugLogging = enableDebug;
                }
                
                ImGui.Indent(10);
                ImGui.TextColored(new System.Numerics.Vector4(1, 0.6f, 0.6f, 1), "Warning: Enabling debug logging will create a large ModDebug.txt file");
                ImGui.Text("Only enable this when troubleshooting issues with strongbox mod detection");
                
                if (enableDebug)
                {
                    // Input field for log file path
                    ImGui.Text("Debug Log File Path:");
                    string logFilePath = Settings.DebugLogFilePath;
                    if (ImGui.InputText("##DebugLogFilePath", ref logFilePath, 256))
                    {
                        Settings.DebugLogFilePath = logFilePath;
                        ModManager.DebugLogFilePath = logFilePath;
                    }
                }
                
                if (ImGui.Button("Clear Debug Log File"))
                {
                    ClearDebugLogFile("Debug log cleared manually at " + DateTime.Now);
                }
                
                ImGui.Unindent(10);
            }
            
            // Visual Settings
            if (ImGui.CollapsingHeader("Visual Settings"))
            {
                Settings.ShowGreenBorderOnReadyStrongboxes.Value = ImGuiExtension.Checkbox("Show green border on ready strongboxes", Settings.ShowGreenBorderOnReadyStrongboxes);
                ImGui.Indent(10);
                ImGui.Text("Draws a green border around strongboxes that have passed the crafting checks");
                
                Settings.ShowRedBorderOnLockedStrongboxes.Value = ImGuiExtension.Checkbox("Show red border on locked strongboxes", Settings.ShowRedBorderOnLockedStrongboxes);
                ImGui.Indent(10);
                ImGui.Text("Draws a red border around strongboxes that are locked (monsters spawned but not killed)");
                ImGui.Unindent(10);
                
                if (Settings.ShowGreenBorderOnReadyStrongboxes.Value || Settings.ShowRedBorderOnLockedStrongboxes.Value)
                {
                    Settings.BorderThickness.Value = ImGuiExtension.IntSlider("Border thickness", Settings.BorderThickness);
                }
                
                ImGui.Unindent(10);
            }
            
            // Engineer's Orb Fast Apply Settings
            if (ImGui.CollapsingHeader("Engineer's Orb Fast Apply Settings"))
            {
                Settings.UseFastApplyForEngineerOrbs.Value = ImGuiExtension.Checkbox("Use Fast Apply Mode for Engineer's Orbs", Settings.UseFastApplyForEngineerOrbs);
                ImGui.Indent(10);
                ImGui.Text("Fast apply mode holds Shift to apply multiple Engineer's Orbs quickly");
                ImGui.Text("It will stop automatically when quality reaches 20% or when the strongbox is not normal rarity");
                Settings.FastApplyDelay.Value = ImGuiExtension.IntSlider("Delay between applications (ms)", Settings.FastApplyDelay);
                ImGui.Unindent(10);
            }
            
            // Mod Selection System
            bool useModSystem = Settings.UseModSelectionSystem.Value;
            if (ImGui.Checkbox("Use Mod Selection System", ref useModSystem))
            {
                Settings.UseModSelectionSystem.Value = useModSystem;
            }

            // Show either regex settings or mod selection settings based on the toggle
            if (!Settings.UseModSelectionSystem.Value)
            {
                // Original regex settings
                Settings.ModsRegex = ImGuiExtension.InputText("RegEx for mod text, I.E. 'Guarded by \\d rare monsters'. Not case sensitive", Settings.ModsRegex, 1024, ImGuiInputTextFlags.None);
                Settings.ArcanistRegex = ImGuiExtension.InputText("RegEx for Arcanist boxes (currency)", Settings.ArcanistRegex, 1024, ImGuiInputTextFlags.None);
                Settings.DivinerRegex = ImGuiExtension.InputText("RegEx for Diviner boxes", Settings.DivinerRegex, 1024, ImGuiInputTextFlags.None);
                Settings.CartogRegex = ImGuiExtension.InputText("RegEx for Cartographer boxes", Settings.CartogRegex, 1024, ImGuiInputTextFlags.None);
            }
            else
            {
                // Mod selection settings
                
                // Regular Strongboxes
                if (ImGui.CollapsingHeader("Regular Strongboxes"))
                {
                    // Upgrade to rare only option
                    bool upgradeToRareOnly = Settings.RegularUpgradeToRareOnly.Value;
                    if (ImGui.Checkbox("Upgrade to rare only (ignore mods)", ref upgradeToRareOnly))
                    {
                        Settings.RegularUpgradeToRareOnly.Value = upgradeToRareOnly;
                    }
                    
                    if (!upgradeToRareOnly)
                    {
                        ImGui.Indent(10);
                        
                        // Desired mods section - moved outside of collapsible
                        ImGui.Text("Desired Mods:");
                        ImGui.Indent(10);
                        
                        var desiredMods = ModManager.GetModsByType(SBType.Regular).Where(m => !m.IsUndesired).ToList();
                        foreach (var mod in desiredMods)
                        {
                            bool isDesired = mod.IsDesired;
                            if (ImGui.Checkbox("##desired_" + mod.Name, ref isDesired))
                            {
                                ModManager.SetModDesired(mod.Name, isDesired);
                            }
                            
                            ImGui.SameLine();
                            ImGui.Text(mod.Description);
                        }
                        
                        ImGui.Unindent(10);
                        
                        // Undesired mods section
                        if (ImGui.CollapsingHeader("Undesired Mods##Regular"))
                        {
                            // Get all potential undesired mods for this box type
                            var undesiredModCandidates = ModManager.GetModsByType(SBType.Regular)
                                .Where(m => m.Name.StartsWith("regular_") && !m.Name.Contains("_additional_") && !m.Name.Contains("_chest_level") && !m.Name.Contains("_quantity"))
                                .ToList();
                            
                            foreach (var mod in undesiredModCandidates)
                            {
                                bool isUndesired = mod.IsUndesired;
                                if (ImGui.Checkbox("##undesired_" + mod.Name, ref isUndesired))
                                {
                                    ModManager.SetModUndesired(mod.Name, isUndesired);
                                }
                                
                                ImGui.SameLine();
                                ImGui.Text(mod.Description);
                            }
                        }
                        
                        // Required mods slider - moved after undesired mods
                        int regularRequired = Settings.RegularRequiredMods.Value;
                        if (ImGui.SliderInt("Required Desired Mods##Regular", ref regularRequired, 1, 3))
                        {
                            Settings.RegularRequiredMods.Value = regularRequired;
                            ModManager.SetRequiredDesiredMods(SBType.Regular, regularRequired);
                        }
                        
                        ImGui.Unindent(10);
                    }
                }
                
                // Arcanist Strongboxes
                if (ImGui.CollapsingHeader("Arcanist Strongboxes"))
                {
                    // Alch/Scour and Engineer orb options
                    Settings.UseAlchScourForArcanist.Value = ImGuiExtension.Checkbox("Use Alch/Scour for Arcanist boxes", Settings.UseAlchScourForArcanist);
                    Settings.UseEngForArcanist.Value = ImGuiExtension.Checkbox("Use Engineer orbs on Arcanist boxes if available", Settings.UseEngForArcanist);
                    
                    ImGui.Indent(10);
                    
                    // Desired mods section - moved outside of collapsible
                    ImGui.Text("Desired Mods:");
                    ImGui.Indent(10);
                    
                    var desiredMods = ModManager.GetModsByType(SBType.Arcanist).Where(m => !m.IsUndesired).ToList();
                    foreach (var mod in desiredMods)
                    {
                        bool isDesired = mod.IsDesired;
                        if (ImGui.Checkbox("##desired_" + mod.Name, ref isDesired))
                        {
                            ModManager.SetModDesired(mod.Name, isDesired);
                        }
                        
                        ImGui.SameLine();
                        ImGui.Text(mod.Description);
                    }
                    
                    ImGui.Unindent(10);
                    
                    // Undesired mods section
                    if (ImGui.CollapsingHeader("Undesired Mods##Arcanist"))
                    {
                        // Get all potential undesired mods for this box type
                        var undesiredModCandidates = ModManager.GetModsByType(SBType.Arcanist)
                            .Where(m => m.Name.StartsWith("arcanist_") && !m.Name.Contains("_additional_") && !m.Name.Contains("_chest_level") && !m.Name.Contains("_quantity"))
                            .ToList();
                        
                        foreach (var mod in undesiredModCandidates)
                        {
                            bool isUndesired = mod.IsUndesired;
                            if (ImGui.Checkbox("##undesired_" + mod.Name, ref isUndesired))
                            {
                                ModManager.SetModUndesired(mod.Name, isUndesired);
                            }
                            
                            ImGui.SameLine();
                            ImGui.Text(mod.Description);
                        }
                    }
                    
                    // Required mods slider - moved after undesired mods
                    int arcanistRequired = Settings.ArcanistRequiredMods.Value;
                    if (ImGui.SliderInt("Required Desired Mods##Arcanist", ref arcanistRequired, 1, 3))
                    {
                        Settings.ArcanistRequiredMods.Value = arcanistRequired;
                        ModManager.SetRequiredDesiredMods(SBType.Arcanist, arcanistRequired);
                    }
                    
                    ImGui.Unindent(10);
                }
                
                // Diviner Strongboxes
                if (ImGui.CollapsingHeader("Diviner Strongboxes"))
                {
                    // Alch/Scour and Engineer orb options
                    Settings.UseAlchScourForDiviner.Value = ImGuiExtension.Checkbox("Use Alch/Scour for Diviner boxes", Settings.UseAlchScourForDiviner);
                    Settings.UseEngForDiviner.Value = ImGuiExtension.Checkbox("Use Engineer orbs on Diviner boxes if available", Settings.UseEngForDiviner);
                    
                    ImGui.Indent(10);
                    
                    // Desired mods section - moved outside of collapsible
                    ImGui.Text("Desired Mods:");
                    ImGui.Indent(10);
                    
                    var desiredMods = ModManager.GetModsByType(SBType.Diviner).Where(m => !m.IsUndesired).ToList();
                    foreach (var mod in desiredMods)
                    {
                        bool isDesired = mod.IsDesired;
                        if (ImGui.Checkbox("##desired_" + mod.Name, ref isDesired))
                        {
                            ModManager.SetModDesired(mod.Name, isDesired);
                        }
                        
                        ImGui.SameLine();
                        ImGui.Text(mod.Description);
                    }
                    
                    ImGui.Unindent(10);
                    
                    // Undesired mods section
                    if (ImGui.CollapsingHeader("Undesired Mods##Diviner"))
                    {
                        // Get all potential undesired mods for this box type
                        var undesiredModCandidates = ModManager.GetModsByType(SBType.Diviner)
                            .Where(m => m.Name.StartsWith("diviner_") && !m.Name.Contains("_additional_") && !m.Name.Contains("_chest_level") && !m.Name.Contains("_quantity") && !m.Name.Contains("_corrupted") && !m.Name.Contains("_currency") && !m.Name.Contains("_unique"))
                            .ToList();
                        
                        foreach (var mod in undesiredModCandidates)
                        {
                            bool isUndesired = mod.IsUndesired;
                            if (ImGui.Checkbox("##undesired_" + mod.Name, ref isUndesired))
                            {
                                ModManager.SetModUndesired(mod.Name, isUndesired);
                            }
                            
                            ImGui.SameLine();
                            ImGui.Text(mod.Description);
                        }
                    }
                    
                    // Required mods slider - moved after undesired mods
                    int divinerRequired = Settings.DivinerRequiredMods.Value;
                    if (ImGui.SliderInt("Required Desired Mods##Diviner", ref divinerRequired, 1, 3))
                    {
                        Settings.DivinerRequiredMods.Value = divinerRequired;
                        ModManager.SetRequiredDesiredMods(SBType.Diviner, divinerRequired);
                    }
                    
                    ImGui.Unindent(10);
                }
                
                // Cartographer Strongboxes
                if (ImGui.CollapsingHeader("Cartographer Strongboxes"))
                {
                    // Alch/Scour and Engineer orb options
                    Settings.UseAlchScourForCartog.Value = ImGuiExtension.Checkbox("Use Alch/Scour for Cartographer boxes", Settings.UseAlchScourForCartog);
                    Settings.UseEngForCartog.Value = ImGuiExtension.Checkbox("Use Engineer orbs on Cartographer boxes if available", Settings.UseEngForCartog);
                    
                    ImGui.Indent(10);
                    
                    // Desired mods section - moved outside of collapsible
                    ImGui.Text("Desired Mods:");
                    ImGui.Indent(10);
                    
                    var desiredMods = ModManager.GetModsByType(SBType.Cartographer).Where(m => !m.IsUndesired).ToList();
                    foreach (var mod in desiredMods)
                    {
                        bool isDesired = mod.IsDesired;
                        if (ImGui.Checkbox("##desired_" + mod.Name, ref isDesired))
                        {
                            ModManager.SetModDesired(mod.Name, isDesired);
                        }
                        
                        ImGui.SameLine();
                        ImGui.Text(mod.Description);
                    }
                    
                    ImGui.Unindent(10);
                    
                    // Undesired mods section
                    if (ImGui.CollapsingHeader("Undesired Mods##Cartographer"))
                    {
                        // Get all potential undesired mods for this box type
                        var undesiredModCandidates = ModManager.GetModsByType(SBType.Cartographer)
                            .Where(m => m.Name.StartsWith("cartographer_") && !m.Name.Contains("_map_") && !m.Name.Contains("_unique") && !m.Name.Contains("_rarity") && !m.Name.Contains("_quantity") && !m.Name.Contains("_identified") && !m.Name.Contains("_additional_") && !m.Name.Contains("_magic_item") && !m.Name.Contains("_rare_item") && !m.Name.Contains("_quality") && !m.Name.Contains("_chest_level"))
                            .ToList();
                        
                        foreach (var mod in undesiredModCandidates)
                        {
                            bool isUndesired = mod.IsUndesired;
                            if (ImGui.Checkbox("##undesired_" + mod.Name, ref isUndesired))
                            {
                                ModManager.SetModUndesired(mod.Name, isUndesired);
                            }
                            
                            ImGui.SameLine();
                            ImGui.Text(mod.Description);
                        }
                    }
                    
                    // Required mods slider - moved after undesired mods
                    int cartographerRequired = Settings.CartographerRequiredMods.Value;
                    if (ImGui.SliderInt("Required Desired Mods##Cartographer", ref cartographerRequired, 1, 3))
                    {
                        Settings.CartographerRequiredMods.Value = cartographerRequired;
                        ModManager.SetRequiredDesiredMods(SBType.Cartographer, cartographerRequired);
                    }
                    
                    ImGui.Unindent(10);
                }
            }

            // Stash crafting settings
            Settings.EnableStashCrafting.Value = ImGuiExtension.Checkbox("Enable stash crafting (WIP)", Settings.EnableStashCrafting);
            Settings.StashCraftingStartHotKey = ImGuiExtension.HotkeySelector("Craft box Key: " + Settings.StashCraftingStartHotKey.Value.ToString(), Settings.StashCraftingStartHotKey);
            Settings.StashCraftingRegex = ImGuiExtension.InputText("RegEx for stash crafting: ", Settings.StashCraftingRegex, 1024, ImGuiInputTextFlags.None);
        }
        
        private bool _showModSelectionUI = false;
        
        public override void Render()
        {
            base.Render();
            
            // Draw all borders from the drawing list
            foreach (var border in StrongboxBorderDrawList)
            {
                Graphics.DrawFrame(border.Item1, border.Item2, border.Item3);
            }
        }
        
        // Method to update the strongbox label cache
        private List<LabelOnGround> UpdateStrongboxLabelList()
        {
            if (GameController.Game.IngameState.IngameUi.ItemsOnGroundLabelsVisible.Count == 0)
                return new List<LabelOnGround>();
                
            return GameController.Game.IngameState.IngameUi.ItemsOnGroundLabelsVisible
                .Where(x => 
                    x.ItemOnGround?.Metadata?.Contains("Metadata/Chests/StrongBoxes") == true && 
                    !x.ItemOnGround.IsOpened && 
                    x.ItemOnGround.DistancePlayer < 100)
                .ToList();
        }

        internal IEnumerator MainWorkCoroutine()
        {
            while (true)
            {
                yield return FindSBToFix();

                coroutineCounter++;
                pickItCoroutine.UpdateTicks(coroutineCounter);
                yield return _workCoroutine;
            }
        }

        public override Job Tick()
        {
            List<string> toLog = new();
            //if (Settings.BoxCraftingUseAltsAugs && !CraftingManager.GetTransmutesFromInv().Any())
            //{
            //    toLog.Add("Trying to craft but no Orbs of Transmutation found in inventory.");

            //}
            //if (Settings.BoxCraftingUseAltsAugs && !CraftingManager.GetAltsFromInv().Any())
            //{
            //    toLog.Add("Trying to craft but no Orbs of Alteration found in inventory.");
            //}
            //if (Settings.BoxCraftingUseAltsAugs && !CraftingManager.GetAugsFromInv().Any())
            //{
            //    toLog.Add("Trying to craft but no Orbs of Augmentation found in inventory.");
            //}
            //if (!CraftingManager.GetScoursFromInv().Any())
            //{
            //    toLog.Add("Trying to craft but no Orbs of Scouring found in inventory.");
            //}
            //if (
            //    (
            //        Settings.UseAlchScourForArcanist||
            //        Settings.UseAlchScourForDiviner||
            //        Settings.UseAlchScourForCartog ||
            //        !Settings.BoxCraftingUseAltsAugs)
            //    && !CraftingManager.GetAlchsFromInv().Any())
            //{
            //    toLog.Add("Trying to craft but no Orbs of Alchemy found in inventory.");
            //}
            //for (int i = 0; i < toLog.Count; i++)
            //{
            //    DrawText(toLog[i], i * 20);
            //}
            InventoryItems = GameController.Game.IngameState.ServerData.PlayerInventories[0].Inventory;
            inventorySlots = Misc.GetContainer2DArray(InventoryItems);

            if (Input.GetKeyState(Settings.LazyLootingPauseKey)) DisableLazyLootingTill = DateTime.Now.AddSeconds(2);
            if (Input.GetKeyState(Keys.Escape))
            {
                FullWork = true;
                pickItCoroutine.Pause();
            }

            if (Input.GetKeyState(Settings.CraftBoxKey.Value))
            {
                DebugTimer.Restart();

                if (pickItCoroutine.IsDone)
                {
                    var firstOrDefault = Core.ParallelRunner.Coroutines.FirstOrDefault(x => x.OwnerName == nameof(StrongboxRolling));

                    if (firstOrDefault != null)
                        pickItCoroutine = firstOrDefault;
                }

                pickItCoroutine.Resume();
                FullWork = false;
            }
            else if (Input.GetKeyState(Settings.StashCraftingStartHotKey.Value))
            {
                DebugTimer.Restart();

                if (pickItCoroutine.IsDone)
                {
                    var firstOrDefault = Core.ParallelRunner.Coroutines.FirstOrDefault(x => x.OwnerName == nameof(StrongboxRolling));

                    if (firstOrDefault != null)
                        pickItCoroutine = firstOrDefault;
                }

                pickItCoroutine.Resume();
                FullWork = false;
            }
            else
            {
                if (FullWork && pickItCoroutine != null)
                {
                    pickItCoroutine.Pause();
                    DebugTimer.Reset();
                }
            }

            if (DebugTimer.ElapsedMilliseconds > 300)
            {
                //FullWork = true;
                //LogMessage("Error pick it stop after time limit 300 ms", 1);
                DebugTimer.Reset();
            }
            //Graphics.DrawText($@"PICKIT :: Debug Tick Timer ({DebugTimer.ElapsedMilliseconds}ms)", new Vector2(100, 100), FontAlign.Left);
            //DebugTimer.Reset();

            // Clear the drawing list at the start of each tick
            StrongboxBorderDrawList.Clear();
            
            // Only process if the plugin is enabled and border drawing is enabled
            if (Settings.Enable.Value && (Settings.ShowGreenBorderOnReadyStrongboxes.Value || Settings.ShowRedBorderOnLockedStrongboxes.Value))
            {
                ProcessStrongboxBorders();
            }

            return null;
        }
        
        // Method to process strongbox borders and prepare drawing data
        private void ProcessStrongboxBorders()
        {
            try
            {
                // Get all visible strongbox labels from cache
                var strongboxLabels = StrongboxLabelCache.Value;
                
                // Check each strongbox
                foreach (var label in strongboxLabels)
                {
                    // Get the label's rectangle
                    var rect = label.Label.GetClientRectCache;
                    
                    // Check if the strongbox is locked (monsters spawned but not killed)
                    bool isLocked = false;
                    if (Settings.ShowRedBorderOnLockedStrongboxes.Value)
                    {
                        // Try to get the Chest component to check if it's locked
                        var chestComponent = label.ItemOnGround.GetComponent<ExileCore.PoEMemory.Components.Chest>();
                        if (chestComponent != null && chestComponent.IsLocked)
                        {
                            // Add a red border to the drawing list
                            StrongboxBorderDrawList.Add(new Tuple<RectangleF, Color, int>(rect, Color.Red, Settings.BorderThickness.Value));
                            isLocked = true;
                        }
                    }
                    
                    // If not locked, check if it's ready for crafting
                    if (!isLocked && Settings.ShowGreenBorderOnReadyStrongboxes.Value)
                    {
                        bool isReady = false;
                        var now = DateTime.Now;

                        // Check if we have a cached result that's still valid
                        if (StrongboxModCheckCache.TryGetValue(label, out var cachedResult))
                        {
                            if ((now - cachedResult.lastCheck).TotalMilliseconds < ModCheckCacheDurationMs)
                            {
                                isReady = cachedResult.isReady;
                            }
                            else
                            {
                                // Cache expired, perform new check
                                isReady = CraftingManager.IsStrongboxReady(label);
                                StrongboxModCheckCache[label] = (isReady, now);
                            }
                        }
                        else
                        {
                            // No cache entry, perform new check
                            isReady = CraftingManager.IsStrongboxReady(label);
                            StrongboxModCheckCache[label] = (isReady, now);
                        }

                        if (isReady)
                        {
                            // Add a green border to the drawing list
                            StrongboxBorderDrawList.Add(new Tuple<RectangleF, Color, int>(rect, Color.Green, Settings.BorderThickness.Value));
                        }
                    }
                }

                // Clean up cache entries for labels that are no longer visible
                var visibleLabels = new HashSet<LabelOnGround>(strongboxLabels);
                var keysToRemove = StrongboxModCheckCache.Keys.Where(k => !visibleLabels.Contains(k)).ToList();
                foreach (var key in keysToRemove)
                {
                    StrongboxModCheckCache.Remove(key);
                }
            }
            catch (Exception ex)
            {
                LogError($"Error in ProcessStrongboxBorders: {ex.Message}");
            }
        }

        public void DrawText(string text, int offset)
        {
            Graphics.DrawTextWithBackground(text, new(50, 100 + offset), Color.Crimson, Color.Black);
        }
        //TODO: Make function pretty





        public override void ReceiveEvent(string eventId, object args)
        {
            if (!Settings.Enable.Value) return;

            if (eventId == "frsm_display_data")
            {

                var argSerialised = JsonConvert.SerializeObject(args);
                FullRareSetManagerData = JsonConvert.DeserializeObject<FRSetManagerPublishInformation>(argSerialised);
            }
        }
        internal IEnumerator FindSBToFix()
        {
            if (!GameController.Window.IsForeground()) yield break;
            var window = GameController.Window.GetWindowRectangleTimeCache;
            var rect = new RectangleF(window.X, window.X, window.X + window.Width, window.Y + window.Height);
            var playerPos = GameController.Player.GridPos;
            var items = InventoryItems;

            List<CustomItem> currentLabels;
            var morphPath = "Metadata/MiscellaneousObjects/Metamorphosis/MetamorphosisMonsterMarker";
            List<string> labelsToLog = new();







            if (!FullWork)
            {
                if (IngameState.pTheGame.IngameState.IngameUi.StashElement.IsVisibleLocal)
                {
                    yield return (TryToCraftFromStash());
                }
                else
                {
                    yield return TryToCraftSB(GetClosestChest());
                }

                //FullWork = true;
            }
        }
        public LabelOnGround GetClosestChest()
        {
            IList<ExileCore.PoEMemory.Elements.LabelOnGround> otherLabels = GameController.Game.IngameState.IngameUi.ItemsOnGroundLabelsVisible;
            return otherLabels.Where(x => ((bool)x.ItemOnGround?.Metadata?.Contains("Metadata/Chests/StrongBoxes") && !x.ItemOnGround.IsOpened && x.ItemOnGround.DistancePlayer < 70)).OrderBy(x => x.ItemOnGround.DistancePlayer).MinBy(x => x.ItemOnGround.DistancePlayer);
        }




        /// <summary>
        /// LazyLoot item independent checks
        /// </summary>
        /// <returns></returns>


        /// <summary>
        /// LazyLoot item dependent checks
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        internal bool ShouldLazyLoot(CustomItem item)
        {
            var itemPos = item.LabelOnGround.ItemOnGround.Pos;
            var playerPos = GameController.Player.Pos;
            if (Math.Abs(itemPos.Z - playerPos.Z) > 50) return false;
            var dx = itemPos.X - playerPos.X;
            var dy = itemPos.Y - playerPos.Y;
            if (dx * dx + dy * dy > 275 * 275) return false;

            if (item.IsElder || item.IsFractured || item.IsShaper ||
                item.IsHunter || item.IsCrusader || item.IsRedeemer || item.IsWarlord || item.IsHeist)
                return true;

            if (item.Rarity == MonsterRarity.Rare && item.Width * item.Height > 1) return false;

            return true;
        }


        internal IEnumerator TryToCraftFromStash()
        {


            Entity? item = StashCraftingManager.GetItemInCraftingZone("$");
            if (item is null)
            {
                FullWork = true;
                yield break;
            }
            bool val = StashCraftingManager.CraftStep(new Regex(Settings.StashCraftingRegex),item);
            if (val)
            {
                FullWork = true;
                yield break;
            }

            //yield return waitForNextTry;

            //   Mouse.MoveCursorToPosition(oldMousePosition);
        }

        internal IEnumerator TryToCraftSB(LabelOnGround sbLabel)
        {





            if (sbLabel is null || sbLabel.Label is null)
            {
                FullWork = true;
                yield break;
            }


            var centerOfItemLabel = sbLabel.Label.GetClientRectCache.Center;
            var rectangleOfGameWindow = GameController.Window.GetWindowRectangleTimeCache;

            var oldMousePosition = Mouse.GetCursorPositionVector();
            _clickWindowOffset = rectangleOfGameWindow.TopLeft;
            rectangleOfGameWindow.Inflate(-36, -36);
            centerOfItemLabel.X += rectangleOfGameWindow.Left;
            centerOfItemLabel.Y += rectangleOfGameWindow.Top;
            if (!rectangleOfGameWindow.Intersects(new RectangleF(centerOfItemLabel.X, centerOfItemLabel.Y, 3, 3)))
            {
                //FullWork = true;
                //LogMessage($"Label outside game window. Label: {centerOfItemLabel} Window: {rectangleOfGameWindow}", 5, Color.Red);
                yield break;
            }

            var tryCount = 0;

            var completeItemLabel = sbLabel?.Label;

            if (completeItemLabel == null)
            {
                if (tryCount > 0)
                {
                    //LogMessage("Probably item already picked.", 3);
                    yield break;
                }

                //LogError("Label for item not found.", 5);
                yield break;
            }

            //while (GameController.Player.GetComponent<Actor>().isMoving)
            //{
            //    yield return waitPlayerMove;
            //}
            var clientRect = completeItemLabel.GetClientRect();

            var clientRectCenter = clientRect.Center;

            var vector2 = clientRectCenter + _clickWindowOffset;

            if (!rectangleOfGameWindow.Intersects(new RectangleF(vector2.X, vector2.Y, 3, 3)))
            {
                FullWork = true;
                //LogMessage($"x,y outside game window. Label: {centerOfItemLabel} Window: {rectangleOfGameWindow}", 5, Color.Red);
                yield break;
            }



            yield return CraftingManager.CraftBox(sbLabel);


            //yield return waitForNextTry;

            //   Mouse.MoveCursorToPosition(oldMousePosition);
        }

        internal Vector2 GetPos(InventSlotItem l)
        {
            Vector2 centerOfItemLabel = l.GetClientRect().TopLeft;
            RectangleF rectangleOfGameWindow = GameController.Window.GetWindowRectangleTimeCache;

            var oldMousePosition = Mouse.GetCursorPositionVector();
            _clickWindowOffset = rectangleOfGameWindow.TopLeft;
            rectangleOfGameWindow.Inflate(-36, -36);
            centerOfItemLabel.X += rectangleOfGameWindow.Left;
            centerOfItemLabel.Y += rectangleOfGameWindow.Top;
            return centerOfItemLabel;
        }
        internal Vector2 GetPos(Entity l)
        {
            RenderItem pos = l.GetComponent<RenderItem>();
            
            Vector2 centerOfItemLabel = l.GridPos;
            RectangleF rectangleOfGameWindow = GameController.Window.GetWindowRectangleTimeCache;

            var oldMousePosition = Mouse.GetCursorPositionVector();
            _clickWindowOffset = rectangleOfGameWindow.TopLeft;
            rectangleOfGameWindow.Inflate(-36, -36);
            centerOfItemLabel.X += rectangleOfGameWindow.Left;
            centerOfItemLabel.Y += rectangleOfGameWindow.Top;
            return centerOfItemLabel;
        }
        internal Vector2 GetPos(LabelOnGround l)
        {
            Vector2 botLeftOfLabel = l.Label.GetClientRect().BottomLeft;
            Vector2 centerOfLabel = l.Label.GetClientRect().Center;
            RectangleF rectangleOfGameWindow = GameController.Window.GetWindowRectangleTimeCache;

            var oldMousePosition = Mouse.GetCursorPositionVector();
            _clickWindowOffset = rectangleOfGameWindow.TopLeft;
            rectangleOfGameWindow.Inflate(-36, -36);
            botLeftOfLabel.X += rectangleOfGameWindow.Left;
            botLeftOfLabel.Y += rectangleOfGameWindow.Top;
            float prevX = botLeftOfLabel.X;
            float prevY = botLeftOfLabel.Y;

            return botLeftOfLabel with { X = centerOfLabel.X + 10, Y = prevY - 60 };
        }





        public HashSet<string> LoadPickit(string fileName)
        {
            var hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (fileName == string.Empty)
            {
                return hashSet;
            }

            var pickitFile = $@"{DirectoryFullName}\{StrongboxRollingRuleDirectory}\{fileName}.txt";

            if (!File.Exists(pickitFile))
            {
                return hashSet;
            }

            var lines = File.ReadAllLines(pickitFile);

            foreach (var x in lines.Where(x => !string.IsNullOrWhiteSpace(x) && !x.StartsWith("#")))
            {
                hashSet.Add(x.Trim());
            }

            LogMessage($"PICKIT :: (Re)Loaded {fileName}", 5, Color.GreenYellow);
            return hashSet;
        }



        public override void OnPluginDestroyForHotReload()
        {
            pickItCoroutine.Done(true);
        }



        #region Adding / Removing Entities

        public override void EntityAdded(Entity Entity)
        {
        }

        public override void EntityRemoved(Entity Entity)
        {
        }

        #endregion

        internal DateTime DisableLazyLootingTill { get; set; }

        // Add a method for clearing the debug log file
        private void ClearDebugLogFile(string message = null)
        {
            try
            {
                string logFilePath = Settings.DebugLogFilePath;
                string logMessage = message ?? $"Debug log initialized at {DateTime.Now}";
                File.WriteAllText(logFilePath, $"{logMessage}\n");
                LogMessage($"Debug log file cleared: {logFilePath}", 5);
            }
            catch (Exception ex)
            {
                LogError($"Failed to clear debug log file: {ex.Message}");
            }
        }
    }
}
