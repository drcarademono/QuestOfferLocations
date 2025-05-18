using UnityEngine;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallConnect.Arena2;
using Assets.Scripts.Game.MacadaynuMods.QuestOfferLocations;
using DaggerfallConnect;
using DaggerfallWorkshop.Game.Guilds;
using Assets.Scripts.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Utility;
using System;
using System.Collections.Generic;
using DaggerfallWorkshop.Game.Questing;
using System.Linq;

namespace DaggerfallWorkshop.Game.UserInterfaceWindows
{
    public class QuestOfferLocationGuildServicePopUpWindow : DaggerfallGuildServicePopupWindow
    {
        public static Dictionary<int, int> npcQuestOffers = new Dictionary<int, int>();
        public static Dictionary<int, List<string>> npcQuestOfferNames = new Dictionary<int, List<string>>();
        public static Dictionary<int, (int day, int year)> npcLastQuestOfferDate = new Dictionary<int, (int day, int year)>();
        public static Dictionary<int, List<int>> npcInvalidQuestIndices = new Dictionary<int, List<int>>();
        private bool[] isVisible;

        public int SelectedIndex;
        public NearestQuest NearestQuest;

        #region Constructors

        public QuestOfferLocationGuildServicePopUpWindow(IUserInterfaceManager uiManager, StaticNPC npc, FactionFile.GuildGroups guildGroup, int buildingFactionId)
            : base(uiManager, npc, guildGroup, buildingFactionId)
        {
        }

        #endregion

        protected override void OfferQuest()
        {
            Debug.Log($"Original offered quest: {offeredQuest?.QuestName ?? "null"}");

            // Determine the maximum number of quests allowed for this location.
            int maxQuestsForLocation = GetMaxQuestsForLocation();

            // Get the current quest offers count for this NPC.
            int questOffers = GetQuestOffersForNPC(serviceNPC);

            // If we have reached the limit, show a failure message.
            if (QuestOfferLocationsMod.limitGuildQuestions && questOffers >= maxQuestsForLocation)
            {
                ShowFailGetQuestMessage();
                //Debug.Log("[QOL] ShowFailGetQuestMessage (limit reached)");
                return;
            }

            // NPC keys used for tracking unique quests.
            int factionId = serviceNPC.Data.factionID;
            int npcKey = serviceNPC.Data.nameSeed;

            // First: Ensure the quest is unique (if configured).
            DateTime uniquenessStart = DateTime.Now;
            offeredQuest = EnsureUniqueQuest(offeredQuest, factionId, npcKey, uniquenessStart, QuestOfferLocationsMod.maxSearchTimeInSeconds);
            if (offeredQuest == null)
            {
                ShowFailGetQuestMessage();
                //Debug.Log("[QOL] ShowFailGetQuestMessage (no unique quest)");
                return;
            }

            // Second: If nearby quest preference is enabled, run the nearby search loop.
            if (QuestOfferLocationsMod.preferNearbyQuests)
            {
                offeredQuest = FindNearbyQuest(offeredQuest, factionId, npcKey);
            }

            // If no quest is found, show a failure message.
            if (offeredQuest == null)
            {
                ShowFailGetQuestMessage();
                //Debug.Log("[QOL] ShowFailGetQuestMessage (after nearby quest search)");
                return;
            }
            else
            {
                // Set external context if the NPC is a guild member.
                IGuild guild = guildManager.GetGuild(guildGroup);
                if (guild.IsMember())
                    offeredQuest.ExternalMCP = guild;

                // Create the quest offer message box.
                DaggerfallMessageBox messageBox = QuestOfferMessageHelper.CreateQuestOffer(offeredQuest);
                if (messageBox != null)
                {
                    messageBox.OnButtonClick += OfferQuest_OnButtonClick;
                    messageBox.Show();
                }

                // Increment and store quest offers.
                SetQuestOffersForNPC(serviceNPC, questOffers + 1);

                // Add the quest name to the list of offered quests.
                if (!npcQuestOfferNames.ContainsKey(npcKey))
                    npcQuestOfferNames[npcKey] = new List<string>();
                npcQuestOfferNames[npcKey].Add(offeredQuest.QuestName);

                // Record the date of this quest offer.
                int currentDay = DaggerfallUnity.Instance.WorldTime.Now.DayOfYear;
                int currentYear = DaggerfallUnity.Instance.WorldTime.Now.Year;
                npcLastQuestOfferDate[npcKey] = (currentDay, currentYear);

                //Debug.Log($"[QOL] NPC {npcKey} last offered a quest on day {currentDay} of year {currentYear}.");
                string offeredQuestList = string.Join(", ", npcQuestOfferNames[npcKey]);
                //Debug.Log($"[QOL] NPC {npcKey} has offered the following quests: {offeredQuestList}");
            }

            // Finally, clear the quest pool.
            questPool.Clear();
        }

