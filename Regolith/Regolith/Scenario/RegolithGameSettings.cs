using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Regolith.Common;
using UnityEngine;

namespace Regolith.Scenario
{
    public class RegolithGameSettings
    {
        public int Seed { get; set; }
        public List<DepletionData> DepletionInfo { get; private set; }
        public List<BiomeLockData> BiomeLockInfo { get; private set; }
        public ConfigNode SettingsNode { get; private set; }

        public void Load(ConfigNode node)
        {
            if (node.HasNode("RegolithGameSettings"))
            {
                SettingsNode = node.GetNode("RegolithGameSettings");
                Seed = GetValue(SettingsNode, "GameSeed", Seed);
                DepletionInfo = SetupDepletionInfo();
                BiomeLockInfo = SetupBiomeLockInfo();
            }
            else
            {
                //Set our seed
                var r = new System.Random();
                Seed = r.Next(1, Int32.MaxValue);
                DepletionInfo = new List<DepletionData>();
                BiomeLockInfo = new List<BiomeLockData>();
            }
        }


        private List<DepletionData> SetupDepletionInfo()
        {
            var depletionNodes = SettingsNode.GetNodes("DEPLETION_DATA");
            return Utilities.ImportDepletionNodeList(depletionNodes);
        }

        private List<BiomeLockData> SetupBiomeLockInfo()
        {
            var biomeLockNodes = SettingsNode.GetNodes("BIOME_LOCK_DATA");
            return Utilities.ImportBiomeLockNodeList(biomeLockNodes);
        }

        
        public void Save(ConfigNode node)
        {
            if (node.HasNode("RegolithGameSettings"))
            {
                SettingsNode = node.GetNode("RegolithGameSettings");
            }
            else
            {
                SettingsNode = node.AddNode("RegolithGameSettings");
            }

            SettingsNode.AddValue("GameSeed", Seed);
            foreach (var dd in DepletionInfo)
            {
                var dNode = new ConfigNode("DEPLETION_DATA");
                dNode.AddValue("PlanetId", dd.PlanetId);
                dNode.AddValue("ResourceName", dd.ResourceName);

                foreach (var dn in dd.DepletionNodes)
                {
                    var nNode = new ConfigNode("DEPLETION_NODE");
                    nNode.AddValue("X", dn.X);
                    nNode.AddValue("Y", dn.Y);
                    nNode.AddValue("Value", dn.Value);
                    nNode.AddValue("LastUpdate", dn.LastUpdate);
                    dNode.AddNode(nNode);
                }
                SettingsNode.AddNode(dNode);
            }

            foreach (var bd in BiomeLockInfo)
            {
                var bNode = new ConfigNode("BIOME_LOCK_DATA");
                bNode.AddValue("PlanetId", bd.PlanetId);
                bNode.AddValue("BiomeName", bd.BiomeName);
                SettingsNode.AddNode(bNode);
            }        
        
        }

        public static int GetValue(ConfigNode config, string name, int currentValue)
        {
            int newValue;
            if (config.HasValue(name) && int.TryParse(config.GetValue(name), out newValue))
            {
                return newValue;
            }
            else
            {
                return currentValue;
            }
        }


        private DepletionNode GetDepletionNode(int planetId, string resource, int x, int y)
        {
            //Does a node exist?
            var planetInfo = DepletionInfo.FirstOrDefault(n => n.PlanetId == planetId && n.ResourceName == resource);
            if (planetInfo == null)
                return null;  //Default is 100%

            var node = planetInfo.DepletionNodes.FirstOrDefault(d => d.X == x && d.Y == y);
            return node;
        }

        public float GetDepletionNodeValue(int planetId, string resource, int x, int y)
        {
            var node = GetDepletionNode(planetId, resource, x, y);
            if (node == null)
                return 1f;
            return node.Value;
        }

        public void SetDepletionNodeValue(int planetId, string resource, int x, int y, float value)
        {
            var planetInfo = DepletionInfo.FirstOrDefault(n => n.PlanetId == planetId && n.ResourceName == resource);
            if (planetInfo == null)
            {
                planetInfo = new DepletionData();
                planetInfo.PlanetId = planetId;
                planetInfo.ResourceName = resource;
                DepletionInfo.Add(planetInfo);
            }

            var node = GetDepletionNode(planetId, resource, x, y);
            if (node == null)
            {
                node = new DepletionNode();
                node.X = x;
                node.Y = y;
                planetInfo.DepletionNodes.Add(node);
            }
            node.Value = value;
            node.LastUpdate = Planetarium.GetUniversalTime();
        }

        public bool IsBiomeUnlocked(int planetId, string biomeName)
        {
            //Does a node exist?
            return BiomeLockInfo.Any(n => n.PlanetId == planetId && n.BiomeName == biomeName);
        }

        public bool IsPlanetUnlocked(int planetId)
        {
            //Does a node exist?
            return BiomeLockInfo.Any(n => n.PlanetId == planetId);
        }
        public void UnlockBiome(int planetId, string biomeName)
        {
            if (IsBiomeUnlocked(planetId, biomeName))
                return;

            var biomeInfo = new BiomeLockData
                            {
                                PlanetId = planetId,
                                BiomeName = biomeName
                            };
            BiomeLockInfo.Add(biomeInfo);
        }

    
    }
}