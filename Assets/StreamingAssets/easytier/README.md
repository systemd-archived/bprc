# EasyTier 运行时（win-x64）

本目录是 `Assets/Scripts/Assembly-CSharp/Multiplayer/Online/EasyTier/` 那套 C# 封装所驱动的
EasyTier 可执行文件暂存目录。游戏本身不含任何 EasyTier 源码，只是把上游官方 release 的
二进制放在这里，由 `EasyTierRunner` 以子进程方式拉起。

## 上游版本

| 项目 | 值 |
| --- | --- |
| EasyTier 版本 | **v2.6.4** |
| 发布资源 | `easytier-windows-x86_64-v2.6.4.zip` |
| 下载地址 | <https://github.com/EasyTier/EasyTier/releases/download/v2.6.4/easytier-windows-x86_64-v2.6.4.zip> |
| zip SHA-256 | `27AF91E270E554709B048BD32327FEFD2DFCE5062AE1E8701AF7550C6F525F84` |
| zip 大小 | 32,657,720 字节 |

## 暂存了什么

| 文件 | 作用 | 备注 |
| --- | --- | --- |
| `easytier-core.exe` | 虚拟局域网核心进程，由 `EasyTierRunner` 拉起 | 必需 |
| `easytier-cli.exe` | 查询 peer 列表 / 节点信息（`EasyTierCli`、`EasyTierMonitor` 用它取延迟与丢包） | 必需 |
| `wintun.dll` | Windows TUN 虚拟网卡驱动 | 仅在**创建 TUN**（即没有 `--no-tun`）时需要 |
| `WinDivert64.sys` | 抓包驱动，供 `--enable-udp-broadcast-relay` 转发局域网 UDP 广播 | 仅在该开关打开时需要 |
| `Packet.dll` | **Npcap** 的运行库 | 官方 `easytier-core.exe` 静态导入它，见下节 |
| `SHA256.json` | 本次暂存的版本、来源、每个文件的 SHA-256 与大小 | 由脚本自动生成，用于校验 |

`SHA256.json` 里的 `npcapIncluded` 字段说明本次是否包含 `Packet.dll`。

## 刷新 / 重新暂存

从仓库根目录执行（脚本需要 `-ExecutionPolicy Bypass`，因为它属于本地脚本）：

```powershell
# 用本机已经解压好的官方压缩包目录（不联网）
powershell -ExecutionPolicy Bypass -File Tools/Online/fetch-easytier.ps1 `
    -FromDirectory "$env:TEMP\et_bin\easytier-windows-x86_64"

# 从 GitHub 下载（缓存到 %TEMP%\easytier-cache，校验固定 SHA-256 后解压）
powershell -ExecutionPolicy Bypass -File Tools/Online/fetch-easytier.ps1

# 强制重新覆盖，并且**不**暂存 Packet.dll
powershell -ExecutionPolicy Bypass -File Tools/Online/fetch-easytier.ps1 -Force -SkipNpcap
```

脚本参数：`-CacheDir`（下载缓存目录）、`-FromDirectory`（改用已解压目录）、`-Force`（强制覆盖）、
`-SkipNpcap`（不暂存 `Packet.dll`）、`-DestDir`（改输出目录）、`-SkipImportScan`（跳过导入表扫描）。

脚本结束前会调用既有的 `Tools/Online/pe-imports.ps1`，打印 `easytier-core.exe` 的导入表并明确
回答 `packet.dll` 是否为静态导入。

## ⚠️ Npcap 授权警告（发布前必读）

`Packet.dll` 是 **Npcap**（<https://npcap.com>，版权归 **Nmap Project**）的一部分。
Npcap 的免费授权**不允许**把它随你的软件一起分发；只有购买 **Npcap OEM** 再分发授权才允许。

而官方 `easytier-core.exe` **静态导入 `packet.dll`**（已用 `Tools/Online/pe-imports.ps1` 在本机
复核，导入表中确实存在 `packet.dll`），所以本游戏只有两条合法路线：

1. **换用不依赖 Npcap 的 `easytier-core.exe` 自编译版本**，并用 `-SkipNpcap` 暂存。
   届时导入表里不会出现 `packet.dll`，`EasyTierPaths.HasPacketDll` 为 `false` 也不影响启动。
2. **不分发 `Packet.dll`**，要求最终用户自行到 <https://npcap.com/#download> 安装 Npcap。
   游戏必须检测缺失并用中文提示用户（`EasyTierPaths.MissingReason` / `DescribeInstallation()`
   已经在缺少 `Packet.dll` 时给出提示，`EasyTierRunner.LastError` 会带上 core 进程的失败原因）。

**不要把本目录的 `Packet.dll` 提交进版本库、打包进安装包或对外发布。**
任何要离开本机的产物请一律用 `-SkipNpcap` 重新暂存。

## Windows 上创建 TUN 网卡需要管理员权限

- `easytier-core.exe` 创建 TUN 虚拟网卡（`wintun.dll`）**必须提权**，否则启动即失败。
- `--enable-udp-broadcast-relay`（转发局域网 UDP 广播，帮助局域网游戏互相发现房间）
  同样**需要管理员权限**。
- `--no-tun`（`EasyTierOptions.NoTun = true`）**不需要**管理员权限，可以正常启动，
  但不会创建虚拟网卡，只能靠子网代理访问对端。

`EasyTierRunner.StartNetwork(options, allowElevation, out error)` 会先以普通权限启动；如果日志
出现 wintun / 拒绝访问一类的提权失败特征（或进程很快以非零码退出），且 `allowElevation` 为
`true` 且 `NoTun` 为 `false`，就自动用 `Verb = "runas"` 重启一次（会弹 UAC）。
提权模式无法重定向 stdout/stderr，此时就绪判断只依赖 RPC 端口探测；`IsElevated` 会变成 `true`。
用户在 UAC 弹窗点“否”会直接失败，`LastError` 会写明原因。

## 指向自定义构建

默认路径是 `<StreamingAssets>/easytier/win-x64`（即本目录）。在代码里可以改：

```csharp
// 指向开发者自己编译的 easytier-core.exe（例如不含 Npcap 依赖的版本）
EasyTierPaths.OverrideBaseDirectory(@"D:\build\easytier-custom");
// 传 null 或 "" 恢复默认
EasyTierPaths.OverrideBaseDirectory(null);
```

`EasyTierPaths.IsSupportedPlatform` / `IsInstalled` / `MissingReason` 会在非 Windows、
非 64 位或缺文件时给出中文、可执行的说明，且**不会抛异常**。

## 冒烟测试（可选）

`Tools/Online/EasyTierHarness/` 是一个不依赖 Unity 的独立控制台程序，用 Unity 自带编译器
只编译 `EasyTierPeer.cs`（它刻意不引用 `UnityEngine`）+ 本目录的真实二进制，验证
`EasyTierJson` 能正确解析真实的 CLI 输出：

```powershell
powershell -ExecutionPolicy Bypass -File Tools/Online/EasyTierHarness/run-harness.ps1 -Mode live -RpcPort 15921
powershell -ExecutionPolicy Bypass -File Tools/Online/EasyTierHarness/run-harness.ps1 -Mode tolerance
powershell -ExecutionPolicy Bypass -File Tools/Online/EasyTierHarness/run-harness.ps1 -Mode parse
```
