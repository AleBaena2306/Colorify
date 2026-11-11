using KovusPride.Publishing;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Colorify
{
    // Perfil en el menú (arriba a la izquierda): emblema y nombre del jugador, siempre a la vista desde la primera
    // partida. Al pulsarlo, cada plataforma usa la herramienta que permite para el nombre:
    //   · Portfolio: pantalla YOUR NAME con «Sign in with Google» (nombre reservado en el servidor propio).
    //   · Playgama: la misma pantalla sin Google (no hay servidor propio): el nombre se guarda en el dispositivo.
    //   · CrazyGames: prohíbe inicios de sesión externos; se usa su propio aviso de inicio de sesión (SDK
    //     user.showAuthPrompt) y el nombre pasa a ser el de la cuenta de CrazyGames. Con la sesión ya iniciada, el
    //     nombre no se cambia en el juego: se abre COLLECTION > PROFILE (emblema y color del nombre).
    //   · GameDistribution e itch.io: sin cuentas; se abre COLLECTION > PROFILE.
    // Desde la 3.ª partida, si el jugador aún no ha registrado su nombre donde se puede, el tutorial del menú
    // (ColorifyGame.Coach.cs) le obliga a pulsar este perfil una vez.
    public partial class ColorifyGame
    {
        private Button menuProfile;
        private RectTransform menuProfileEmblem;
        private TextMeshProUGUI menuProfileName;
        private string menuProfileEmblemId;
        private string forceCollectionTab;   // ShowCollection abre esta pestaña (una vez)

        private const float ProfileChipW = 460f, ProfileChipH = 120f, ProfileEmblemSize = 85f;   // diseño del 11/11/2026

        private void BuildMenuProfile()
        {
            menuProfile = UIKit.Button("Profile", v.menu.transform, "", UIKit.PaperLight, new Vector2(ProfileChipW, ProfileChipH), ProfileAction, 40);
            UIKit.Place((RectTransform)menuProfile.transform, 0f, 1f, new Vector2(ProfileChipW, ProfileChipH), new Vector2(40f + ProfileChipW * 0.5f, -105f));
            float d = UIKit.DepthUnits(0.6f);

            menuProfileEmblem = UIKit.Rect("EmblemSlot", menuProfile.transform);
            UIKit.Place(menuProfileEmblem, 0f, 0.5f, new Vector2(ProfileEmblemSize, ProfileEmblemSize), new Vector2(18f + ProfileEmblemSize * 0.5f, d * 0.5f));

            // El texto del botón hace de nombre: a la izquierda, junto al emblema, con tamaño automático limitado
            menuProfileName = menuProfile.GetComponentInChildren<TextMeshProUGUI>();
            menuProfileName.alignment = TextAlignmentOptions.Left;
            menuProfileName.enableAutoSizing = true;
            menuProfileName.fontSizeMin = 24f;
            menuProfileName.fontSizeMax = 40f;
            menuProfileName.enableWordWrapping = false;
            menuProfileName.overflowMode = TextOverflowModes.Ellipsis;
            var rt = menuProfileName.rectTransform;
            rt.offsetMin = new Vector2(18f + ProfileEmblemSize + 20f, d + 6f);
            rt.offsetMax = new Vector2(-26f, -6f);

            RefreshMenuProfile();
        }

        // Nombre y emblema actuales; el perfil sale a partir de la primera partida
        private void RefreshMenuProfile()
        {
            if (menuProfile == null || save == null) return;
            bool show = save.normalRunsPlayed > 0;
            menuProfile.gameObject.SetActive(show);
            if (!show) return;
            EnsureIdentity();
            menuProfileName.text = EmblemView.Name(CurrentName, save.nameColor);
            if (menuProfileEmblemId != save.emblem)
            {
                for (int i = menuProfileEmblem.childCount - 1; i >= 0; i--) Destroy(menuProfileEmblem.GetChild(i).gameObject);
                EmblemView.Build(menuProfileEmblem, save.emblem, ProfileEmblemSize);
                menuProfileEmblemId = save.emblem;
            }
        }

        // ¿Puede (y le falta) registrar su nombre en esta plataforma?
        private bool ProfileNeedsRegistration
        {
            get
            {
                if (StorePlatform.AllowsExternalLogin) return !SignedIn;                          // portfolio: Google
                if (LocalProfile) return false;   // Playgama: el nombre es el de su ranking, no se registra otro
                if (StorePlatform.Current == Store.CrazyGames)                                    // cuenta de CrazyGames
                    return StorePlatform.AccountAvailable && string.IsNullOrEmpty(StorePlatform.PlayerName);
                return false;                                                                     // GameDistribution, itch.io
            }
        }

        private void ProfileAction()
        {
            if (StorePlatform.AdShowing) return;
            CompleteCoach(CoachKind.Profile);
            if (CanRename) { ShowNameEditor(); return; }
            if (StorePlatform.Current == Store.CrazyGames && StorePlatform.AccountAvailable && string.IsNullOrEmpty(StorePlatform.PlayerName))
            {
                sfx.Play(Sfx.Id.Click);
                Analytics.Log("account_prompt", ("store", "crazygames"));
                StorePlatform.ShowAuthPrompt(ok =>
                {
                    Analytics.Log("account_prompt_result", ("store", "crazygames"), ("ok", ok));
                    if (!ok) return;
                    platformNameRejected = false;
                    CloseDuel();   // la próxima conexión se presenta con el nombre de la cuenta
                    if (onlineName != null) onlineName.text = CurrentName;
                    RefreshMenuProfile();
                    juice.Punch(menuProfile.transform, 0.12f, 0.3f);
                });
                return;
            }
            forceCollectionTab = "profile";
            ShowCollection();
        }

        // La plataforma ha terminado de arrancar (nombre de la cuenta, cuentas disponibles)
        private void OnPlatformReadyProfile()
        {
            RefreshMenuProfile();
            if (state == State.Menu) MaybeStartMenuCoach();
        }
    }
}
