using Assets.Scripts.Game.UserInterfaceWindows;
using DaggerfallConnect;
using DaggerfallConnect.Arena2;
using DaggerfallConnect.Utility;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Questing;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Utility;
using DaggerfallWorkshop.Utility;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Assets.Scripts.Game.MacadaynuMods.QuestOfferLocations
{
    public static class QuestOfferMessageHelper
    {
        public static DaggerfallMessageBox CreateQuestOffer(Quest quest)
        {
            Message message = quest.GetMessage((int)QuestMachine.QuestMessages.QuestorOffer);

            List<TextFile.Token> tokens = message.GetTextTokens().ToList();

            Message acceptMessage = quest.GetMessage((int)QuestMachine.QuestMessages.AcceptQuest);

            var place = GetLastPlaceMentionedInMessage(acceptMessage);
            if (place == null)
            {
                place = GetLastPlaceMentionedInMessage(message);
            }

            if (place != null)
            {
                tokens.Add(TextFile.CreateFormatToken(TextFile.Formatting.NewLine));

                DFLocation location;
                DaggerfallUnity.Instance.ContentReader.GetLocation(place.SiteDetails.regionName, place.SiteDetails.locationName, out location);

                var compassDirectionToQuest = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(TalkManager.Instance.GetLocationCompassDirection(place).ToLower());

                if (location.LocationIndex == GameManager.Instance.PlayerGPS.CurrentLocation.LocationIndex)
                {
                    tokens.Add(TextFile.CreateTextToken($"This is a local quest."));
                }
                else
                {
                    tokens.Add(TextFile.CreateTextToken($"You will need to travel to {place.SiteDetails.locationName},"));
                    tokens.Add(TextFile.CreateFormatToken(TextFile.Formatting.JustifyCenter));
                    tokens.Add(TextFile.CreateTextToken($"which is {GetTravelTimeToPlace(place)} to the {compassDirectionToQuest}."));
                }

                tokens.Add(TextFile.CreateFormatToken(TextFile.Formatting.JustifyCenter));
            }

            //if (place == null)
            //{
                //tokens.Add(TextFile.CreateFormatToken(TextFile.Formatting.NewLine));

                //tokens.Add(TextFile.CreateTextToken("You will need to ask around for directions."));
                //tokens.Add(TextFile.CreateFormatToken(TextFile.Formatting.JustifyCenter));
            //}

            var textTokens = tokens.ToArray();

            DaggerfallMessageBox messageBox = new DaggerfallMessageBox(DaggerfallUI.UIManager, DaggerfallMessageBox.CommonMessageBoxButtons.YesNo, textTokens);
            messageBox.ClickAnywhereToClose = false;
            messageBox.AllowCancel = false;
            messageBox.ParentPanel.BackgroundColor = Color.clear;

            return messageBox;
        }

        public static string GetNPCQuestReminder()
        {
            var questIds = QuestMachine.Instance.GetAllActiveQuests();

            foreach (var questId in questIds)
            {
                var quest = QuestMachine.Instance.GetQuest(questId);

                QuestResource[] questPeople = quest.GetAllResources(typeof(Person));
                foreach (Person person in questPeople)
                {
                    if (person.IsQuestor)
                    {
                        if (QuestMachine.Instance.IsNPCDataEqual(person.QuestorData, QuestMachine.Instance.LastNPCClicked.Data))
                        {
                            return $"{person.DisplayName} would like you to complete {quest.QuestName} in 2 days.";
                        }
                    }
                }
            }

            return null;
        }

        private static Place GetLastPlaceMentionedInMessage(Message message)
        {
            if (message == null)
                return null;

            QuestMacroHelper helper = new QuestMacroHelper();
            QuestResource[] resources = helper.GetMessageResources(message);
            Debug.Log($"[QOL] GetMessageResources returned {resources?.Length ?? 0} resources.");

            // 1) Try any explicit Place resource first
            if (resources != null)
            {
                Place explicitPlace = GetLastPlaceInResources(resources);
                if (explicitPlace != null)
                    return explicitPlace;
            }
            else
            {
                Debug.Log("[QOL] GetMessageResources returned null.");
            }

            // No Place resource in quest, fallback to Person resource
            // Grab the raw tokens (no macro expansion) so we still see the underscores
            var rawTokens = message.GetTextTokens(variant: -1, expandMacros: false);

            // Regex to catch both NameMacro3 and NameMacro4: ___Foo_ or ____Foo_
            var rePlace = new Regex(@"_{3,4}([A-Za-z0-9\.]+)_");

            Place lastPlace = null;

            foreach (var token in rawTokens)
            {
                // Split on spaces—same as QuestMacroHelper.GetWords
                foreach (var word in token.text.Split(' '))
                {
                    var m = rePlace.Match(word);
                    if (!m.Success)
                        continue;

                    // symbol inside the underscores
                    string symbol = m.Groups[1].Value;

                    // Look up the quest resource by name
                    var resource = message.ParentQuest.GetResource(symbol);
                    if (resource is Place p)
                    {
                        // explicit Place resource mentioned
                        lastPlace = p;
                    }
                    else if (resource is Person person)
                    {
                        // place‐style macro on a Person means "town name" or "region name" macro
                        // so pull their dialog‐place (or home‐place) out
                        var dialogPlace = person.GetDialogPlace() ?? person.GetHomePlace();
                        if (dialogPlace != null)
                            lastPlace = dialogPlace;
                    }
                }
            }

            return lastPlace;
        }

        public static Place GetLastPlaceInResources(QuestResource[] resources)
        {
            if (resources == null || resources.Length == 0)
            {
                //Debug.Log("[QOL] GetLastPlaceInResources: resource array is null or empty.");
                return null;
            }

            Place lastPlace = null;
            for (int i = 0; i < resources.Length; i++)
            {
                QuestResource resource = resources[i];
                if (resource is Place place)
                {
                    lastPlace = place;
                    //Debug.Log($"[QOL] GetLastPlaceInResources: Resource[{i}] is a Place - LocationName: {place.SiteDetails.locationName}, Region: {place.SiteDetails.regionName}");
                }
                //else if (resource is Person person)
                //{
                    //Debug.Log($"[QOL] GetLastPlaceInResources: Resource[{i}] is a Person - DisplayName: {person.DisplayName}");
                //}
                //else
                //{
                    //Debug.Log($"[QOL] GetLastPlaceInResources: Resource[{i}] Type: {resource.GetType().Name}, ToString: {resource.ToString()}");
                //}
            }

            //if (lastPlace == null)
                //Debug.Log("[QOL] GetLastPlaceInResources did not find any Place resource after checking all resources.");

            return lastPlace;
        }

        private static string GetTravelTimeToPlace(Place place)
        {
            DFLocation location;
            DaggerfallUnity.Instance.ContentReader.GetLocation(place.SiteDetails.regionName, place.SiteDetails.locationName, out location);
           
            if (QuestOfferLocationsMod.useTravelOptionsTimeCalc)
            {
                return GetTravelTimeToLocationFromTravelOptions(location);
            }
            else
            {
                var travelTimeDays = GetTravelTimeToLocation(location);
                return $"{(int)travelTimeDays} days travel";
            }
        }


        static float ConvertMinutesToDays(int minutes)
        {
            TimeSpan result = TimeSpan.FromMinutes(minutes);

            return (float)result.TotalDays;
        }


        public static float GetTravelTimeToLocation(DFLocation location)
        {
            DFPosition position = MapsFile.LongitudeLatitudeToMapPixel(location.MapTableData.Longitude, location.MapTableData.Latitude);

            if (QuestOfferLocationsMod.useTravelOptionsTimeCalc)
            {
                TransportManager transportManager = GameManager.Instance.TransportManager;
                bool horse = transportManager.TransportMode == TransportModes.Horse;
                bool cart = transportManager.TransportMode == TransportModes.Cart;

                var travelTimeCalculator = new TravelTimeCalculator();

                var travelTimeTotalMins = travelTimeCalculator.CalculateTravelTime(position,
                    speedCautious: false,
                    sleepModeInn: false,
                    travelShip: false,
                    hasHorse: horse,
                    hasCart: cart);
                travelTimeTotalMins = GameManager.Instance.GuildManager.FastTravel(travelTimeTotalMins);

                //TODO: can make this calc dynamic based on mod settings
                float travelTimeMinsMult = 0.8f * 2;
                travelTimeTotalMins = (int)(travelTimeTotalMins / travelTimeMinsMult);
                //int travelTimeHours = travelTimeTotalMins / 60;
                //int travelTimeMinutes = travelTimeTotalMins % 60;

                //return string.Format("{0} hours {1} mins travel", travelTimeHours, travelTimeMinutes);

                return ConvertMinutesToDays(travelTimeTotalMins);

                //return string.Format("{0} hours {1} mins travel", travelTimeHours, travelTimeMinutes);
            }
            else
            {
                bool hasHorse = GameManager.Instance.TransportManager.HasHorse();
                bool hasCart = GameManager.Instance.TransportManager.HasCart();

                //TODO: can consider ship calcs
                //bool hasShip = GameManager.Instance.TransportManager.ShipAvailiable();

                var travelTimeCalculator = new TravelTimeCalculator();

                var travelTimeTotalMins = travelTimeCalculator.CalculateTravelTime(position,
                    speedCautious: true,
                    sleepModeInn: true,
                    travelShip: false,
                    hasHorse: hasHorse,
                    hasCart: hasCart);

                // Players can have fast travel benefit from guild memberships
                travelTimeTotalMins = GameManager.Instance.GuildManager.FastTravel(travelTimeTotalMins);

                int travelTimeDaysTotal = (travelTimeTotalMins / 1440);

                // Classic always adds 1. For DF Unity, only add 1 if there is a remainder to round up.
                if ((travelTimeTotalMins % 1440) > 0)
                    travelTimeDaysTotal += 1;

        //        return $"{travelTimeDaysTotal} days travel";
        //    }
        //}
                return travelTimeDaysTotal;
            }
        }

        public static string GetTravelTimeToLocationFromTravelOptions(DFLocation location)
        {
            DFPosition position = MapsFile.LongitudeLatitudeToMapPixel(location.MapTableData.Longitude, location.MapTableData.Latitude);

            TransportManager transportManager = GameManager.Instance.TransportManager;
            bool horse = transportManager.TransportMode == TransportModes.Horse;
            bool cart = transportManager.TransportMode == TransportModes.Cart;

            var travelTimeCalculator = new TravelTimeCalculator();

            var travelTimeTotalMins = travelTimeCalculator.CalculateTravelTime(position,
                speedCautious: false,
                sleepModeInn: false,
                travelShip: false,
                hasHorse: horse,
                hasCart: cart);
            travelTimeTotalMins = GameManager.Instance.GuildManager.FastTravel(travelTimeTotalMins);

            //TODO: can make this calc dynamic based on mod settings
            float travelTimeMinsMult = 0.8f * 2;
            travelTimeTotalMins = (int)(travelTimeTotalMins / travelTimeMinsMult);
            int travelTimeHours = travelTimeTotalMins / 60;
            int travelTimeMinutes = travelTimeTotalMins % 60;

            return string.Format("{0} hours {1} mins travel", travelTimeHours, travelTimeMinutes);            
        }
    }
}
