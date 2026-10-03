using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BadPiggies.Multiplayer.Online;
using BadPiggies.Multiplayer.Online.EasyTier;
using BadPiggies.Multiplayer.Online.Qr;
using BadPiggies.Multiplayer.Online.Ui;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Verifies the rebuilt online (Multiplayer) lobby: that nothing in it needs a public server, that every
/// feature the redesign promised is present in the service and in the UI, that the
/// invite/QR/LAN-address plumbing round trips, and that the MCTier-style window really builds —
/// pages, navigation, controls, fonts and nine-slice panels — inside the Editor.
///
/// Run from the menu (Tools/Multiplayer/Audit Online Lobby) or in batch mode:
///   Unity.exe -batchmode -nographics -quit -projectPath . -executeMethod OnlineLobbyAudit.Run -logFile -
/// </summary>
public static class OnlineLobbyAudit
{
    private const string OnlineSourceFolder = "Assets/Scripts/Assembly-CSharp/Multiplayer/Online";
    private const string UiSourceFolder = "Assets/Scripts/Assembly-CSharp/Multiplayer/Online/Ui";
    private const string BootstrapPath = "Assets/Scripts/Assembly-CSharp/Multiplayer/MultiplayerBootstrap.cs";
    private const string HeatUiFontPath = "Assets/Resources/HeatUI/Chinese Fallback.asset";
    private const string HeatUiIconsFolder = "Assets/Resources/HeatUI/Icons";
    private const string EasyTierFolder = "Assets/StreamingAssets/easytier";
    private const string EasyTierBinaryFolder = "Assets/StreamingAssets/easytier/win-x64";
    private const string FetchScriptPath = "Tools/Online/fetch-easytier.ps1";
    private const string ReadmePath = "Tools/Online/README.md";
    private const string ReportPath = "Builds/QA/online-lobby-audit.txt";

    private const string CanvasName = "Online Lobby Canvas";
    private const string EventSystemName = "[Online UI Events]";

    /// <summary>Client APIs that would mean the online layer talks to something off-machine.</summary>
    private static readonly string[] BannedApiTokens =
    {
        "HttpClient", "HttpWebRequest", "UnityWebRequest", "WebClient", "DownloadString",
        "WebRequest.Create", "HttpListener"
    };

    /// <summary>Prose/host patterns that only count when they appear in code, not in a comment.</summary>
    private static readonly string[] BannedEndpointTokens =
    {
        "public.easytier", "easytier.cn", "easytier.top", "relay.easytier", "matchmaking",
        "http://", "https://"
    };

    private static readonly string[] PageRoots =
    {
        "Page_Home", "Page_Create", "Page_Join", "Page_Lan", "Page_Recent", "Page_Settings"
    };

    private static readonly string[] NavButtons =
    {
        "NavHome", "NavCreate", "NavJoin", "NavLan", "NavRecent", "NavSettings"
    };

    /// <summary>Feature -> the controls that prove it exists in the built window.</summary>
    private static readonly Dictionary<string, string[]> RequiredControls =
        new Dictionary<string, string[]>
        {
            { "房间号/邀请链接加入", new[] { "HomeJoin", "JoinAddress", "JoinInput", "JoinButton", "PasteButton" } },
            { "二维码邀请", new[] { "InviteButton", "QrImage" } },
            { "局域网自动发现", new[] { "HomeLan", "LanRefresh", "LanScroll" } },
            { "最近与收藏大厅", new[] { "HomeRecent", "RecentScroll" } },
            { "大厅文字聊天", new[] { "ChatScroll", "ChatInput", "SendButton" } },
            { "成员列表/人数上限", new[] { "MemberScroll", "RoomMaxMinus", "RoomMaxPlus" } },
            { "房主管理/公告", new[] { "AnnouncementEdit", "ReadyButton", "StartButton", "LeaveButton" } },
            { "设置/昵称/虚拟局域网", new[] { "PlayerName", "SaveName", "SettingsVirtualLan", "SettingsBroadcast" } },
            { "自建节点", new[] { "NodeScroll", "NewNode", "AddNode" } },
            { "创建房间", new[] { "HomeCreate", "CreateButton", "MaxMinus", "MaxPlus" } }
        };

    private static readonly List<string> s_log = new List<string>();
    private static readonly List<string> s_failures = new List<string>();

