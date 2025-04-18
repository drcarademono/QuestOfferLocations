using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using UnityEngine;

namespace Assets.Scripts.Game.UserInterfaceWindows
{
    public class QuestOfferLocationsMod : MonoBehaviour
    {
        static Mod mod;
        public static bool useTravelOptionsTimeCalc;
        public static bool preferNearbyQuests;
        public static float maxTravelDistanceInDays;
        public static int maxSearchTimeInSeconds;
        public static bool limitGuildQuestions;
        public static int limitVillageQuests;
        public static int limitTownQuests;
        public static int limitCityQuests;
        public static bool avoidRepeatingGuildQuests;

        public void Awake()
        {
            mod.IsReady = true;
        }

        public void Start()
        {
            UIWindowFactory.RegisterCustomUIWindow(UIWindowType.GuildServicePopup, typeof(QuestOfferLocationGuildServicePopUpWindow));
            UIWindowFactory.RegisterCustomUIWindow(UIWindowType.QuestOffer, typeof(QuestOfferLocationWindow));

            Mod travelOptionsMod = ModManager.Instance.GetMod("TravelOptions");

            if (travelOptionsMod != null)
            {
                var travelOptionsModEnabled = travelOptionsMod.Enabled;
                var travelOptionsSettings = travelOptionsMod.GetSettings();
                var travelOptionsCautiousTravel = travelOptionsSettings.GetBool("CautiousTravel", "PlayerControlledCautiousTravel");
                var travelOptionsStopAtInnsTravel = travelOptionsSettings.GetBool("StopAtInnsTravel", "PlayerControlledInnsTravel");

                useTravelOptionsTimeCalc = travelOptionsModEnabled && travelOptionsCautiousTravel && travelOptionsStopAtInnsTravel;
            }
        }

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;           
            var go = new GameObject(mod.Title);
            go.AddComponent<QuestOfferLocationsMod>();

            var settings = mod.GetSettings();
            preferNearbyQuests = settings.GetBool("LocationSettings", "PreferNearbyQuests");
            maxTravelDistanceInDays = settings.GetFloat("LocationSettings", "MaxTravelDistanceInDays");
            maxSearchTimeInSeconds = settings.GetInt("LocationSettings", "MaxSearchTimeInSeconds");
            limitGuildQuestions = settings.GetBool("LocationSettings", "LimitGuildQuests");
            limitVillageQuests = settings.GetInt("LocationSettings", "LimitVillageQuests");
            limitTownQuests = settings.GetInt("LocationSettings", "LimitTownQuests");
            limitCityQuests = settings.GetInt("LocationSettings", "LimitCityQuests");
            avoidRepeatingGuildQuests = settings.GetBool("LocationSettings", "AvoidRepeatingGuildQuests");

            // Register the save data handler
            mod.SaveDataInterface = QuestOfferLocationSaveDataHandler.Instance;
        }
    }
}
