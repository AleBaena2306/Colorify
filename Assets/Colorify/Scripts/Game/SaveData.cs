using System;
using System.Collections.Generic;
using UnityEngine;

namespace Colorify
{
    // Guardado local (PlayerPrefs; en la web, IndexedDB del navegador). Sin cuentas ni nube.
    // Candidato a KovusPride.Save cuando exista el módulo.
    [Serializable]
    public class RunRecord
    {
        public long score;
        public int stage;
        public int round;
        public bool victory;
        public List<string> cards = new List<string>();
        public string date;
    }

    [Serializable]
    public class SaveData
    {
        public long bestScore;          // Supervivencia
        public int bestNormalHits;      // Normal (aciertos; lo usa el duelo para emparejar)
        public long bestNormalPoints;   // Normal (puntos, desde las formas y efectos especiales)
        public int normalRunsPlayed;
        public int runsPlayed;
        public int runsSinceContinueOffer;
        public bool soundOn = true;
        public bool vibrationOn = true;
        public List<RunRecord> topRuns = new List<RunRecord>();
        // Online: id anónimo y nombre generado («Teal Fox 42»); sin datos personales
        public string playerId = "";
        public string playerName = "";      // nombre generado (o el último elegido de las listas)
        public string accountName = "";     // con cuenta de Google: el nombre de la cuenta
        public string accountToken = "";    // sesión de la cuenta (no es la contraseña de Google; se puede revocar)
        public int onlineWins, onlineLosses;
        public int trophies;                // copia local de los trofeos (los manda el servidor)
        public int raceMaxPlayers = 8, raceRounds = 10, raceWins, racesPlayed;

        private const string Key = "colorify.save.v1";
        public const int MaxRuns = 10;

        public static SaveData Load()
        {
            try
            {
                string json = PlayerPrefs.GetString(Key, "");
                if (!string.IsNullOrEmpty(json)) return JsonUtility.FromJson<SaveData>(json) ?? new SaveData();
            }
            catch (Exception e) { Debug.LogWarning("[Colorify] Guardado ilegible, se empieza de cero: " + e.Message); }
            return new SaveData();
        }

        public void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }

        // Devuelve la posición en el top local (0 = mejor) o -1 si no entra
        public int AddRun(RunRecord r)
        {
            runsPlayed++;
            if (r.score > bestScore) bestScore = r.score;
            topRuns.Add(r);
            topRuns.Sort((x, y) => y.score.CompareTo(x.score));
            if (topRuns.Count > MaxRuns) topRuns.RemoveRange(MaxRuns, topRuns.Count - MaxRuns);
            return topRuns.IndexOf(r);
        }
    }
}