        /// <summary>
        /// Ensure that the candidate quest is unique for the NPC.
        /// If it's not unique, re-request a new candidate (up to the timeout).
        /// </summary>
        private Quest EnsureUniqueQuest(Quest candidate, int factionId, int npcKey, DateTime startTime, double maxSearchTimeInSeconds)
        {
            while (QuestOfferLocationsMod.avoidRepeatingGuildQuests &&
                   npcQuestOfferNames.TryGetValue(npcKey, out var offeredNames) &&
                   offeredNames.Contains(candidate.QuestName))
            {
                if (DateTime.Now.Subtract(startTime).TotalSeconds >= maxSearchTimeInSeconds)
                {
                    // We've exceeded the search time; return the current candidate even if it's not unique.
                    return null;
                }
                candidate = RequestNewCandidate(factionId);
                if (candidate == null)
                    return null;
            }
            return candidate;
        }

        /// <summary>
        /// Find a nearby quest by checking travel times.
        /// It loops until the candidate quest is within the acceptable travel range,
        /// or until the search times out (in which case it returns the best fallback candidate).
        /// </summary>
        private Quest FindNearbyQuest(Quest initialQuest, int factionId, int npcKey)
        {
            Quest candidate = initialQuest;
            NearestQuest fallbackCandidate = null;
            DateTime searchStart = DateTime.Now;

            while (true)
            {
                if (candidate == null)
                    break;

                // 1) Gather all places: explicit + person‑derived
                var places = new List<Questing.Place>();
                // explicit Place resources
                places.AddRange(candidate.GetAllResources(typeof(Questing.Place))
                                        .OfType<Questing.Place>());

                // any Person resources -> their dialog/home place
                foreach (var person in candidate.GetAllResources(typeof(Person)).OfType<Person>())
                {
                    var dialogPlace = person.GetDialogPlace() ?? person.GetHomePlace();
                    if (dialogPlace != null)
                        places.Add(dialogPlace);
                }

                // 2) Compute farthest travel time among all
                float farthestTravelTimeInDays = 0f;
                foreach (var questPlace in places)
                {
                    DFLocation location;
                    DaggerfallUnity.Instance.ContentReader
                        .GetLocation(
                            questPlace.SiteDetails.regionName,
                            questPlace.SiteDetails.locationName,
                            out location);

                    float travelTimeDays = QuestOfferMessageHelper.GetTravelTimeToLocation(location);
                    if (travelTimeDays > farthestTravelTimeInDays)
                        farthestTravelTimeInDays = travelTimeDays;
                }

                // 3) If within range, accept this quest
                if (farthestTravelTimeInDays <= QuestOfferLocationsMod.maxTravelDistanceInDays)
                    return candidate;

                // 4) Otherwise consider it for fallback and get a new one
                if (fallbackCandidate == null
                    || farthestTravelTimeInDays < fallbackCandidate.TimeToTravelToQuestInDays)
                {
                    fallbackCandidate = new NearestQuest
                    {
                        Quest = candidate,
                        TimeToTravelToQuestInDays = farthestTravelTimeInDays
                    };
                }

                candidate = RequestNewCandidate(factionId);
                candidate = EnsureUniqueQuest(candidate, factionId, npcKey, searchStart, QuestOfferLocationsMod.maxSearchTimeInSeconds);

                // 5) Timeout?
                if ((DateTime.Now - searchStart).TotalSeconds >= QuestOfferLocationsMod.maxSearchTimeInSeconds)
                    return fallbackCandidate?.Quest;
            }

            return fallbackCandidate?.Quest;
        }

