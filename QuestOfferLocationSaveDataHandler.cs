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
            npcQuestOffers = new Dictionary<int, int>()
        };
    }

    public object GetSaveData()
    {
        return new QuestOfferLocationSaveData
        {
            npcQuestOffers = QuestOfferLocationGuildServicePopUpWindow.npcQuestOffers
        };
    }

    public void RestoreSaveData(object saveData)
    {
        var data = saveData as QuestOfferLocationSaveData;
        if (data != null)
        {
            QuestOfferLocationGuildServicePopUpWindow.npcQuestOffers = data.npcQuestOffers ?? new Dictionary<int, int>();
        }
    }

    [Serializable]
    public class QuestOfferLocationSaveData
    {
        public Dictionary<int, int> npcQuestOffers;
    }
}
