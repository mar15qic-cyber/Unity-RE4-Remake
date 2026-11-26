public enum VillageEntrance
{
    None,
    Farm,
    House
}

public sealed partial class MissionRulesModel
{
    private VillageEntrance firstEntrance;
    private bool houseVisited;
    private bool pendingFarmEntrance;
    private bool pendingHouseEntrance;
    private bool houseHasSpawn;

    public VillageEntrance PendingEntrance
    {
        get
        {
            if (!RuntimeMiddle || Completed)
            {
                return VillageEntrance.None;
            }

            if (pendingFarmEntrance)
            {
                return VillageEntrance.Farm;
            }

            if (pendingHouseEntrance)
            {
                return VillageEntrance.House;
            }

            return VillageEntrance.None;
        }
    }

    public void ObserveVillageEntrances(bool enteredHouse, bool approachedFarm, int killThreshold, float timeThreshold)
    {
        if (!RuntimeMiddle || Completed || bellKillTarget == 0)
        {
            return;
        }

        if (enteredHouse && !houseVisited)
        {
            houseVisited = true;
            pendingHouseEntrance = true;
            if (firstEntrance == VillageEntrance.None)
            {
                firstEntrance = VillageEntrance.House;
                houseHasSpawn = true;
            }
            else if (firstEntrance == VillageEntrance.Farm && ChainsawKill == 1 && ChainsawAlive == 0)
            {
                houseHasSpawn = true;
            }
        }

        if (firstEntrance == VillageEntrance.None && (approachedFarm || NormalKill >= killThreshold || UsedTime >= timeThreshold))
        {
            firstEntrance = VillageEntrance.Farm;
            pendingFarmEntrance = true;
        }
    }

    public bool CanSpawnAtEntrance(VillageEntrance entrance)
    {
        if (entrance == VillageEntrance.None || entrance != PendingEntrance || ChainsawAlive != 0 || ChainsawHasGenerate >= 2)
        {
            return false;
        }

        return entrance == VillageEntrance.Farm || houseHasSpawn;
    }

    public bool CompleteEntrance(VillageEntrance entrance)
    {
        if (entrance == VillageEntrance.None || entrance != PendingEntrance)
        {
            return false;
        }

        if (entrance == VillageEntrance.Farm)
        {
            pendingFarmEntrance = false;
        }
        else
        {
            pendingHouseEntrance = false;
            houseHasSpawn = false;
        }

        return true;
    }

    private void ResetVillageEntrance()
    {
        firstEntrance = VillageEntrance.None;
        houseVisited = false;
        pendingFarmEntrance = false;
        pendingHouseEntrance = false;
        houseHasSpawn = false;
    }
}
