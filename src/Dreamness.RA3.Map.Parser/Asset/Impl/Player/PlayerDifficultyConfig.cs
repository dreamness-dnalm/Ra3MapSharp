using System.Text.Json.Serialization;

namespace Dreamness.Ra3.Map.Parser.Asset.Impl.Player;

public class PlayerDifficultyConfig
{
    public bool Easy { get; private set; }
    
    public bool Normal{ get; private set; }
    
    public bool Hard { get; private set; }
    
    public bool Brutal{ get; private set; }

    [JsonConstructor]
    public PlayerDifficultyConfig(bool easy, bool normal, bool hard, bool brutal)
    {

        Easy = easy;
        Normal = normal;
        Hard = hard;
        Brutal = brutal;
    }

    
    public static PlayerDifficultyConfig Of(bool easy, bool normal, bool hard, bool brutal)
    {
        return new PlayerDifficultyConfig(easy, normal, hard, brutal);
    }
    
    public static PlayerDifficultyConfig Of(int? value)
    {
        
        if (value == null)
        {
            return Of(false, false, false, false);
        }
        else
        {
            var easy = (value & 1) != 0;
            var normal = (value & 2) != 0;
            var hard = (value & 4) != 0;
            var brutal = (value & 8) != 0;
            
            return Of(easy, normal, hard, brutal);
            
        }
    }
    
    public int ToValue()
    {
        int _value = 0;
        if (Easy) _value |= 1;
        if (Normal) _value |= 2;
        if (Hard) _value |= 4;
        if (Brutal) _value |= 8;
        return _value;
    }

}