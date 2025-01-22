using UnityEngine;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallConnect.Arena2;
using Assets.Scripts.Game.MacadaynuMods.QuestOfferLocations;
using DaggerfallConnect;
using DaggerfallWorkshop.Game.Guilds;
using Assets.Scripts.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Utility;
using System.Collections.Generic;
using DaggerfallWorkshop.Game.Questing;

namespace DaggerfallWorkshop.Game.UserInterfaceWindows
{
    public class QuestOfferLocationGuildServicePopUpWindow : DaggerfallGuildServicePopupWindow
    {
        public static Dictionary<int, int> npcQuestOffers = new Dictionary<int, int>();
        public static Dictionary<int, List<string>> npcQuestOfferNames = new Dictionary<int, List<string>>();
        public static Dictionary<int, (int day, int year)> npcLastQuestOfferDate = new Dictionary<int, (int day, int year)>();
        public static Dictionary<int, List<int>> npcInvalidQuestIndices = new Dictionary<int, List<int>>();

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

            // Determine the maximum number of quests based on LocationType
            int maxQuestsForLocation = GetMaxQuestsForLocation();

            // Get quest offers for this NPC from external storage
            int questOffers = GetQuestOffersForNPC(serviceNPC);

            // Check if the maximum offers have been reached
            if (QuestOfferLocationsMod.limitGuildQuestions && questOffers >= maxQuestsForLocation)
            {
                ShowFailGetQuestMessage();
                Debug.Log("ShowFailGetQuestMessage 1");
                return;
            }

            // Start checking for unique quests
            int npcKey = serviceNPC.Data.hash;
            var timeStart = System.DateTime.Now;
            bool foundUniqueQuest = false;

            if (QuestOfferLocationsMod.avoidRepeatingGuildQuests)
            {
                while (!foundUniqueQuest)
                {
                    // Check timeout
                    if (System.DateTime.Now.Subtract(timeStart).TotalSeconds >= QuestOfferLocationsMod.maxSearchTimeInSeconds)
                    {
                        ShowFailGetQuestMessage();
                        Debug.Log("ShowFailGetQuestMessage 2");
                        SetQuestOffersForNPC(serviceNPC, maxQuestsForLocation);
                        return;
                    }

                    // Check if offeredQuest is already on the list of previously offered quests
                    if (npcQuestOfferNames.TryGetValue(npcKey, out var offeredQuestNames) && offeredQuestNames.Contains(offeredQuest.QuestName))
                    {
                        // Request a new quest
                        if (DaggerfallUnity.Settings.GuildQuestListBox)
                        {
                            offeredQuest = GameManager.Instance.QuestListsManager.LoadQuest(questPool[SelectedIndex], serviceNPC.Data.factionID);
                        }
                        else
                        {
                            offeredQuest = GameManager.Instance.QuestListsManager.GetGuildQuest(
                                guildGroup,
                                guildManager.GetGuild(guildGroup).IsMember() ? Questing.MembershipStatus.Member : Questing.MembershipStatus.Nonmember,
                                guildManager.GetGuild(guildGroup).GetFactionId(),
                                guildManager.GetGuild(guildGroup).GetReputation(playerEntity),
                                guildManager.GetGuild(guildGroup).Rank
                            );
                            Debug.Log($"New offered quest candidate in foundUniqueQuest loop: {offeredQuest?.QuestName ?? "null"}");
                        }

                        // If offeredQuest is null, continue to avoid null reference errors
                        if (offeredQuest == null)
                            continue;
                    }
                    else
                    {
                        // Quest is unique, we can proceed
                        foundUniqueQuest = true;
                    }
                }
            }

