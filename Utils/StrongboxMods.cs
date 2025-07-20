using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace StrongboxRolling.Utils
{
    public class StrongboxMod
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public SBType BoxType { get; set; }
        public bool IsDesired { get; set; }
        public bool IsUndesired { get; set; }

        public StrongboxMod(string name, string description, SBType boxType, bool isDesired = false, bool isUndesired = false)
        {
            Name = name;
            Description = description;
            BoxType = boxType;
            IsDesired = isDesired;
            IsUndesired = isUndesired;
        }
    }

    public class ModSelectionManager
    {
        private readonly string _modsFilePath;
        private List<StrongboxMod> _allMods;
        private Dictionary<SBType, int> _requiredDesiredMods;
        
        // Add debug toggle property
        public bool EnableDebugLogging { get; set; } = false;
        
        // Add property for debug log file path
        public string DebugLogFilePath { get; set; } = "./ModDebug.txt";
        
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
                    using (StreamWriter writer = File.AppendText(DebugLogFilePath))
                    {
                        writer.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [ModSelectionManager] {message}");
                    }
                }
                catch (Exception)
                {
                    // Silently fail if we can't write to the log file
                }
            }
        }
        
        public ModSelectionManager(string modsFilePath)
        {
            _modsFilePath = modsFilePath;
            _allMods = new List<StrongboxMod>();
            _requiredDesiredMods = new Dictionary<SBType, int>();
            
            // Initialize with default values
            _requiredDesiredMods[SBType.Regular] = 1;
            _requiredDesiredMods[SBType.Arcanist] = 1;
            _requiredDesiredMods[SBType.Diviner] = 1;
            _requiredDesiredMods[SBType.Cartographer] = 1;
            
            LoadModsFile();
        }

        private void LoadModsFile()
        {
            if (File.Exists(_modsFilePath))
            {
                try
                {
                    string json = File.ReadAllText(_modsFilePath);
                    var savedData = JsonConvert.DeserializeObject<SavedModData>(json);
                    _allMods = savedData.Mods;
                    _requiredDesiredMods = savedData.RequiredDesiredMods;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading mods file: {ex.Message}");
                    // Create default mods if file doesn't exist or is invalid
                    CreateDefaultMods();
                }
            }
            else
            {
                CreateDefaultMods();
            }
        }

        private void CreateDefaultMods()
        {
            _allMods = new List<StrongboxMod>();

            // Arcanist Strongbox Mods
            // Desired mods - initially checked
            _allMods.Add(new StrongboxMod("arcanist_quantity", "increased Quantity of Contained Items", SBType.Arcanist, true, false));
            _allMods.Add(new StrongboxMod("arcanist_additional_items", "additional Items", SBType.Arcanist, true, false));
            _allMods.Add(new StrongboxMod("arcanist_chest_level", "+1 Chest level", SBType.Arcanist, true, false));

            // Undesired mods - initially unchecked
            _allMods.Add(new StrongboxMod("arcanist_freezes", "Freezes you when activated", SBType.Arcanist, false, false));
            _allMods.Add(new StrongboxMod("arcanist_explodes", "Explodes", SBType.Arcanist, false, false));
            _allMods.Add(new StrongboxMod("arcanist_caustic", "Spreads Caustic Ground", SBType.Arcanist, false, false));
            _allMods.Add(new StrongboxMod("arcanist_lightning", "Casts Lightning Storm", SBType.Arcanist, false, false));
            _allMods.Add(new StrongboxMod("arcanist_firestorm", "Casts Firestorm", SBType.Arcanist, false, false));
            _allMods.Add(new StrongboxMod("arcanist_ignites", "Ignites you when activated", SBType.Arcanist, false, false));
            _allMods.Add(new StrongboxMod("arcanist_curse", "Casts a random Hex Curse Spell when activated", SBType.Arcanist, false, false));
            _allMods.Add(new StrongboxMod("arcanist_stream", "Guarded by a stream of Monsters", SBType.Arcanist, false, false));
            _allMods.Add(new StrongboxMod("arcanist_skeletons", "Summons Skeletons", SBType.Arcanist, false, false));
            _allMods.Add(new StrongboxMod("arcanist_ice_nova", "Casts Ice Nova", SBType.Arcanist, false, false));
            _allMods.Add(new StrongboxMod("arcanist_rare_monsters", "Guarded by 3 Rare Monsters", SBType.Arcanist, false, false));
            _allMods.Add(new StrongboxMod("arcanist_magic_monsters", "Guarded by a pack of Magic Monsters", SBType.Arcanist, false, false));
            _allMods.Add(new StrongboxMod("arcanist_exile", "Guarded by a Rogue Exile", SBType.Arcanist, false, false));
            _allMods.Add(new StrongboxMod("arcanist_corpses", "Detonates nearby corpses", SBType.Arcanist, false, false));
            _allMods.Add(new StrongboxMod("arcanist_revives", "Revives nearby dead Monsters with Onslaught", SBType.Arcanist, false, false));

            // Diviner Strongbox Mods
            // Desired mods - initially checked
            _allMods.Add(new StrongboxMod("diviner_additional_items", "additional Items", SBType.Diviner, true, false));
            _allMods.Add(new StrongboxMod("diviner_chest_level", "+1 Chest level", SBType.Diviner, true, false));
            _allMods.Add(new StrongboxMod("diviner_quantity", "increased Quantity of Contained Items", SBType.Diviner, true, false));
            _allMods.Add(new StrongboxMod("diviner_corrupted", "additional Divination Cards that give Corrupted Items", SBType.Diviner, true, false));
            _allMods.Add(new StrongboxMod("diviner_currency", "additional Divination Cards that give Currency", SBType.Diviner, true, false));
            _allMods.Add(new StrongboxMod("diviner_unique", "additional Divination Cards that give Unique Items", SBType.Diviner, true, false));

            // Undesired mods - initially unchecked
            _allMods.Add(new StrongboxMod("diviner_freezes", "Freezes you when activated", SBType.Diviner, false, false));
            _allMods.Add(new StrongboxMod("diviner_explodes", "Explodes", SBType.Diviner, false, false));
            _allMods.Add(new StrongboxMod("diviner_caustic", "Spreads Caustic Ground", SBType.Diviner, false, false));
            _allMods.Add(new StrongboxMod("diviner_lightning", "Casts Lightning Storm", SBType.Diviner, false, false));
            _allMods.Add(new StrongboxMod("diviner_firestorm", "Casts Firestorm", SBType.Diviner, false, false));
            _allMods.Add(new StrongboxMod("diviner_ignites", "Ignites you when activated", SBType.Diviner, false, false));
            _allMods.Add(new StrongboxMod("diviner_curse", "Casts a random Hex Curse Spell when activated", SBType.Diviner, false, false));
            _allMods.Add(new StrongboxMod("diviner_stream", "Guarded by a stream of Monsters", SBType.Diviner, false, false));
            _allMods.Add(new StrongboxMod("diviner_skeletons", "Summons Skeletons", SBType.Diviner, false, false));
            _allMods.Add(new StrongboxMod("diviner_ice_nova", "Casts Ice Nova", SBType.Diviner, false, false));
            _allMods.Add(new StrongboxMod("diviner_rare_monsters", "Guarded by 3 Rare Monsters", SBType.Diviner, false, false));
            _allMods.Add(new StrongboxMod("diviner_magic_monsters", "Guarded by a pack of Magic Monsters", SBType.Diviner, false, false));
            _allMods.Add(new StrongboxMod("diviner_exile", "Guarded by a Rogue Exile", SBType.Diviner, false, false));
            _allMods.Add(new StrongboxMod("diviner_corpses", "Detonates nearby corpses", SBType.Diviner, false, false));
            _allMods.Add(new StrongboxMod("diviner_revives", "Revives nearby dead Monsters with Onslaught", SBType.Diviner, false, false));

            // Cartographer Strongbox Mods
            // Desired mods - initially checked
            _allMods.Add(new StrongboxMod("cartographer_map_currency", "additional Map Currency Items", SBType.Cartographer, true, false));
            _allMods.Add(new StrongboxMod("cartographer_unique", "additional Unique Item", SBType.Cartographer, true, false));
            _allMods.Add(new StrongboxMod("cartographer_rarity", "more Rarity of Contained Items", SBType.Cartographer, true, false));
            _allMods.Add(new StrongboxMod("cartographer_quantity", "increased Quantity of Contained Items", SBType.Cartographer, true, false));
            _allMods.Add(new StrongboxMod("cartographer_identified", "Contains Identified Items", SBType.Cartographer, true, false));
            _allMods.Add(new StrongboxMod("cartographer_additional_item", "additional Item", SBType.Cartographer, true, false));
            _allMods.Add(new StrongboxMod("cartographer_magic_item", "additional Magic Item", SBType.Cartographer, true, false));
            _allMods.Add(new StrongboxMod("cartographer_rare_item", "additional Rare Item", SBType.Cartographer, true, false));
            _allMods.Add(new StrongboxMod("cartographer_quality", "Quality", SBType.Cartographer, true, false));
            _allMods.Add(new StrongboxMod("cartographer_chest_level", "+1 Chest level", SBType.Cartographer, true, false));

            // Undesired mods - initially unchecked
            _allMods.Add(new StrongboxMod("cartographer_freezes", "Freezes you when activated", SBType.Cartographer, false, false));
            _allMods.Add(new StrongboxMod("cartographer_explodes", "Explodes", SBType.Cartographer, false, false));
            _allMods.Add(new StrongboxMod("cartographer_caustic", "Spreads Caustic Ground", SBType.Cartographer, false, false));
            _allMods.Add(new StrongboxMod("cartographer_lightning", "Casts Lightning Storm", SBType.Cartographer, false, false));
            _allMods.Add(new StrongboxMod("cartographer_firestorm", "Casts Firestorm", SBType.Cartographer, false, false));
            _allMods.Add(new StrongboxMod("cartographer_ignites", "Ignites you when activated", SBType.Cartographer, false, false));
            _allMods.Add(new StrongboxMod("cartographer_curse", "Casts a random Hex Curse Spell when activated", SBType.Cartographer, false, false));
            _allMods.Add(new StrongboxMod("cartographer_stream", "Guarded by a stream of Monsters", SBType.Cartographer, false, false));
            _allMods.Add(new StrongboxMod("cartographer_skeletons", "Summons Skeletons", SBType.Cartographer, false, false));
            _allMods.Add(new StrongboxMod("cartographer_ice_nova", "Casts Ice Nova", SBType.Cartographer, false, false));
            _allMods.Add(new StrongboxMod("cartographer_rare_monsters", "Guarded by 3 Rare Monsters", SBType.Cartographer, false, false));
            _allMods.Add(new StrongboxMod("cartographer_magic_monsters", "Guarded by a pack of Magic Monsters", SBType.Cartographer, false, false));
            _allMods.Add(new StrongboxMod("cartographer_exile", "Guarded by a Rogue Exile", SBType.Cartographer, false, false));
            _allMods.Add(new StrongboxMod("cartographer_corpses", "Detonates nearby corpses", SBType.Cartographer, false, false));
            _allMods.Add(new StrongboxMod("cartographer_revives", "Revives nearby dead Monsters with Onslaught", SBType.Cartographer, false, false));

            // Regular Strongbox Mods
            // Desired mods - initially checked
            _allMods.Add(new StrongboxMod("regular_additional_items", "additional Items", SBType.Regular, true, false));
            _allMods.Add(new StrongboxMod("regular_chest_level", "+1 Chest level", SBType.Regular, true, false));
            _allMods.Add(new StrongboxMod("regular_quantity", "increased Quantity of Contained Items", SBType.Regular, true, false));
            _allMods.Add(new StrongboxMod("regular_rare_items", "additional Rare Items", SBType.Regular, true, false));
            _allMods.Add(new StrongboxMod("regular_sockets", "additional Sockets", SBType.Regular, true, false));
            _allMods.Add(new StrongboxMod("regular_magic_items", "additional Magic Items", SBType.Regular, true, false));
            _allMods.Add(new StrongboxMod("regular_mirrored", "Mirrored Items", SBType.Regular, true, false));
            _allMods.Add(new StrongboxMod("regular_quality", "Quality", SBType.Regular, true, false));
            _allMods.Add(new StrongboxMod("regular_unique", "additional Unique Item", SBType.Regular, true, false));
            _allMods.Add(new StrongboxMod("regular_rarity", "more Rarity of Contained Items", SBType.Regular, true, false));
            _allMods.Add(new StrongboxMod("regular_linked", "fully Linked", SBType.Regular, true, false));
            _allMods.Add(new StrongboxMod("regular_identified", "Identified Items", SBType.Regular, true, false));
            _allMods.Add(new StrongboxMod("regular_scarabs", "additional Scarabs", SBType.Regular, true, false));

            // Undesired mods - initially unchecked
            _allMods.Add(new StrongboxMod("regular_freezes", "Freezes you when activated", SBType.Regular, false, false));
            _allMods.Add(new StrongboxMod("regular_explodes", "Explodes", SBType.Regular, false, false));
            _allMods.Add(new StrongboxMod("regular_caustic", "Spreads Caustic Ground", SBType.Regular, false, false));
            _allMods.Add(new StrongboxMod("regular_lightning", "Casts Lightning Storm", SBType.Regular, false, false));
            _allMods.Add(new StrongboxMod("regular_firestorm", "Casts Firestorm", SBType.Regular, false, false));
            _allMods.Add(new StrongboxMod("regular_ignites", "Ignites you when activated", SBType.Regular, false, false));
            _allMods.Add(new StrongboxMod("regular_curse", "Casts a random Hex Curse Spell when activated", SBType.Regular, false, false));
            _allMods.Add(new StrongboxMod("regular_stream", "Guarded by a stream of Monsters", SBType.Regular, false, false));
            _allMods.Add(new StrongboxMod("regular_skeletons", "Summons Skeletons", SBType.Regular, false, false));
            _allMods.Add(new StrongboxMod("regular_ice_nova", "Casts Ice Nova", SBType.Regular, false, false));
            _allMods.Add(new StrongboxMod("regular_rare_monsters", "Guarded by 3 Rare Monsters", SBType.Regular, false, false));
            _allMods.Add(new StrongboxMod("regular_magic_monsters", "Guarded by a pack of Magic Monsters", SBType.Regular, false, false));
            _allMods.Add(new StrongboxMod("regular_exile", "Guarded by a Rogue Exile", SBType.Regular, false, false));
            _allMods.Add(new StrongboxMod("regular_corpses", "Detonates nearby corpses", SBType.Regular, false, false));
            _allMods.Add(new StrongboxMod("regular_revives", "Revives nearby dead Monsters with Onslaught", SBType.Regular, false, false));

            SaveModsFile();
        }

        private void SaveModsFile()
        {
            try
            {
                string directory = Path.GetDirectoryName(_modsFilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var dataToSave = new SavedModData
                {
                    Mods = _allMods,
                    RequiredDesiredMods = _requiredDesiredMods
                };

                string json = JsonConvert.SerializeObject(dataToSave, Formatting.Indented);
                File.WriteAllText(_modsFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving mods file: {ex.Message}");
            }
        }

        public List<StrongboxMod> GetAllMods()
        {
            return _allMods;
        }

        public List<StrongboxMod> GetModsByType(SBType boxType)
        {
            return _allMods.Where(m => m.BoxType == boxType).ToList();
        }

        public void SetModDesired(string modName, bool isDesired)
        {
            var mod = _allMods.FirstOrDefault(m => m.Name == modName);
            if (mod != null)
            {
                mod.IsDesired = isDesired;
                if (isDesired)
                {
                    mod.IsUndesired = false;
                }
                SaveModsFile();
            }
        }

        public void SetModUndesired(string modName, bool isUndesired)
        {
            var mod = _allMods.FirstOrDefault(m => m.Name == modName);
            if (mod != null)
            {
                // Only update the IsUndesired property, don't touch IsDesired
                mod.IsUndesired = isUndesired;
                SaveModsFile();
            }
        }

        public int GetRequiredDesiredMods(SBType boxType)
        {
            if (_requiredDesiredMods.TryGetValue(boxType, out int count))
            {
                return count;
            }
            return 1; // Default to 1 if not set
        }

        public void SetRequiredDesiredMods(SBType boxType, int count)
        {
            _requiredDesiredMods[boxType] = count;
            SaveModsFile();
        }

        public bool CheckStrongboxMods(string[] modTexts, SBType boxType)
        {
            int desiredModsFound = 0;
            
            // Get all mods for this box type
            var boxMods = GetModsByType(boxType);
            
            // Debug logging - only if enabled
            if (EnableDebugLogging)
            {
                LogDebug($"Checking {modTexts.Length} mods against {boxMods.Count} defined mods for box type {boxType}", true);
                LogDebug($"Required desired mods: {GetRequiredDesiredMods(boxType)}", true);
                
                // Log all desired mods
                var desiredMods = boxMods.Where(m => m.IsDesired).ToList();
                LogDebug($"Desired mods ({desiredMods.Count}):", true);
                foreach (var mod in desiredMods)
                {
                    LogDebug($"- {mod.Description}", true);
                }
                
                // Log all undesired mods
                var undesiredMods = boxMods.Where(m => m.IsUndesired).ToList();
                LogDebug($"Undesired mods ({undesiredMods.Count}):", true);
                foreach (var mod in undesiredMods)
                {
                    LogDebug($"- {mod.Description}", true);
                }
            }
            
            // We're now working with a combined string of all mods
            string combinedModText = modTexts[0].ToLower();
            
            // First check for undesired mods - if any checked undesired mod is found, reject the strongbox
            foreach (var mod in boxMods.Where(m => m.IsUndesired))
            {
                // More flexible matching - check if the key part of the description is in the mod text
                string keyPart = mod.Description.ToLower();
                
                // Remove numeric values and "contains" prefix for more flexible matching
                keyPart = keyPart.Replace("#", "").Replace("contains ", "");
                
                if (combinedModText.Contains(keyPart))
                {
                    // Found an undesired mod that's checked, reject the strongbox
                    if (EnableDebugLogging)
                    {
                        LogDebug($"Found undesired mod: '{keyPart}' in combined text", false); // Don't throttle important matches
                    }
                    return false;
                }
            }
            
            // Now check for desired mods
            foreach (var mod in boxMods.Where(m => m.IsDesired))
            {
                // More flexible matching - check if the key part of the description is in the mod text
                string keyPart = mod.Description.ToLower();
                
                // Remove numeric values and "contains" prefix for more flexible matching
                keyPart = keyPart.Replace("#", "").Replace("contains ", "");
                
                if (combinedModText.Contains(keyPart))
                {
                    desiredModsFound++;
                    if (EnableDebugLogging)
                    {
                        LogDebug($"Found desired mod: '{keyPart}' in combined text", false); // Don't throttle important matches
                    }
                }
            }
            
            // Get the required number of desired mods for this box type
            int requiredDesiredMods = GetRequiredDesiredMods(boxType);
            
            // Return true if we have enough desired mods
            bool result = desiredModsFound >= requiredDesiredMods;
            if (EnableDebugLogging)
            {
                LogDebug($"Found {desiredModsFound} desired mods, need {requiredDesiredMods}, result: {result}", false); // Don't throttle important results
            }
            return result;
        }
        
        // Method to get matching mods for a strongbox
        public List<StrongboxMod> GetMatchingMods(string combinedModText, SBType boxType)
        {
            var matchingMods = new List<StrongboxMod>();
            
            // Get all mods for this box type
            var boxMods = GetModsByType(boxType);
            
            if (EnableDebugLogging)
            {
                LogDebug($"GetMatchingMods: Checking for matching mods in {boxType}", true);
            }
            
            // Check for matching desired mods
            foreach (var mod in boxMods.Where(m => m.IsDesired))
            {
                // More flexible matching - check if the key part of the description is in the mod text
                string keyPart = mod.Description.ToLower();
                
                // Remove numeric values and "contains" prefix for more flexible matching
                keyPart = keyPart.Replace("#", "").Replace("contains ", "");
                
                if (combinedModText.ToLower().Contains(keyPart))
                {
                    matchingMods.Add(mod);
                    
                    if (EnableDebugLogging)
                    {
                        LogDebug($"GetMatchingMods: Found matching mod: {mod.Description}", false); // Don't throttle important matches
                    }
                }
            }
            
            if (EnableDebugLogging)
            {
                LogDebug($"GetMatchingMods: Found {matchingMods.Count} matching mods", false); // Don't throttle important results
            }
            
            return matchingMods;
        }
    }

    public class SavedModData
    {
        public List<StrongboxMod> Mods { get; set; }
        public Dictionary<SBType, int> RequiredDesiredMods { get; set; }
    }
} 