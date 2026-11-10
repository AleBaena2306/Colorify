// Kovu's Pride · Publishing
// Graba vídeos de juego para las fichas de los portales (CrazyGames pide uno horizontal 16:9 y otro vertical 2:3,
// de 15-20 s y sin sonido). Usa Unity Recorder: graba la Game view a la resolución exacta, sin cursor ni audio.
// Uso: menú «Kovu's Pride > Vídeo para portales > Grabar …». Unity entra en Play y empieza a grabar; juegue
// normalmente y salga de Play cuando tenga 20-30 s buenos (se para solo a los 60 s). El archivo queda en
// <proyecto>/Publishing/store/video/raw-<orientación>.mp4; después se recorta y se le pone la portada delante.
using System.IO;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;

namespace KovusPride.Publishing.Editor
{
    [InitializeOnLoad]
    public static class KovusPrideStoreVideo
    {
        private const string PendingKey = "KovusPride.StoreVideo.Pending";
        private const double MaxSeconds = 60;

        private static RecorderController controller;
        private static string currentFile;
        private static double startedAt;

        static KovusPrideStoreVideo()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("Kovu's Pride/Vídeo para portales/Grabar vertical 2:3 (1080x1620)")]
        private static void RecordPortrait() => Begin("portrait", 1080, 1620);

        [MenuItem("Kovu's Pride/Vídeo para portales/Grabar horizontal 16:9 (1920x1080)")]
        private static void RecordLandscape() => Begin("landscape", 1920, 1080);

        [MenuItem("Kovu's Pride/Vídeo para portales/Abrir la carpeta de vídeos")]
        private static void OpenFolder()
        {
            Directory.CreateDirectory(VideoFolder);
            EditorUtility.RevealInFinder(VideoFolder);
        }

        private static string VideoFolder =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Publishing", "store", "video"));

        private static void Begin(string orientation, int width, int height)
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                EditorUtility.DisplayDialog("Vídeo para portales", "Salga primero del modo Play (y espere a que Unity termine de compilar).", "Vale");
                return;
            }
            SessionState.SetString(PendingKey, orientation + "|" + width + "|" + height);
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                string pending = SessionState.GetString(PendingKey, "");
                if (pending.Length == 0) return;
                SessionState.EraseString(PendingKey);
                string[] p = pending.Split('|');
                StartRecording(p[0], int.Parse(p[1]), int.Parse(p[2]));
            }
            else if (change == PlayModeStateChange.ExitingPlayMode && controller != null)
            {
                StopRecording();
            }
        }

        private static void StartRecording(string orientation, int width, int height)
        {
            Directory.CreateDirectory(VideoFolder);
            currentFile = Path.Combine(VideoFolder, "raw-" + orientation);

            var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name = "Store video " + orientation;
            movie.Enabled = true;
            movie.EncoderSettings = new CoreEncoderSettings
            {
                EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High,
                Codec = CoreEncoderSettings.OutputCodec.MP4
            };
            movie.AudioInputSettings.PreserveAudio = false;
            movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = width, OutputHeight = height };
            movie.OutputFile = currentFile;

            settings.AddRecorderSettings(movie);
            settings.SetRecordModeToManual();
            settings.FrameRate = 30f;
            settings.CapFrameRate = true;
            RecorderOptions.VerboseMode = false;

            controller = new RecorderController(settings);
            controller.PrepareRecording();
            controller.StartRecording();
            startedAt = EditorApplication.timeSinceStartup;
            EditorApplication.update += Watch;
            Debug.Log($"[Kovu's Pride] Grabando vídeo {orientation} ({width}x{height}). Juegue y salga de Play cuando tenga 20-30 s buenos (máximo {MaxSeconds:0} s).");
        }

        private static void Watch()
        {
            if (controller == null) { EditorApplication.update -= Watch; return; }
            if (EditorApplication.timeSinceStartup - startedAt >= MaxSeconds) EditorApplication.isPlaying = false;
        }

        private static void StopRecording()
        {
            EditorApplication.update -= Watch;
            if (controller.IsRecording()) controller.StopRecording();
            controller = null;
            double seconds = EditorApplication.timeSinceStartup - startedAt;
            Debug.Log($"[Kovu's Pride] Vídeo guardado: {currentFile}.mp4 ({seconds:0} s).");
        }
    }
}
