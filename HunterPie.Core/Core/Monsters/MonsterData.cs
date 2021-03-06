﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using HunterPie.Core.Enums;
using HunterPie.Core.Monsters;
using HunterPie.Logger;

namespace HunterPie.Core
{
    public class MonsterData
    {
        private static XmlDocument MonsterDataDocument;
        private static Dictionary<int, MonsterInfo> monstersInfo;
        private static List<AilmentInfo> ailmentsInfo;

        public static IReadOnlyDictionary<int, MonsterInfo> MonstersInfo => monstersInfo;
        public static IReadOnlyCollection<AilmentInfo> AilmentsInfo => ailmentsInfo;

        static public void LoadMonsterData()
        {
            MonsterDataDocument = new XmlDocument();
            if (ConfigManager.Settings.HunterPie.Debug.LoadCustomMonsterData)
            {
                try
                {
                    MonsterDataDocument.Load(ConfigManager.Settings.HunterPie.Debug.CustomMonsterData);
                    Debugger.Warn(GStrings.GetLocalizationByXPath("/Console/String[@ID='MESSAGE_MONSTER_DATA_LOAD']"));

                    LoadAilments();
                    LoadMonsters();

                    MonsterDataDocument = null;
                    return;
                }
                catch (Exception err)
                {
                    Debugger.Error(err);
                }
            }
            MonsterDataDocument.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "HunterPie.Resources/Data/MonsterData.xml"));

            LoadAilments();
            LoadMonsters();

            Debugger.Warn(GStrings.GetLocalizationByXPath("/Console/String[@ID='MESSAGE_MONSTER_DATA_LOAD']"));

