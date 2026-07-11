# TS3AudioBot

[English](README.md) · 简体中文 · [繁體中文](README.zh-TW.md)

这是一个开源的 TeamSpeak3 机器人，可以播放音乐以及更多功能。

- **有问题？** 查看我们的 [Wiki](https://github.com/Splamy/TS3AudioBot/wiki)、[FAQ](https://github.com/Splamy/TS3AudioBot/wiki/FAQ)，或在 [![加入 Gitter 聊天](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/TS3AudioBot/Lobby?utm_source=share-link&utm_medium=link&utm_campaign=share-link) 上提问
- **遇到故障或复杂问题？** [提交 Issue](https://github.com/Splamy/TS3AudioBot/issues/new/choose)
  - 请使用并填写我们提供的模板，除非模板不适用于你的情况或有充分理由不这样做。
    这有助于我们更快地处理技术问题
  - 请尽量用英文提交 Issue，这样便于大家参与，也便于相关联的 Issue 能被正确引用。
- **想支持此项目？**
  - 你可以参与讨论并提出功能建议。不过我们的[待办列表](https://github.com/Splamy/TS3AudioBot/projects/2)很长，功能请求可能需要一些时间
  - 你可以贡献代码。我们对此非常感激，请在**开始之前**提交 Issue 或联系维护者进行讨论。
  - 你可以通过 [![Patreon][patreon-badge]][patreon-link] 或 [![Paypal][paypal-badge]][paypal-link] 支持我

[patreon-badge]: https://img.shields.io/badge/Patreon-Donate!-F96854.svg?logo=patreon&style=flat-square
[patreon-link]: https://patreon.com/Splamy

[paypal-badge]: https://img.shields.io/badge/Paypal-Donate!-00457C.svg?logo=paypal&style=flat-square
[paypal-link]: https://paypal.me/Splamy

## 特性

* 播放 YouTube 和 SoundCloud 歌曲，以及流式播放 Twitch（可通过插件扩展）
* 歌曲历史记录
* 多种语音订阅模式，包括：关注客户端、频道和耳语组
* 面向所有用户的播放列表管理
* 强大的权限配置
* 插件支持
* Web API
* 多实例
* 本地化
* 凭借自研无头 TS3 客户端，CPU 和内存占用低

想了解计划中的和正在进行的功能，请查看我们的[路线图](https://github.com/Splamy/TS3AudioBot/projects/2)。

## 机器人命令

机器人完全可通过聊天进行操作。
开始使用，请向机器人发送 `!help`。
如需查看所有命令，请查阅我们的在线 [OpenApiV3 生成器](http://tab.splamy.de/openapi/index.html)。
深入的命令教程请参见[此处的 Wiki](https://github.com/Splamy/TS3AudioBot/wiki/CommandSystem)。

## 安装

### 下载

根据你的平台和偏好选择并下载构建版本：

|  | 稳定版 | 实验版 |
| -- | -- | -- |
| | 版本通常被认为是稳定的，但重大功能更新不会很快。 | 始终拥有最新最好的功能，但可能不完全稳定或存在有问题的功能。 |
| Windows_x64 | [![下载](https://img.shields.io/badge/Download-master-green.svg)](https://splamy.de/api/nightly/ts3ab/master_win_x64/download) | [![下载](https://img.shields.io/badge/Download-develop-green.svg)](https://splamy.de/api/nightly/ts3ab/develop_win_x64/download) |
| Linux_x64 | [![下载](https://img.shields.io/badge/Download-master-green.svg)](https://splamy.de/api/nightly/ts3ab/master_linux_x64/download) | [![下载](https://img.shields.io/badge/Download-develop-green.svg)](https://splamy.de/api/nightly/ts3ab/develop_linux_x64/download) |
| Docker | [![Docker](https://img.shields.io/badge/Docker-0.11.0-0db7ed.svg)](https://github.com/getdrunkonmovies-com/TS3AudioBot_docker)（注意：此构建版本由社区维护。它包含了所有依赖项以及预配置的 youtube-dl） | - |

（我们还有更多构建版本，如 linux arm/arm64 和依赖 .NET framework 的版本，可在我们的 [nightly 服务器](https://splamy.de/Nightly#ts3ab) 上获取）

#### Linux

安装所需的依赖：

* **Ubuntu**/**Debian**：
  运行 `sudo apt-get install libopus-dev ffmpeg`
* **Arch Linux**：
  运行 `sudo pacman -S opus ffmpeg`
* **CentOS 7**：
  运行
  ```
  sudo yum -y install epel-release
  sudo rpm -Uvh http://li.nux.ro/download/nux/dextop/el7/x86_64/nux-dextop-release-0-5.el7.nux.noarch.rpm
  sudo yum -y install ffmpeg opus-devel
  ```
* **手动安装**：
  1. 确保已安装 C 编译器
  2. 赋予 Opus 脚本可执行权限：`chmod u+x InstallOpus.sh`，然后运行 `./InstallOpus.sh`
  3. 获取 ffmpeg [32位](https://johnvansickle.com/ffmpeg/builds/ffmpeg-git-i686-static.tar.xz) 或 [64位](https://johnvansickle.com/ffmpeg/builds/ffmpeg-git-amd64-static.tar.xz) 二进制文件
  4. 解压 ffmpeg 压缩包：`tar -vxf ffmpeg-git-*XXbit*-static.tar.xz`
  5. 从 `ffmpeg-git-*DATE*-amd64-static/ffmpeg` 获取 ffmpeg 二进制文件，并复制到你的 TS3AudioBot 文件夹中

#### Windows

1. 获取 ffmpeg [32位](https://ffmpeg.zeranoe.com/builds/win32/static/ffmpeg-latest-win32-static.zip) 或 [64位](https://ffmpeg.zeranoe.com/builds/win64/static/ffmpeg-latest-win64-static.zip) 二进制文件
2. 打开压缩包，将 `ffmpeg-latest-winXX-static/bin/ffmpeg.exe` 中的 ffmpeg 二进制文件复制到你的 TS3AudioBot 文件夹中

### 可选依赖

如果机器人无法播放某些 YouTube 视频，可能是因为嵌入限制导致的。
你可以安装 [youtube-dl](https://github.com/rg3/youtube-dl/) 的二进制文件或源代码文件夹（并在配置中指定路径），以尝试绕过此限制。

### 首次设置

1. 运行机器人：`./TS3AudioBot`（Linux）或 `TS3AudioBot.exe`（Windows），然后按照设置说明操作。
2. （可选）关闭机器人，根据需要配置 `rights.toml` 文件。
   你可以使用自动生成文件中建议的模板规则，
   或深入了解[这里的权限语法](https://github.com/Splamy/TS3AudioBot/wiki/Rights)。
   然后重新启动机器人。
3. （可选，但强烈建议以确保一切正常运行）。
   - 为 ServerAdmin 组（或具有等效权限的组）创建一个权限密钥。
   - 向机器人发送私信 `!bot setup <权限密钥>`。
4. 恭喜，你已经完成了！尽情享受你最喜爱的音乐，体验这个强大的命令系统，或者随心所欲地探索吧 ;)。
   更多信息请查阅 [CommandSystem](https://github.com/Splamy/TS3AudioBot/wiki/CommandSystem)。

## 手动构建

| master | develop |
|:--:|:--:|
| [![构建状态](https://ci.appveyor.com/api/projects/status/i7nrhqkbntdhwpxp/branch/master?svg=true)](https://ci.appveyor.com/project/Splamy/ts3audiobot/branch/master) | [![构建状态](https://ci.appveyor.com/api/projects/status/i7nrhqkbntdhwpxp/branch/develop?svg=true)](https://ci.appveyor.com/project/Splamy/ts3audiobot/branch/develop) |

### 下载源码

使用 `git clone --recurse-submodules https://github.com/Splamy/TS3AudioBot.git` 克隆代码仓库。

#### Linux

1. 按照[此教程](https://docs.microsoft.com/dotnet/core/install/linux-package-managers)获取最新的 `dotnet core 3.1` 版本，并选择你的平台
2. 进入仓库目录：`cd TS3AudioBot`
3. 运行 `dotnet build --framework netcoreapp3.1 --configuration Release TS3AudioBot` 构建 AudioBot
4. 生成的二进制文件位于 `./TS3AudioBot/bin/Release/netcoreapp3.1`，可以通过 `dotnet TS3AudioBot.dll` 运行

#### Windows

1. 确保已安装包含 `dotnet core 3.1` 开发工具链的 `Visual Studio`
2. 使用 Visual Studio 构建 AudioBot

### 构建 Web 界面

1. 打开终端，进入 `./WebInterface` 文件夹
2. 运行 `npm install` 恢复或更新此项目的所有依赖
3. 运行 `npm run build` 构建项目。
   构建后的项目位于 `./WebInterface/dist`。
   请确保在 ts3audiobot.toml 中将 webinterface 路径设置为此文件夹。
4. 你也可以使用 `npm run start` 进行开发。
   这将使用 webpack 开发服务器并支持热重载，而非使用 ts3ab 自带的服务器。

## 社区

### 本地化

:speech_balloon: *想要帮助翻译或改进翻译？*
加入我们的 [Transifex](https://www.transifex.com/respeak/ts3audiobot/) 帮助翻译，
或加入我们的 [Gitter](https://gitter.im/TS3AudioBot/Lobby?utm_source=share-link&utm_medium=link&utm_campaign=share-link) 讨论或提问！
所有帮助都值得感谢 :heart:

翻译需要经过人工审核，然后将自动构建并部署到[我们的 nightly 服务器](https://splamy.de/TS3AudioBot)。

## 许可证

本项目采用 [OSL-3.0](https://opensource.org/licenses/OSL-3.0) 许可。

为什么选择 OSL-3.0：

- OSL 允许你在链接到我们的库时无需公开自己的项目，这对于你想将 TSLib 作为库使用的情况可能很有用。
- 如果你创建插件，不必像 GPL 那样公开它们。（不过我们仍然希望你能分享它们 :)
- 通过 OSL，我们希望允许你将 TS3AB 作为服务提供（甚至是商业服务）。我们不希望本软件被出售，而是希望服务被提供。我们希望这款软件对所有人都是免费的。
- TL; DR? https://tldrlegal.com/license/open-software-licence-3.0

---
[![forthebadge](http://forthebadge.com/images/badges/60-percent-of-the-time-works-every-time.svg)](http://forthebadge.com) [![forthebadge](http://forthebadge.com/images/badges/built-by-developers.svg)](http://forthebadge.com) [![forthebadge](http://forthebadge.com/images/badges/built-with-love.svg)](http://forthebadge.com) [![forthebadge](http://forthebadge.com/images/badges/contains-cat-gifs.svg)](http://forthebadge.com) [![forthebadge](http://forthebadge.com/images/badges/made-with-c-sharp.svg)](http://forthebadge.com)
