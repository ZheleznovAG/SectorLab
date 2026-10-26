using System;
using SectorLab.Battle;
using UnityEngine;
using UnityEngine.UI;

namespace SectorLab.Network
{
    // "Сеть" in the HUD header: host a game, join one by IP on the same Wi-Fi, or go back to the AI.
    // Built at runtime from the header's own buttons, so it matches the HUD without scene edits.
    [RequireComponent(typeof(RectTransform))]
    public sealed class NetworkMenu : MonoBehaviour
    {
        public const string AddressPreference = "SectorLab.Network.LastAddress";
        private BattleController battle;
        private GameObject panel;
        private Button toggle;
        private Text toggleLabel, status;
        private InputField address;

        public bool IsOpen => panel != null && panel.activeSelf;
        public Button HostButton { get; private set; }
        public Button JoinButton { get; private set; }
        public Button OfflineButton { get; private set; }
        public InputField Address => address;
        public string Status => status.text;

        private void Awake()
        {
            battle = FindAnyObjectByType<BattleController>();
            var top = (RectTransform)transform.Find("TopBar");
            var pause = (RectTransform)top.Find("Pause");
            pause.anchorMax = new Vector2(0.885f, pause.anchorMax.y);
            toggle = Clone(pause, top, "Network", "Сеть");
            var rect = (RectTransform)toggle.transform;
            rect.anchorMin = new Vector2(0.895f, pause.anchorMin.y);
            rect.anchorMax = new Vector2(0.99f, pause.anchorMax.y);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            toggleLabel = toggle.GetComponentInChildren<Text>();
            toggle.onClick.AddListener(() => SetOpen(!IsOpen));
            BuildPanel(pause);
            SetOpen(false);
            ShowRole();
        }

        private static Button Clone(RectTransform template, Transform parent, string name, string caption)
        {
            var copy = Instantiate(template.gameObject, parent, false);
            copy.name = name;
            var button = copy.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.interactable = true;
            copy.GetComponentInChildren<Text>().text = caption;
            return button;
        }

        private void BuildPanel(RectTransform template)
        {
            panel = new GameObject("NetworkMenu", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = new Vector2(0.25f, 0.18f);
            rect.anchorMax = new Vector2(0.75f, 0.86f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0.07f, 0.09f, 0.13f, 0.97f);
            Font font = template.GetComponentInChildren<Text>().font;

            Label(rect, font, "Игра по сети", 0.86f, 0.98f, 34);
            status = Label(rect, font, "", 0.70f, 0.85f, 22);
            HostButton = Row(template, rect, "Host", "Создать игру (вы — синие)", 0.56f, 0.68f);
            HostButton.onClick.AddListener(HostGame);
            address = Field(rect, font, 0.42f, 0.54f);
            address.text = PlayerPrefs.GetString(AddressPreference, "192.168.");
            JoinButton = Row(template, rect, "Join", "Подключиться (вы — красные)", 0.28f, 0.40f);
            JoinButton.onClick.AddListener(JoinGame);
            OfflineButton = Row(template, rect, "Offline", "Играть с ИИ", 0.14f, 0.26f);
            OfflineButton.onClick.AddListener(PlayOffline);
            Row(template, rect, "Close", "Закрыть", 0.02f, 0.12f).onClick.AddListener(() => SetOpen(false));
            panel.transform.SetAsLastSibling();
        }

        private static Text Label(RectTransform parent, Font font, string text, float bottom, float top, int size)
        {
            var label = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            var rect = (RectTransform)label.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.05f, bottom);
            rect.anchorMax = new Vector2(0.95f, top);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            label.font = font; label.fontSize = size; label.color = Color.white; label.text = text;
            label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false;
            label.resizeTextForBestFit = true; label.resizeTextMinSize = 16; label.resizeTextMaxSize = size;
            return label;
        }

        private Button Row(RectTransform template, RectTransform parent, string name, string caption, float bottom, float top)
        {
            Button button = Clone(template, parent, name, caption);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = new Vector2(0.08f, bottom);
            rect.anchorMax = new Vector2(0.92f, top);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return button;
        }

        private static InputField Field(RectTransform parent, Font font, float bottom, float top)
        {
            var root = new GameObject("Address", typeof(RectTransform), typeof(Image), typeof(InputField));
            var rect = (RectTransform)root.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.08f, bottom);
            rect.anchorMax = new Vector2(0.92f, top);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            root.GetComponent<Image>().color = new Color(0.92f, 0.94f, 0.97f);
            Text text = Label(rect, font, "", 0, 1, 28);
            text.color = new Color(0.07f, 0.09f, 0.13f);
            text.supportRichText = false;
            Text hint = Label(rect, font, "IP хоста, например 192.168.1.20", 0, 1, 24);
            hint.color = new Color(0.35f, 0.38f, 0.45f);
            var field = root.GetComponent<InputField>();
            field.textComponent = text;
            field.placeholder = hint;
            field.contentType = InputField.ContentType.Standard;
            field.characterLimit = 45;
            return field;
        }

        // The menu owns only its pause reason; clients block input without changing host time.
        public void SetOpen(bool open)
        {
            if (open == panel.activeSelf) { if (open) ShowRole(); return; }
            panel.SetActive(open);
            if (open) ShowRole();
            if (battle != null) battle.SetPaused(PauseReason.Menu, open);
        }

        private void OnDisable() { if (battle != null) battle.SetPaused(PauseReason.Menu, false); }

        private void ShowRole()
        {
            string role = NetworkLaunch.Role == SessionRole.Host ? "хост"
                : NetworkLaunch.Role == SessionRole.Client ? "клиент" : "";
            toggleLabel.text = role.Length == 0 ? "Сеть" : "Сеть: " + role;
            if (status == null) return;
            status.text = NetworkLaunch.Role == SessionRole.Host ? "Вы хост. Соперник вводит IP: " + NetworkLauncher.LocalAddressesText()
                : NetworkLaunch.Role == SessionRole.Client ? "Вы подключены как красные."
                : "Сейчас: игра с ИИ. Оба телефона — в одной Wi-Fi сети.";
        }

        private void HostGame() => Relaunch(() => NetworkLauncher.StartHost());

        private void JoinGame()
        {
            string ip = address.text.Trim();
            if (!System.Net.IPAddress.TryParse(ip, out System.Net.IPAddress parsed)
                || parsed.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            {
                status.text = "Введите IPv4-адрес хоста, например 192.168.1.20";
                return;
            }
            PlayerPrefs.SetString(AddressPreference, ip);
            PlayerPrefs.Save();
            Relaunch(() => NetworkLauncher.Join(ip));
        }

        private void PlayOffline() => Relaunch(NetworkLauncher.Leave);

        private void Relaunch(Action start)
        {
            try
            {
                NetworkLauncher.Leave();
                start();
            }
            catch (Exception error) when (error is InvalidOperationException || error is ArgumentException)
            {
                NetworkLauncher.Leave();
                status.text = "Не удалось: " + error.Message;
                return;
            }
            battle.Restart(); // a fresh Main starts in the chosen role
        }
    }
}
