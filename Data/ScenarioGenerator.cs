using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QDND.Combat.Entities;
using QDND.Data.CharacterModel;

namespace QDND.Data
{
    public class ScenarioGenerator
    {
        private readonly CharacterDataRegistry _characterDataRegistry;
        private readonly Random _random;

        public ScenarioGenerator(CharacterDataRegistry characterDataRegistry, int seed)
        {
            _characterDataRegistry = characterDataRegistry;
            _random = new Random(seed);
        }

        public ScenarioDefinition GenerateRandomScenario(int team1Size, int team2Size)
        {
            var scenario = new ScenarioDefinition
            {
                Id = "random_2v2",
                Name = "Random 2v2 Combat",
                Seed = _random.Next(),
                Units = new List<ScenarioUnit>()
            };

            var races = _characterDataRegistry.GetAllRaces().ToList();
            var classes = _characterDataRegistry.GetAllClasses().ToList();

            if (races.Count == 0) throw new InvalidOperationException("No races available in CharacterDataRegistry.");
            if (classes.Count == 0) throw new InvalidOperationException("No classes available in CharacterDataRegistry.");

            for (int i = 0; i < team1Size; i++)
            {
                var race = races[_random.Next(races.Count)];
                var aClass = classes[_random.Next(classes.Count)];
                var unit = new ScenarioUnit
                {
                    Id = $"player_{i+1}",
                    Name = $"{race.Name} {aClass.Name}",
                    Faction = Faction.Player,
                    Initiative = _random.Next(1, 21),
                    X = -2 * i,
                    Y = 0,
                    Z = 0,
                    RaceId = race.Id,
                    ClassLevels = new List<ClassLevelEntry> { new ClassLevelEntry { ClassId = aClass.Id, Levels = 1 } }
                };
                scenario.Units.Add(unit);
            }

            for (int i = 0; i < team2Size; i++)
            {
                var race = races[_random.Next(races.Count)];
                var aClass = classes[_random.Next(classes.Count)];
                var unit = new ScenarioUnit
                {
                    Id = $"enemy_{i+1}",
                    Name = $"{race.Name} {aClass.Name}",
                    Faction = Faction.Hostile,
                    Initiative = _random.Next(1, 21),
                    X = 2 * i,
                    Y = 0,
                    Z = 4,
                    RaceId = race.Id,
                    ClassLevels = new List<ClassLevelEntry> { new ClassLevelEntry { ClassId = aClass.Id, Levels = 1 } }
                };
                scenario.Units.Add(unit);
            }

            return scenario;
        }
    }
}
