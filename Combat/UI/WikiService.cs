using System;
using System.Threading.Tasks;
using Godot;

namespace QDND.Combat.UI
{
    public class WikiService
    {
        public async Task<string> GetTooltipFor(string abilityName)
        {
            // BG3 wiki URL format: https://bg3.wiki/wiki/[Ability_Name]
            var url = $"https://bg3.wiki/wiki/{abilityName.Replace(" ", "_")}";
            
            // This is where the web fetching will happen.
            // For now, we'll just return a placeholder.
            await Task.Delay(100); // Simulate network latency
            return $"Tooltip for {abilityName} from {url}";
        }
    }
}
