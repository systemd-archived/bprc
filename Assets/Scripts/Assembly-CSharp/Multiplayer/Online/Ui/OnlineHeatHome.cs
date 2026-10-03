using System;
using System.Linq;
using Michsky.UI.Heat;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BadPiggies.Multiplayer.Online.Ui
{
    /// <summary>Adapts the actual Heat desktop Home Panel, preserving its artwork and box animations.</summary>
    public sealed class OnlineHeatHome : MonoBehaviour
    {
        private TMP_Text title;
        private TMP_Text description;
        private ButtonManager details;
        private Transform[] timers;
        private OnlineLobbyWindow window;
        private int slide;
        private float nextSlide;
        private readonly string[] titles = { "与好友一起开局", "发现身边的房间", "继续上次的对战" };
        private readonly string[] descriptions = {
            "分享邀请链接或二维码，让好友加入房间。两人对战，其他成员可以观战。",
            "同一局域网内自动发现房间。跨网络时，请使用包含可达房主地址的邀请链接。",
            "在最近与收藏中找到玩过的房间。你收藏的房间会保留在本机。"
        };
        private readonly OnlineLobbyWindow.Page[] destinations = {
            OnlineLobbyWindow.Page.Settings, OnlineLobbyWindow.Page.Lan, OnlineLobbyWindow.Page.Recent
        };

        public void Initialize(OnlineLobbyWindow owner)
        {
            window = owner;
            foreach (HotkeyEvent key in GetComponentsInChildren<HotkeyEvent>(true)) key.gameObject.SetActive(false);
            RectTransform content = (RectTransform)transform.Find("Content");
            MctierWidgets.Stretch(content);
            RectTransform boxes = (RectTransform)content.Find("Box Container");
            MctierWidgets.Stretch(boxes);
            BoxContainer entrance = boxes.GetComponent<BoxContainer>();
            entrance.updateMode = BoxContainer.UpdateMode.UnscaledTime;
            Transform hotkeys = content.Find("Hotkeys");
            if (hotkeys != null) hotkeys.gameObject.SetActive(false);
            // Original sample rows use fixed demo dimensions; use the same proportions at any aspect ratio.
            RectTransform[] rows = { (RectTransform)boxes.Find("Row 1"), (RectTransform)boxes.Find("Row 2"), (RectTransform)boxes.Find("Row 3") };
            float[] starts = { 0f, .325f, .69f }, ends = { .31f, .675f, 1f };
            for (int i = 0; i < rows.Length; i++)
            {
                rows[i].GetComponent<VerticalLayoutGroup>().enabled = false;
                MctierWidgets.SetAnchors(rows[i], new Vector2(starts[i],0), new Vector2(ends[i],1), Vector2.zero, Vector2.zero);
            }
            BindBox(rows[0].Find("Continue"), "HomeJoin", "加入房间", "使用好友发来的邀请链接或房间号", OnlineLobbyWindow.Page.Join);
            BindBox(rows[0].Find("New Game"), "HomeCreate", "创建房间", "创建房间，邀请好友一起对战", OnlineLobbyWindow.Page.Create);
            BindBox(rows[1].Find("Load Game"), "HomeLan", "局域网房间", "发现同一网络中的好友", OnlineLobbyWindow.Page.Lan);
            RectTransform join = (RectTransform)rows[0].GetChild(0), create = (RectTransform)rows[0].GetChild(1);
            MctierWidgets.SetAnchors(join, new Vector2(0,.5f), Vector2.one, new Vector2(0,12), Vector2.zero);
            MctierWidgets.SetAnchors(create, Vector2.zero, new Vector2(1,.5f), Vector2.zero, new Vector2(0,-12));
            MctierWidgets.Stretch((RectTransform)rows[1].GetChild(0));

            NewsSlider news = rows[2].GetComponentInChildren<NewsSlider>(true);
            news.enabled = false; // Reuse its authored view; the lobby supplies the three local slides below.
            MctierWidgets.SetAnchors((RectTransform)news.transform, new Vector2(0,.24f), Vector2.one, new Vector2(0,12), Vector2.zero);
            Transform item = news.transform.Find("Content/News Slider Item");
            item.GetComponent<Animator>().enabled = false;
            item.GetComponent<CanvasGroup>().alpha = 1f;
            item.Find("Background").GetComponent<Image>().sprite = news.items[1].background;
            title = item.Find("Title").GetComponent<TMP_Text>();
            description = item.Find("Description").GetComponent<TMP_Text>();
            details = item.GetComponentInChildren<ButtonManager>(true);
            details.useLocalization = false;
            details.autoFitContent = true;
            details.useSounds = false;
            details.onClick = new UnityEvent();
            details.onClick.AddListener(() => window.ShowPage(destinations[slide]));
            timers = news.transform.Find("Timer Parent").Cast<Transform>().ToArray();
            for (int i = 0; i < timers.Length; i++)
            {
                int index = i;
                foreach (Animator animation in timers[i].GetComponentsInChildren<Animator>(true)) animation.enabled = false;
                var button = timers[i].GetComponentInChildren<UnityEngine.UI.Button>(true);
                button.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
                button.onClick.AddListener(() => ShowSlide(index));
            }

            SocialsWidget socials = rows[2].GetComponentInChildren<SocialsWidget>(true);
            socials.enabled = false;
            socials.name = "HomeRecent";
            MctierWidgets.SetAnchors((RectTransform)socials.transform, Vector2.zero, new Vector2(1,.24f), Vector2.zero, new Vector2(0,-12));
            foreach (Animator animation in socials.GetComponentsInChildren<Animator>(true)) animation.enabled = false;
            foreach (CanvasGroup group in socials.GetComponentsInChildren<CanvasGroup>(true))
                if (group.name == "Example" || group.name == "Content") group.alpha = 1f;
            socials.GetComponentInChildren<TMP_Text>(true).text = "联机快捷入口";
            ButtonManager[] shortcuts = socials.GetComponentsInChildren<ButtonManager>(true);
            string[] icons = { "Chapters", "Multiplayer", "Settings" };
            OnlineLobbyWindow.Page[] pages = { OnlineLobbyWindow.Page.Recent, OnlineLobbyWindow.Page.Lan, OnlineLobbyWindow.Page.Settings };
            for (int i = 0; i < shortcuts.Length; i++)
            {
                int index = i % pages.Length;
                shortcuts[i].useSounds = false;
                shortcuts[i].onClick = new UnityEvent();
                shortcuts[i].onClick.AddListener(() => window.ShowPage(pages[index]));
                shortcuts[i].SetIcon(Resources.Load<UnityEngine.Sprite>("HeatUI/Icons/" + icons[index]));
            }
            ShowSlide(0);
            ApplyFonts();
        }

        private void BindBox(Transform target, string name, string caption, string hint, OnlineLobbyWindow.Page destination)
        {
            target.name = name;
            BoxButtonManager box = target.GetComponent<BoxButtonManager>();
            box.useLocalization = false;
            box.useSounds = false;
            box.buttonTitle = caption;
            box.buttonDescription = hint;
            box.onClick = new UnityEvent();
            box.UpdateUI();
            // Keep a uGUI action surface for the existing lobby/navigation contract.
            var button = target.gameObject.AddComponent<UnityEngine.UI.Button>();
            Image image = target.gameObject.AddComponent<Image>();
            image.color = Color.clear;
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => window.ShowPage(destination));
        }

        private void ShowSlide(int index)
        {
            slide = index % titles.Length;
            title.text = titles[slide];
            description.text = descriptions[slide];
            details.SetText(slide == 0 ? "联机设置" : slide == 1 ? "查找房间" : "最近与收藏");
            for (int i = 0; i < timers.Length; i++)
                foreach (CanvasGroup group in timers[i].GetComponentsInChildren<CanvasGroup>(true))
                    group.alpha = group.name == "Bar" ? (i == slide ? 1 : 0) : 1;
            nextSlide = Time.unscaledTime + 8f;
        }

        private void Update()
        {
            if (title != null && Time.unscaledTime >= nextSlide) ShowSlide((slide + 1) % titles.Length);
        }

        private void ApplyFonts()
        {
            foreach (UIManagerText tint in GetComponentsInChildren<UIManagerText>(true)) { tint.useCustomFont = true; tint.useCustomColor = true; tint.enabled = false; }
            foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
            {
                text.font = MctierTheme.Font;
                text.characterSpacing = 1f;
                text.fontStyle = FontStyles.Normal;
            }
            foreach (BoxButtonManager box in GetComponentsInChildren<BoxButtonManager>(true))
            {
                box.titleObj.fontSize = 28f;
                box.descriptionObj.fontSize = 16f;
            }
            title.fontSize = 23f;
            description.fontSize = 16f;
        }
    }
}
