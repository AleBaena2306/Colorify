using System;
using System.Collections;
using Colorify.Online;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace Colorify
{
    // Nombre del jugador y cuenta de Google.
    //   · Sin cuenta: el nombre que escriba dura hasta cerrar el juego (el generado sí se guarda).
    //   · Con cuenta de Google: el nombre se guarda en el servidor, queda reservado y los trofeos y récords
    //     se conservan en cualquier dispositivo. Se puede cerrar sesión y borrar la cuenta.
    // Escribir: en el ordenador y en Android, directamente en la casilla (teclado físico o del sistema);
    // en el navegador del móvil, con el cuadro de texto del navegador, que abre el teclado del móvil.
    public partial class ColorifyGame
    {
        [Serializable] private class LinkStatus { public bool ok; public string state, player, name, token, error; public int trophies; }
        [Serializable] private class ApiResult { public bool ok; public string error, name; }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int KP_IsMobileBrowser();
        [DllImport("__Internal")] private static extern string KP_Prompt(string title, string value);
        [DllImport("__Internal")] private static extern void KP_OpenOnRelease(string url);
#endif

        private string sessionName;   // nombre escrito sin cuenta (solo esta sesión)
        private bool SignedIn => !string.IsNullOrEmpty(save.accountToken);
        private string CurrentName => SignedIn && !string.IsNullOrEmpty(save.accountName) ? save.accountName
            : !string.IsNullOrEmpty(sessionName) ? sessionName : PlatformName ?? save.playerName;

        // En los portales con cuenta (CrazyGames), el nombre de la cuenta: así los amigos se reconocen. Si el servidor
        // no lo acepta (formato o nombre reservado), se vuelve al generado.
        private bool platformNameRejected;
        private string PlatformName
        {
            get
            {
                string n = KovusPride.Publishing.StorePlatform.PlayerName;
                if (platformNameRejected || !KovusPride.Publishing.StorePlatform.IsPortal || string.IsNullOrEmpty(n)) return null;
                return PlayerNames.Problem(n) == null ? n : null;
            }
        }

        private CanvasGroup nameEditor, signInWait;
        private TMPro.TMP_InputField nameInput;      // ordenador y Android
        private TMPro.TextMeshProUGUI nameDisplay;   // navegador del móvil
        private RectTransform nameBox;
        private string NameField
        {
            get => nameInput != null ? nameInput.text : nameDisplay.text;
            set { if (nameInput != null) nameInput.text = value; else nameDisplay.text = value; }
        }
        private TMPro.TextMeshProUGUI nameStatus, nameNote, accountLabel, signInText;
        private Button googleButton, signOutButton, deleteButton;
        private bool deleteArmed;
        private string linkCode;
        private Coroutine linkPoll;

        private static bool UsesBrowserPrompt
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return KP_IsMobileBrowser() == 1;
#else
                return false;
#endif
            }
        }

        // =====================================================================
        // Pantallas
        // =====================================================================

        private void BuildAccountOverlays()
        {
            nameEditor = v.Overlay("NameEditor", 0.96f);
            var t = UIKit.Text("Title", nameEditor.transform, "YOUR NAME", 96, UIKit.Ink, black: true);
            UIKit.Place(t.rectTransform, 0.5f, 0.5f, new Vector2(1000, 130), new Vector2(0, 640));

            // Casilla de texto
            var box = UIKit.Box("Field", nameEditor.transform, UIKit.PaperDark, 0.5f, raycast: true);
            UIKit.Place(box.rectTransform, 0.5f, 0.5f, new Vector2(820, 140), new Vector2(0, 470));
            var area = UIKit.Stretch(UIKit.Rect("TextArea", box.transform), 28f);
            area.gameObject.AddComponent<RectMask2D>();
            nameBox = box.rectTransform;
            if (UsesBrowserPrompt)
            {
                // Móvil en el navegador: la casilla solo enseña el nombre; al tocarla se abre el cuadro de texto
                // del navegador (con el teclado del móvil). Aquí no se usa TMP_InputField.
                nameDisplay = UIKit.Text("Text", area, "", 60, UIKit.Ink, black: true);
                UIKit.Stretch(nameDisplay.rectTransform);
                var tap = box.gameObject.AddComponent<Button>();
                tap.targetGraphic = box;
                tap.onClick.AddListener(PromptName);
            }
            else
            {
                var placeholder = UIKit.Text("Placeholder", area, "Type your name", 60, UIKit.InkSoft);
                UIKit.Stretch(placeholder.rectTransform);
                var text = UIKit.Text("Text", area, "", 60, UIKit.Ink, black: true);
                UIKit.Stretch(text.rectTransform);
                text.enableWordWrapping = false;
                nameInput = box.gameObject.AddComponent<TMPro.TMP_InputField>();
                nameInput.textViewport = area;
                nameInput.textComponent = text;
                nameInput.placeholder = placeholder;
                nameInput.fontAsset = text.font;
                nameInput.pointSize = 60;
                nameInput.characterLimit = 16;
                nameInput.lineType = TMPro.TMP_InputField.LineType.SingleLine;
                nameInput.targetGraphic = box;
                nameInput.onSubmit.AddListener(_ => SaveName());
                nameInput.onValueChanged.AddListener(_ => { nameStatus.text = ""; });
            }

            nameStatus = UIKit.Text("Status", nameEditor.transform, "", 36, UIKit.Danger);
            UIKit.Place(nameStatus.rectTransform, 0.5f, 0.5f, new Vector2(960, 60), new Vector2(0, 360));
            var random = UIKit.Button("Random", nameEditor.transform, "RANDOM", UIKit.PaperDark, new Vector2(360, 120), RandomName, 50);
            UIKit.Place((RectTransform)random.transform, 0.5f, 0.5f, new Vector2(360, 120), new Vector2(-200, 250));
            var saveBtn = UIKit.Button("Save", nameEditor.transform, "SAVE", ColorifyView.TitleColors[2], new Vector2(360, 120), SaveName, 56);
            UIKit.Place((RectTransform)saveBtn.transform, 0.5f, 0.5f, new Vector2(360, 120), new Vector2(200, 250));

            nameNote = UIKit.Text("Note", nameEditor.transform, "", 36, UIKit.InkSoft);
            UIKit.Place(nameNote.rectTransform, 0.5f, 0.5f, new Vector2(940, 200), new Vector2(0, 70));
            accountLabel = UIKit.Text("Account", nameEditor.transform, "", 40, UIKit.Ink, black: true);
            UIKit.Place(accountLabel.rectTransform, 0.5f, 0.5f, new Vector2(960, 70), new Vector2(0, -60));

            googleButton = UIKit.Button("Google", nameEditor.transform, "SIGN IN WITH GOOGLE", ColorifyView.TitleColors[0], new Vector2(760, 140), null, 50);
            UIKit.Place((RectTransform)googleButton.transform, 0.5f, 0.5f, new Vector2(760, 140), new Vector2(0, -150));
            googleButton.onClick.RemoveAllListeners();
            // Al pulsar (no al soltar): en la web, la pestaña de Google tiene que abrirse dentro del mismo toque
            googleButton.gameObject.AddComponent<PointerDownAction>().action = StartGoogleSignIn;

            signOutButton = UIKit.Button("SignOut", nameEditor.transform, "SIGN OUT", UIKit.PaperDark, new Vector2(460, 110), SignOut, 44);
            UIKit.Place((RectTransform)signOutButton.transform, 0.5f, 0.5f, new Vector2(460, 110), new Vector2(0, -160));
            deleteButton = UIKit.Button("Delete", nameEditor.transform, "DELETE ACCOUNT", UIKit.WithAlpha(UIKit.Danger, 0.25f), new Vector2(460, 100), DeleteAccount, 38);
            UIKit.Place((RectTransform)deleteButton.transform, 0.5f, 0.5f, new Vector2(460, 100), new Vector2(0, -290));

            var back = UIKit.Button("Back", nameEditor.transform, "BACK", UIKit.PaperDark, new Vector2(420, 120), CloseNameEditor, 50);
            UIKit.Place((RectTransform)back.transform, 0.5f, 0f, new Vector2(420, 120), new Vector2(0, 110));

            // Esperando a que termine el inicio de sesión en la otra pestaña o en el navegador
            signInWait = v.Overlay("SignIn", 0.97f);
            var st = UIKit.Text("Title", signInWait.transform, "SIGN IN", 96, UIKit.Ink, black: true);
            UIKit.Place(st.rectTransform, 0.5f, 0.5f, new Vector2(1000, 130), new Vector2(0, 420));
            signInText = UIKit.Text("Text", signInWait.transform, "", 40, UIKit.InkSoft);
            UIKit.Place(signInText.rectTransform, 0.5f, 0.5f, new Vector2(960, 400), new Vector2(0, 120));
            var cancel = UIKit.Button("Cancel", signInWait.transform, "CANCEL", UIKit.PaperDark, new Vector2(420, 120), CancelSignIn, 50);
            UIKit.Place((RectTransform)cancel.transform, 0.5f, 0.5f, new Vector2(420, 120), new Vector2(0, -220));
        }

        private void ShowNameEditor()
        {
            sfx.Play(Sfx.Id.Click);
            EnsureIdentity();
            deleteArmed = false;
            NameField = CurrentName;
            nameStatus.text = "";
            RefreshAccountUI();
            Show(nameEditor);
            if (nameInput != null)
            {
                nameInput.ActivateInputField();   // en el ordenador se puede escribir ya; Enter guarda
                nameInput.MoveTextEnd(false);
            }
        }

        private void CloseNameEditor()
        {
            sfx.Play(Sfx.Id.Click);
            if (nameInput != null) nameInput.DeactivateInputField();
            Hide(nameEditor);
            onlineName.text = CurrentName;
        }

        private void RefreshAccountUI()
        {
            bool signed = SignedIn;
            nameNote.text = signed
                ? "Your name is saved to your account and nobody else can use it."
                : "Without an account, your name lasts until you close the game.\nSign in with Google to keep your name, trophies and records on any device.";
            accountLabel.text = signed ? $"<color=#{ColorUtility.ToHtmlStringRGB(ColorifyView.TitleColors[2])}>SIGNED IN WITH GOOGLE</color>" : "";
            googleButton.gameObject.SetActive(!signed);
            signOutButton.gameObject.SetActive(signed);
            deleteButton.gameObject.SetActive(signed);
            deleteButton.GetComponentInChildren<TMPro.TextMeshProUGUI>().text = "DELETE ACCOUNT";
        }

        // =====================================================================
        // Nombre
        // =====================================================================

        private void PromptName()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string r = KP_Prompt("Your name (3-16 letters or numbers)", NameField);
            if (r == "\u0001") return;   // cancelado
            NameField = r.Trim();
            if (NameField.Length > 0) SaveName();
