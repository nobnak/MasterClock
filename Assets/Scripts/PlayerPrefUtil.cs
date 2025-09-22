using UnityEngine;

public static class PlayerPrefUtil {

    public static bool LoadFromPlayerPrefs<T>(this T data, string key) {
        try {
            if (PlayerPrefs.HasKey(key)) {
                var json = PlayerPrefs.GetString(key);
                JsonUtility.FromJsonOverwrite(json, data);
                return true;
            }
        } catch(System.Exception ex) {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError(ex);
#endif
        }
        return false;
    }
    public static bool SaveToPlayerPrefs<T>(this T data, string key, bool prettyPrint = true) {
        try {
            var json = JsonUtility.ToJson(data, prettyPrint);
            PlayerPrefs.SetString(key, json);
            return true;
        } catch(System.Exception ex) {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError(ex);
#endif
        }
        return false;
    }
}