    [MenuItem("Tools/Multiplayer/Audit Online Lobby")]
    public static void Run()
    {
        s_log.Clear();
        s_failures.Clear();

        CheckServerFreeDefaults();
        CheckSourcesAreServerFree();
        CheckFeatureSurface();
        CheckRoomCodes();
        CheckInviteRoundTrip();
        CheckQrPipeline();
        CheckLanHelpers();
        CheckEasyTierPayload();
        CheckLobbyUiBuild();

        WriteReport();
        if (s_failures.Count > 0)
        {
            throw new Exception("ONLINE_LOBBY_AUDIT_FAILED with " + s_failures.Count + " failure(s): " +
                                string.Join("; ", s_failures.ToArray()));
        }
        Debug.Log("ONLINE_LOBBY_AUDIT_PASS " + string.Join(" | ", s_log.ToArray()));
    }

    // ------------------------------------------------------------------ no server, ever

    private static void CheckServerFreeDefaults()
    {
        Check(OnlineNetConstants.DefaultExternalNode == "",
            "默认 EasyTier 节点为空（实际=\"" + OnlineNetConstants.DefaultExternalNode + "\"）");
        Check(OnlineNetConstants.DefaultExternalNode.IndexOf("://", StringComparison.Ordinal) < 0,
            "默认节点不带协议前缀");
        Check(OnlineNetConstants.LanBeaconPort > 0 && OnlineNetConstants.LanBeaconPort < 65536,
            "局域网广播端口合法（" + OnlineNetConstants.LanBeaconPort + "）");
        Check(OnlineNetConstants.DefaultGamePort == 7777, "默认游戏端口是 7777");
        Check(OnlineNetConstants.DefaultListenerPort == 11010, "默认 EasyTier 监听端口是 11010");

        OnlineRoomOptions options = new OnlineRoomOptions();
        Check(options.Transport == OnlineTransportKind.Direct, "建房默认走直连，不需要虚拟局域网");
        Check(string.IsNullOrEmpty(options.ExternalNode), "建房选项默认不带 EasyTier 节点");

        OnlineRoomInfo info = new OnlineRoomInfo();
        Check(info.Transport == OnlineTransportKind.Direct, "房间信息默认走直连");
        Check(string.IsNullOrEmpty(info.ExternalNode), "房间信息默认不带 EasyTier 节点");
        Check(info.ListenerPort == OnlineNetConstants.DefaultListenerPort, "房间信息带默认监听端口");
        Check(!info.HasDirectAddress, "新建的房间信息不会凭空带一个直连地址");
        Check(info.ResolvePeerAddress() == "", "没有直连地址时 ResolvePeerAddress() 为空");

        EasyTierOptions easyTier = new EasyTierOptions();
        Check(string.IsNullOrEmpty(easyTier.ExternalNode), "EasyTier 选项默认不带公共节点");

        OnlinePrefsData prefs = new OnlinePrefsData();
        Check(!prefs.EnableVirtualLan, "虚拟局域网默认关闭");

        Note("默认传输=直连，默认节点=\"\"，虚拟局域网默认关闭");
    }

    private static void CheckSourcesAreServerFree()
    {
        string[] files = Directory.GetFiles(OnlineSourceFolder, "*.cs", SearchOption.AllDirectories);
        Check(files.Length >= 20, "联机模块源文件齐全（" + files.Length + " 个）");

        foreach (string file in files)
        {
            string text = File.ReadAllText(file);
            foreach (string token in BannedApiTokens)
            {
                if (text.IndexOf(token, StringComparison.Ordinal) >= 0)
                {
                    Fail("服务端依赖 \"" + token + "\" 出现在 " + Relative(file));
                }
            }
            // Prose may explain that no server is needed; only code counts.
            string code = StripComments(text);
            foreach (string token in BannedEndpointTokens)
            {
                if (code.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Fail("代码里出现外部端点 \"" + token + "\"：" + Relative(file));
                }
            }
        }

        // The lobby must be reachable from the bootstrap, and the legacy IMGUI panel may only
        // remain behind the new window inside OpenLobby().
        string bootstrap = File.ReadAllText(BootstrapPath);
        Check(bootstrap.IndexOf("OnlineLobbyWindow", StringComparison.Ordinal) >= 0,
            "MultiplayerBootstrap 打开新的大厅窗口");
        string openLobby = ExtractMethodBody(bootstrap, "OpenLobby");
        Check(openLobby.Length > 0, "能定位到 MultiplayerBootstrap.OpenLobby()");
        int newWindowAt = openLobby.IndexOf("OnlineLobbyWindow", StringComparison.Ordinal);
        int legacyAt = openLobby.IndexOf("MultiplayerLobbyUI", StringComparison.Ordinal);
        Check(newWindowAt >= 0, "OpenLobby() 先尝试新窗口 OnlineLobbyWindow");
        Check(legacyAt < 0 || legacyAt > newWindowAt, "旧 IMGUI 大厅只作为新窗口之后的兜底路径");

        Note("扫描 " + files.Length + " 个源文件：没有 HTTP 客户端、没有公共节点代码");
    }

