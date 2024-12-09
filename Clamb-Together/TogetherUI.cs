using System.Collections;
using Il2CppTMPro;
using Il2CppXRClimbGame;
using MelonLoader;
using Steamworks;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ClambTogether;

public class TogetherUI {
    private const float UI_BUTTONS_WIDTH_INCREASE = 100;

    private readonly ClambTogether clambTogether;
    private readonly GameObject buttonTemplate;
    private readonly GameObject textTemplate;
    private readonly List<GameObject> lobbyEntries = new ();
    private readonly Dictionary<ulong, GameObject> lobbyMemberEntries = new ();

    private GameObject entriesContent = null!;
    private Button buttonCreateLeave = null!;
    private TMP_Text buttonTextCreateLeave = null!;
    private Button buttonRefresh = null!;

    public TogetherUI(ClambTogether clambTogether) {
        this.clambTogether = clambTogether;

        var panelsManager = GameObject.FindWithTag("Character Controller").transform.Find("UI Main Menu").GetComponent<XRUIPanelsManager>();
        var mainMenu = panelsManager.transform.Find("Main Menu/Canvas_Main Menu");
        var panelSettings = mainMenu.Find("Panel Settings").GetComponent<XRUIPanel>();
        var buttonQuit = panelSettings.transform.Find("Disolve Mask/Panel/Button Quit").GetComponent<RectTransform>();
        var buttonResume = panelSettings.transform.Find("Disolve Mask/Panel/Button Resume").GetComponent<RectTransform>();
        var buttonsX = buttonQuit.anchoredPosition.x - buttonQuit.sizeDelta.x / 2;
        var buttonsWidth = buttonResume.anchoredPosition.x + buttonResume.sizeDelta.x / 2 - buttonsX + UI_BUTTONS_WIDTH_INCREASE;
        var newButtonsX = buttonsX - UI_BUTTONS_WIDTH_INCREASE / 2;
        var newButtonWidth = buttonsWidth / 3;

        buttonTemplate = buttonResume.gameObject;
        textTemplate = panelSettings.transform.Find("Disolve Mask/Panel/Panel Elapsed Time/Text Elapsed Time").gameObject;

        var togetherPanel = CreatePanelTogether(panelsManager, panelSettings);

        mainMenu.GetComponent<RectTransform>().sizeDelta = new Vector2(1000, 1000);

        var togetherPanelIndex = panelsManager.panels.Count;
        panelsManager.panels.Add(togetherPanel);

        var buttonTogether = CreateButton("Button Together", buttonResume.parent, "Together", () => {
            panelsManager.EnablePanel(togetherPanelIndex, true);

            RefreshLobbies();
        }).GetComponent<RectTransform>();

        var buttonQuitX = newButtonsX + newButtonWidth / 2;
        SetXAndWidth(buttonQuit, buttonQuitX, newButtonWidth);

        var buttonResumeX = buttonQuitX + newButtonWidth;
        SetXAndWidth(buttonResume, buttonResumeX, newButtonWidth);

        SetXAndWidth(buttonTogether, buttonResumeX + newButtonWidth, newButtonWidth);
    }

    public void OnLobbyEntered() {
        buttonTextCreateLeave.text = "Leave Lobby";
        buttonCreateLeave.interactable = true;

        foreach (var lobbyEntry in lobbyEntries) {
            Object.Destroy(lobbyEntry);
        }

        lobbyEntries.Clear();

        foreach (var member in clambTogether.GetLobby().Members) {
            AddLobbyMemberEntry(member);
        }
    }

    public void OnLobbyLeft() {
        buttonTextCreateLeave.text = "Create Lobby";
        buttonCreateLeave.interactable = true;

        foreach (var gameObject in lobbyMemberEntries.Values) {
            Object.Destroy(gameObject);
        }

        lobbyMemberEntries.Clear();

        RefreshLobbies();
    }

    public void OnLobbyMemberJoined(Friend member) {
        AddLobbyMemberEntry(member);
    }

    public void OnLobbyMemberLeave(Friend member) {
        if (!lobbyMemberEntries.Remove(member.Id, out var entry)) return;

        Object.Destroy(entry);
    }

