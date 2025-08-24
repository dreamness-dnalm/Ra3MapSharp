// using Dreamness.RA3.Map.Parser.Core.Asset;
//
// namespace Dreamness.RA3.Map.Facade.model;
//
// public class WaypointModel
// {
//     private MapObject _mapObject;
//     
//     public WaypointModel(MapObject mapObject)
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
//     // the value of waypoint_name and unique_id may be the same
//     public string waypoint_name
//     {
//         get => _mapObject.assetPropertyCollection.getProperty("waypointName").data.ToString();
//         set
//         {
//             _mapObject.assetPropertyCollection.getProperty("uniqueID").data = value;
//             _mapObject.assetPropertyCollection.getProperty("waypointName").data = value;
//         }
//     }
//
//     public string unique_id
//     {
//         get => _mapObject.assetPropertyCollection.getProperty("uniqueID").data.ToString();
//         set
//         {
//             _mapObject.assetPropertyCollection.getProperty("uniqueID").data = value;
//             _mapObject.assetPropertyCollection.getProperty("waypointName").data = value;
//         }
//     }
//
//     public override string ToString()
//     {
//         return "WaypointModel{" +
//                "waypoint_name='" + waypoint_name + '\'' +
//                ", unique_id='" + unique_id + '\'' +
//                ", pos=(" + x + ", " + y + ", " + z + ")" +
//                '}';
//     }
// }