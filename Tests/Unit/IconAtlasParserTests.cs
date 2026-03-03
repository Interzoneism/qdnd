using System;
using System.IO;
using System.Linq;
using Xunit;
using QDND.Data.Icons;

namespace QDND.Tests.Unit
{
    [Trait("Category", "Unit")]
    public class IconAtlasParserTests
    {
        private static string FindRepoRoot()
        {
            var dir = AppContext.BaseDirectory;
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir, "project.godot")))
                    return dir;
                dir = Directory.GetParent(dir)?.FullName;
            }
            return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        }

        private static string GetBG3DataPath() => Path.Combine(FindRepoRoot(), "BG3_Data");

        private static string GetSkillsLsxPath() =>
            Path.Combine(GetBG3DataPath(), "Shared", "Public", "Shared", "GUI", "Icons_Skills.lsx");

        private static string GetItemsLsxPath() =>
            Path.Combine(GetBG3DataPath(), "Shared", "Public", "Shared", "GUI", "Icons_Items.lsx");

        // ── IconAtlasParser ──────────────────────────────────────────────

        [Fact]
        public void Parse_ValidLsxFile_ReturnsEntries()
        {
            var entries = IconAtlasParser.Parse(GetSkillsLsxPath(), "Icons_Skills");

            Assert.NotNull(entries);
            Assert.True(entries.Count > 1000,
                $"Expected > 1000 entries from Icons_Skills.lsx but got {entries.Count}");
        }

        [Fact]
        public void Parse_ValidLsxFile_ParsesUVCoordinatesCorrectly()
        {
            var entries = IconAtlasParser.Parse(GetSkillsLsxPath(), "Icons_Skills");

            foreach (var entry in entries)
            {
                Assert.InRange(entry.U1, 0f, 1f);
                Assert.InRange(entry.V1, 0f, 1f);
                Assert.InRange(entry.U2, 0f, 1f);
                Assert.InRange(entry.V2, 0f, 1f);
            }
        }

        [Fact]
        public void Parse_ValidLsxFile_SetsAtlasName()
        {
            var entries = IconAtlasParser.Parse(GetSkillsLsxPath(), "Icons_Skills");

            Assert.All(entries, e => Assert.Equal("Icons_Skills", e.AtlasFile));
        }

        [Fact]
        public void Parse_ValidLsxFile_HasKnownIcon()
        {
            var entries = IconAtlasParser.Parse(GetSkillsLsxPath(), "Icons_Skills");

            var known = entries.FirstOrDefault(e =>
                e.Name == "Action_Dash" || e.Name == "Action_AbsolutePower");

            Assert.NotNull(known);
        }

        [Fact]
        public void Parse_NonExistentFile_ThrowsException()
        {
            Assert.ThrowsAny<Exception>(() =>
                IconAtlasParser.Parse("/nonexistent/fake_path.lsx", "Fake"));
        }

        [Fact]
        public void Parse_ItemsAtlas_ReturnsEntries()
        {
            var entries = IconAtlasParser.Parse(GetItemsLsxPath(), "Icons_Items");

            Assert.NotNull(entries);
            Assert.True(entries.Count > 0,
                $"Expected entries from Icons_Items.lsx but got {entries.Count}");
        }

        // ── IconService ──────────────────────────────────────────────────

        [Fact]
        public void LoadFromBG3Data_LoadsAllAtlases()
        {
            var service = new IconService();
            service.LoadFromBG3Data(GetBG3DataPath());

            Assert.True(service.EntryCount > 3000,
                $"Expected > 3000 total entries but got {service.EntryCount}");
            Assert.Equal(7, service.AtlasCount);
        }

        [Fact]
        public void TryGetEntry_KnownIcon_ReturnsTrue()
        {
            var service = new IconService();
            service.LoadFromBG3Data(GetBG3DataPath());

            bool found = service.TryGetEntry("Action_AbsolutePower", out var entry);

            Assert.True(found);
            Assert.NotNull(entry);
            Assert.Equal("Action_AbsolutePower", entry.Name);
        }

        [Fact]
        public void TryGetEntry_UnknownIcon_ReturnsFalse()
        {
            var service = new IconService();
            service.LoadFromBG3Data(GetBG3DataPath());

            bool found = service.TryGetEntry("Totally_Fake_Icon_12345", out var entry);

            Assert.False(found);
            Assert.Null(entry);
        }

        [Fact]
        public void HasIcon_CaseInsensitive_ReturnsTrue()
        {
            var service = new IconService();
            service.LoadFromBG3Data(GetBG3DataPath());

            Assert.True(service.HasIcon("action_absolutepower"),
                "HasIcon should be case-insensitive");
        }

        [Fact]
        public void TryGetEntry_ReturnsValidUVCoordinates()
        {
            var service = new IconService();
            service.LoadFromBG3Data(GetBG3DataPath());

            bool found = service.TryGetEntry("Action_AbsolutePower", out var entry);

            Assert.True(found);
            Assert.True(entry!.U1 < entry.U2, $"U1 ({entry.U1}) should be < U2 ({entry.U2})");
            Assert.True(entry.V1 < entry.V2, $"V1 ({entry.V1}) should be < V2 ({entry.V2})");
            Assert.InRange(entry.U1, 0f, 1f);
            Assert.InRange(entry.V1, 0f, 1f);
            Assert.InRange(entry.U2, 0f, 1f);
            Assert.InRange(entry.V2, 0f, 1f);
        }
    }
}