    // ------------------------------------------------------------------ feature checklist

    private static void CheckFeatureSurface()
    {
        Dictionary<string, string> features = new Dictionary<string, string>
        {
            { "房间号", HasMethod(typeof(OnlineRoomCode), "Generate") + "/" + HasMethod(typeof(OnlineRoomCode), "IsValid") },
            { "邀请链接", HasMethod(typeof(OnlineInvite), "Build") + "/" + HasMethod(typeof(OnlineInvite), "TryParse") },
            { "二维码邀请", (TypeExists("BadPiggies.Multiplayer.Online.Qr.QrEncoder") && TypeExists("BadPiggies.Multiplayer.Online.Qr.QrTexture")) ? "1/1" : "0/0" },
            { "局域网自动发现", HasMethod(typeof(OnlineLanDiscovery), "GetOrCreate") + "/" + HasMethod(typeof(OnlineLanDiscovery), "FindRoom") },
            { "最近与收藏", HasMethod(typeof(OnlinePrefs), "Favorites") + "/" + HasMethod(typeof(OnlinePrefs), "ToggleFavorite") },
            { "大厅文字聊天", HasMethod(typeof(OnlineLobbyService), "SendChat") + "/" + (TypeExists("BadPiggies.Multiplayer.Online.OnlineChatEntry") ? "1" : "0") },
            { "成员列表", HasField(typeof(OnlineMemberView), "IsReady") + "/" + HasField(typeof(OnlineMemberView), "LatencyMs") + "/" + HasField(typeof(OnlineMemberView), "LossRate") },
            { "房主管理", HasMethod(typeof(OnlineLobbyService), "Kick") + "/" + HasMethod(typeof(OnlineLobbyService), "SetMaxPlayers") + "/" + HasMethod(typeof(OnlineLobbyService), "SetAnnouncement") },
            { "免服务器加入", HasMethod(typeof(OnlineLobbyService), "JoinFromText") + "/" + HasMethod(typeof(OnlineRoomInfo), "ResolvePeerAddress") },
            { "大厅窗口", (TypeExists("BadPiggies.Multiplayer.Online.Ui.OnlineLobbyWindow") && TypeExists("BadPiggies.Multiplayer.Online.Ui.OnlineRoomView")) ? "1/1" : "0/0" }
        };

        foreach (KeyValuePair<string, string> feature in features)
        {
            Check(feature.Value.IndexOf('0') < 0, "功能存在: " + feature.Key + " (" + feature.Value + ")");
        }

        Check(File.Exists(HeatUiFontPath), "工程内 HeatUI 中文字体存在");
        Check(Directory.Exists(HeatUiIconsFolder), "工程内 HeatUI 图标目录存在");

        // Favourite/forget are row buttons: they only exist once the recent list has rows, so the
        // wiring is checked in the source instead of in the (empty) built window.
        string pagesSource = File.ReadAllText(Path.Combine(UiSourceFolder, "OnlineLobbyPages.cs"));
        Check(pagesSource.IndexOf("\"Favorite\"", StringComparison.Ordinal) >= 0 &&
              pagesSource.IndexOf("OnlinePrefs.ToggleFavorite", StringComparison.Ordinal) >= 0,
            "最近页面的收藏按钮接在 OnlinePrefs.ToggleFavorite 上");
        Check(pagesSource.IndexOf("\"Forget\"", StringComparison.Ordinal) >= 0 &&
              pagesSource.IndexOf("OnlinePrefs.ForgetRoom", StringComparison.Ordinal) >= 0,
            "最近页面的删除按钮接在 OnlinePrefs.ForgetRoom 上");

        string[] uiFiles = Directory.GetFiles(UiSourceFolder, "*.cs", SearchOption.TopDirectoryOnly);
        Check(uiFiles.Length >= 6, "大厅 UI 源文件齐全（" + uiFiles.Length + " 个）");
    }

    // ------------------------------------------------------------------ room codes and invites

