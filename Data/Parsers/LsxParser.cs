using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using QDND.Data.ActionResources;

namespace QDND.Data.Parsers
{
    /// <summary>
    /// Parser for BG3's LSX (Larian Studio XML) files.
    /// LSX files are structured XML with typed attributes used throughout BG3's data files.
    /// </summary>
    public class LsxParser
    {
        /// <summary>
        /// Parse ActionResourceDefinitions.lsx file.
        /// </summary>
        /// <param name="filePath">Path to ActionResourceDefinitions.lsx</param>
        /// <returns>List of parsed action resource definitions</returns>
        public static List<ActionResourceDefinition> ParseActionResourceDefinitions(string filePath)
        {
            var results = new List<ActionResourceDefinition>();
            
            try
            {
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"LSX file not found: {filePath}");
                }
                
                XDocument doc = XDocument.Load(filePath);
                
                // Navigate to ActionResourceDefinitions region
                var region = doc.Descendants("region")
                    .FirstOrDefault(r => r.Attribute("id")?.Value == "ActionResourceDefinitions");
                
                if (region == null)
                {
                    throw new InvalidDataException("ActionResourceDefinitions region not found in LSX file");
                }
                
                // Find all ActionResourceDefinition nodes
                var definitions = region.Descendants("node")
                    .Where(n => n.Attribute("id")?.Value == "ActionResourceDefinition");
                
                foreach (var node in definitions)
                {
                    var def = ParseActionResourceDefinition(node);
                    if (def != null)
                    {
                        results.Add(def);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error parsing LSX file '{filePath}': {ex.Message}", ex);
            }
            
            return results;
        }
        
        /// <summary>
        /// Parse a single ActionResourceDefinition node.
        /// </summary>
        private static ActionResourceDefinition ParseActionResourceDefinition(XElement node)
        {
            var def = new ActionResourceDefinition();
            
            try
            {
                foreach (var attr in node.Elements("attribute"))
                {
                    string id = attr.Attribute("id")?.Value;
                    string type = attr.Attribute("type")?.Value;
                    string value = attr.Attribute("value")?.Value;
                    string handle = attr.Attribute("handle")?.Value;
                    
                    if (string.IsNullOrEmpty(id))
                        continue;
                    
                    switch (id)
                    {
                        case "UUID":
                            if (!string.IsNullOrEmpty(value) && Guid.TryParse(value, out var guid))
                            {
                                def.UUID = guid;
                            }
                            break;
                            
                        case "Name":
                            def.Name = value ?? string.Empty;
                            break;
                            
                        case "DisplayName":
                            // TranslatedString uses handle attribute for actual text
                            def.DisplayName = handle ?? value ?? string.Empty;
                            break;
                            
                        case "Description":
                            def.Description = handle ?? value ?? string.Empty;
                            break;
                            
                        case "Error":
                            def.Error = handle ?? value ?? string.Empty;
                            break;
                            
                        case "ReplenishType":
                            if (!string.IsNullOrEmpty(value) && 
                                Enum.TryParse<ReplenishType>(value, true, out var replenish))
                            {
                                def.ReplenishType = replenish;
                            }
                            break;
                            
                        case "MaxLevel":
                            if (!string.IsNullOrEmpty(value) && uint.TryParse(value, out var maxLevel))
                            {
                                def.MaxLevel = maxLevel;
                            }
                            break;
                            
                        case "MaxValue":
                            if (!string.IsNullOrEmpty(value) && uint.TryParse(value, out var maxValue))
                            {
                                def.MaxValue = maxValue;
                            }
                            break;
                            
                        case "DiceType":
                            if (!string.IsNullOrEmpty(value) && uint.TryParse(value, out var diceType))
                            {
                                def.DiceType = diceType;
                            }
                            break;
                            
                        case "IsSpellResource":
                            if (!string.IsNullOrEmpty(value) && bool.TryParse(value, out var isSpell))
                            {
                                def.IsSpellResource = isSpell;
                            }
                            break;
                            
                        case "UpdatesSpellPowerLevel":
                            if (!string.IsNullOrEmpty(value) && bool.TryParse(value, out var updates))
                            {
                                def.UpdatesSpellPowerLevel = updates;
                            }
                            break;
                            
                        case "ShowOnActionResourcePanel":
                            if (!string.IsNullOrEmpty(value) && bool.TryParse(value, out var show))
                            {
                                def.ShowOnActionResourcePanel = show;
                            }
                            break;
                            
                        case "IsHidden":
                            if (!string.IsNullOrEmpty(value) && bool.TryParse(value, out var hidden))
                            {
                                def.IsHidden = hidden;
                            }
                            break;
                            
                        case "PartyActionResource":
                            if (!string.IsNullOrEmpty(value) && bool.TryParse(value, out var party))
                            {
                                def.PartyActionResource = party;
                            }
                            break;
                    }
                }
                
                // Parse resource type enum from name
                def.ParseResourceType();
                
                return def;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error parsing ActionResourceDefinition node: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// Generic helper to get a typed attribute value from an LSX node.
        /// </summary>
        /// <param name="node">The XML node to search</param>
        /// <param name="attributeId">The attribute id to find</param>
        /// <returns>The value attribute as a string, or null if not found</returns>
        public static string GetAttributeValue(XElement node, string attributeId)
        {
            return node.Elements("attribute")
                .FirstOrDefault(a => a.Attribute("id")?.Value == attributeId)
                ?.Attribute("value")?.Value;
        }
        
        /// <summary>
        /// Get a GUID attribute value from an LSX node.
        /// </summary>
        public static Guid? GetGuidAttribute(XElement node, string attributeId)
        {
            string value = GetAttributeValue(node, attributeId);
            if (!string.IsNullOrEmpty(value) && Guid.TryParse(value, out var guid))
            {
                return guid;
            }
            return null;
        }
        
        /// <summary>
        /// Get a boolean attribute value from an LSX node.
        /// </summary>
        public static bool GetBoolAttribute(XElement node, string attributeId, bool defaultValue = false)
        {
            string value = GetAttributeValue(node, attributeId);
            if (!string.IsNullOrEmpty(value) && bool.TryParse(value, out var result))
            {
                return result;
            }
            return defaultValue;
        }
        
        /// <summary>
        /// Get an integer attribute value from an LSX node.
        /// </summary>
        public static int GetIntAttribute(XElement node, string attributeId, int defaultValue = 0)
        {
            string value = GetAttributeValue(node, attributeId);
            if (!string.IsNullOrEmpty(value) && int.TryParse(value, out var result))
            {
                return result;
            }
            return defaultValue;
        }
        
        /// <summary>
        /// Get a uint attribute value from an LSX node.
        /// </summary>
        public static uint GetUIntAttribute(XElement node, string attributeId, uint defaultValue = 0)
        {
            string value = GetAttributeValue(node, attributeId);
            if (!string.IsNullOrEmpty(value) && uint.TryParse(value, out var result))
            {
                return result;
            }
            return defaultValue;
        }
        
        /// <summary>
        /// Get a TranslatedString attribute (uses handle attribute, not value).
        /// </summary>
        public static string GetTranslatedString(XElement node, string attributeId)
        {
            var attr = node.Elements("attribute")
                .FirstOrDefault(a => a.Attribute("id")?.Value == attributeId);
            
            // TranslatedString type uses "handle" attribute for the text
            return attr?.Attribute("handle")?.Value ?? 
                   attr?.Attribute("value")?.Value ?? 
                   string.Empty;
        }
    }
}
