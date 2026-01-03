using Dreamness.RA3.Map.Parser.Asset.Impl.MissionObjective;

namespace Dreamness.Ra3.Map.Facade.Core;

public partial class Ra3MapFacade
{
    private MissionObjectivesAsset _missionObjectivesAsset;
    
    public List<MissionObjective> GetMissionObjectives()
    {
        if (_missionObjectivesAsset == null)
        {
            return new List<MissionObjective>();
        }
        return _missionObjectivesAsset.Objectives.ToList();
    }

    public MissionObjective AddMissionObjective(string id, string text, string description, bool isBonus=false, string type="Attack")
    {
        if (_missionObjectivesAsset == null)
        {
            ra3Map.Context.OverrideAsset(MissionObjectivesAsset.Default(ra3Map.Context));
            LoadMissionObjectPart();
        }
        var objective = MissionObjective.Of(id, text, description, isBonus, type);
        _missionObjectivesAsset.Add(objective);
        return objective;
    }
    
    
    
    // ----- init -------
    
    private void LoadMissionObjectPart()
    {
        _missionObjectivesAsset = ra3Map.Context.MissionObjectivesAsset;
    }
}