    private XRUIPanel CreatePanelTogether(XRUIPanelsManager panelsManager, XRUIPanel panelSettings) {
        var panelTogether = Object.Instantiate(
            panelSettings,
            panelSettings.transform.parent,
            false
        );

        panelTogether.gameObject.SetActive(false);
        panelTogether.gameObject.name = "Panel Together";

        var dissolveMask = panelTogether.transform.Find("Disolve Mask");
        var panel = dissolveMask.Find("Panel");
        var backgrounds = panel.Find("Background");

        Object.Destroy(backgrounds.Find("Panel_BG Stats").gameObject);
        Object.Destroy(backgrounds.Find("Panel_BG Pause").gameObject);
        Object.Destroy(panel.Find("Panel Elapsed Time").gameObject);
        Object.Destroy(panel.Find("Panel Personal Best").gameObject);
        Object.Destroy(panel.Find("Panel Clears").gameObject);
        Object.Destroy(panel.Find("Panel Settings").gameObject);
        Object.Destroy(panel.Find("Button Resume").gameObject);
        Object.Destroy(panel.Find("Button Quit").gameObject);

        var panelBackground = backgrounds.Find("Panel_BG").GetComponent<RectTransform>();
        panelBackground.sizeDelta = new Vector2(1000, 1000);

        FullSizeStretch(panelTogether.GetComponent<RectTransform>());
        FullSizeStretch(dissolveMask.GetComponent<RectTransform>());
        FullSizeStretch(panel.GetComponent<RectTransform>());
        FullSizeStretch(backgrounds.GetComponent<RectTransform>());
        FullSizeStretch(panelBackground);

        var contents = NewUIGameObject("Contents", panel);

        FullSizeStretch(contents.GetComponent<RectTransform>());

        var contentsLayout = contents.AddComponent<VerticalLayoutGroup>();
        contentsLayout.padding = new RectOffset(100, 100, 100, 100);
        contentsLayout.spacing = 20;
        contentsLayout.childAlignment = TextAnchor.UpperCenter;
        contentsLayout.childControlWidth = true;
        contentsLayout.childControlHeight = true;
        contentsLayout.childForceExpandHeight = false;

        var entries = NewUIGameObject("Entries", contents.transform);
        entries.AddComponent<LayoutElement>().flexibleHeight = 1;

        var entriesViewport = NewUIGameObject("Entries Viewport", entries.transform);
        FullSizeStretch(entriesViewport.GetComponent<RectTransform>());
        entriesViewport.AddComponent<Image>().sprite = Resources.GetBuiltinResource<Sprite>("UIMask");
        entriesViewport.AddComponent<Mask>().showMaskGraphic = false;

        entriesContent = NewUIGameObject("Entries Content", entriesViewport.transform);
        SetRectTransform(
            entriesContent.GetComponent<RectTransform>(),
            0, 1,
            1, 1,
            0, 0,
            0, 0,
            0.5f, 1
        );
        entriesContent.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.MinSize;

        var entriesContentLayout = entriesContent.AddComponent<VerticalLayoutGroup>();
        entriesContentLayout.spacing = 20;
        entriesContentLayout.childControlWidth = true;
        entriesContentLayout.childForceExpandWidth = true;
        entriesContentLayout.childForceExpandHeight = false;

        var entriesScrollRect = entries.AddComponent<ScrollRect>();
        entriesScrollRect.content = entriesContent.GetComponent<RectTransform>();
        entriesScrollRect.horizontal = false;
        entriesScrollRect.viewport = entriesViewport.GetComponent<RectTransform>();

        var buttons = NewUIGameObject("Buttons", contents.transform);

        var buttonsLayoutElement = buttons.AddComponent<LayoutElement>();
        buttonsLayoutElement.minHeight = 45;
        buttonsLayoutElement.flexibleHeight = 0;

        var buttonsLayout = buttons.AddComponent<HorizontalLayoutGroup>();
        buttonsLayout.childAlignment = TextAnchor.MiddleCenter;
        buttonsLayout.childControlHeight = true;

        CreateButton("Button Back", buttons.transform, "Back", () => {
            panelsManager.EnablePanel(0, true);
        });

        buttonRefresh = CreateButton("Button Refresh", buttons.transform, "Refresh", RefreshLobbies).GetComponent<Button>();

        var createButton = CreateButton("Button Create/Leave Lobby", buttons.transform, "Create Lobby", () => {
            if (clambTogether.GetLobby().Id == 0) {
                clambTogether.CreateLobby();
            } else {
                clambTogether.LeaveLobby();
            }

            buttonCreateLeave.interactable = false;
        });
        buttonCreateLeave = createButton.GetComponent<Button>();
        buttonTextCreateLeave = createButton.transform.Find("Normal/Text").GetComponent<TMP_Text>();

        return panelTogether;
    }

