using UnityEngine;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallConnect.Arena2;
using Assets.Scripts.Game.MacadaynuMods.QuestOfferLocations;
using DaggerfallConnect;
using DaggerfallWorkshop.Game.Guilds;
using Assets.Scripts.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game;
using System.Collections.Generic;

namespace DaggerfallWorkshop.Game.UserInterfaceWindows
{
    public class QuestOfferLocationGuildServicePopUpWindow : DaggerfallGuildServicePopupWindow
    {
        public static Dictionary<int, int> npcQuestOffers = new Dictionary<int, int>();
        public static Dictionary<int, List<string>> npcQuestOfferNames = new Dictionary<int, List<string>>();


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
            if (questOffers >= maxQuestsForLocation)
            {
                ShowFailGetQuestMessage();
                Debug.Log("ShowFailGetQuestMessage 1");
                return;
            }

            // Start checking for unique quests
            int npcKey = serviceNPC.Data.hash;
            var timeStart = System.DateTime.Now;
            bool foundUniqueQuest = false;

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
                        Debug.Log($"New offered quest candidate: {offeredQuest?.QuestName ?? "null"}");
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
                                offeredQuest = GameManager.Instance.QuestListsManager.GetGuildQuest(
                                    guildGroup,
                                    guild.IsMember() ? Questing.MembershipStatus.Member : Questing.MembershipStatus.Nonmember,
                                    guild.GetFactionId(),
                                    rep,
                                    guild.Rank
                                );
                                Debug.Log($"New offered quest candidate: {offeredQuest.QuestName}");
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
            return npcQuestOffers.TryGetValue(npcKey, out int offers) ? offers : 0;
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
                    return 6; // Maximum quests for TownCity
                case DFRegion.LocationTypes.TownHamlet:
                    return 4; // Maximum quests for TownHamlet
                default:
                    return 2; // Default maximum for other locations
            }
        }

        protected override void QuestPicker_OnItemPicked(int index, string name)
        {
            SelectedIndex = index;

            base.QuestPicker_OnItemPicked(index, name);
        }
    }
}