    private static void CheckRoomCodes()
    {
        string code = OnlineRoomCode.Generate();
        Check(OnlineRoomCode.IsValid(code), "生成的房间号合法（" + code + "）");
        Check(code.StartsWith(OnlineNetConstants.RoomCodePrefix, StringComparison.Ordinal),
            "房间号带 BP- 前缀");
        Check(OnlineRoomCode.Normalize(OnlineRoomCode.Flatten(code)) == code,
            "房间号归一化往返一致");
        Check(OnlineRoomCode.Generate() != OnlineRoomCode.Generate(), "两次生成的房间号不同");

        string networkName = OnlineRoomCode.ToNetworkName(code);
        Check(!string.IsNullOrEmpty(networkName), "房间号能派生出 EasyTier 网络名（" + networkName + "）");
        Check(OnlineRoomCode.ToNetworkName(code) == networkName, "网络名稳定");
        Check(OnlineRoomCode.ToNetworkSecret(code, "pw") != OnlineRoomCode.ToNetworkSecret(code, ""),
            "有密码和没密码的网络密钥不同");
    }

    private static void CheckInviteRoundTrip()
    {
        OnlineRoomInfo host = new OnlineRoomInfo
        {
            RoomCode = OnlineRoomCode.Generate(),
            RoomName = "审计房间",
            HostName = "审计主机",
            GamePort = OnlineNetConstants.DefaultGamePort,
            Transport = OnlineTransportKind.Direct,
            DirectAddress = "192.168.31.24",
            ListenerPort = OnlineNetConstants.DefaultListenerPort
        };

        string link = OnlineInvite.Build(host);
        Check(!string.IsNullOrEmpty(link), "邀请链接生成成功");
        Check(link.IndexOf("192.168.31.24", StringComparison.Ordinal) >= 0,
            "邀请链接带上房主地址，客人不需要服务器就能加入");
        foreach (string token in BannedEndpointTokens.Concat(BannedApiTokens))
        {
            Check(link.IndexOf(token, StringComparison.OrdinalIgnoreCase) < 0, "邀请链接不含 " + token);
        }

        OnlineRoomInfo parsed;
        string error;
        Check(OnlineInvite.TryParse(link, out parsed, out error), "邀请链接解析成功");
        if (parsed != null)
        {
            Check(parsed.RoomCode == host.RoomCode, "解析后的房间号一致");
            Check(parsed.Transport == OnlineTransportKind.Direct, "解析后的传输方式是直连");
            Check(parsed.DirectAddress == host.DirectAddress, "解析后的房主地址保留");
            Check(parsed.GamePort == host.GamePort, "解析后的游戏端口保留");
            Check(parsed.ListenerPort == host.ListenerPort, "解析后的监听端口保留");
            Check(parsed.HasDirectAddress, "解析后的房间可直连");
            Check(parsed.ResolvePeerAddress() == "udp://192.168.31.24:" + OnlineNetConstants.DefaultListenerPort,
                "EasyTier 直连对端地址正确（" + parsed.ResolvePeerAddress() + "）");
        }

        Check(OnlineInvite.ExtractCode(link) == host.RoomCode, "能从链接里提取房间号");

        OnlineRoomInfo bare;
        Check(OnlineInvite.TryParse(host.RoomCode, out bare, out error), "裸房间号能解析");
        if (bare != null)
        {
            Check(bare.Transport == OnlineTransportKind.Direct, "裸房间号按直连处理");
            Check(string.IsNullOrEmpty(bare.ExternalNode), "裸房间号不带 EasyTier 节点");
        }

        Note("邀请链接: " + link);
    }

    // ------------------------------------------------------------------ QR pipeline