    private GameObject CreateButton(string name, Transform parent, string text, Action clickedHandler) {
        var button = Object.Instantiate(buttonTemplate, parent, false);

        button.name = name;
        button.transform.Find("Normal/Text").GetComponent<TMP_Text>().text = text;
        button.transform.Find("Highlighted/Text").GetComponent<TMP_Text>().text = text;

        var onClick = new Button.ButtonClickedEvent();
        button.GetComponent<Button>().onClick = onClick;
        onClick.AddListener(clickedHandler);

        LayoutRebuilder.MarkLayoutForRebuild(button.GetComponent<RectTransform>());

        return button;
    }

    private GameObject CreateText(string name, Transform parent, string text) {
        var textObject = Object.Instantiate(textTemplate, parent, false);

        textObject.name = name;
        textObject.GetComponent<TMP_Text>().text = text;

        LayoutRebuilder.MarkLayoutForRebuild(textObject.GetComponent<RectTransform>());

        return textObject;
    }

    private void AddLobbyMemberEntry(Friend member) {
        lobbyMemberEntries.Add(
            member.Id,
            CreateText("Member Entry", entriesContent.transform, member.Name)
        );
    }

    private void RefreshLobbies() {
        MelonCoroutines.Start(RefreshLobbiesCoroutine());
    }

    private IEnumerator RefreshLobbiesCoroutine() {
        clambTogether.LoggerInstance.Msg("Refreshing lobbies...");

        buttonRefresh.enabled = false;

        var lobbiesTask = SteamMatchmaking.LobbyList.FilterDistanceWorldwide().RequestAsync();

        while (!lobbiesTask.IsCompleted) {
            yield return null;

            if (clambTogether.GetLobby().Id != 0) {
                buttonRefresh.enabled = true;
                yield break;
            }
        }

        buttonRefresh.enabled = true;

        if (lobbiesTask.IsCanceled) {
            clambTogether.LoggerInstance.Warning("Lobby refresh got cancelled");
            yield break;
        }

        if (!lobbiesTask.IsCompletedSuccessfully || lobbiesTask.Result == null) {
            clambTogether.LoggerInstance.Error("Failed to refresh lobbies", lobbiesTask.Exception);
            yield break;
        }

        clambTogether.LoggerInstance.Msg("Lobbies refreshed");

        foreach (var lobbyEntry in lobbyEntries) {
            Object.Destroy(lobbyEntry);
        }

        lobbyEntries.Clear();

        foreach (var lobby in lobbiesTask.Result) {
            var button = CreateButton(
                "Lobby Entry",
                entriesContent.transform,
                $"{lobby.Owner.Name}'s lobby ({lobby.MemberCount}/{lobby.MaxMembers})",
                () => lobby.Join()
            );

            button.AddComponent<LayoutElement>().minHeight = 45;

            lobbyEntries.Add(button);
        }
    }

    private static GameObject NewUIGameObject(string name, Transform parent) {
        var gameObject = new GameObject(name);
        gameObject.layer = LayerMask.NameToLayer("UI");
        gameObject.transform.SetParent(parent, false);
        AddRectTransform(gameObject);
        return gameObject;
    }

    private static RectTransform AddRectTransform(GameObject gameObject) {
        var transform = gameObject.AddComponent<RectTransform>();
        LayoutRebuilder.MarkLayoutForRebuild(transform);
        return transform;
    }

    private static void SetXAndWidth(RectTransform transform, float x, float width) {
        var pos = transform.anchoredPosition;
        pos.x = x;
        transform.anchoredPosition = pos;

        var size = transform.sizeDelta;
        size.x = width;
        transform.sizeDelta = size;
    }

    private static RectTransform FullSizeStretch(RectTransform transform) {
        return SetRectTransform(
            transform,
            0, 0,
            1, 1,
            0, 0,
            0, 0,
            0.5f, 0.5f
        );
    }

    private static RectTransform SetRectTransform(
        RectTransform transform,
        float anchorMinX,
        float anchorMinY,
        float anchorMaxX,
        float anchorMaxY,
        float anchoredPositionX,
        float anchoredPositionY,
        float sizeDeltaX,
        float sizeDeltaY,
        float pivotX,
        float pivotY
    ) {
        transform.anchorMin = new Vector2(anchorMinX, anchorMinY);
        transform.anchorMax = new Vector2(anchorMaxX, anchorMaxY);
        transform.anchoredPosition = new Vector2(anchoredPositionX, anchoredPositionY);
        transform.sizeDelta = new Vector2(sizeDeltaX, sizeDeltaY);
        transform.pivot = new Vector2(pivotX, pivotY);

        return transform;
    }
}