            if (offeredQuest != null)
            {
                if (QuestOfferLocationsMod.preferNearbyQuests)
                {
                    NearestQuest = null;

                    IGuild guild = guildManager.GetGuild(guildGroup);
                    int rep = guild.GetReputation(playerEntity);
                    int factionId = offeredQuest.FactionId;

                    timeStart = System.DateTime.Now;
                    bool found = false;
                    while (!found)
                    {
                        // Timeout to fallback on nearest quest
                        if (System.DateTime.Now.Subtract(timeStart).TotalSeconds >= QuestOfferLocationsMod.maxSearchTimeInSeconds)
                        {
                            found = true;
                            offeredQuest = NearestQuest?.Quest;
                            break;
                        }

                        if (offeredQuest == null)
                        {
                            continue;
                        }

                        var farthestTravelTimeInDays = 0.0f;
                        var questPlaces = offeredQuest.GetAllResources(typeof(Questing.Place));
                        foreach (Questing.Place questPlace in questPlaces)
                        {
                            DFLocation location;
                            DaggerfallUnity.Instance.ContentReader.GetLocation(questPlace.SiteDetails.regionName, questPlace.SiteDetails.locationName, out location);

                            var travelTimeDays = QuestOfferMessageHelper.GetTravelTimeToLocation(location);

                            if (travelTimeDays > farthestTravelTimeInDays)
                            {
                                farthestTravelTimeInDays = travelTimeDays;
                            }
                        }

                        if (farthestTravelTimeInDays > QuestOfferLocationsMod.maxTravelDistanceInDays)
                        {
                            if (NearestQuest == null || NearestQuest.TimeToTravelToQuestInDays > farthestTravelTimeInDays)
                            {
                                NearestQuest = new NearestQuest
                                {
                                    Quest = offeredQuest,
                                    TimeToTravelToQuestInDays = farthestTravelTimeInDays
                                };
                            }

                            if (DaggerfallUnity.Settings.GuildQuestListBox)
                            {
                                offeredQuest = GameManager.Instance.QuestListsManager.LoadQuest(questPool[SelectedIndex], factionId);
                            }
                            else
                            {
                                if (QuestOfferLocationsMod.avoidRepeatingGuildQuests)
                                {
                                    while (!foundUniqueQuest)
                                    {
                                        // Check timeout
                                        if (System.DateTime.Now.Subtract(timeStart).TotalSeconds >= QuestOfferLocationsMod.maxSearchTimeInSeconds)
                                        {
                                            ShowFailGetQuestMessage();
                                            Debug.Log("ShowFailGetQuestMessage 2");
                                            SetQuestOffersForNPC(serviceNPC, maxQuestsForLocation);
                                            return;
                                        }

                                        // Check if offeredQuest is already on the list of previously offered quests
                                        if (npcQuestOfferNames.TryGetValue(npcKey, out var offeredQuestNames) && offeredQuestNames.Contains(offeredQuest.QuestName))
                                        {
                                            // Request a new quest
                                            if (DaggerfallUnity.Settings.GuildQuestListBox)
                                            {
                                                offeredQuest = GameManager.Instance.QuestListsManager.LoadQuest(questPool[SelectedIndex], serviceNPC.Data.factionID);
                                            }
                                            else
                                            {
                                                offeredQuest = GameManager.Instance.QuestListsManager.GetGuildQuest(
                                                    guildGroup,
                                                    guildManager.GetGuild(guildGroup).IsMember() ? Questing.MembershipStatus.Member : Questing.MembershipStatus.Nonmember,
                                                    guildManager.GetGuild(guildGroup).GetFactionId(),
                                                    guildManager.GetGuild(guildGroup).GetReputation(playerEntity),
                                                    guildManager.GetGuild(guildGroup).Rank
                                                );
                                                Debug.Log($"New offered quest candidate in foundUniqueQuest loop: {offeredQuest?.QuestName ?? "null"}");
                                            }

                                            // If offeredQuest is null, continue to avoid null reference errors
                                            if (offeredQuest == null)
                                                continue;
                                        }
                                        else
                                        {
                                            // Quest is unique, we can proceed
                                            foundUniqueQuest = true;
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            found = true;
                            break;
                        }
                    }
                }

                if (offeredQuest == null)
                {
                    ShowFailGetQuestMessage();
                    Debug.Log("ShowFailGetQuestMessage 3");
                }
                else
                {
                    // Offer the quest to player, setting external context provider to guild if a member
                    if (guildManager.GetGuild(guildGroup).IsMember())
                        offeredQuest.ExternalMCP = guildManager.GetGuild(guildGroup);

                    DaggerfallMessageBox messageBox = QuestOfferMessageHelper.CreateQuestOffer(offeredQuest);
                    if (messageBox != null)
                    {
                        messageBox.OnButtonClick += OfferQuest_OnButtonClick;
                        messageBox.Show();
                    }

                    // Increment and save quest offers
                    SetQuestOffersForNPC(serviceNPC, questOffers + 1);

                    // Add the final offered quest name to the list
                    if (!npcQuestOfferNames.ContainsKey(npcKey))
                    {
                        npcQuestOfferNames[npcKey] = new List<string>();
                    }
                    npcQuestOfferNames[npcKey].Add(offeredQuest.QuestName);

                    // **Record the date of this quest offer**
                    int currentDay = DaggerfallUnity.Instance.WorldTime.Now.DayOfYear;
                    int currentYear = DaggerfallUnity.Instance.WorldTime.Now.Year;
                    npcLastQuestOfferDate[npcKey] = (currentDay, currentYear);

                    // Debug message showing the recorded date
                    Debug.Log($"NPC {npcKey} last offered a quest on day {currentDay} of year {currentYear}.");


                    // Debug message listing all quest names offered by this NPC
                    string offeredQuestList = string.Join(", ", npcQuestOfferNames[npcKey]);
                    Debug.Log($"NPC {npcKey} has offered the following quests: {offeredQuestList}");
                }
            }
            else
            {
                ShowFailGetQuestMessage();
                Debug.Log("ShowFailGetQuestMessage 4");
            }
            questPool.Clear();
        }

        private int GetQuestOffersForNPC(StaticNPC npc)
        {
            int npcKey = npc.Data.hash; // Use a unique identifier for the NPC
            int baseOffers = npcQuestOffers.TryGetValue(npcKey, out int offers) ? offers : 0;

            // Retrieve the last offer date
            if (npcLastQuestOfferDate.TryGetValue(npcKey, out var lastOfferDate))
            {
                int lastDay = lastOfferDate.day;
                int lastYear = lastOfferDate.year;

                // Calculate weeks since the last quest offer
                int currentDay = DaggerfallUnity.Instance.WorldTime.Now.DayOfYear;
                int currentYear = DaggerfallUnity.Instance.WorldTime.Now.Year;

                int daysElapsed = (currentYear - lastYear) * DaggerfallDateTime.DaysPerYear + (currentDay - lastDay);
                int monthsElapsed = Mathf.FloorToInt(daysElapsed / 30.0f);

                // Reduce the number of offers by monthsElapsed, down to a minimum of zero
                baseOffers = Mathf.Max(baseOffers - monthsElapsed, 0);
                npcQuestOffers[npcKey] = baseOffers;

                // Remove oldest quest names from the list as time passes
                if (npcQuestOfferNames.TryGetValue(npcKey, out var offeredQuestNames) && monthsElapsed > 0)
                {
                    int namesToRemove = Mathf.Min(monthsElapsed, offeredQuestNames.Count);
                    offeredQuestNames.RemoveRange(0, namesToRemove);

                    // Update the dictionary with the pruned list
                    if (offeredQuestNames.Count == 0)
                    {
                        npcQuestOfferNames.Remove(npcKey); // Clean up if no quests remain
                    }
                    else
                    {
                        npcQuestOfferNames[npcKey] = offeredQuestNames;
                    }
                }
            }

            return baseOffers;
        }

        private void SetQuestOffersForNPC(StaticNPC npc, int offers)
        {
            int npcKey = npc.Data.hash; // Use the same unique identifier
            npcQuestOffers[npcKey] = offers;
        }

        private int GetMaxQuestsForLocation()
        {
            PlayerGPS playerGPS = GameManager.Instance.PlayerGPS;
            switch (playerGPS.CurrentLocationType)
            {
                case DFRegion.LocationTypes.TownCity:
                    return QuestOfferLocationsMod.limitCityQuests; // Maximum quests for TownCity
                case DFRegion.LocationTypes.TownHamlet:
                    return QuestOfferLocationsMod.limitTownQuests; // Maximum quests for TownHamlet
                default:
                    return QuestOfferLocationsMod.limitVillageQuests; // Default maximum for other locations
            }
        }

        protected override void GettingQuestsBox_OnClose()
        {
            // Create a new ListPickerWindow
            DaggerfallListPickerWindow questPicker = new DaggerfallListPickerWindow(uiManager, uiManager.TopWindow);
            questPicker.OnItemPicked += QuestPicker_OnItemPicked;

            // Get the NPC key to filter previously invalid quests
            int npcKey = serviceNPC.Data.hash;

            // Ensure a list exists for this NPC in the dictionary
            if (!npcInvalidQuestIndices.ContainsKey(npcKey))
                npcInvalidQuestIndices[npcKey] = new List<int>();

            // Prepare a list of indices to remove from the questPool in case of failures
            List<int> failures = new List<int>();

            // Iterate through the questPool to populate the ListPickerWindow
            for (int i = 0; i < questPool.Count; i++)
            {
                try
                {
                    // Skip quests that are invalid for this NPC
                    if (npcInvalidQuestIndices[npcKey].Contains(i))
                        continue;

                    // Retrieve the faction ID safely
                    int factionId = guildManager.GetGuild(guildGroup).GetFactionId();

                    // Load the quest partially to get the human-readable name
                    Quest quest = GameManager.Instance.QuestListsManager.LoadQuest(questPool[i], factionId, true);

                    // Add quest name to the ListBox
                    string displayName = quest.DisplayName;
                    string localizedDisplayName = QuestMachine.Instance.GetLocalizedQuestDisplayName(quest.QuestName);
                    if (!string.IsNullOrEmpty(localizedDisplayName))
                        displayName = localizedDisplayName;

                    questPicker.ListBox.AddItem(displayName ?? quest.QuestName);
                    quest.Dispose();
                }
                catch
                {
                    // Record failures to remove these quests from the questPool
                    failures.Add(i);
                }
            }

            // Remove any quests that failed partial parsing
            foreach (int i in failures)
                questPool.RemoveAt(i);

            // Push the ListPickerWindow to the UI stack
            uiManager.PushWindow(questPicker);
        }

        protected override void QuestPicker_OnItemPicked(int visibleIndex, string name)
        {
            // Get the NPC key
            int npcKey = serviceNPC.Data.hash;

            // Ensure a list exists for this NPC in the dictionary
            if (!npcInvalidQuestIndices.ContainsKey(npcKey))
                npcInvalidQuestIndices[npcKey] = new List<int>();

            // Convert the visible index to the original index in questPool
            int adjustedIndex = visibleIndex;
            if (npcInvalidQuestIndices[npcKey].Count > 0)
            {
                foreach (int invalidIndex in npcInvalidQuestIndices[npcKey])
                {
                    if (invalidIndex <= adjustedIndex)
                        adjustedIndex++;
                }
            }

            // Add the adjusted index to the invalid indices for this NPC
            if (!npcInvalidQuestIndices[npcKey].Contains(adjustedIndex))
                npcInvalidQuestIndices[npcKey].Add(adjustedIndex);

            // Attempt to load the quest using the adjusted index
            int factionId = guildManager.GetGuild(guildGroup).GetFactionId(); // Retrieve faction ID safely
            offeredQuest = GameManager.Instance.QuestListsManager.LoadQuest(questPool[adjustedIndex], factionId);

            // Record the adjusted index
            SelectedIndex = adjustedIndex;

            DaggerfallUI.Instance.PlayOneShot(SoundClips.ButtonClick);
            DaggerfallUI.UIManager.PopWindow();
            if (offeredQuest != null && adjustedIndex < questPool.Count)
            {
                offeredQuest = GameManager.Instance.QuestListsManager.LoadQuest(questPool[adjustedIndex], factionId);
                OfferQuest();
            } else
            {
                // Show failure message and skip the base method call
                ShowFailGetQuestMessage();
                Debug.LogWarning($"Failed to load quest at adjusted index {adjustedIndex}.");
            }
        }
    }
}

