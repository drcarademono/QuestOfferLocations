using System;
using System.Collections;
using System.Collections.Generic;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.UserInterface;
using Assets.Scripts.Game.MacadaynuMods.QuestOfferLocations;
using Assets.Scripts.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.UserInterfaceWindows;

public class QuestOfferLocationSaveDataHandler : IHasModSaveData
{
    private static QuestOfferLocationSaveDataHandler instance;

    public static QuestOfferLocationSaveDataHandler Instance
    {
        get
        {
            if (instance == null)
                instance = new QuestOfferLocationSaveDataHandler();
            return instance;
        }
    }

    public Type SaveDataType => typeof(QuestOfferLocationSaveData);

    public object NewSaveData()
    {
        return new QuestOfferLocationSaveData
        {
            npcQuestOffers = new Dictionary<int, int>(),
            npcQuestOfferNames = new Dictionary<int, List<string>>(),
            npcLastQuestOfferDate = new Dictionary<int, (int day, int year)>()
        };
    }

    public object GetSaveData()
    {
        return new QuestOfferLocationSaveData
        {
            npcQuestOffers = QuestOfferLocationGuildServicePopUpWindow.npcQuestOffers,
            npcQuestOfferNames = QuestOfferLocationGuildServicePopUpWindow.npcQuestOfferNames,
            npcLastQuestOfferDate = QuestOfferLocationGuildServicePopUpWindow.npcLastQuestOfferDate
        };
    }

    public void RestoreSaveData(object saveData)
    {
        var data = saveData as QuestOfferLocationSaveData;
        if (data != null)
        {
            QuestOfferLocationGuildServicePopUpWindow.npcQuestOffers = data.npcQuestOffers ?? new Dictionary<int, int>();
            QuestOfferLocationGuildServicePopUpWindow.npcQuestOfferNames = data.npcQuestOfferNames ?? new Dictionary<int, List<string>>();
            QuestOfferLocationGuildServicePopUpWindow.npcLastQuestOfferDate = data.npcLastQuestOfferDate ?? new Dictionary<int, (int day, int year)>();
        }
    }

    [Serializable]
    public class QuestOfferLocationSaveData
    {
        public Dictionary<int, int> npcQuestOffers;
        public Dictionary<int, List<string>> npcQuestOfferNames;
        public Dictionary<int, (int day, int year)> npcLastQuestOfferDate;
    }
}
