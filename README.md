# 视频批处理工具 (VideoBatchStudio) V9.5.2

![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011%20(64--bit)-blue.svg)
![Runtime](https://img.shields.io/badge/.NET-Framework%204.8-brightgreen.svg)
![Version](https://img.shields.io/badge/Version-V9.5.2-orange.svg)
![License](https://img.shields.io/badge/License-Proprietary%20%2F%20Team%20Edition-lightgrey.svg)

**VideoBatchStudio (视频批处理工作台)** 是一套面向短视频矩阵团队、出海影视制作与电商营销的高性能 Windows 音视频全流程批量生产系统。软件集成了 **10 大核心生产力工作区**，内嵌免安装独立运行环境与轻量字幕模型，支持开箱即用、一键扩展 AI 离线模型与全自动无感更新。

---

## ⚡ 团队与小白极速上手指南

软件提供**开箱即用**的完整便携包，无需复杂的技术配置或环境搭建，新手即可轻松上手。

### 1. 下载与解压
* 从 [GitHub Releases](https://github.com/100142armin-lgtm/VideoBatchStudio/releases/tag/v9.5.2) 下载最新版本的 **`VideoBatchStudio_Portable.zip`**（约 430 MB）。
* **重要**：下载后请**完整解压**到电脑的个人文件夹（如 `D:\VideoBatchStudio\` 或桌面）。**切勿在压缩包内直接双击运行！**

### 2. 双击启动
* 双击文件夹中的 **`双击启动软件.bat`**（或 `Start_VideoBatchStudio.cmd`）。
* 若新电脑缺少微软基础运行环境（如 .NET Framework 4.8），启动脚本会自动调取经过微软数字签名验证的安全向导完成就绪。

### 3. 开箱自带组件（解压即用，无需配置）
完整便携包已自带以下核心组件，解压即可直接出片：
* **FFmpeg / FFprobe**：音视频合成、剪辑与无损混流核心。
* **yt-dlp + Node.js**：全网高清直链解析与下载辅助。
* **Edge-TTS 独立运行时**：内嵌免配置的嵌入式 Python 环境，百种自然音色免费在线配音。
* **Whisper 离线字幕引擎 + Base 极速模型**：无需显卡即可极速生成中英及多语种双语字幕。

### 4. 扩展 AI 模型（按需一键安装）
如需更高精度的离线字幕或本地声音克隆功能：
* 点击软件右下角的 **【组件 / 模型】** 按钮（或双击 **`Install_Components.cmd`**）。
* 在弹出的组件管理界面中，勾选所需模型（如 **Whisper Turbo** 或 **Chatterbox 本地克隆**），点击 **【安装勾选项】**。
* 程序会自动执行多线程断点续传下载与 SHA256 防篡改校验，全程可视化进度条，小白也能一键搞定！

---

## 🚀 10 大核心功能工作区全览

| 图标 | 工作区板块 | 核心生产力特性 |
| :---: | :--- | :--- |
| 📦 | **批量合并**<br>*(WorkspaceMerge)* | • 智能画面相似度聚类与多文件夹交叉轮巡<br>• 尾组防落单自动重排平衡（如 3+3+1 自动均衡为 3+2+2）<br>• 严格目标时长控制（平均截取 / 随机截取 / 自动补段）<br>• 12 种平滑转场与流拷贝（Stream Copy）极速无损合并 |
| ✂️ | **视频拆分**<br>*(WorkspaceSplit)* | • 固定秒数精准切分（智能处理不足时长合并或舍弃）<br>• 2～100 等份智能均匀拆分<br>• 内置 0.05 秒关键帧对齐容差与失败自动逐段高精度补做 |
| 💧 | **水印处理**<br>*(WorkspaceWatermark)* | • 支持同时叠加 3 组文字水印与 3 个图片水印库<br>• Canva 级立体花字预设（波普、静态霓虹、荧光绿、鎏金衬线等）<br>• 图片多图轮播（Slideshow）、真实交叉叠化与换图间隔控制<br>• 24 种入场、退场、动态扫光与粒子停留特效，完美支持透明 PNG Alpha 通道 |
| 🖼️ | **图片成片**<br>*(WorkspaceImageVideo)* | • 静态图集一键智能生成指定时长与数量的高清视频<br>• 杂志三联、四宫格、六宫格、九宫格等丰富多屏排版<br>• 自带画面动态呼吸律动、智能调色美化与组间柔和转场 |
| 📥 | **视频下载**<br>*(WorkspaceDownload)* | • 聚合主流短视频与视频平台直链解析<br>• 谷歌云盘（Google Drive）图片原格式无损保留，杜绝强制转视频<br>• 针对 Facebook Reels / AV1 编码视频自动转码为全平台通用 H.264+AAC MP4 |
| 🎙️ | **智能字幕**<br>*(WorkspaceTranscribe)* | • 本地离线 Whisper 语音识别内核，零隐私泄露风险<br>• 覆盖 Base (148M) / Small (488M) / Turbo (574M) / Large-v3 (1.08G) 全尺寸模型<br>• 自动输出标准精确 SRT 字幕，支持烧录与时间轴自动校准 |
| 📝 | **文案成片**<br>*(WorkspaceScriptBatch)* | • 文案直接一键批量生成短视频成品<br>• **9:16 手机实机内嵌播放器**：顶部融合控制台，支持快进/音量/全屏与点击停播<br>• 动态 GIF 贴纸拖拽自由定位与层叠优化，内存流自动回收杜绝泄露<br>• 爆款滚动字幕、底色磨砂卡片、多音轨 BGM 音乐池智能混音 |
| ▦ | **视频拼屏**<br>*(WorkspaceSplitScreen)* | • 多格分屏（双屏/四/六/九宫格）与画中画（单/双/三小窗）模板<br>• 实时首帧画布交互点选，各区域独立边框（样式/粗细/颜色）与音量独立配置<br>• 水印同态实时映射，支持“固定成品时长”或“跟随主区域逐条完整导出” |
| 🎬 | **视频剪辑**<br>*(WorkspaceCutEdit)* | • 逐视频独立微调参数：水平镜像翻转、时间倒放、横屏裁中（自动转 9:16）<br>• 0.1x～3.0x 无级变速变调保持、0%～300% 独立音量增益<br>• 全局多音频混音通道，CRF 17 画质无损重采样 |
| 🚀 | **导出交付**<br>*(WorkspaceDeliver)* | • 灵活的命名规则宏模板（时间戳、分辨率、原文件名、批次编号）<br>• 重名文件智能自动防碰撞递增（如 `(2)`、`(3)`）<br>• 智能检测无损音频流直接封装（Stream Copy），大幅提升出片吞吐率 |

---

## 🎙️ AI 语音引擎矩阵体系

软件原生内置三大语音引擎矩阵，满足从免费轻量到电影级高保真及本地私密克隆的全场景需求：

1. **Chatterbox 本地离线配音与声音克隆（Resemble AI V3 Multilingual）**：
   * **100% 本地离线**：模型加载后完全断网运行，无端口开放，保障商业声音资产安全。
   * **声音克隆**：仅需导入 6～12 秒清晰单人参考音频，即可快速克隆指定声音口音与音色。
   * **逐词声学对齐**：根据生成音频实际波形进行毫秒级字词对齐，字幕咬字分毫不差。
   * **显存与硬件优化**：支持 NVIDIA GPU（推荐 8GB 显存）与 CPU 模式，提供空闲自动释放显存机制与跨会话试听缓存。
   * *详细技术规范见 [docs/Chatterbox_使用说明.md](docs/Chatterbox_使用说明.md) 与 [docs/Chatterbox_组件说明.txt](docs/Chatterbox_组件说明.txt)。*

2. **Edge-TTS 免费在线语音**：
   * 预置独立 Python 运行库，免去团队人员手动配置环境的烦恼。
   * 覆盖数百种全球官方音色，支持多语种播报，永久免费。

3. **ElevenLabs 商业高保真语音**：
   * 支持填写团队私有 API Key，享受超写实的情感语调与工业级声音还原度。

---

## 📦 组件与模型生命周期管理 (ComponentManager)

软件将所依赖的 13 项组件统一纳入规范化管理，每项资源均配置有官方 HTTPS 来源与 SHA256 完整性指纹：

```
VideoBatchStudio/
├── VideoBatchStudio.exe          # 主程序（安全加固，零误报，支持长路径）
├── VideoBatchStudio.exe.config   # 运行配置（启用 LongPathAware 与 .NET 4.8 优化）
├── 双击启动软件.bat              # 小白极速一键启动脚本
├── Install_Components.cmd        # 组件与模型图形化安装器快捷入口
├── Start_VideoBatchStudio.cmd    # 命令行极速启动脚本
├── Setup/                        # 安装与运行时配置
│   ├── components.json           # 13 大组件模型官方源与 SHA256 清单
│   ├── edge-requirements.lock    # Edge-TTS 依赖锁文件
│   ├── Install-MicrosoftRuntime.ps1 # 微软官方运行时静默安装器
│   └── Package-Release.ps1       # 全自动团队发行包打包脚本
├── ScriptBatch/                  # 配音与渲染工作脚本
├── fonts/                        # 内置商用开源花字字体库
└── docs/                         # 技术文档与组件架构说明
```

* 团队人员通过界面中的 **【组件 / 模型】** 按钮即可一目了然查看各组件的运行状态（`已就绪` / `未安装` / `文件损坏`），并支持一键断点续传修复。

---

## 🛡️ 安全加固与系统兼容性

* **杀毒软件零误报**：
  全面规范 PE 标准元数据与无损输出流，关闭非托管内联与冗余特征，彻底杜绝 Windows Defender、Bitdefender、360 等主流杀软的误报或破坏性拦截。
* **Windows 长路径无限制**：
  原生集成 `longPathAware` 与 `Switch.System.IO.BlockLongPaths=false`，超过 260 字符的深层素材文件夹均可自由读写。
* **全球与海外系统兼容**：
  全链路采用 UTF-8 编码读取与流解析，彻底根治非中文 Windows（ANSI/CP1252）解压或路径异常痛点。

---

## 🛠️ 本地开发与编译

针对开发者与二次构建人员：

### 环境要求
* Windows 10 (1903 及以上) / Windows 11 (64-bit)
* .NET Framework 4.8 Developer Pack 或 .NET 8/9/10 SDK
* Visual Studio 2022+ 或 VS Code

### 本地构建
```powershell
# 编译 Release 版本
dotnet build VideoBatchStudio.csproj -c Release

# 一键打包并校验便携安装包
./Setup/Package-Release.ps1 -BuildDirectory bin_out/Release/net48 -OutputDirectory release_assets
```

---

## 📄 版权与开源许可证

* 本项目核心业务代码版权所有。
* 所集成的第三方开源组件（FFmpeg、yt-dlp、Node.js、Whisper.cpp、Chatterbox、WebView2、OFL 字体等）遵循各自原始开源许可证，详见 [THIRD_PARTY_NOTICES.txt](THIRD_PARTY_NOTICES.txt) 与 `licenses/` 目录。