    private static void CheckQrPipeline()
    {
        Check(QrEncoder.MaxSupportedVersion == 10, "二维码最大版本是 10");
        Check(QrEncoder.MinSupportedVersion == 1, "二维码最小版本是 1");

        string[] payloads =
        {
            OnlineRoomCode.Generate(),
            OnlineInvite.Build(new OnlineRoomInfo
            {
                RoomCode = OnlineRoomCode.Generate(),
                RoomName = "审计房间",
                Transport = OnlineTransportKind.Direct,
                DirectAddress = "192.168.31.24",
                ListenerPort = OnlineNetConstants.DefaultListenerPort
            }),
            new string('A', 200)
        };

        foreach (string payload in payloads)
        {
            int version;
            int mask;
            string error;
            bool[,] matrix = QrEncoder.Encode(payload, out version, out mask, out error);
            Check(matrix != null, "二维码编码成功（" + payload.Length + " 字节）: " + error);
            if (matrix == null)
            {
                continue;
            }

            int size = 17 + 4 * version;
            Check(matrix.GetLength(0) == size && matrix.GetLength(1) == size,
                "二维码尺寸与版本一致（" + size + "x" + size + " v" + version + "）");
            CheckMatrixStructure(matrix, size, payload.Length);

            string textureError;
            Texture2D texture = QrTexture.Create(payload, 8, 4, null, null, out textureError);
            Check(texture != null, "二维码贴图生成成功: " + textureError);
            if (texture == null)
            {
                continue;
            }
            try
            {
                int expected = (size + 8) * 8;
                Check(texture.width == expected && texture.height == expected,
                    "二维码贴图尺寸正确（" + texture.width + "x" + texture.height + "，含 4 模块静默区）");

                Color32[] pixels = texture.GetPixels32();
                int darkPixels = 0;
                for (int i = 0; i < pixels.Length; i++)
                {
                    if (pixels[i].r + pixels[i].g + pixels[i].b < 3 * 128)
                    {
                        darkPixels++;
                    }
                }
                int darkModules = 0;
                for (int row = 0; row < size; row++)
                {
                    for (int column = 0; column < size; column++)
                    {
                        if (matrix[row, column])
                        {
                            darkModules++;
                        }
                    }
                }
                Check(darkPixels == darkModules * 64,
                    "二维码黑白像素与模块一一对应（" + darkPixels + " = " + darkModules + "*64）");

                Color32 corner = texture.GetPixel(0, 0);
                Check(corner.r + corner.g + corner.b >= 3 * 128, "二维码静默区是浅色");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
            Note("二维码 " + payload.Length + " 字节 -> v" + version + " mask " + mask);
        }

        string tooLongError;
        bool[,] tooLong = QrEncoder.Encode(new string('A', 4096), out tooLongError);
        Check(tooLong == null && !string.IsNullOrEmpty(tooLongError), "超长内容带错误信息返回，不崩溃");
    }

    private static void CheckMatrixStructure(bool[,] matrix, int size, int payloadLength)
    {
        // Three finder patterns: dark ring, light gap, dark 3x3 core.
        int[][] origins = { new[] { 0, 0 }, new[] { 0, size - 7 }, new[] { size - 7, 0 } };
        int errors = 0;
        foreach (int[] origin in origins)
        {
            for (int row = 0; row < 7; row++)
            {
                for (int column = 0; column < 7; column++)
                {
                    int radius = Math.Max(Math.Abs(row - 3), Math.Abs(column - 3));
                    bool expected = radius != 2;
                    if (matrix[origin[0] + row, origin[1] + column] != expected)
                    {
                        errors++;
                    }
                }
            }
        }
        Check(errors == 0, "二维码定位图案正确（" + payloadLength + " 字节）");

        int timingErrors = 0;
        for (int i = 8; i <= size - 9; i++)
        {
            bool expected = (i % 2) == 0;
            if (matrix[6, i] != expected)
            {
                timingErrors++;
            }
            if (matrix[i, 6] != expected)
            {
                timingErrors++;
            }
        }
        Check(timingErrors == 0, "二维码时序图案正确");
    }

    // ------------------------------------------------------------------ LAN helpers

    private static void CheckLanHelpers()
    {
        List<string> addresses = OnlineLanDiscovery.GetLanAddresses();
        Check(addresses != null, "GetLanAddresses() 可用");
        if (addresses != null)
        {
            int unusable = addresses.Count(address => address == "127.0.0.1" ||
                                                      address.StartsWith("169.254.", StringComparison.Ordinal) ||
                                                      address.StartsWith("10.126.126.", StringComparison.Ordinal));
            Check(unusable == 0, "局域网地址列表过滤掉回环/链路本地/EasyTier 虚拟地址");
            string primary = OnlineLanDiscovery.GetPrimaryLanAddress();
            Check(addresses.Count == 0 || primary == addresses[0], "GetPrimaryLanAddress() 与列表首项一致");
            Note("本机可用于直连的地址: " +
                 (addresses.Count == 0 ? "(无，需要手动填地址)" : string.Join(", ", addresses.ToArray())));
        }

        Check(OnlineLanDiscovery.FindRoom("BP-ZZZZ-ZZZZ") == null, "查询不存在的房间返回 null");
        Note("局域网信标: " + OnlineNetConstants.LanBeaconMagic + " @ udp/" + OnlineNetConstants.LanBeaconPort);
    }

    // ------------------------------------------------------------------ EasyTier payload + docs

    private static void CheckEasyTierPayload()
    {
        Check(File.Exists(FetchScriptPath), "EasyTier 获取脚本存在: " + FetchScriptPath);
        Check(File.Exists(ReadmePath), "联机说明文档存在: " + ReadmePath);

        bool supported = EasyTierPaths.IsSupportedPlatform;
        string core = EasyTierPaths.CoreExecutable;
        string cli = EasyTierPaths.CliExecutable;
        Note("EasyTier 平台支持=" + supported + " core=" + (File.Exists(core) ? "有" : "无") +
             " cli=" + (File.Exists(cli) ? "有" : "无"));

        if (supported)
        {
            Check(File.Exists(core), "Windows 下存在 easytier-core.exe");
            Check(File.Exists(cli), "Windows 下存在 easytier-cli.exe");
            Check(EasyTierPaths.IsInstalled, "EasyTierPaths.IsInstalled 为 true: " + EasyTierPaths.MissingReason);
            Check(EasyTierPaths.BaseDirectory.Replace('\\', '/').IndexOf("StreamingAssets/easytier", StringComparison.Ordinal) >= 0,
                "EasyTier 运行文件位于 StreamingAssets 下");
        }

        string readme = File.ReadAllText(ReadmePath);
        Check(readme.IndexOf("Npcap", StringComparison.Ordinal) >= 0, "文档说明了 Npcap 的再分发限制");
        Check(readme.IndexOf("服务器", StringComparison.Ordinal) >= 0, "文档说明了免服务器的连接方式");

        string streamingReadme = Path.Combine(EasyTierFolder, "README.md");
        Check(File.Exists(streamingReadme), "StreamingAssets 里带 EasyTier 说明: " + streamingReadme);
        if (File.Exists(streamingReadme))
        {
            string streamingText = File.ReadAllText(streamingReadme);
            Check(streamingText.IndexOf("Npcap", StringComparison.Ordinal) >= 0,
                "StreamingAssets 说明也标注了 Npcap 再分发限制");
        }
        Check(Directory.Exists(EasyTierBinaryFolder), "EasyTier 二进制目录存在: " + EasyTierBinaryFolder);
    }

    // ------------------------------------------------------------------ the UI actually builds

    private static void CheckLobbyUiBuild()
    {
        Canvas[] preExisting = FindCanvases();
        bool hadEventSystem = UnityEngine.Object.FindObjectOfType<EventSystem>() != null;

        GameObject host = new GameObject("[Online Lobby Audit]");
        OnlineLobbyWindow window = host.AddComponent<OnlineLobbyWindow>();
        Canvas canvas = null;
        try
        {
            MethodInfo ensureBuilt = typeof(OnlineLobbyWindow).GetMethod("EnsureBuilt",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Check(ensureBuilt != null, "能找到 OnlineLobbyWindow.EnsureBuilt（审计与实现同步）");
            if (ensureBuilt != null)
            {
                ensureBuilt.Invoke(window, null);
            }

            Check(string.IsNullOrEmpty(window.LastBuildError), "构建大厅 UI 没有异常: " + window.LastBuildError);

            canvas = FindCanvases().FirstOrDefault(c => c.gameObject.name == CanvasName &&
                                                        !preExisting.Contains(c));
            Check(canvas != null, "生成大厅 Canvas（" + CanvasName + "）");
            if (canvas == null)
            {
                return;
            }

            Check(canvas.renderMode == RenderMode.ScreenSpaceOverlay, "大厅 Canvas 是屏幕覆盖模式");
            Check(canvas.GetComponent<GraphicRaycaster>() != null, "大厅 Canvas 带 GraphicRaycaster");
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            Check(scaler != null, "大厅 Canvas 带 CanvasScaler");
            if (scaler != null)
            {
                Check(scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize, "CanvasScaler 按屏幕缩放");
            }
            Check(UnityEngine.Object.FindObjectOfType<EventSystem>() != null, "存在 EventSystem，按钮可点");

            Transform root = canvas.transform;
            Transform windowRect = FindDescendant(root, "Window");
            Check(windowRect != null, "存在 Window 面板");
            foreach (string shell in new[] { "Sidebar", "Header", "Content", "StatusBar", "Overlay" })
            {
                Check(FindDescendant(root, shell) != null, "存在外壳节点 " + shell);
            }

            Transform content = FindDescendant(root, "Content");
            foreach (string page in PageRoots)
            {
                Check(FindDescendant(content, page) != null, "存在页面 " + page);
            }
            Transform sidebar = FindDescendant(root, "Sidebar");
            foreach (string nav in NavButtons)
            {
                Check(FindDescendant(sidebar, nav) != null, "存在导航按钮 " + nav);
            }
            Check(FindDescendant(root, "RoomView") != null, "存在房间视图 RoomView");
            Check(FindDescendant(root, "InviteOverlay") != null, "存在邀请浮层 InviteOverlay");
            Check(FindDescendant(root, "InviteCard") != null, "邀请浮层有卡片 InviteCard");

            Check(MctierTheme.Font != null, "有可用的 TMP 字体（中文不会变成方块）");
            if (MctierTheme.Font != null)
            {
                string fontPath = AssetDatabase.GetAssetPath(MctierTheme.Font);
                Note("大厅字体: " + fontPath);
                Check(fontPath == HeatUiFontPath, "大厅使用工程内 HeatUI 中文字体（实际=" + fontPath + "）");
            }

            TMP_Text[] labels = canvas.GetComponentsInChildren<TMP_Text>(true);
            Check(labels.Length >= 40, "大厅文本控件数量充足（" + labels.Length + " 个）");
            Check(labels.Count(label => label == null || label.font == null) == 0, "每个文本控件都有字体");
            Note("文本控件 " + labels.Length + " 个，其中空文本 " +
                 labels.Count(label => string.IsNullOrEmpty(label.text)) + " 个（运行时才填的字段）");

            Image[] images = canvas.GetComponentsInChildren<Image>(true);
            int badSlices = images.Count(image => image.type == Image.Type.Sliced && image.sprite != null &&
                                                  image.sprite.border == Vector4.zero);
            Check(badSlices == 0, "九宫格图片都带边框（Heat UI 的描边/圆形精灵本身带 border）");
            Check(MctierWidgets.HeatSpritesAvailable,
                "边框与圆形用的是工程内 Heat UI 的精灵，而不是回退的自绘圆角图");
            Check(images.Count(image => image.sprite != null) >= 10,
                "多数图片使用工程内 Heat UI 精灵（" + images.Count(image => image.sprite != null) + " 个）");
            Note("图片 " + images.Length + " 个：带精灵 " + images.Count(image => image.sprite != null) +
                 " 个，九宫格 " + images.Count(image => image.type == Image.Type.Sliced) + " 个");

            Check(canvas.GetComponentsInChildren<TMP_InputField>(true).Length >= 4, "输入框数量充足");
            Check(canvas.GetComponentsInChildren<Toggle>(true).Length >= 3, "开关数量充足");
            Check(canvas.GetComponentsInChildren<ScrollRect>(true).Length >= 1, "存在滚动视图");

            string[] icons = MctierWidgets.AvailableIcons();
            Check(icons != null && icons.Length > 0, "Resources/HeatUI/Icons 里有图标（" +
                                                     (icons == null ? 0 : icons.Length) + " 个）");

            // Each promised feature must be reachable from a real control in the built window.
            foreach (KeyValuePair<string, string[]> feature in RequiredControls)
            {
                List<string> missing = feature.Value.Where(control => FindDescendant(root, control) == null).ToList();
                Check(missing.Count == 0, "界面入口存在: " + feature.Key +
                                          (missing.Count == 0 ? "" : "（缺少 " + string.Join(", ", missing.ToArray()) + "）"));
            }

            // Every list row must keep the Dot/Name/Detail/Action contract the views rely on.
            GameObject probe = MctierWidgets.CreateListRow("AuditRowProbe", windowRect, 64f);
            foreach (string child in new[] { "Dot", "Name", "Detail", "Action" })
            {
                Check(FindDescendant(probe.transform, child) != null, "成员行含 " + child + " 列");
            }
            MctierWidgets.DestroyObject(probe);

            Note("层次结构:\n  " + string.Join("\n  ", MctierWidgets.DescribeHierarchy(root, 3).ToArray()));
        }
        catch (TargetInvocationException ex)
        {
            Fail("构建大厅 UI 抛异常: " + (ex.InnerException ?? ex));
        }
        catch (Exception ex)
        {
            Fail("构建大厅 UI 抛异常: " + ex);
        }
        finally
        {
            if (canvas != null)
            {
                UnityEngine.Object.DestroyImmediate(canvas.gameObject);
            }
            UnityEngine.Object.DestroyImmediate(host);
            if (!hadEventSystem)
            {
                GameObject events = GameObject.Find(EventSystemName);
                if (events != null)
                {
                    UnityEngine.Object.DestroyImmediate(events);
                }
            }
        }
    }

    // ------------------------------------------------------------------ plumbing

    private static void Check(bool condition, string description)
    {
        if (condition)
        {
            s_log.Add("PASS " + description);
        }
        else
        {
            Fail(description);
        }
    }

    private static void Fail(string description)
    {
        s_failures.Add(description);
        s_log.Add("FAIL " + description);
        Debug.LogError("[OnlineLobbyAudit] FAIL " + description);
    }

    private static void Note(string message)
    {
        s_log.Add("  -  " + message);
        Debug.Log("[OnlineLobbyAudit] " + message);
    }

    private static bool TypeExists(string fullName)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Any(assembly => assembly.GetType(fullName, false) != null);
    }

    private static string HasMethod(Type type, string name)
    {
        return type.GetMethod(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance) != null ? "1" : "0";
    }

    private static string HasField(Type type, string name)
    {
        return type.GetField(name, BindingFlags.Public | BindingFlags.Instance) != null ? "1" : "0";
    }

    /// <summary>
    /// Replaces comments with blanks, keeping string literals (and line breaks) intact, so that
    /// documentation explaining "no server is needed" is not mistaken for server usage while a URL
    /// inside a literal still is.
    /// </summary>
    private static string StripComments(string text)
    {
        StringBuilder output = new StringBuilder(text.Length);
        bool inString = false;
        bool inChar = false;
        bool inLineComment = false;
        bool inBlockComment = false;
        for (int i = 0; i < text.Length; i++)
        {
            char current = text[i];
            char next = i + 1 < text.Length ? text[i + 1] : '\0';
            if (inLineComment)
            {
                if (current == '\n')
                {
                    inLineComment = false;
                    output.Append(current);
                }
                else
                {
                    output.Append(' ');
                }
                continue;
            }
            if (inBlockComment)
            {
                if (current == '*' && next == '/')
                {
                    inBlockComment = false;
                    output.Append("  ");
                    i++;
                }
                else
                {
                    output.Append(current == '\n' ? '\n' : ' ');
                }
                continue;
            }
            if (inString || inChar)
            {
                output.Append(current);
                if (current == '\\' && next != '\0')
                {
                    output.Append(next);
                    i++;
                    continue;
                }
                if (inString && current == '"')
                {
                    inString = false;
                }
                else if (inChar && current == '\'')
                {
                    inChar = false;
                }
                continue;
            }
            if (current == '"')
            {
                inString = true;
                output.Append(current);
                continue;
            }
            if (current == '\'')
            {
                inChar = true;
                output.Append(current);
                continue;
            }
            if (current == '/' && next == '/')
            {
                inLineComment = true;
                output.Append("  ");
                i++;
                continue;
            }
            if (current == '/' && next == '*')
            {
                inBlockComment = true;
                output.Append("  ");
                i++;
                continue;
            }
            output.Append(current);
        }
        return output.ToString();
    }

    /// <summary>Body of a method, from its signature to the matching closing brace.</summary>
    private static string ExtractMethodBody(string source, string methodName)
    {
        int signature = source.IndexOf(methodName + "(", StringComparison.Ordinal);
        if (signature < 0)
        {
            return "";
        }
        int open = source.IndexOf('{', signature);
        if (open < 0)
        {
            return "";
        }
        int depth = 0;
        for (int i = open; i < source.Length; i++)
        {
            if (source[i] == '{')
            {
                depth++;
            }
            else if (source[i] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return source.Substring(open, i - open + 1);
                }
            }
        }
        return "";
    }