            // Unload XmlDocument since we don't need it anymore
            MonsterDataDocument = null;
        }

        static private MonsterInfo MonsterXmlNodeToInfo(XmlNode node)
        {
            if (node?.Attributes == null)
            {
                throw new ArgumentNullException(nameof(node), "XmlNode and its attributes cannot be null!");
            }

            CrownInfo MonsterCrowns = new CrownInfo
            {
                Mini = float.Parse(node.SelectSingleNode("Crown/@Mini")?.Value ?? "0.9", System.Globalization.CultureInfo.InvariantCulture),
                Silver = float.Parse(node.SelectSingleNode("Crown/@Silver")?.Value ?? "1.15", System.Globalization.CultureInfo.InvariantCulture),
                Gold = float.Parse(node.SelectSingleNode("Crown/@Gold")?.Value ?? "1.23", System.Globalization.CultureInfo.InvariantCulture)
            };

            MonsterInfo monster = new MonsterInfo
            {
                Em = node.Attributes["ID"].Value,
                Id = int.Parse(node.Attributes["GameID"].Value),
                Crowns = MonsterCrowns,
                Capture = float.Parse(node.Attributes["Capture"].Value ?? "0"),
                Weaknesses = GetMonsterWeaknesses(node),
                MaxParts = int.Parse(node.SelectSingleNode("Parts/@Max")?.Value ?? "0"),
                Parts = GetMonsterPartsInfo(node)
            };

            monster.MaxRemovableParts = monster.Parts.Where(p => p.IsRemovable).Count();

            return monster;
        }

        static private WeaknessInfo[] GetMonsterWeaknesses(XmlNode node)
        {
            List<WeaknessInfo> weaknesses = new List<WeaknessInfo>();
            if (node == null) return null;

            XmlNodeList mWeaknesses = node.SelectNodes("Weaknesses/Weakness");
            foreach (XmlNode weaknessData in mWeaknesses)
            {
                WeaknessInfo wInfo = new WeaknessInfo
                {
                    Id = weaknessData.Attributes["ID"]?.Value,
                    Stars = int.Parse(weaknessData.Attributes["Stars"]?.Value ?? "0")
                };
                weaknesses.Add(wInfo);
            }

            return weaknesses.ToArray();
        }

        static private uint[] ParseTenderizedIdsToArray(string TenderizedIds)
        {
            if (TenderizedIds == "") return Array.Empty<uint>();
            string[] ids = TenderizedIds.Split(',');
            uint[] parsed = new uint[ids.Length];
            uint i = 0;
            foreach (string id in ids)
            {
                parsed[i] = Convert.ToUInt32(id);
                i++;
            }
            return parsed;
        }

        static private PartInfo[] GetMonsterPartsInfo(XmlNode node)
        {
            List<PartInfo> parts = new List<PartInfo>();
            if (node == null) return null;

            XmlNodeList mParts = node.SelectNodes("Parts/Part");
            uint RemovablePartIndex = 0;
            foreach (XmlNode partData in mParts)
            {
                PartInfo pInfo = new PartInfo
                {
                    Id = partData.Attributes["Name"]?.Value ?? "MONSTER_PART_UNKNOWN",
                    IsRemovable = bool.Parse(partData.Attributes["IsRemovable"]?.Value ?? "false"),
                    GroupId = partData.Attributes["Group"]?.Value ?? "MISC",
                    Skip = bool.Parse(partData.Attributes["Skip"]?.Value ?? "false"),
                    Index = uint.Parse(partData.Attributes["Index"]?.Value ?? RemovablePartIndex.ToString()),
                    TenderizeIds = ParseTenderizedIdsToArray(partData.Attributes["TenderizeIds"]?.Value ?? "")
                };

                if (pInfo.IsRemovable) RemovablePartIndex++;

                XmlNodeList breaks = partData.SelectNodes("Break");
                pInfo.BreakThresholds = ConvertToThresholdArray(breaks);

                parts.Add(pInfo);
            }

            return parts.ToArray();
        }

        private static ThresholdInfo[] ConvertToThresholdArray(XmlNodeList dataList)
        {
            if (dataList == null)
                return Array.Empty<ThresholdInfo>();

            List<ThresholdInfo> info = new List<ThresholdInfo>(dataList.Count);
            foreach (XmlNode node in dataList)
            {
                bool hasConditions = bool.Parse(node.Attributes["HasConditions"]?.Value ?? "False");
                ThresholdInfo dummy = new ThresholdInfo
                {
                    Threshold = int.Parse(node.Attributes["Threshold"].Value),
                    HasConditions = hasConditions,
                    MinFlinch = hasConditions ? int.Parse(node.Attributes["MinFlinch"]?.Value ?? "0") : 0,
                    MinHealth = hasConditions ? int.Parse(node.Attributes["MinHealth"]?.Value ?? "100") : 100
                };
                info.Add(dummy);
            }
            return info.ToArray();
        }

        static private void LoadMonsters()
        {
            XmlNodeList monstersData = MonsterDataDocument.SelectNodes("//Monsters/Monster");
            monstersInfo = monstersData.Cast<XmlNode>()
                .Select(node => MonsterXmlNodeToInfo(node))
                .ToDictionary(m => m.Id);

        }

        static private AilmentInfo AilmentXmlNodeToInfo(XmlNode node)
        {
            if (node?.Attributes == null) throw new ArgumentNullException(nameof(node), "XmlNode and its attributes cannot be null!");

            AilmentInfo ailment = new AilmentInfo
            {
                Name = node.Attributes["Name"]?.Value,
                Id = Convert.ToUInt32(node.Attributes["Id"]?.Value ?? "1000"),
                CanSkip = Convert.ToBoolean(node.Attributes["Skip"]?.Value ?? "true"),
                Group = node.Attributes["Group"]?.Value ?? "UNKNOWN",
                Type = (AilmentType)Convert.ToInt32(node.Attributes["Type"]?.Value ?? "0")
            };

            return ailment;
        }

        static private void LoadAilments()
        {
            XmlNodeList ailmentsData = MonsterDataDocument.SelectNodes("//Monsters/Ailments/Ailment");

            ailmentsInfo = ailmentsData.Cast<XmlNode>()
                .Select(node => AilmentXmlNodeToInfo(node))
                .ToList();
        }

        /// <summary>
        /// Gets Ailment based on its Id
        /// </summary>
        /// <param name="Id">Ailment Id</param>
        /// <returns></returns>
        public static AilmentInfo GetAilmentInfoById(uint Id)
        {
            return AilmentsInfo.Where(a => a.Id == Id).FirstOrDefault();
        }

    }
}