        /// <summary>
        /// Requests a new quest candidate using the proper method based on settings.
        /// </summary>
        private Quest RequestNewCandidate(int factionId)
        {
            if (DaggerfallUnity.Settings.GuildQuestListBox)
            {
                return GameManager.Instance.QuestListsManager.LoadQuest(questPool[SelectedIndex], factionId);
            }
            else
            {
                return GameManager.Instance.QuestListsManager.GetGuildQuest(
                    guildGroup,
                    guildManager.GetGuild(guildGroup).IsMember() ? Questing.MembershipStatus.Member : Questing.MembershipStatus.Nonmember,
                    factionId,
                    guildManager.GetGuild(guildGroup).GetReputation(playerEntity),
                    guildManager.GetGuild(guildGroup).Rank
                );
            }
        }

        private int GetQuestOffersForNPC(StaticNPC npc)
        {
            int npcKey = npc.Data.nameSeed;
            int baseOffers = npcQuestOffers.TryGetValue(npcKey, out int offers) ? offers : 0;
            if (npcLastQuestOfferDate.TryGetValue(npcKey, out var lastOfferDate))
            {
                int lastDay = lastOfferDate.day;
                int lastYear = lastOfferDate.year;
                int currentDay = DaggerfallUnity.Instance.WorldTime.Now.DayOfYear;
                int currentYear = DaggerfallUnity.Instance.WorldTime.Now.Year;
                int daysElapsed = (currentYear - lastYear) * DaggerfallDateTime.DaysPerYear + (currentDay - lastDay);
                int monthsElapsed = Mathf.FloorToInt(daysElapsed / 30.0f);
                baseOffers = Mathf.Max(baseOffers - monthsElapsed, 0);
                npcQuestOffers[npcKey] = baseOffers;
                if (npcQuestOfferNames.TryGetValue(npcKey, out var offeredQuestNames) && monthsElapsed > 0)
                {
                    int namesToRemove = Mathf.Min(monthsElapsed, offeredQuestNames.Count);
                    offeredQuestNames.RemoveRange(0, namesToRemove);
                    if (offeredQuestNames.Count == 0)
                        npcQuestOfferNames.Remove(npcKey);
                    else
                        npcQuestOfferNames[npcKey] = offeredQuestNames;
                }
            }
            return baseOffers;
        }

        private void SetQuestOffersForNPC(StaticNPC npc, int offers)
        {
            int npcKey = npc.Data.nameSeed;
            npcQuestOffers[npcKey] = offers;
        }

        private int GetMaxQuestsForLocation()
        {
            PlayerGPS playerGPS = GameManager.Instance.PlayerGPS;
            switch (playerGPS.CurrentLocationType)
            {
                case DFRegion.LocationTypes.TownCity:
                    return QuestOfferLocationsMod.limitCityQuests;
                case DFRegion.LocationTypes.TownHamlet:
                    return QuestOfferLocationsMod.limitTownQuests;
                default:
                    return QuestOfferLocationsMod.limitVillageQuests;
            }
        }