    private static Canvas[] FindCanvases()
    {
        return UnityEngine.Object.FindObjectsOfType<Canvas>(true);
    }

    private static Transform FindDescendant(Transform root, string name)
    {
        if (root == null)
        {
            return null;
        }
        if (root.name == name)
        {
            return root;
        }
        for (int i = 0; i < root.childCount; i++)
        {
            Transform hit = FindDescendant(root.GetChild(i), name);
            if (hit != null)
            {
                return hit;
            }
        }
        return null;
    }

    private static string Relative(string path)
    {
        string full = Path.GetFullPath(path).Replace('\\', '/');
        string root = Path.GetFullPath(".").Replace('\\', '/');
        return full.StartsWith(root, StringComparison.Ordinal) ? full.Substring(root.Length).TrimStart('/') : full;
    }

    private static void WriteReport()
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Online lobby audit - " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        builder.AppendLine("Unity " + Application.unityVersion + ", failures: " + s_failures.Count);
        builder.AppendLine();
        foreach (string line in s_log)
        {
            builder.AppendLine(line);
        }
        if (s_failures.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("FAILURES:");
            foreach (string failure in s_failures)
            {
                builder.AppendLine("  * " + failure);
            }
        }
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, builder.ToString(), new UTF8Encoding(false));
        Debug.Log("[OnlineLobbyAudit] report: " + ReportPath + " (" + s_failures.Count + " failure(s))");
    }
}
