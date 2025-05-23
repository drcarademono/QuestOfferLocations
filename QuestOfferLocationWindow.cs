using UnityEngine;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.Questing;
using DaggerfallConnect.Arena2;
using DaggerfallWorkshop.Game.Entity;
using Assets.Scripts.Game.MacadaynuMods.QuestOfferLocations;
using DaggerfallConnect;
using Assets.Scripts.Game.UserInterfaceWindows;
using System.Linq;
using System.Collections.Generic;

namespace DaggerfallWorkshop.Game.UserInterfaceWindows
{
    public class QuestOfferLocationWindow : DaggerfallQuestOfferWindow
    {
        public NearestQuest NearestQuest;

        #region Constructors

        public QuestOfferLocationWindow(IUserInterfaceManager uiManager, StaticNPC.NPCData npc, FactionFile.SocialGroups socialGroup, bool menu)
            : base(uiManager, npc, socialGroup, menu)
        {
        }

        #endregion

        protected override void Setup()
        {
            CloseWindow();
            GetQuest();
        }

        #region Quest handling

        protected override void GetQuest()
        {
            // Just exit if this NPC is already involved in an active quest
            if (QuestMachine.Instance.IsLastNPCClickedAnActiveQuestor())
            {
                CloseWindow();
                return;
            }

            // Get the faction id for affecting reputation on success/failure, and current rep
            int factionId = questorNPC.factionID;
            Genders gender = questorNPC.gender;
            int reputation = GameManager.Instance.PlayerEntity.FactionData.GetReputation(factionId);
            int level = GameManager.Instance.PlayerEntity.Level;

            // Also get a unique key for this NPC (using the NPC's hash)
            int npcKey = questorNPC.nameSeed;

            // Set up the initial offered quest using the correct factionId.
            offeredQuest = GameManager.Instance.QuestListsManager.GetSocialQuest(socialGroup, factionId, gender, reputation, level);

            // Filter out quests already offered by this NPC using the unique key.
            if (QuestOfferLocationGuildServicePopUpWindow.npcQuestOfferNames.TryGetValue(npcKey, out var offeredQuestNames))
            {
                while (offeredQuest != null && offeredQuestNames.Contains(offeredQuest.QuestName))
                {
                    offeredQuest = GameManager.Instance.QuestListsManager.GetSocialQuest(socialGroup, factionId, gender, reputation, level);
                }
            }

            if (QuestOfferLocationsMod.preferNearbyQuests)
            {
                NearestQuest = null;

                var timeStart = System.DateTime.Now;
                bool found = false;
                while (!found)
                {
                    // Timeout to fallback to nearest quest
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
                    else
                    {
                        var places = new List<Place>();
                        places.AddRange(offeredQuest
                            .GetAllResources(typeof(Place))
                            .OfType<Place>());
                        foreach (Person person in offeredQuest
                            .GetAllResources(typeof(Person))
                            .OfType<Person>())
                        {
                            var dialogPlace = person.GetDialogPlace() ?? person.GetHomePlace();
                            if (dialogPlace != null)
                                places.Add(dialogPlace);
                        }

                        float farthestTravelTimeInDays = 0.0f;
                        foreach (Place questPlace in places)
                        {
                            DFLocation location;
                            DaggerfallUnity.Instance.ContentReader.GetLocation(questPlace.SiteDetails.regionName, questPlace.SiteDetails.locationName, out location);

                            float travelTimeDays = QuestOfferMessageHelper.GetTravelTimeToLocation(location);

                            if (travelTimeDays > farthestTravelTimeInDays)
                            {
                                farthestTravelTimeInDays = travelTimeDays;
                            }
                        }

                        if (!found && farthestTravelTimeInDays > QuestOfferLocationsMod.maxTravelDistanceInDays)
                        {
                            // Store the nearest quest in the loop as a fallback
                            if (NearestQuest == null || NearestQuest.TimeToTravelToQuestInDays > farthestTravelTimeInDays)

                            {
                                NearestQuest = new NearestQuest
                                {
                                    Quest = offeredQuest,
                                    TimeToTravelToQuestInDays = farthestTravelTimeInDays
                                };
                            }

                            offeredQuest = GameManager.Instance.QuestListsManager.GetSocialQuest(socialGroup, factionId, gender, reputation, level);
                            continue;
                        }
                        else
                        {
                            found = true;
                            break;
                        }
                    }
                }
            }

            if (offeredQuest != null)
            {
                // Debug: Check for Place resources in the quest.
                QuestResource[] placeResources = offeredQuest.GetAllResources(typeof(Place));
                if (placeResources != null && placeResources.Length > 0)
                {
                    foreach (QuestResource resource in placeResources)
                    {
                        Place place = resource as Place;
                        if (place != null)
                        {
                            //Debug.Log($"[QOL] Found Place resource: LocationName = {place.SiteDetails.locationName}, Region = {place.SiteDetails.regionName}");
                        }
                    }
                }
                else
                {
                    //Debug.Log("[QOL] No Place resource found in offeredQuest.");
                }
                
                // Offer the quest to the player
                var messageBox = QuestOfferMessageHelper.CreateQuestOffer(offeredQuest);
                if (messageBox != null)
                {
                    messageBox.OnButtonClick += OfferQuest_OnButtonClick;
                    messageBox.Show();
                }
            }
            else if (!GameManager.Instance.IsPlayerInsideCastle) // Failed get quest messages do not appear inside castles in classic.
            {
                ShowFailGetQuestMessage();
            }

        }

        #endregion
    }
}
