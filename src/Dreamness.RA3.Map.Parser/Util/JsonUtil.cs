using System.ComponentModel.Design.Serialization;
using System.Text.Json;
using Dreamness.Ra3.Map.Parser.Asset.SubAsset;

namespace Dreamness.Ra3.Map.Parser.Util;

public class JsonUtil
{
    public static string Serialize(object obj)
    {
        return System.Text.Json.JsonSerializer.Serialize(obj, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    public static String GetString(JsonDocument doc, string key)
    {
        try
        {
            var ret = doc.RootElement.GetProperty(key).GetString();
            if (ret == null)
            {
                throw new KeyNotFoundException(key);
            }

            return ret;
        }
        catch (System.Exception e)
        {
            throw new System.Exception("parse string key error, key" + key, e);
        }
        
    }
    
    public static String GetStringOrNull(JsonDocument doc, string key)
    {
        try
        {
            var ret = doc.RootElement.GetProperty(key).GetString();
            return ret;
        }
        catch (System.Exception e)
        {
            return null;
        }
    }

    public static bool GetBool(JsonDocument doc, string key)
    {
        try
        {
            var ret = doc.RootElement.GetProperty(key).GetBoolean();
            if (ret == null)
            {
                throw new KeyNotFoundException(key);
            }
            return ret;
        } catch (System.Exception e)
        {
            throw new System.Exception("parse bool key error, key" + key, e);
        }
        
        
    }

    public static bool? GetBoolOrNull(JsonDocument doc, string key)
    {
        try
        {
            var ret = doc.RootElement.GetProperty(key).GetBoolean();
            return ret;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    public static int GetInt(JsonDocument doc, string key)
    {
        try
        {
            var ret = doc.RootElement.GetProperty(key).GetInt32();
            if (ret == null)
            {
                throw new KeyNotFoundException(key);
            }
            return ret;
        } catch (System.Exception e)
        {
            throw new System.Exception("parse int key error, key" + key, e);
        }

    }
    
    public static int? GetIntOrNull(JsonDocument doc, string key)
    {
        try
        {
            var ret = doc.RootElement.GetProperty(key).GetInt32();
            return ret;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
    
    public static List<String> GetStringList(JsonDocument doc, string key)
    {
        try
        {
            if (doc.RootElement.TryGetProperty(key, out var element) && element.ValueKind == JsonValueKind.Array)
            {
                return element.EnumerateArray().Select(e => e.GetString()).ToList();
            }
            throw new KeyNotFoundException(key);
        } catch (System.Exception e)
        {
            throw new System.Exception("parse list<string> key error, key" + key, e);
        }

    }
    
    public static T GetObject<T>(JsonDocument doc, string key) where T : class
    {
        try
        {
            if (doc.RootElement.TryGetProperty(key, out var element) && element.ValueKind == JsonValueKind.Object)
            {
                var ret = System.Text.Json.JsonSerializer.Deserialize<T>(element.GetRawText());
                if (ret == null)
                {
                    throw new KeyNotFoundException(key + " could be null");
                }
                return ret;
            }
            throw new KeyNotFoundException(key);
        } catch (System.Exception e)
        {
            throw new System.Exception("parse string key error, key" + key, e);
        }

    }
    
    public static T? GetObjectOrNull<T>(JsonDocument doc, string key) where T : class
    {
        try
        {
            var jsonElement = doc.RootElement.GetProperty(key);
            if (jsonElement.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            else
            {
                return jsonElement.Deserialize<T>();
            }
        } catch (System.Exception e)
        {
            return null;
            // throw new System.Exception("parse string key error, key" + key, e);
        }

    }
    
}