        protected override void GettingQuestsBox_OnClose()
        {
            DaggerfallListPickerWindow questPicker = new DaggerfallListPickerWindow(uiManager, uiManager.TopWindow);
            questPicker.OnItemPicked += QuestPicker_OnItemPicked;
            int npcKey = serviceNPC.Data.nameSeed;
            if (!npcInvalidQuestIndices.ContainsKey(npcKey))
                npcInvalidQuestIndices[npcKey] = new List<int>();

            int maxQuestsForLocation = GetMaxQuestsForLocation();
            List<int> validQuestIndices = new List<int>();

            for (int i = 0; i < questPool.Count; i++)
            {
                if (!npcInvalidQuestIndices[npcKey].Contains(i))
                    validQuestIndices.Add(i);
            }

            if (QuestOfferLocationsMod.limitGuildQuestions && validQuestIndices.Count > maxQuestsForLocation)
            {
                System.Random rng = new System.Random();
                var indicesToExclude = validQuestIndices
                    .OrderBy(x => rng.Next())
                    .Take(validQuestIndices.Count - maxQuestsForLocation)
                    .ToList();
                npcInvalidQuestIndices[npcKey].AddRange(indicesToExclude);
            }

            isVisible = new bool[questPool.Count];
            List<int> failures = new List<int>();

            for (int i = 0; i < questPool.Count; i++)
            {
                try
                {
                    if (QuestOfferLocationsMod.avoidRepeatingGuildQuests && npcInvalidQuestIndices[npcKey].Contains(i))
                    {
                        isVisible[i] = false;
                        continue;
                    }
                    int factionId = guildManager.GetGuild(guildGroup).GetFactionId();
                    Quest quest = GameManager.Instance.QuestListsManager.LoadQuest(questPool[i], factionId, true);
                    string displayName = quest.DisplayName;
                    string localizedDisplayName = QuestMachine.Instance.GetLocalizedQuestDisplayName(quest.QuestName);
                    if (!string.IsNullOrEmpty(localizedDisplayName))
                        displayName = localizedDisplayName;
                    questPicker.ListBox.AddItem(displayName ?? quest.QuestName);
                    isVisible[i] = true;
                    quest.Dispose();
                }
                catch
                {
                    failures.Add(i);
                }
            }

            foreach (int i in failures)
            {
                if (i >= 0 && i < questPool.Count)
                    questPool.RemoveAt(i);
            }

            // If no valid quests were added to the ListBox, show the failure message.
            if (questPicker.ListBox.Count == 0)
            {
                //Debug.Log("[QOL] QuestPicker is empty - no valid quests to show.");
                ShowFailGetQuestMessage();
                return;
            }

            uiManager.PushWindow(questPicker);
        }

        protected override void QuestPicker_OnItemPicked(int visibleIndex, string name)
        {
            //Debug.Log($"[QOL] isVisible array: {string.Join(", ", isVisible)}");
            //Debug.Log($"[QOL] Visible index selected: {visibleIndex}");
            int adjustedIndex = -1;
            int visibleCount = 0;
            for (int i = 0; i < isVisible.Length; i++)
            {
                if (isVisible[i])
                {
                    if (visibleCount == visibleIndex)
                    {
                        adjustedIndex = i;
                        break;
                    }
                    visibleCount++;
                }
            }
            if (adjustedIndex == -1)
            {
                //Debug.LogError($"[QOL] Failed to find adjusted index for visible index {visibleIndex}. isVisible array: {string.Join(", ", isVisible)}");
                ShowFailGetQuestMessage();
                return;
            }
            int npcKey = serviceNPC.Data.nameSeed;
            if (!npcInvalidQuestIndices.ContainsKey(npcKey))
                npcInvalidQuestIndices[npcKey] = new List<int>();
            if (!npcInvalidQuestIndices[npcKey].Contains(adjustedIndex))
                npcInvalidQuestIndices[npcKey].Add(adjustedIndex);
            int factionId = guildManager.GetGuild(guildGroup).GetFactionId();
            offeredQuest = GameManager.Instance.QuestListsManager.LoadQuest(questPool[adjustedIndex], factionId);
            DaggerfallUI.Instance.PlayOneShot(SoundClips.ButtonClick);
            DaggerfallUI.UIManager.PopWindow();
            if (offeredQuest != null)
            {
                SelectedIndex = adjustedIndex;
                OfferQuest();
            }
            else
            {
                //Debug.LogWarning($"[QOL] Failed to load quest at adjusted index {adjustedIndex}.");
                ShowFailGetQuestMessage();
            }
        }
    }
}
