// using Dreamness.RA3.Map.Parser.Core.Asset;
// using Dreamness.RA3.Map.Facade.enums;
//
// namespace Dreamness.RA3.Map.Facade.models;
//
// public class ObjectModel
// {
//
//     
//     public MapObject _mapObject;
//     
//     public ObjectModel(MapObject mapObject)
//     {
//         _mapObject = mapObject;
//     }
//     
//     public float angle
//     {
//         get => _mapObject.angle;
//         set => _mapObject.angle = value;
//     }
//     
//     public float x
//     {
//         get => _mapObject.position.x;
//         set => _mapObject.position.x = value;
//     }
//     
//     public float y
//     {
//         get => _mapObject.position.y;
//         set => _mapObject.position.y = value;
//     }
//     
//     public float z
//     {
//         get => _mapObject.position.z;
//         set => _mapObject.position.z = value;
//     }
//     
//     public string unique_id => _mapObject.assetPropertyCollection.getProperty("uniqueID").data.ToString();
//     
//     public string type_name
//     {
//         get => _mapObject.typeName;
//         set => _mapObject.typeName = value;
//     }
//
//     public string belong_to_team_full_name
//     {
//         get => _mapObject.assetPropertyCollection.getProperty("originalOwner").data.ToString();
//         set => _mapObject.assetPropertyCollection.getProperty("originalOwner").data = value;
//     }
//
//     public string? object_name
//     {
//         get
//         {
//             var assetProperty = _mapObject.assetPropertyCollection.getProperty("objectName");
//             if (assetProperty == null)
//             {
//                 return null;
//             }
//             else
//             {
//                 return assetProperty.data.ToString();
//             }
//         }
//         set => _mapObject.assetPropertyCollection.getProperty("objectName").data = value;
//     }
//
//     public int? initial_health
//     {
//         get
//         {
//             var assetProperty = _mapObject.assetPropertyCollection.getProperty("objectInitialHealth");
//             if(assetProperty == null){
//                 return null;
//             }else{
//                 return (int)assetProperty.data;
//             }
//         }
//         set => _mapObject.assetPropertyCollection.getProperty("objectInitialHealth").data = value;
//     }
//
//     public bool? enabled
//     {
//         get
//         {
//             var assetProperty = _mapObject.assetPropertyCollection.getProperty("objectEnabled");
//             return assetProperty == null ? null : (bool)assetProperty.data;
//         }
//         set => _mapObject.assetPropertyCollection.getProperty("objectEnabled").data = value;
//     }
//
//     public bool? indestructible
//     {
//         get
//         {
//             var assetProperty = _mapObject.assetPropertyCollection.getProperty("objectIndestructible");
//             return assetProperty == null ? null : (bool)assetProperty.data;
//         }
//         set => _mapObject.assetPropertyCollection.getProperty("objectIndestructible").data = value;
//     }
//     
//     public bool? unsellable
//     {
//         get
//         {
//             var assetProperty = _mapObject.assetPropertyCollection.getProperty("objectUnsellable");
//             return assetProperty == null ? null : (bool)assetProperty.data;
//         }
//         set => _mapObject.assetPropertyCollection.getProperty("objectUnsellable").data = value;
//     }
//     
//     public bool? powered
//     {
//         get
//         {
//             var assetProperty = _mapObject.assetPropertyCollection.getProperty("objectPowered");
//             return assetProperty == null ? null : (bool)assetProperty.data;
//         }
//         set => _mapObject.assetPropertyCollection.getProperty("objectPowered").data = value;
//     }
//     
//     public bool? recruitable_ai
//     {
//         get
//         {
//             var assetProperty = _mapObject.assetPropertyCollection.getProperty("objectRecruitableAI");
//             return assetProperty == null ? null : (bool)assetProperty.data;
//         }
//         set => _mapObject.assetPropertyCollection.getProperty("objectRecruitableAI").data = value;
//     }
//     
//     public bool? targetable
//     {
//         get
//         {
//             var assetProperty = _mapObject.assetPropertyCollection.getProperty("objectTargetable");
//             return assetProperty == null ? null : (bool)assetProperty.data;
//         }
//         set => _mapObject.assetPropertyCollection.getProperty("objectTargetable").data = value;
//     }
//     
//     public bool? sleeping
//     {
//         get
//         {
//             var assetProperty = _mapObject.assetPropertyCollection.getProperty("objectSleeping");
//             return assetProperty == null ? null : (bool)assetProperty.data;
//         }
//         set => _mapObject.assetPropertyCollection.getProperty("objectSleeping").data = value;
//     }
//     
//     public int? base_priority
//     {
//         get
//         {
//             var assetProperty = _mapObject.assetPropertyCollection.getProperty("objectBasePriority");
//             return assetProperty == null ? null : (int)assetProperty.data;
//         }
//         set => _mapObject.assetPropertyCollection.getProperty("objectBasePriority").data = value;
//     }
//     
//     public int? base_phase
//     {
//         get
//         {
//             var assetProperty = _mapObject.assetPropertyCollection.getProperty("objectBasePhase");
//             return assetProperty == null ? null : (int)assetProperty.data;
//         }
//         set => _mapObject.assetPropertyCollection.getProperty("objectBasePhase").data = value;
//     }
//     
//     public string? layer
//     {
//         get
//         {
//             var assetProperty = _mapObject.assetPropertyCollection.getProperty("objectLayer");
//             return assetProperty == null ? null : assetProperty.data.ToString();
//         }
//         set => _mapObject.assetPropertyCollection.getProperty("objectLayer").data = value;
//     }
//     
//     public string? stance
//     {
//         get
//         {
//             var assetProperty = _mapObject.assetPropertyCollection.getProperty("objectInitialStance");
//             return assetProperty == null ? null : ((StanceEnum)((int)assetProperty.data)).ToString();
//         }
//         set => _mapObject.assetPropertyCollection.getProperty("objectInitialStance").data = value == null ? null : (int)Enum.Parse<StanceEnum>(value);
//     }
//     
//     public int? experience_level
//     {
//         get {
//             var assetProperty = _mapObject.assetPropertyCollection.getProperty("objectExperienceLevel");
//             if(assetProperty == null){
//                 return null;
//             }else{
//                 return (int)assetProperty.data;
//             }
//         }
//         set
//         {
//             if (value < 1 || value > 4)
//             {
//                 throw new ArgumentException("Experience level must be between 1 and 4");
//             }
//             _mapObject.assetPropertyCollection.getProperty("objectExperienceLevel").data = value;
//         }
//     }
//
//     public override string ToString()
//     {
//         return "ObjectModel{" +
//                "angle=" + angle +
//                ", x=" + x +
//                ", y=" + y +
//                ", z=" + z +
//                ", unique_id='" + unique_id + '\'' +
//                ", type_name='" + type_name + '\'' +
//                ", belong_to_team_name='" + belong_to_team_full_name + '\'' +
//                ", object_name='" + object_name + '\'' +
//                ", initial_health=" + initial_health +
//                ", enabled=" + enabled +
//                ", indestructible=" + indestructible +
//                ", unsellable=" + unsellable +
//                ", powered=" + powered +
//                ", recruitable_ai=" + recruitable_ai +
//                ", targetable=" + targetable +
//                ", sleeping=" + sleeping +
//                ", base_priority=" + base_priority +
//                ", base_phase=" + base_phase +
//                ", layer='" + layer + '\'' +
//                ", stance='" + stance + '\'' +
//                ", experience_level=" + experience_level +
//                '}';
//     }
// }