#endif
        }

        private void RandomName()
        {
            sfx.Play(Sfx.Id.Click);
            NameField = PlayerNames.Random(rng);
            juice.Punch(nameBox, 0.08f);
        }

        private void SaveName()
        {
            string name = PlayerNames.Clean(NameField);
            NameField = name;
            string problem = PlayerNames.Problem(name);
            if (problem != null) { nameStatus.text = problem; sfx.Play(Sfx.Id.Wrong); return; }
            sfx.Play(Sfx.Id.Click);
            if (SignedIn)
            {
                nameStatus.text = $"<color=#{ColorUtility.ToHtmlStringRGB(UIKit.InkSoft)}>Saving…</color>";
                string json = "{\"token\":\"" + save.accountToken + "\",\"name\":\"" + OnlineApi.Escape(name) + "\"}";
                StartCoroutine(OnlineApi.PostJson(cfg, "name", json, text =>
                {
                    var r = Parse<ApiResult>(text);
                    if (r == null) { nameStatus.text = "No connection. Try again."; return; }
                    if (!r.ok) { nameStatus.text = NameErrorText(r.error); return; }
                    save.accountName = r.name;
                    save.Save();
                    AfterNameChange();
                }));
                return;
            }
            // Sin cuenta: el generado se guarda; uno escrito, solo esta sesión
            if (PlayerNames.IsValid(name)) { save.playerName = name; save.Save(); sessionName = null; }
            else sessionName = name;
            AfterNameChange();
        }

        private void AfterNameChange()
        {
            CloseDuel();   // la próxima conexión se presenta con el nombre nuevo
            onlineName.text = CurrentName;
            nameStatus.text = $"<color=#{ColorUtility.ToHtmlStringRGB(ColorifyView.TitleColors[2])}>Saved!</color>";
            juice.Punch(nameBox, 0.1f);
        }

        private static string NameErrorText(string code)
        {
            switch (code)
            {
                case "name_taken": return "Someone already uses that name. Try another one.";
                case "bad_name": return "That name isn't allowed. Try another one.";
                case "no_account": return "Your session expired. Sign in again.";
                default: return "Couldn't save the name. Try again.";
            }
        }

        // Errores del servidor al conectarse con un nombre
        private bool NameError(string code)
        {
            if (code != "bad_name" && code != "name_taken") return false;
            if (sessionName == null && PlatformName != null) platformNameRejected = true;
            sessionName = null;
            CloseDuel();
            Hide(searching);
            HideRaceOverlays(false);
            ostage = OnlineStage.Lobby;
            mode = Mode.Normal;
            onlineName.text = CurrentName;
            onlineStatus.text = NameErrorText(code) + "\nWe switched you back to " + CurrentName + ".";
            Show(onlineMenu);
            return true;
        }

        // =====================================================================
        // Cuenta de Google
        // =====================================================================

        private void StartGoogleSignIn()
        {
            sfx.Play(Sfx.Id.Click);
            EnsureIdentity();
            const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var chars = new char[8];
            for (int i = 0; i < 8; i++) chars[i] = alphabet[rng.Next(alphabet.Length)];
            linkCode = new string(chars);
            string url = OnlineApi.BaseUrl(cfg) + "/api/colorify/link?code=" + linkCode;
#if UNITY_WEBGL && !UNITY_EDITOR
            KP_OpenOnRelease(url);
#else
            Application.OpenURL(url);
#endif
            signInText.text = "Finish signing in on the page that just opened, then come back here.\n\n" +
                              $"If it didn't open, go to:\n<color=#{ColorUtility.ToHtmlStringRGB(UIKit.Ink)}>{url.Replace("https://", "")}</color>";
            Show(signInWait);
            string json = "{\"player\":\"" + save.playerId + "\",\"name\":\"" + OnlineApi.Escape(CurrentName) + "\",\"code\":\"" + linkCode + "\"}";
            StartCoroutine(OnlineApi.PostJson(cfg, "link/start", json, text =>
            {
                var r = Parse<LinkStatus>(text);
                if (r == null || !r.ok) { signInText.text = "Couldn't reach the server. Check your connection and try again."; return; }
                if (linkPoll != null) StopCoroutine(linkPoll);
                linkPoll = StartCoroutine(PollLink(linkCode));
            }));
        }

        private IEnumerator PollLink(string code)
        {
            float until = Time.realtimeSinceStartup + 15 * 60f;
            while (Time.realtimeSinceStartup < until && linkCode == code)
            {
                yield return new WaitForSecondsRealtime(2f);
                LinkStatus r = null;
                yield return OnlineApi.PostJson(cfg, "link/status", "{\"code\":\"" + code + "\"}", text => r = Parse<LinkStatus>(text));
                if (linkCode != code) yield break;
                if (r == null) continue;
                if (!r.ok) { signInText.text = "The sign-in code expired. Try again."; yield break; }
                if (r.state != "done") continue;
                save.playerId = r.player;
                save.accountName = r.name;
                save.accountToken = r.token;
                save.trophies = r.trophies;
                save.Save();
                sessionName = null;
                CloseDuel();
                linkCode = null;
                Hide(signInWait);
                NameField = CurrentName;
                onlineName.text = CurrentName;
                RefreshTrophies();
                RefreshAccountUI();
                nameStatus.text = $"<color=#{ColorUtility.ToHtmlStringRGB(ColorifyView.TitleColors[2])}>Signed in as {r.name}</color>";
                sfx.Play(Sfx.Id.Clear);
                yield break;
            }
        }

        private void CancelSignIn()
        {
            sfx.Play(Sfx.Id.Click);
            linkCode = null;
            if (linkPoll != null) { StopCoroutine(linkPoll); linkPoll = null; }
            Hide(signInWait);
        }

        // Cerrar sesión: este dispositivo vuelve a jugar como invitado (la cuenta y su progreso siguen en el servidor)
        private void SignOut()
        {
            sfx.Play(Sfx.Id.Click);
            BecomeGuest();
            nameStatus.text = "Signed out. Sign in again to get your name and trophies back.";
        }

        private void DeleteAccount()
        {
            if (!deleteArmed)
            {
                deleteArmed = true;
                sfx.Play(Sfx.Id.Wrong);
                deleteButton.GetComponentInChildren<TMPro.TextMeshProUGUI>().text = "TAP AGAIN TO DELETE";
                nameStatus.text = "This deletes your account, name, trophies and online scores for good.";
                return;
            }
            deleteArmed = false;
            string json = "{\"token\":\"" + save.accountToken + "\"}";
            StartCoroutine(OnlineApi.PostJson(cfg, "account/delete", json, text =>
            {
                var r = Parse<ApiResult>(text);
                if (r == null) { nameStatus.text = "No connection. Try again."; return; }
                if (!r.ok && r.error != "no_account") { nameStatus.text = "Couldn't delete the account. Try again."; return; }
                BecomeGuest();
                nameStatus.text = "Your account and its data were deleted.";
            }));
        }

        private void BecomeGuest()
        {
            save.accountToken = "";
            save.accountName = "";
            save.playerId = PlayerNames.NewPlayerId();
            save.playerName = PlayerNames.Random(rng);
            save.trophies = 0;
            save.onlineWins = save.onlineLosses = 0;
            save.Save();
            sessionName = null;
            CloseDuel();
            NameField = CurrentName;
            onlineName.text = CurrentName;
            RefreshTrophies();
            RefreshAccountUI();
        }

        private static T Parse<T>(string text) where T : class
        {
            if (string.IsNullOrEmpty(text)) return null;
            try { return JsonUtility.FromJson<T>(text); } catch (Exception) { return null; }
        }
    }

    // Botón que actúa al pulsar (pointer down) en lugar de al soltar
    public class PointerDownAction : MonoBehaviour, IPointerDownHandler
    {
        public Action action;
        public void OnPointerDown(PointerEventData e) => action?.Invoke();
    }
}
